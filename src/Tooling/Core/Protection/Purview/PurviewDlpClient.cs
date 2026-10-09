// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Protection.Purview
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Client for Microsoft Purview data loss prevention (DLP) and audit through the Microsoft Graph
    /// <c>processContent</c> API.
    /// </summary>
    /// <remarks>
    /// Each evaluation sends one text to <c>POST {GraphBaseUrl}/me/dataSecurityAndGovernance/processContent</c> (or
    /// <c>/users/{id}/...</c> when the token resolver names a user). Purview applies the DLP policies scoped to the
    /// agent's application and writes the audit record. A policy action whose <c>restrictionAction</c> is
    /// <c>block</c> blocks; other actions allow. Each call carries a unique <c>client-request-id</c>, returned as
    /// <see cref="PurviewDlpEvaluationResult.CorrelationId"/>.
    /// </remarks>
    public sealed class PurviewDlpClient
    {
        /// <summary>The header Graph logs each call under.</summary>
        public const string ClientRequestIdHeader = "client-request-id";

        /// <summary>
        /// The <see cref="PurviewDlpEvaluationResult.Error"/> of a result whose text was longer than
        /// <see cref="PurviewDlpOptions.MaxContentCharacters"/> and that Purview allowed: Purview evaluated a truncated
        /// copy, so the result follows <see cref="PurviewDlpOptions.FailClosed"/>.
        /// </summary>
        public const string TruncatedContentError = "content exceeded MaxContentCharacters; Purview evaluated a truncated copy";

        /// <summary>The agent name used when <see cref="PurviewDlpAgentContext.AgentName"/> is not set.</summary>
        public const string DefaultAgentName = "agent365-agent";

        private const string ProcessContentPath = "/dataSecurityAndGovernance/processContent";
        private const string AppVersion = "1.0";
        private const string RequestBlocked = "The request was blocked by a Microsoft Purview data loss prevention policy.";
        private const string ResponseBlocked = "The response was blocked by a Microsoft Purview data loss prevention policy.";
        private const string UnavailableFailClosed =
            "Data loss prevention validation is unavailable and this agent is configured to fail closed.";
        private const string TruncatedFailClosed =
            "Microsoft Purview could not evaluate all of the content and this agent is configured to fail closed.";

        // Codes and error types Graph reports in processingErrors are kept only when they look like identifiers, so no
        // message text from the response reaches the result.
        private static readonly Regex DiagnosticToken = new("^[A-Za-z][A-Za-z0-9_.-]{0,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Used when the caller passes no HttpClient. Pooled connections are recycled so DNS changes are picked up, and
        // redirects are never followed: a 307 or 308 would replay the token and the agent's content to another host.
        internal static readonly SocketsHttpHandler SharedHandler = new()
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AllowAutoRedirect = false,
        };

        internal static readonly HttpClient SharedHttpClient = new(SharedHandler);

        private readonly HttpClient _httpClient;
        private readonly Func<Guid> _idFactory;
        private readonly TimeProvider _timeProvider;
        private readonly ConcurrentDictionary<string, long> _sequences = new(StringComparer.Ordinal);

        // The first sequence number a session the client does not track gets: above every number given to a session it
        // dropped, so a session that comes back keeps increasing.
        private long _sequenceFloor;

        /// <summary>Initializes a new instance of the <see cref="PurviewDlpClient"/> class.</summary>
        /// <param name="options">The Purview configuration, for example <see cref="PurviewDlpOptions.FromEnvironment()"/>.</param>
        /// <param name="httpClient">
        /// The HTTP client, for example from <c>IHttpClientFactory</c>; defaults to a shared client that does not
        /// follow redirects. A client passed here must not follow them either: a redirected request is treated as a
        /// failure, since the token and content have been sent to another host.
        /// </param>
        /// <param name="idFactory">Creates request and content ids (tests).</param>
        /// <param name="timeProvider">The clock (tests).</param>
        public PurviewDlpClient(
            PurviewDlpOptions options,
            HttpClient? httpClient = null,
            Func<Guid>? idFactory = null,
            TimeProvider? timeProvider = null)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            options.Validate();
            _httpClient = httpClient ?? SharedHttpClient;
            _idFactory = idFactory ?? Guid.NewGuid;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>Gets the configuration this client uses.</summary>
        public PurviewDlpOptions Options { get; }

        /// <summary>How many sessions' sequences are tracked; settable for tests, so eviction needs few sessions.</summary>
        internal int MaxTrackedSessions { get; init; } = 1000;

        /// <summary>
        /// Evaluates a text with Purview: <see cref="PurviewDlpActivity.UploadText"/> for content entering the agent,
        /// such as the user's prompt, or <see cref="PurviewDlpActivity.DownloadText"/> for its reply.
        /// </summary>
        /// <remarks>
        /// One deadline, <see cref="PurviewDlpOptions.Timeout"/>, covers token acquisition and the request. When no
        /// verdict is obtained (token, transport or HTTP failure, an unexpected response, or processing errors that
        /// Graph reports inline) the result follows <see cref="PurviewDlpOptions.FailClosed"/>. A text longer than
        /// <see cref="PurviewDlpOptions.MaxContentCharacters"/> is cut and sent with <c>isTruncated</c>; Purview's block
        /// stands, but its allow follows the fail mode (<see cref="PurviewDlpEvaluationResult.Truncated"/>). A block
        /// also stands when Graph reports processing errors, or actions of another shape, with it.
        /// </remarks>
        /// <param name="activity">The activity the text belongs to.</param>
        /// <param name="text">The text to evaluate.</param>
        /// <param name="agent">
        /// The agent and turn: <see cref="PurviewDlpAgentContext.SessionId"/> and one of
        /// <see cref="PurviewDlpAgentContext.ApplicationId"/>, <see cref="PurviewDlpAgentContext.BlueprintId"/> or
        /// <see cref="PurviewDlpAgentContext.AgentId"/> are required.
        /// </param>
        /// <param name="tokenResolver">Resolves the Microsoft Graph token, for example <see cref="PurviewDlpTokenResolvers.FromAgenticUser"/>.</param>
        /// <param name="cancellationToken">Cancels the evaluation.</param>
        /// <returns>The result, or null (and no call) when Purview DLP is disabled or the text is empty.</returns>
        /// <exception cref="ArgumentException">The agent context has no session id or application id.</exception>
        public async Task<PurviewDlpEvaluationResult?> EvaluateAsync(
            PurviewDlpActivity activity,
            string? text,
            PurviewDlpAgentContext agent,
            PurviewDlpTokenResolver tokenResolver,
            CancellationToken cancellationToken = default)
        {
            if (!Options.Enabled)
            {
                return null;
            }

            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            if (tokenResolver == null)
            {
                throw new ArgumentNullException(nameof(tokenResolver));
            }

            if (activity is not (PurviewDlpActivity.UploadText or PurviewDlpActivity.DownloadText))
            {
                throw new ArgumentOutOfRangeException(nameof(activity));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var sessionId = FirstNonEmpty(agent.SessionId)
                ?? throw new ArgumentException("The agent context's SessionId is required.", nameof(agent));
            var applicationId = FirstNonEmpty(agent.ApplicationId, agent.BlueprintId, agent.AgentId)
                ?? throw new ArgumentException("The agent context's ApplicationId, BlueprintId or AgentId is required.", nameof(agent));
            var started = _timeProvider.GetTimestamp();
            var correlationId = _idFactory().ToString();
            var sequence = agent.SequenceNumber is long given && given >= 0 ? given : NextSequence(sessionId);

            // Text over the limit is cut, never split inside a surrogate pair, and sent with isTruncated set.
            var maxCharacters = Options.MaxContentCharacters;
            var truncated = text.Length > maxCharacters;
            var data = ReplaceLoneSurrogates(truncated ? text.Substring(0, KeepSurrogatePairs(text, maxCharacters)) : text);

            // One deadline covers token acquisition and the request, so the fail mode applies within Options.Timeout,
            // before an agent-hooks interceptor timeout would.
            using var timeout = new CancellationTokenSource(Options.Timeout, _timeProvider);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

            PurviewDlpToken? token;
            try
            {
                token = await ResolveTokenAsync(agent, tokenResolver, Options.AuthenticationScope, deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Failure(activity, correlationId, sessionId, "entra token timeout", null, started);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Only the type: the exception's message can carry credentials.
                return Failure(activity, correlationId, sessionId, $"entra token unavailable ({ex.GetType().Name})", null, started);
            }

            if (token == null)
            {
                return Failure(activity, correlationId, sessionId, "entra token unavailable", null, started);
            }

            // A token that arrives as the deadline passes is not used: the evaluation is out of time, and a handler that
            // ignores cancellation would otherwise still send the content.
            if (deadline.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Failure(activity, correlationId, sessionId, "entra token timeout", null, started);
            }

            var body = BuildBody(activity, data, truncated, agent, applicationId, sessionId, sequence);
            return await PostAsync(activity, body, token, correlationId, sessionId, truncated, started, deadline.Token, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// A result for an evaluation that could not be made (for example, no agent identity): it follows
        /// <see cref="PurviewDlpOptions.FailClosed"/>, like a transport failure.
        /// </summary>
        /// <param name="activity">The activity.</param>
        /// <param name="error">Why no verdict was obtained.</param>
        /// <param name="sessionId">The session id, when known.</param>
        /// <param name="httpStatus">The HTTP status, when a response was received.</param>
        /// <param name="latency">Time spent before the failure.</param>
        /// <returns>The not-evaluated result.</returns>
        public PurviewDlpEvaluationResult Unavailable(
            PurviewDlpActivity activity,
            string error,
            string? sessionId = null,
            int? httpStatus = null,
            TimeSpan latency = default) => new()
            {
                Allowed = !Options.FailClosed,
                Evaluated = false,
                Activity = activity,
                CorrelationId = _idFactory().ToString(),
                SessionId = sessionId,
                HttpStatus = httpStatus,
                Latency = latency,
                Error = error,
                BlockReason = Options.FailClosed ? UnavailableFailClosed : null,
            };

        /// <summary>
        /// Calls the resolver within the deadline, which also bounds a resolver that ignores cancellation. It starts on
        /// the thread pool, so a resolver that blocks before it returns its task is bounded too, and the caller, such as
        /// an agent-hooks emitter timing the interceptor, is never held by it.
        /// </summary>
        private static async Task<PurviewDlpToken?> ResolveTokenAsync(
            PurviewDlpAgentContext agent,
            PurviewDlpTokenResolver tokenResolver,
            string scope,
            CancellationToken deadline)
        {
            var resolving = Task.Run(
                () => tokenResolver(agent, scope, deadline) ?? throw new InvalidOperationException("The token resolver returned no task."),
                CancellationToken.None);
            try
            {
                return await resolving.WaitAsync(deadline).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                ObserveFailure(resolving);
                throw;
            }
        }

        /// <summary>Observes the failure of a task no caller awaits, so it is not reported as unobserved.</summary>
        private static void ObserveFailure(Task task) =>
            _ = task.ContinueWith(
                static completed => { _ = completed.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

        /// <summary>
        /// The <c>processContent</c> request: one conversation entry with the text, the agent, the session and its
        /// sequence, and the application the DLP policies are scoped to.
        /// </summary>
        private JsonObject BuildBody(
            PurviewDlpActivity activity,
            string data,
            bool truncated,
            PurviewDlpAgentContext agent,
            string applicationId,
            string sessionId,
            long sequence)
        {
            // Graph reports an entry without a name as a permanent BadRequest in processingErrors, with HTTP 200 and no
            // policy actions, so the name is never empty.
            var agentName = ReplaceLoneSurrogates(FirstNonEmpty(agent.AgentName) ?? DefaultAgentName);
            var activityName = ActivityName(activity);
            var timestamp = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            var agentInfo = new JsonObject { ["@odata.type"] = "microsoft.graph.aiAgentInfo" };
            if (FirstNonEmpty(agent.BlueprintId) is { } blueprintId)
            {
                agentInfo["blueprintId"] = ReplaceLoneSurrogates(blueprintId);
            }

            agentInfo["identifier"] = ReplaceLoneSurrogates(FirstNonEmpty(agent.AgentId) ?? applicationId);
            agentInfo["name"] = agentName;
            agentInfo["version"] = AppVersion;

            return new JsonObject
            {
                ["contentToProcess"] = new JsonObject
                {
                    ["contentEntries"] = new JsonArray(new JsonObject
                    {
                        ["@odata.type"] = "microsoft.graph.processConversationMetadata",
                        ["identifier"] = _idFactory().ToString(),
                        ["content"] = new JsonObject
                        {
                            ["@odata.type"] = "microsoft.graph.textContent",
                            ["data"] = data,
                        },
                        ["agents"] = new JsonArray(agentInfo),
                        ["name"] = $"{agentName} {activityName}",
                        ["correlationId"] = ReplaceLoneSurrogates(sessionId),
                        ["sequenceNumber"] = sequence,
                        ["isTruncated"] = truncated,
                        ["createdDateTime"] = timestamp,
                        ["modifiedDateTime"] = timestamp,
                        ["contentCategory"] = "ai",
                    }),
                    ["activityMetadata"] = new JsonObject { ["activity"] = activityName },
                    ["integratedAppMetadata"] = new JsonObject { ["name"] = agentName, ["version"] = AppVersion },
                    ["protectedAppMetadata"] = new JsonObject
                    {
                        ["name"] = agentName,
                        ["version"] = AppVersion,
                        ["applicationLocation"] = new JsonObject
                        {
                            ["@odata.type"] = "microsoft.graph.policyLocationApplication",
                            ["value"] = ReplaceLoneSurrogates(applicationId),
                        },
                    },
                },
            };
        }

        /// <summary>Posts the request within <paramref name="deadline"/>, the evaluation's one deadline, and reads the verdict.</summary>
        private async Task<PurviewDlpEvaluationResult> PostAsync(
            PurviewDlpActivity activity,
            JsonObject body,
            PurviewDlpToken token,
            string correlationId,
            string sessionId,
            bool truncated,
            long started,
            CancellationToken deadline,
            CancellationToken cancellationToken)
        {
            // Options is mutable: read the base URL once, and never send the token or content to one that is not HTTPS.
            var baseUrl = Options.GraphBaseUrl;
            if (!PurviewDlpOptions.IsHttpsBaseUrl(baseUrl))
            {
                return Failure(activity, correlationId, sessionId, "graph base URL is not an absolute https URL", null, started);
            }

            var user = token.UserId == null ? "/me" : "/users/" + Uri.EscapeDataString(token.UserId);
            var url = new Uri(baseUrl.AbsoluteUri.TrimEnd('/') + user + ProcessContentPath);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            request.Headers.TryAddWithoutValidation(ClientRequestIdHeader, correlationId);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, deadline).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Failure(activity, correlationId, sessionId, "request timeout", null, started);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Any transport failure, including resilience handlers' exceptions, is no verdict and follows the
                // fail mode; only the exception's type is kept.
                return Failure(activity, correlationId, sessionId, $"request failed ({ex.GetType().Name})", null, started);
            }

            using (response)
            {
                var status = (int)response.StatusCode;

                // A client that follows redirects has sent the token and content to another host; what came back is no
                // verdict.
                if (request.RequestUri != url)
                {
                    return Failure(activity, correlationId, sessionId, "request was redirected", status, started);
                }

                // Accepted without an inline verdict: Purview processes the content and applies no restriction to it.
                if (status is 202 or 204)
                {
                    return Verdict(activity, correlationId, sessionId, new PurviewDlpDecision(), null, status, truncated, started);
                }

                // The body of an error is never read into the result: it can echo the request.
                if (!response.IsSuccessStatusCode)
                {
                    return Failure(activity, correlationId, sessionId, $"http {status}", status, started);
                }

                string content;
                try
                {
                    content = await response.Content.ReadAsStringAsync(deadline).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return Failure(activity, correlationId, sessionId, "request timeout", status, started);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    return Failure(activity, correlationId, sessionId, $"response body could not be read ({ex.GetType().Name})", status, started);
                }

                JsonNode? payload;
                try
                {
                    payload = JsonNode.Parse(content);
                }
                catch (JsonException)
                {
                    return Failure(activity, correlationId, sessionId, "non-JSON response", status, started);
                }

                return Interpret(activity, payload, correlationId, sessionId, truncated, status, started);
            }
        }

        /// <summary>
        /// Reads Purview's verdict, checking the shape of each node. A block action blocks, even when processing errors or
        /// actions of another shape come with it. Otherwise processing errors, or a response of another shape, are no
        /// verdict: Graph always returns <c>policyActions</c>, empty when nothing applies, so a body without it, such
        /// as another service's, is not an allow.
        /// </summary>
        private PurviewDlpEvaluationResult Interpret(
            PurviewDlpActivity activity,
            JsonNode? payload,
            string correlationId,
            string sessionId,
            bool truncated,
            int status,
            long started)
        {
            if (payload is not JsonObject response)
            {
                return Failure(activity, correlationId, sessionId, "response contained no verdict", status, started);
            }

            JsonNode? policyActions;
            JsonNode? processingErrors;
            string? scopeState;
            try
            {
                policyActions = response["policyActions"];
                processingErrors = response["processingErrors"];
                scopeState = ReadString(response["protectionScopeState"]);
            }
            catch (ArgumentException)
            {
                // JsonObject builds its dictionary when a member is first read, so a property that appears twice
                // surfaces here: the response is ambiguous, not a verdict.
                return Failure(activity, correlationId, sessionId, "response contained duplicate properties", status, started);
            }

            if (policyActions is not JsonArray actions)
            {
                var error = policyActions == null ? "response contained no verdict" : "response contained unexpected policyActions";
                return Failure(activity, correlationId, sessionId, error, status, started, scopeState: scopeState);
            }

            var decision = ReadDecision(actions, out var malformed);
            if (!decision.BlockAction)
            {
                if (malformed)
                {
                    return Failure(activity, correlationId, sessionId, "response contained unexpected policyActions", status, started, decision, scopeState);
                }

                switch (processingErrors)
                {
                    case null:
                        break;
                    case JsonArray errors when errors.Count == 0:
                        break;
                    case JsonArray errors:
                        // A permanent BadRequest is reported here with HTTP 200 and no policy actions: the content was
                        // not evaluated, so it is not an allow.
                        return Failure(activity, correlationId, sessionId, ProcessingErrors(errors), status, started, decision, scopeState);
                    default:
                        return Failure(activity, correlationId, sessionId, "response contained unexpected processingErrors", status, started, decision, scopeState);
                }
            }

            return Verdict(activity, correlationId, sessionId, decision, scopeState, status, truncated, started);
        }

        /// <summary>
        /// The policy actions. Any action restricted with <c>block</c> blocks, whatever shape the other actions have;
        /// <paramref name="malformed"/> is set when an action is not an object whose <c>restrictionAction</c>, when
        /// present, is a string.
        /// </summary>
        private static PurviewDlpDecision ReadDecision(JsonArray actions, out bool malformed)
        {
            malformed = false;
            string? blocking = null;
            foreach (var entry in actions)
            {
                if (!TryReadRestriction(entry, out var restriction))
                {
                    malformed = true;
                }
                else if (blocking == null && restriction != null && restriction.Equals("block", StringComparison.OrdinalIgnoreCase))
                {
                    blocking = restriction;
                }
            }

            return new PurviewDlpDecision
            {
                BlockAction = blocking != null,
                RestrictionAction = blocking,
                ActionCount = actions.Count,
            };
        }

        /// <summary>
        /// An action's <c>restrictionAction</c>, and whether the action has the expected shape: an object whose
        /// <c>restrictionAction</c>, when present, is a string, and whose properties each appear once.
        /// </summary>
        private static bool TryReadRestriction(JsonNode? entry, out string? restriction)
        {
            restriction = null;
            if (entry is not JsonObject action)
            {
                return false;
            }

            try
            {
                var value = action["restrictionAction"];
                restriction = ReadString(value);
                return value == null || restriction != null;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>The processing errors as a count, with the codes and error types that look like identifiers.</summary>
        private static string ProcessingErrors(JsonArray errors)
        {
            var details = new List<string>();
            foreach (var error in errors.OfType<JsonObject>())
            {
                try
                {
                    foreach (var value in new[] { ReadString(error["code"]), ReadString(error["errorType"]) })
                    {
                        if (value != null && details.Count < 8 && DiagnosticToken.IsMatch(value) && !details.Contains(value))
                        {
                            details.Add(value);
                        }
                    }
                }
                catch (ArgumentException)
                {
                    // An error whose properties repeat still counts; its codes are not read.
                }
            }

            return details.Count > 0
                ? $"processing errors: {errors.Count} ({string.Join(", ", details)})"
                : $"processing errors: {errors.Count}";
        }

        /// <summary>
        /// A verdict: a block stands whatever was cut; an allow of a truncated copy does not cover the rest, so it
        /// follows the fail mode.
        /// </summary>
        private PurviewDlpEvaluationResult Verdict(
            PurviewDlpActivity activity,
            string correlationId,
            string sessionId,
            PurviewDlpDecision decision,
            string? scopeState,
            int status,
            bool truncated,
            long started)
        {
            var allowed = true;
            string? error = null;
            string? blockReason = null;
            if (decision.BlockAction)
            {
                allowed = false;
                blockReason = activity == PurviewDlpActivity.UploadText ? RequestBlocked : ResponseBlocked;
            }
            else if (truncated)
            {
                allowed = !Options.FailClosed;
                error = TruncatedContentError;
                blockReason = allowed ? null : TruncatedFailClosed;
            }

            return new PurviewDlpEvaluationResult
            {
                Allowed = allowed,
                Evaluated = true,
                Truncated = truncated,
                Activity = activity,
                CorrelationId = correlationId,
                SessionId = sessionId,
                Decision = decision,
                ProtectionScopeState = scopeState,
                HttpStatus = status,
                Latency = _timeProvider.GetElapsedTime(started),
                Error = error,
                BlockReason = blockReason,
            };
        }

        private PurviewDlpEvaluationResult Failure(
            PurviewDlpActivity activity,
            string correlationId,
            string sessionId,
            string error,
            int? status,
            long started,
            PurviewDlpDecision? decision = null,
            string? scopeState = null) => new()
            {
                Allowed = !Options.FailClosed,
                Evaluated = false,
                Activity = activity,
                CorrelationId = correlationId,
                SessionId = sessionId,
                Decision = decision ?? new PurviewDlpDecision(),
                ProtectionScopeState = scopeState,
                HttpStatus = status,
                Latency = _timeProvider.GetElapsedTime(started),
                Error = error,
                BlockReason = Options.FailClosed ? UnavailableFailClosed : null,
            };

        private static string ActivityName(PurviewDlpActivity activity) =>
            activity == PurviewDlpActivity.UploadText ? "uploadText" : "downloadText";

        private long NextSequence(string sessionId)
        {
            var next = _sequences.AddOrUpdate(sessionId, _ => Interlocked.Read(ref _sequenceFloor), (_, current) => current + 1);
            if (_sequences.Count > MaxTrackedSessions)
            {
                foreach (var entry in _sequences.Where(entry => entry.Key != sessionId).Take(_sequences.Count - MaxTrackedSessions).ToList())
                {
                    // The floor rises before the session is dropped, and the session is dropped only if it was not
                    // numbered since, so a concurrent call for it cannot fall below the floor.
                    RaiseSequenceFloor(entry.Value + 1);
                    _sequences.TryRemove(entry);
                }
            }

            return next;
        }

        private void RaiseSequenceFloor(long sequence)
        {
            var floor = Interlocked.Read(ref _sequenceFloor);
            while (floor < sequence)
            {
                var observed = Interlocked.CompareExchange(ref _sequenceFloor, sequence, floor);
                if (observed == floor)
                {
                    return;
                }

                floor = observed;
            }
        }

        /// <summary>
        /// The text with lone surrogates replaced by U+FFFD: they are not valid UTF-16, so Graph cannot parse a request
        /// that carries them. Valid surrogate pairs are kept.
        /// </summary>
        private static string ReplaceLoneSurrogates(string text)
        {
            var index = 0;
            while (index < text.Length && !char.IsSurrogate(text[index]))
            {
                index++;
            }

            if (index == text.Length)
            {
                return text;
            }

            var builder = new StringBuilder(text.Length).Append(text, 0, index);
            for (; index < text.Length; index++)
            {
                var character = text[index];
                if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                {
                    builder.Append(character).Append(text[++index]);
                }
                else
                {
                    builder.Append(char.IsSurrogate(character) ? '\uFFFD' : character);
                }
            }

            return builder.ToString();
        }

        /// <summary>Moves a cut back one code unit when it would split a surrogate pair.</summary>
        private static int KeepSurrogatePairs(string value, int cut) =>
            cut > 0 && cut < value.Length && char.IsHighSurrogate(value[cut - 1]) && char.IsLowSurrogate(value[cut]) ? cut - 1 : cut;

        private static string? ReadString(JsonNode? node) =>
            node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        private static string? FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
