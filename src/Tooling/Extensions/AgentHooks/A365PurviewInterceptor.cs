// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentHooks
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using global::AgentHooks;
    using Microsoft.Agents.A365.Tooling.Protection.Purview;
    using Microsoft.Extensions.Logging;

    /// <summary>The agent and credentials for the Purview call of one emitted context.</summary>
    /// <param name="Agent">
    /// The agent and turn. It is not modified: the evaluation uses a copy with the context's <c>session.id</c> and
    /// <c>sequence</c>.
    /// </param>
    /// <param name="TokenResolver">Resolves the Microsoft Graph token, for example
    /// <see cref="PurviewDlpTokenResolvers.FromAgenticUser"/>.</param>
    public sealed record A365PurviewCall(PurviewDlpAgentContext Agent, PurviewDlpTokenResolver TokenResolver);

    /// <summary>
    /// An agent-hooks interceptor for Microsoft Purview data loss prevention (DLP) and audit. The user's message at
    /// <c>input</c> is evaluated as <c>uploadText</c>, and a policy action that blocks it denies the action. The reply
    /// at <c>output</c> is evaluated as <c>downloadText</c>: by default for audit only, without waiting
    /// (<see cref="PurviewDlpResponseMode.Audit"/>), or, with <see cref="PurviewDlpResponseMode.Enforce"/>, waiting for
    /// a verdict that decides like the input's. Other points are allowed without a call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The text is the content's string, or every string and number in structured content, one per line. Empty content
    /// is allowed without a call. Each evaluation uses the context's session id as the content's
    /// <c>correlationId</c> and its sequence as <c>sequenceNumber</c>.
    /// </para>
    /// <para>
    /// When no verdict is obtained (no agent identity, identity resolution, token, transport or HTTP failure, or
    /// processing errors Graph reports inline), or Purview allowed a truncated copy
    /// (<see cref="PurviewDlpEvaluationResult.Truncated"/>), the verdict follows
    /// <see cref="PurviewDlpOptions.FailClosed"/>: allow with a <c>purview:unverified</c> warning, or deny with reason
    /// <c>runtime_error:purview_unverified</c>, which is never reported as a detection. A reply audited in the
    /// background is never blocked, whatever the fail mode.
    /// </para>
    /// <para>
    /// Register it on an emitter whose per-interceptor timeout is longer than <see cref="PurviewDlpOptions.Timeout"/>,
    /// such as one from <see cref="A365AgentHooks.CreateProtectionEmitter"/>; a shorter timeout turns a slow evaluation
    /// into a fail-closed agent-hooks host error. The evaluation callback runs on the thread pool once the verdict is
    /// decided, outside that timeout, and never changes the verdict: an exception it throws is logged and ignored.
    /// </para>
    /// </remarks>
    public sealed class A365PurviewInterceptor : IInterceptor
    {
        /// <summary>The name the interceptor is registered under.</summary>
        public const string Name = "purview";

        private const string NoIdentity = "no agent identity was resolved";
        private const string Blocked = "The content was blocked by a Microsoft Purview data loss prevention policy.";
        private const string UnavailableFailClosed =
            "Data loss prevention validation is unavailable and this agent is configured to fail closed.";

        private readonly PurviewDlpClient _client;
        private readonly Func<AgentContext, A365PurviewCall?> _resolveCall;
        private readonly Action<PurviewDlpEvaluationResult>? _onEvaluated;
        private readonly ILogger? _logger;

        /// <summary>Initializes a new instance of the <see cref="A365PurviewInterceptor"/> class.</summary>
        /// <param name="client">The Purview client.</param>
        /// <param name="resolveCall">
        /// Returns the agent and token resolver for a context, for example from the current turn. It is invoked only
        /// when Purview DLP is enabled, at <c>input</c> and <c>output</c>, for content with text. When it returns null
        /// (no agent identity is available) or throws, the content is not sent and the verdict follows the fail mode;
        /// the result records only the type of an exception, which is logged to <paramref name="logger"/>.
        /// </param>
        /// <param name="onEvaluated">
        /// Receives each evaluation for logging and telemetry (for example the correlation id), including the
        /// background audit of replies. It runs on the thread pool once the verdict is decided, so it may run after the
        /// interceptor returns and alongside later evaluations. It never changes the verdict: an exception it throws is
        /// logged to <paramref name="logger"/>, when given, and ignored.
        /// </param>
        /// <param name="logger">Receives failures of the evaluation and of <paramref name="onEvaluated"/>.</param>
        public A365PurviewInterceptor(
            PurviewDlpClient client,
            Func<AgentContext, A365PurviewCall?> resolveCall,
            Action<PurviewDlpEvaluationResult>? onEvaluated = null,
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
            if (!_client.Options.Enabled)
            {
                return new Verdict(Decision.Allow);
            }

            var point = InterceptionPointExtensions.ToWireName(context.InterceptionPoint);
            PurviewDlpActivity activity;
            JsonNode? content;
            switch (point)
            {
                case "input":
                    activity = PurviewDlpActivity.UploadText;
                    content = (context.Json["input"] as JsonObject)?["content"];
                    break;
                case "output":
                    activity = PurviewDlpActivity.DownloadText;
                    content = (context.Json["output"] as JsonObject)?["content"];
                    break;
                default:
                    return new Verdict(Decision.Allow);
            }

            // Content without text has nothing for Purview to inspect.
            var text = ContentText(content);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new Verdict(Decision.Allow);
            }

            var sessionId = ReadString((context.Json["session"] as JsonObject)?["id"]);
            var sequence = ReadSequence(context.Json["sequence"]);
            if (activity == PurviewDlpActivity.DownloadText && _client.Options.ResponseMode == PurviewDlpResponseMode.Audit)
            {
                AuditInBackground(context, point, text, sessionId, sequence);
                return new Verdict(Decision.Allow);
            }

            PurviewDlpEvaluationResult? result;
            try
            {
                // Without an agent identity Purview cannot be called; like any missing verdict, that follows the fail
                // mode rather than allowing silently.
                result = ResolveCall(context, sessionId, sequence) is { } call
                    ? await _client.EvaluateAsync(activity, text, call.Agent, call.TokenResolver, cancellationToken).ConfigureAwait(false)
                    : _client.Unavailable(activity, NoIdentity, sessionId);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // A failure to resolve the identity or to evaluate is never a verdict: it follows the fail mode. Only the
                // exception's type reaches the result, since its message can carry credentials or content; the exception
                // itself goes to the logger.
                _logger?.LogWarning(ex, "The Purview evaluation failed at {InterceptionPoint}; the fail mode applies.", point);
                result = _client.Unavailable(activity, $"evaluation failed ({ex.GetType().Name})", sessionId);
            }

            if (result == null)
            {
                return new Verdict(Decision.Allow);
            }

            // The verdict is decided before the callback sees the result, and the callback runs on the thread pool, off
            // the emitter's timed interception.
            var verdict = ToVerdict(result);
            Report(result);
            return verdict;
        }

        /// <summary>Maps a Purview evaluation to the agent-hooks verdict the host composes.</summary>
        /// <param name="result">The Purview evaluation.</param>
        /// <returns>The agent-hooks verdict.</returns>
        public static Verdict ToVerdict(PurviewDlpEvaluationResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            // Purview's allow of a truncated copy does not cover the text that was cut, so it is reported like a missing
            // verdict; a block of a truncated copy stands.
            var authoritative = result.Evaluated && !(result.Truncated && !result.Decision.BlockAction);
            if (authoritative)
            {
                return result.Allowed
                    ? new Verdict(Decision.Allow)
                    : new Verdict(
                        Decision.Deny,
                        Reason: $"{Name}:block",
                        Message: result.BlockReason ?? Blocked,
                        Evidence: new Evidence(
                            $"{Name}-verdict",
                            new Dictionary<string, string> { ["correlation"] = $"urn:a365:{Name}:{Uri.EscapeDataString(result.CorrelationId)}" }));
            }

            var unverified = new[] { new Warning($"{Name}:unverified", result.Error ?? "no verdict was returned") };
            return result.Allowed
                ? new Verdict(Decision.Allow, Warnings: unverified)
                : new Verdict(
                    Decision.Deny,
                    Reason: $"runtime_error:{Name}_unverified",
                    Message: result.BlockReason ?? UnavailableFailClosed,
                    Warnings: unverified);
        }

        /// <summary>
        /// The text Purview inspects in input or output content: a string as it is; otherwise every string and number in
        /// the content, in document order, one per line. Property names are structure, not content, and are left out.
        /// The walk keeps its own stack, so content of any depth is read in full.
        /// </summary>
        internal static string ContentText(JsonNode? content)
        {
            if (content is JsonValue single && single.TryGetValue<string>(out var only))
            {
                return only;
            }

            var builder = new StringBuilder();
            var pending = new Stack<JsonNode?>();
            pending.Push(content);
            while (pending.Count > 0)
            {
                switch (pending.Pop())
                {
                    case JsonArray array:
                        for (var index = array.Count - 1; index >= 0; index--)
                        {
                            pending.Push(array[index]);
                        }

                        break;
                    case JsonObject obj:
                        foreach (var property in obj.Reverse())
                        {
                            pending.Push(property.Value);
                        }

                        break;
                    case JsonValue value when value.TryGetValue<string>(out var text):
                        AppendLine(builder, text);
                        break;
                    case JsonValue value when value.GetValueKind() == JsonValueKind.Number:
                        AppendLine(builder, value.ToJsonString());
                        break;
                }
            }

            return builder.ToString();
        }

        private static void AppendLine(StringBuilder builder, string text)
        {
            if (text.Length == 0)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(text);
        }

        /// <summary>
        /// Evaluates the reply for audit without waiting: the call is resolved here, on the interception, where the turn
        /// is at hand, and the evaluation runs on the thread pool, bounded by the client's own deadline rather than the
        /// emitter's token. Its outcome, or a failure, goes only to the callback.
        /// </summary>
        private void AuditInBackground(AgentContext context, string point, string text, string? sessionId, long? sequence)
        {
            (PurviewDlpAgentContext Agent, PurviewDlpTokenResolver TokenResolver)? call;
            try
            {
                call = ResolveCall(context, sessionId, sequence);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Resolving the Purview call failed at {InterceptionPoint}; the reply is not audited.", point);
                Report(_client.Unavailable(PurviewDlpActivity.DownloadText, $"evaluation failed ({ex.GetType().Name})", sessionId));
                return;
            }

            if (call is not { } resolved)
            {
                Report(_client.Unavailable(PurviewDlpActivity.DownloadText, NoIdentity, sessionId));
                return;
            }

            _ = Task.Run(async () =>
            {
                PurviewDlpEvaluationResult? result;
                try
                {
                    result = await _client
                        .EvaluateAsync(PurviewDlpActivity.DownloadText, text, resolved.Agent, resolved.TokenResolver, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "The Purview audit of the reply failed at {InterceptionPoint}.", point);
                    result = _client.Unavailable(PurviewDlpActivity.DownloadText, $"evaluation failed ({ex.GetType().Name})", sessionId);
                }

                if (result != null && _onEvaluated is { } onEvaluated)
                {
                    NotifyEvaluated(onEvaluated, result);
                }
            });
        }

        /// <summary>
        /// The agent and resolver for the context, with the context's session and sequence, or null when no agent
        /// identity was resolved.
        /// </summary>
        private (PurviewDlpAgentContext Agent, PurviewDlpTokenResolver TokenResolver)? ResolveCall(
            AgentContext context,
            string? sessionId,
            long? sequence) =>
            _resolveCall(context) is { Agent: { } agent, TokenResolver: { } tokenResolver }
                ? (agent.WithSession(sessionId, sequence), tokenResolver)
                : null;

        /// <summary>Hands a result to the callback on the thread pool, off the emitter's timed interception.</summary>
        private void Report(PurviewDlpEvaluationResult result)
        {
            if (_onEvaluated is { } onEvaluated)
            {
                _ = Task.Run(() => NotifyEvaluated(onEvaluated, result));
            }
        }

        /// <summary>
        /// Hands the evaluation to the host's callback. The callback is for logging and telemetry, so its failure is
        /// logged at most and never changes the verdict, in either fail mode.
        /// </summary>
        private void NotifyEvaluated(Action<PurviewDlpEvaluationResult> onEvaluated, PurviewDlpEvaluationResult result)
        {
            try
            {
                onEvaluated(result);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _logger?.LogWarning(
                    ex,
                    "The Purview evaluation callback failed for {Activity}; the verdict is unchanged. CorrelationId: {CorrelationId}",
                    result.Activity,
                    result.CorrelationId);
            }
        }

        private static string? ReadString(JsonNode? node) =>
            node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        // Reads the number's JSON text, so a sequence a host built from any integer type counts.
        private static long? ReadSequence(JsonNode? node) =>
            node is JsonValue value
            && value.GetValueKind() == JsonValueKind.Number
            && long.TryParse(value.ToJsonString(), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
                ? sequence
                : null;
    }
}
