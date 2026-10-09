// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentHooks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using global::AgentHooks;
    using Microsoft.Agents.A365.Tooling.Protection.Defender;
    using Microsoft.Agents.A365.Tooling.Protection.Purview;
    using Microsoft.Extensions.Logging;

    /// <summary>The agent identity and credentials for the Defender call of one emitted context.</summary>
    /// <param name="Agent">The agent identity and turn; fills context fields the host did not set.</param>
    /// <param name="TokenResolver">Resolves the agent identity's Defender token, for example
    /// <see cref="DefenderRtpTokenResolvers.FromAgenticConnection"/>.</param>
    public sealed record A365DefenderCall(DefenderRtpAgentContext Agent, DefenderRtpTokenResolver TokenResolver);

    /// <summary>
    /// An agent-hooks interceptor for Microsoft Defender for AI real-time protection. Each context the
    /// host emits at <c>input</c>, <c>pre_tool_call</c>, <c>post_tool_call</c> or <c>output</c> is evaluated
    /// by Defender, and Defender's verdict decides: <c>deny</c> blocks the action. Other points are allowed
    /// without a call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Defender receives a copy of the context fitted to its request validation (spec version, UTC
    /// timestamp, <c>target</c>, envelope and tool objects with only their spec members, repaired optional fields
    /// and clamped content). The copy keeps the context's session, sequence and tool call ids; the host's context is
    /// not modified.
    /// </para>
    /// <para>
    /// When no verdict is obtained (no agent identity, identity resolution, transport, authentication or validation
    /// failure), or Defender allowed a truncated copy of what is under decision
    /// (<see cref="DefenderRtpEvaluationResult.Truncated"/>), the
    /// verdict follows <see cref="DefenderRtpOptions.FailClosed"/>: allow with a <c>defender:unverified</c> warning,
    /// or deny with reason <c>runtime_error:defender_unverified</c>, which is never reported as a detection.
    /// </para>
    /// <para>
    /// Register it on an emitter whose per-interceptor timeout is longer than
    /// <see cref="DefenderRtpOptions.Timeout"/>, such as one from <see cref="A365AgentHooks.CreateProtectionEmitter"/>;
    /// a shorter timeout turns a slow evaluation into a fail-closed agent-hooks host error. The evaluation
    /// callback runs on the thread pool once the verdict is decided, outside that timeout, and never changes the
    /// verdict: an exception it throws is logged and ignored.
    /// </para>
    /// </remarks>
    public sealed class A365DefenderInterceptor : IInterceptor
    {
        /// <summary>The name the interceptor is registered under.</summary>
        public const string Name = "defender";

        // agent-hooks reserves this reason namespace for failures the host synthesizes.
        private const string ReservedReasonPrefix = "host_error:";

        private static readonly Regex InvalidReasonCharacters = new("[^A-Za-z0-9_.-]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly DefenderRtpClient _client;
        private readonly Func<AgentContext, A365DefenderCall?> _resolveCall;
        private readonly Action<DefenderRtpEvaluationResult>? _onEvaluated;
        private readonly ILogger? _logger;

        /// <summary>Initializes a new instance of the <see cref="A365DefenderInterceptor"/> class.</summary>
        /// <param name="client">The Defender client.</param>
        /// <param name="resolveCall">
        /// Returns the agent identity and token resolver for a context, for example from the current turn. It is
        /// invoked only when Defender RTP is enabled and Defender evaluates the context's point. When it returns
        /// null (no agent identity is available) or throws, the context is not sent and the verdict follows the
        /// fail mode; the result records only the type of an exception, which is logged to
        /// <paramref name="logger"/>.
        /// </param>
        /// <param name="onEvaluated">
        /// Receives each evaluation for logging and telemetry (for example the correlation id). It runs on the thread
        /// pool once the verdict is decided, so it may run after the interceptor returns and alongside later
        /// evaluations. It never changes the verdict: an exception it throws is logged to <paramref name="logger"/>,
        /// when given, and ignored.
        /// </param>
        /// <param name="logger">Receives failures of the evaluation and of <paramref name="onEvaluated"/>.</param>
        public A365DefenderInterceptor(
            DefenderRtpClient client,
            Func<AgentContext, A365DefenderCall?> resolveCall,
            Action<DefenderRtpEvaluationResult>? onEvaluated = null,
            ILogger? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _resolveCall = resolveCall ?? throw new ArgumentNullException(nameof(resolveCall));
            _onEvaluated = onEvaluated;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async ValueTask<Verdict> InterceptAsync(AgentContext context, CancellationToken cancellationToken)
        {
            var point = InterceptionPointExtensions.ToWireName(context.InterceptionPoint);
            if (!_client.Options.Enabled || !DefenderRtpClient.IsEvaluatedInterceptionPoint(point))
            {
                return new Verdict(Decision.Allow);
            }

            DefenderRtpEvaluationResult? result;
            try
            {
                // Without an agent identity Defender cannot be called; like any missing verdict, that follows the
                // fail mode rather than allowing silently.
                var call = _resolveCall(context);
                result = call == null
                    ? _client.Unavailable(point, "no agent identity was resolved")
                    : await _client.EvaluateHookContextAsync(context.Json, call.Agent, call.TokenResolver, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // A failure to resolve the identity or to evaluate is never a verdict: it follows the fail mode. Only
                // the exception's type reaches the result, and so the verdict, since its message can carry
                // credentials or content; the exception itself goes to the logger.
                _logger?.LogWarning(ex, "The Defender evaluation failed at {InterceptionPoint}; the fail mode applies.", point);
                result = _client.Unavailable(point, $"evaluation failed ({ex.GetType().Name})");
            }

            if (result == null)
            {
                return new Verdict(Decision.Allow);
            }

            // The verdict is decided before the callback sees the result, and the callback runs on the thread pool,
            // off the emitter's timed interception, so neither what it does nor how long it takes changes the verdict.
            var verdict = ToVerdict(result);
            if (_onEvaluated is { } onEvaluated)
            {
                _ = Task.Run(() => NotifyEvaluated(onEvaluated, result));
            }

            return verdict;
        }

        /// <summary>
        /// Hands the evaluation to the host's callback. The callback is for logging and telemetry, so its
        /// failure is logged at most and never changes the verdict, in either fail mode.
        /// </summary>
        private void NotifyEvaluated(Action<DefenderRtpEvaluationResult> onEvaluated, DefenderRtpEvaluationResult result)
        {
            try
            {
                onEvaluated(result);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _logger?.LogWarning(
                    ex,
                    "The Defender evaluation callback failed at {InterceptionPoint}; the verdict is unchanged. CorrelationId: {CorrelationId}",
                    result.InterceptionPoint,
                    result.CorrelationId);
            }
        }

        /// <summary>Maps a Defender evaluation to the agent-hooks verdict the host composes.</summary>
        /// <param name="result">The Defender evaluation.</param>
        /// <returns>The agent-hooks verdict.</returns>
        public static Verdict ToVerdict(DefenderRtpEvaluationResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            // Defender's allow of a truncated copy does not cover the content that was cut, so it is reported
            // like a missing verdict; a deny (or transform) of a truncated copy stands.
            var authoritative = result.Evaluated && !(result.Truncated && result.Verdict?.Decision == "allow");
            if (authoritative)
            {
                // Copied, so the verdict never shares a list with the result.
                var labels = result.Verdict?.ResultLabels is { Count: > 0 } resultLabels ? resultLabels.ToList() : null;
                if (result.Allowed)
                {
                    var warnings = result.Verdict?.Warnings
                        .Select(warning => new Warning(WarningReason(warning.Reason), warning.Message ?? string.Empty))
                        .ToList();
                    return new Verdict(
                        Decision.Allow,
                        Warnings: warnings is { Count: > 0 } ? warnings : null,
                        ResultLabels: labels);
                }

                var code = result.Verdict?.Reason is { Length: > 0 } reason ? ":" + InvalidReasonCharacters.Replace(reason, "_") : string.Empty;
                return new Verdict(
                    Decision.Deny,
                    Reason: $"{Name}:block{code}",
                    Message: result.BlockReason,
                    Evidence: new Evidence(
                        $"{Name}-verdict",
                        new Dictionary<string, string> { ["correlation"] = $"urn:a365:{Name}:{Uri.EscapeDataString(result.CorrelationId)}" }),
                    ResultLabels: labels);
            }

            var unverified = new[] { new Warning($"{Name}:unverified", result.Error ?? "no verdict was returned") };
            return result.Allowed
                ? new Verdict(Decision.Allow, Warnings: unverified)
                : new Verdict(
                    Decision.Deny,
                    Reason: $"runtime_error:{Name}_unverified",
                    Message: result.BlockReason ?? "Security validation is unavailable and this agent is configured to fail closed.",
                    Warnings: unverified);
        }

        /// <summary>
        /// Defender's warning reason, or the interceptor's own when it is empty or in the <c>host_error:</c>
        /// namespace, which agent-hooks reserves for host failures: the emitter rejects a verdict that uses it, which
        /// would turn Defender's allow into a fail-closed host error whatever the fail mode.
        /// </summary>
        private static string WarningReason(string? reason) =>
            string.IsNullOrWhiteSpace(reason) || reason.StartsWith(ReservedReasonPrefix, StringComparison.Ordinal)
                ? $"{Name}:warning"
                : reason;
    }

    /// <summary>Agent 365 protection helpers for agent-hooks hosts.</summary>
    public static class A365AgentHooks
    {
        /// <summary>
        /// Creates an emitter for Agent 365 protection: enforce mode and the <c>parallel/strictest</c>
        /// profile (an action proceeds only when every interceptor allows it), so Defender and Purview compose on one
        /// emitter and a deny from either wins.
        /// </summary>
        /// <param name="interceptorTimeout">
        /// Per-interceptor timeout; defaults to the longest timeout of the given Defender options and, when Purview DLP
        /// is enabled, Purview options, plus two seconds (ten seconds plus two when neither counts), so an emitter for
        /// Defender alone is sized as before. Each client evaluates within one deadline, its options' <c>Timeout</c>,
        /// that covers token acquisition and the request, so its fail mode applies before this timeout turns into an
        /// agent-hooks host error.
        /// </param>
        /// <param name="defender">The Defender options, whose timeout sets the default.</param>
        /// <param name="purview">The Purview options, whose timeout sets the default when Purview DLP is enabled.</param>
        /// <returns>The configured emitter.</returns>
        public static InterceptionEmitter CreateProtectionEmitter(
            TimeSpan? interceptorTimeout = null,
            DefenderRtpOptions? defender = null,
            PurviewDlpOptions? purview = null)
        {
            var slowest = defender?.Timeout;
            if (purview is { Enabled: true } && (slowest == null || purview.Timeout > slowest))
            {
                slowest = purview.Timeout;
            }

            return new InterceptionEmitter(
                EnforcementMode.Enforce,
                null,
                interceptorTimeout ?? (slowest ?? TimeSpan.FromSeconds(10)) + TimeSpan.FromSeconds(2))
                .SetComposition(CompositionConfig.Strictest(SynthesisPolicy.Deny));
        }

        /// <summary>Registers the Defender interceptor under the name <c>defender</c>.</summary>
        /// <param name="emitter">The emitter.</param>
        /// <param name="interceptor">The Defender interceptor.</param>
        /// <returns>The emitter.</returns>
        public static InterceptionEmitter AddA365Defender(this InterceptionEmitter emitter, A365DefenderInterceptor interceptor)
        {
            if (emitter == null)
            {
                throw new ArgumentNullException(nameof(emitter));
            }

            return emitter.Register(interceptor ?? throw new ArgumentNullException(nameof(interceptor)), A365DefenderInterceptor.Name);
        }

        /// <summary>Registers the Purview interceptor under the name <c>purview</c>.</summary>
        /// <param name="emitter">The emitter.</param>
        /// <param name="interceptor">The Purview interceptor.</param>
        /// <returns>The emitter.</returns>
        public static InterceptionEmitter AddA365Purview(this InterceptionEmitter emitter, A365PurviewInterceptor interceptor)
        {
            if (emitter == null)
            {
                throw new ArgumentNullException(nameof(emitter));
            }

            return emitter.Register(interceptor ?? throw new ArgumentNullException(nameof(interceptor)), A365PurviewInterceptor.Name);
        }
    }
}
