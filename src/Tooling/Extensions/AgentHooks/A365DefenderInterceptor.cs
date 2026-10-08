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

    /// <summary>The agent identity and credentials for the Defender call of one emitted context.</summary>
    /// <param name="Agent">The agent identity and turn; fills context fields the host did not set.</param>
    /// <param name="TokenResolver">Resolves the agent identity's Defender token, for example
    /// <see cref="DefenderRtpTokenResolvers.FromAgenticConnection"/>.</param>
    public sealed record A365DefenderCall(DefenderRtpAgentContext Agent, DefenderRtpTokenResolver TokenResolver);

    /// <summary>
    /// An agent-hooks interceptor for Microsoft Defender for AI real-time protection. Each context the
    /// host emits at <c>input</c>, <c>pre_tool_call</c>, <c>post_tool_call</c> or <c>output</c> is sent to
    /// Defender as-is (keeping its session, sequence and tool call ids) and Defender's verdict decides:
    /// <c>deny</c> blocks the action. Other points are allowed without a call.
    /// </summary>
    /// <remarks>
    /// When no verdict is obtained (transport, authentication or validation failure), the verdict follows
    /// <see cref="DefenderRtpOptions.FailClosed"/>: allow with a <c>defender:unverified</c> warning, or deny
    /// with reason <c>runtime_error:defender_unverified</c>, which is never reported as a detection.
    /// </remarks>
    public sealed class A365DefenderInterceptor : IInterceptor
    {
        /// <summary>The name the interceptor is registered under.</summary>
        public const string Name = "defender";

        private static readonly Regex InvalidReasonCharacters = new("[^A-Za-z0-9_.-]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly DefenderRtpClient _client;
        private readonly Func<AgentContext, A365DefenderCall?> _resolveCall;
        private readonly Action<DefenderRtpEvaluationResult>? _onEvaluated;

        /// <summary>Initializes a new instance of the <see cref="A365DefenderInterceptor"/> class.</summary>
        /// <param name="client">The Defender client.</param>
        /// <param name="resolveCall">
        /// Returns the agent identity and token resolver for a context, for example from the current turn;
        /// null allows the context without a call.
        /// </param>
        /// <param name="onEvaluated">Receives each evaluation, for logging and telemetry (for example the correlation id).</param>
        public A365DefenderInterceptor(
            DefenderRtpClient client,
            Func<AgentContext, A365DefenderCall?> resolveCall,
            Action<DefenderRtpEvaluationResult>? onEvaluated = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _resolveCall = resolveCall ?? throw new ArgumentNullException(nameof(resolveCall));
            _onEvaluated = onEvaluated;
        }

        /// <inheritdoc/>
        public async ValueTask<Verdict> InterceptAsync(AgentContext context, CancellationToken cancellationToken)
        {
            var call = _resolveCall(context);
            if (call == null)
            {
                return new Verdict(Decision.Allow);
            }

            DefenderRtpEvaluationResult? result;
            try
            {
                result = await _client.EvaluateHookContextAsync(context.Json, call.Agent, call.TokenResolver, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // An invalid context or identity is never a verdict: it follows the fail mode.
                result = _client.Unavailable(InterceptionPointExtensions.ToWireName(context.InterceptionPoint), $"{ex.GetType().Name}: {ex.Message}");
            }

            if (result == null)
            {
                return new Verdict(Decision.Allow);
            }

            _onEvaluated?.Invoke(result);
            return ToVerdict(result);
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

            if (result.Evaluated)
            {
                var labels = result.Verdict?.ResultLabels is { Count: > 0 } resultLabels ? resultLabels : null;
                if (result.Allowed)
                {
                    var warnings = result.Verdict?.Warnings
                        .Select(warning => new Warning(warning.Reason ?? $"{Name}:warning", warning.Message ?? string.Empty))
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
    }

    /// <summary>Agent 365 protection helpers for agent-hooks hosts.</summary>
    public static class A365AgentHooks
    {
        /// <summary>
        /// Creates an emitter for Agent 365 protection: enforce mode and the <c>parallel/strictest</c>
        /// profile (an action proceeds only when every interceptor allows it).
        /// </summary>
        /// <param name="interceptorTimeout">
        /// Per-interceptor timeout; defaults to the Defender timeout plus two seconds, so the client's own
        /// timeout and fail mode apply first.
        /// </param>
        /// <param name="defender">The Defender options whose timeout sets the default.</param>
        /// <returns>The configured emitter.</returns>
        public static InterceptionEmitter CreateProtectionEmitter(TimeSpan? interceptorTimeout = null, DefenderRtpOptions? defender = null) =>
            new InterceptionEmitter(
                EnforcementMode.Enforce,
                null,
                interceptorTimeout ?? (defender?.Timeout ?? TimeSpan.FromSeconds(10)) + TimeSpan.FromSeconds(2))
                .SetComposition(CompositionConfig.Strictest(SynthesisPolicy.Deny));

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
    }
}
