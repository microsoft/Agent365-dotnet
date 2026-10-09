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
    /// The text is the content's string, or every string and number in structured content, one per line, read within
    /// <see cref="PurviewDlpOptions.MaxContentCharacters"/>. Content without text is allowed without a call, unless
    /// reading stopped at the limit before any text was found: then the verdict follows the fail mode
    /// (<see cref="PurviewDlpClient.UnreadContentError"/>). Text cut at the limit is sent as a truncated copy. Each
    /// evaluation uses the context's session id as the content's <c>correlationId</c> and its sequence as
    /// <c>sequenceNumber</c>.
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

            var sessionId = ReadString((context.Json["session"] as JsonObject)?["id"]);
            var sequence = ReadSequence(context.Json["sequence"]);
            var audit = activity == PurviewDlpActivity.DownloadText && _client.Options.ResponseMode == PurviewDlpResponseMode.Audit;

            string text;
            bool cut;
            try
            {
                text = ContentText(content, _client.Options.MaxContentCharacters, out cut);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Content that cannot be read, for example a parsed object whose property appears twice, is not
                // skipped silently: like any missing verdict, it follows the fail mode.
                _logger?.LogWarning(ex, "The content at {InterceptionPoint} could not be read; the fail mode applies.", point);
                return Decide(audit, _client.Unavailable(activity, $"content could not be read ({ex.GetType().Name})", sessionId));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                // Content without text has nothing for Purview to inspect, unless reading stopped at the limit before any
                // text was found: what was not read could hold some.
                return cut
                    ? Decide(audit, _client.Unavailable(activity, PurviewDlpClient.UnreadContentError, sessionId))
                    : new Verdict(Decision.Allow);
            }

            if (audit)
            {
                AuditInBackground(context, point, text, cut, sessionId, sequence);
                return new Verdict(Decision.Allow);
            }

            PurviewDlpEvaluationResult? result;
            try
            {
                // Without an agent identity Purview cannot be called; like any missing verdict, that follows the fail
                // mode rather than allowing silently.
                result = ResolveCall(context, sessionId, sequence) is { } call
                    ? await _client.EvaluateTextAsync(activity, text, cut, call.Agent, call.TokenResolver, cancellationToken).ConfigureAwait(false)
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
        /// The text Purview inspects in input or output content: a string as it is (the client cuts a long one);
        /// otherwise every string and number in the content, in document order, one per line, with property names left
        /// out as structure. Reading structured content is bounded: at most <paramref name="maxCharacters"/> values and
        /// containers, and one character more of text than the limit, so neither long text nor a large structure of
        /// empty values is read past it; <paramref name="cut"/> says that reading stopped early. The walk keeps its own
        /// stack, so deep content cannot exhaust the call stack.
        /// </summary>
        internal static string ContentText(JsonNode? content, int maxCharacters, out bool cut)
        {
            cut = false;
            if (content is JsonValue single && single.TryGetValue<string>(out var only))
            {
                return only;
            }

            // One character past the limit tells the client the text is longer than it may send.
            var textLimit = (int)Math.Min(int.MaxValue, (long)maxCharacters + 1);
            var nodes = 0;
            var builder = new StringBuilder();
            var pending = new Stack<IEnumerator<JsonNode?>>();
            try
            {
                var node = content;
                while (true)
                {
                    if (++nodes > maxCharacters)
                    {
                        cut = true;
                        break;
                    }

                    switch (node)
                    {
                        case JsonArray array:
                            pending.Push(array.GetEnumerator());
                            break;
                        case JsonObject obj:
                            pending.Push(obj.Select(property => property.Value).GetEnumerator());
                            break;
                        case JsonValue value when value.TryGetValue<string>(out var text):
                            cut = !AppendLine(builder, text, textLimit);
                            break;
                        case JsonValue value when value.GetValueKind() == JsonValueKind.Number:
                            cut = !AppendLine(builder, value.ToJsonString(), textLimit);
                            break;
                    }

                    // The next node is the next child of the innermost container that has one.
                    while (!cut && pending.Count > 0 && !pending.Peek().MoveNext())
                    {
                        pending.Pop().Dispose();
                    }

                    if (cut || pending.Count == 0)
                    {
                        break;
                    }

                    node = pending.Peek().Current;
                }
            }
            finally
            {
                while (pending.Count > 0)
                {
                    pending.Pop().Dispose();
                }
            }

            return builder.ToString();
        }

        /// <summary>Appends a line of text while the text stays within the limit; false when it was cut.</summary>
        private static bool AppendLine(StringBuilder builder, string text, int limit)
        {
            if (text.Length == 0)
            {
                return true;
            }

            if (builder.Length > 0)
            {
                if (builder.Length >= limit)
                {
                    return false;
                }

                builder.Append('\n');
            }

            var kept = Math.Min(limit - builder.Length, text.Length);
            builder.Append(text, 0, kept);
            return kept == text.Length;
        }

        /// <summary>
        /// The verdict for a result decided without a call: it goes to the callback, and a reply audited in the
        /// background is never blocked.
        /// </summary>
        private Verdict Decide(bool audit, PurviewDlpEvaluationResult result)
        {
            Report(result);
            return audit ? new Verdict(Decision.Allow) : ToVerdict(result);
        }

        /// <summary>
        /// Evaluates the reply for audit without waiting: the call is resolved here, on the interception, where the turn
        /// is at hand, and the evaluation runs on the thread pool, bounded by the client's own deadline rather than the
        /// emitter's token. Its outcome, or a failure, goes only to the callback.
        /// </summary>
        private void AuditInBackground(AgentContext context, string point, string text, bool cut, string? sessionId, long? sequence)
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
                        .EvaluateTextAsync(PurviewDlpActivity.DownloadText, text, cut, resolved.Agent, resolved.TokenResolver, CancellationToken.None)
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
