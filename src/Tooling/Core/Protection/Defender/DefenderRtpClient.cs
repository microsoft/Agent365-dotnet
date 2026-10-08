// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Protection.Defender
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
    /// Client for the Microsoft Defender for AI prevention endpoint (<c>POST .../v1/protection/evaluate</c>).
    /// </summary>
    /// <remarks>
    /// Defender evaluates four agent-hooks/0.1 interception points: <c>input</c> (the user's message,
    /// before the agent runs), <c>pre_tool_call</c>, <c>post_tool_call</c>, and <c>output</c> (the reply,
    /// before it is sent). <see cref="EvaluateHookContextAsync"/> forwards a context emitted by an
    /// agent-hooks host, keeping its session, sequence and tool call ids, after fitting it to Defender's
    /// request validation, and returns the verdict. Each call carries a unique <c>x-ms-correlation-id</c>.
    /// </remarks>
    public sealed class DefenderRtpClient
    {
        /// <summary>The only agent-hooks wire version the prevention endpoint accepts.</summary>
        public const string AgentHooksSpec = "agent-hooks/0.1";

        /// <summary>The header Defender logs each evaluation under.</summary>
        public const string CorrelationIdHeader = "x-ms-correlation-id";

        /// <summary>
        /// The <see cref="DefenderRtpEvaluationResult.Error"/> of a result whose content under decision was longer
        /// than <see cref="DefenderRtpOptions.MaxContentCharacters"/> and that Defender allowed: Defender evaluated a
        /// truncated copy, so the result follows <see cref="DefenderRtpOptions.FailClosed"/>.
        /// </summary>
        public const string TruncatedContentError = "content exceeded MaxContentCharacters; Defender evaluated a truncated copy";

        private const string DefaultFramework = "agent365";
        private const string A365Extension = "a365";
        private const int MaxErrorDetailCharacters = 200;
        private const int MaxCachedTokens = 100;
        private const int MaxTrackedSessions = 1000;
        private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromMinutes(5);
        private static readonly HashSet<string> EvaluatedPoints = new(StringComparer.Ordinal) { "input", "pre_tool_call", "post_tool_call", "output" };
        private static readonly HashSet<string> ActorKinds = new(StringComparer.Ordinal) { "human", "service", "agent" };
        private static readonly Regex ExtensionKeyPattern = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex InvalidFrameworkCharacters = new("[^a-z0-9_-]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Envelope members and members with dedicated handling; any other member a host adds is content.
        private static readonly HashSet<string> KnownMembers = new(StringComparer.Ordinal)
        {
            "spec", "interception_point", "timestamp", "sequence", "request_id", "session", "agent", "tenant", "actor", "model",
            "trace", "input", "output", "target", "tool_call", "tool_result", "messages", "tools", "extensions",
        };

        // Used when the caller passes no HttpClient; pooled connections are recycled so DNS changes are picked up.
        internal static readonly HttpClient SharedHttpClient = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) });

        private readonly HttpClient _httpClient;
        private readonly Func<Guid> _idFactory;
        private readonly TimeProvider _timeProvider;
        private readonly ConcurrentDictionary<string, CachedToken> _tokens = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _inFlightTokens = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, long> _sequences = new(StringComparer.Ordinal);

        /// <summary>Initializes a new instance of the <see cref="DefenderRtpClient"/> class.</summary>
        /// <param name="options">The Defender configuration, for example <see cref="DefenderRtpOptions.FromEnvironment()"/>.</param>
        /// <param name="httpClient">The HTTP client, for example from <c>IHttpClientFactory</c>; defaults to a shared client.</param>
        /// <param name="idFactory">Creates correlation ids (tests).</param>
        /// <param name="timeProvider">The clock (tests).</param>
        public DefenderRtpClient(
            DefenderRtpOptions options,
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
        public DefenderRtpOptions Options { get; }

        /// <summary>Whether Defender evaluates the given agent-hooks interception point.</summary>
        /// <param name="interceptionPoint">The agent-hooks interception point, for example <c>pre_tool_call</c>.</param>
        /// <returns>True for <c>input</c>, <c>pre_tool_call</c>, <c>post_tool_call</c> and <c>output</c>.</returns>
        public static bool IsEvaluatedInterceptionPoint(string? interceptionPoint) =>
            interceptionPoint != null && EvaluatedPoints.Contains(interceptionPoint);

        /// <summary>
        /// Evaluates an agent-hooks context with Defender. The context is not modified.
        /// </summary>
        /// <remarks>
        /// When the content under decision is longer than <see cref="DefenderRtpOptions.MaxContentCharacters"/>,
        /// Defender evaluates a truncated copy: its deny stands, but an allow does not cover the content that was
        /// cut, so the result is marked <see cref="DefenderRtpEvaluationResult.Truncated"/> and follows
        /// <see cref="DefenderRtpOptions.FailClosed"/>.
        /// </remarks>
        /// <param name="context">The agent-hooks/0.1 context emitted by the host.</param>
        /// <param name="agent">The agent identity and turn; fills fields the context does not set.</param>
        /// <param name="tokenResolver">Resolves the agent identity's Defender token.</param>
        /// <param name="cancellationToken">Cancels the evaluation.</param>
        /// <returns>The result, or null when Defender RTP is disabled or the point is not one Defender evaluates.</returns>
        public async Task<DefenderRtpEvaluationResult?> EvaluateHookContextAsync(
            JsonObject context,
            DefenderRtpAgentContext agent,
            DefenderRtpTokenResolver tokenResolver,
            CancellationToken cancellationToken = default)
        {
            if (!Options.Enabled)
            {
                return null;
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            if (tokenResolver == null)
            {
                throw new ArgumentNullException(nameof(tokenResolver));
            }

            var point = ReadString(context["interception_point"]);
            if (!IsEvaluatedInterceptionPoint(point))
            {
                return null;
            }

            RequireString(agent.AgentId, nameof(agent.AgentId));
            RequireString(agent.TenantId, nameof(agent.TenantId));
            var hook = Prepare(context, agent, out var truncated);
            var sessionId = ReadString(hook["session"]?["id"]);
            var started = _timeProvider.GetTimestamp();

            // One deadline covers token acquisition and the request, so the fail mode applies within
            // Options.Timeout, before an agent-hooks interceptor timeout would.
            using var timeout = new CancellationTokenSource(Options.Timeout, _timeProvider);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

            string? token;
            var tokenError = "entra token unavailable";
            try
            {
                token = await GetAccessTokenAsync(agent, tokenResolver, deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                token = null;
                tokenError = "entra token timeout";
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                token = null;
            }

            if (string.IsNullOrEmpty(token))
            {
                return Unavailable(point!, tokenError, sessionId, null, _timeProvider.GetElapsedTime(started));
            }

            return await PostAsync(hook, point!, sessionId, token!, truncated, started, deadline.Token, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Acquires and caches the agent identity's Defender token without evaluating anything, so the
        /// first evaluation does not wait for Entra. A cached token is reused until it expires; within five
        /// minutes of expiry it is refreshed in the background. Throws when no token can be acquired.
        /// </summary>
        /// <param name="agent">The agent identity and tenant.</param>
        /// <param name="tokenResolver">Resolves the agent identity's Defender token.</param>
        /// <param name="cancellationToken">Cancels the acquisition.</param>
        /// <returns>A task that completes when the token is cached.</returns>
        public async Task PrefetchAccessTokenAsync(
            DefenderRtpAgentContext agent,
            DefenderRtpTokenResolver tokenResolver,
            CancellationToken cancellationToken = default)
        {
            if (!Options.Enabled)
            {
                return;
            }

            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            if (tokenResolver == null)
            {
                throw new ArgumentNullException(nameof(tokenResolver));
            }

            RequireString(agent.AgentId, nameof(agent.AgentId));
            RequireString(agent.TenantId, nameof(agent.TenantId));
            await GetAccessTokenAsync(agent, tokenResolver, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// A result for an evaluation that could not be made (for example an invalid context): it follows
        /// <see cref="DefenderRtpOptions.FailClosed"/>, like a transport failure.
        /// </summary>
        /// <param name="interceptionPoint">The agent-hooks interception point.</param>
        /// <param name="error">Why no verdict was obtained.</param>
        /// <param name="sessionId">The agent-hooks session id, when known.</param>
        /// <param name="httpStatus">The HTTP status, when a response was received.</param>
        /// <param name="latency">Time spent before the failure.</param>
        /// <returns>The not-evaluated result.</returns>
        public DefenderRtpEvaluationResult Unavailable(
            string interceptionPoint,
            string error,
            string? sessionId = null,
            int? httpStatus = null,
            TimeSpan latency = default) => new()
            {
                Allowed = !Options.FailClosed,
                Evaluated = false,
                InterceptionPoint = interceptionPoint,
                CorrelationId = _idFactory().ToString(),
                SessionId = sessionId,
                HttpStatus = httpStatus,
                Error = error,
                Latency = latency,
                BlockReason = Options.FailClosed
                    ? "Security validation is unavailable and this agent is configured to fail closed."
                    : null,
            };

        // ---- agent-hooks context -------------------------------------------------------------

        /// <summary>
        /// A copy of the context that meets Defender's request validation: <c>target</c> equals the point's
        /// field, <c>tool_call</c> and <c>tool_result</c> carry only spec members, every content string is
        /// clamped, the timestamp is UTC, and loosely filled optional fields are repaired or dropped.
        /// <paramref name="truncated"/> is set when the content under decision at the point (the field
        /// <c>target</c> mirrors) was cut, so Defender cannot see all of what the verdict would authorize.
        /// </summary>
        private JsonObject Prepare(JsonObject context, DefenderRtpAgentContext agent, out bool truncated)
        {
            var hook = (JsonObject)context.DeepClone();
            var maxCharacters = Options.MaxContentCharacters;
            var decisionTruncated = false;
            hook["spec"] = AgentHooksSpec;
            hook["timestamp"] = UtcTimestamp(hook["timestamp"]);

            var agentNode = hook["agent"] as JsonObject;
            var agentId = FirstNonEmpty(agent.AgentObjectId, ReadString(agentNode?["id"]), agent.AgentId);
            RequireString(agentId, "agent.id");
            var sessionId = ReadString(hook["session"]?["id"]);
            RequireString(sessionId, "session.id");
            if (!IsNonNegativeInteger(hook["sequence"]))
            {
                hook["sequence"] = NextSequence(sessionId!);
            }

            var preparedAgent = new JsonObject
            {
                ["id"] = agentId,
                ["framework"] = SanitizeFramework(FirstNonEmpty(ReadString(agentNode?["framework"]), agent.Framework)),
            };
            if (FirstNonEmpty(ReadString(agentNode?["name"]), agent.AgentName) is { } name)
            {
                preparedAgent["name"] = name;
            }

            if (ReadString(agentNode?["version"]) is { Length: > 0 } version)
            {
                preparedAgent["version"] = version;
            }

            hook["agent"] = preparedAgent;

            // Defender requires tenant.id to equal the token's tid, and the token is always acquired for the
            // agent's tenant, so a host-supplied value is never trusted over it.
            var tenant = hook["tenant"] as JsonObject ?? new JsonObject();
            tenant["id"] = agent.TenantId;
            hook["tenant"] = tenant;

            if (hook["actor"] == null && !string.IsNullOrEmpty(agent.UserId))
            {
                hook["actor"] = new JsonObject { ["id"] = agent.UserId, ["kind"] = agent.ActorKind ?? "human" };
            }

            if (hook["request_id"] == null && !string.IsNullOrEmpty(agent.RequestId))
            {
                hook["request_id"] = agent.RequestId;
            }

            if (hook["model"] == null && !string.IsNullOrEmpty(agent.ModelName))
            {
                hook["model"] = new JsonObject { ["id"] = agent.ModelName };
            }

            DropInvalidOptionalFields(hook);

            switch (ReadString(hook["interception_point"]))
            {
                case "input":
                    {
                        var input = hook["input"] as JsonObject;
                        var role = ReadString(input?["role"]);
                        var prepared = new JsonObject
                        {
                            ["content"] = Clamp(input?["content"], maxCharacters, ref decisionTruncated) ?? JsonValue.Create(string.Empty),
                            ["role"] = role is "system" or "external" ? role : "user",
                        };
                        hook["input"] = prepared;
                        hook["target"] = prepared.DeepClone();
                        break;
                    }

                case "output":
                    {
                        var prepared = new JsonObject
                        {
                            ["content"] = Clamp((hook["output"] as JsonObject)?["content"], maxCharacters, ref decisionTruncated) ?? JsonValue.Create(string.Empty),
                        };
                        hook["output"] = prepared;
                        hook["target"] = prepared.DeepClone();
                        break;
                    }

                case "pre_tool_call":
                case "post_tool_call":
                    {
                        var toolCall = hook["tool_call"] as JsonObject;
                        var toolName = ReadString(toolCall?["name"]);
                        RequireString(toolName, "tool_call.name");
                        var isPreToolCall = ReadString(hook["interception_point"]) == "pre_tool_call";
                        var argsTruncated = false;
                        var args = ToArguments(toolCall?["args"], maxCharacters, ref argsTruncated);
                        hook["tool_call"] = new JsonObject
                        {
                            ["id"] = FirstNonEmpty(ReadString(toolCall?["id"])) ?? GeneratedToolCallId(),
                            ["name"] = toolName,
                            ["args"] = args,
                        };

                        if (isPreToolCall)
                        {
                            decisionTruncated = argsTruncated;
                            hook["target"] = args.DeepClone();
                        }
                        else
                        {
                            var toolResult = hook["tool_result"] as JsonObject;
                            var value = Clamp(toolResult?["value"], maxCharacters, ref decisionTruncated);
                            var isError = toolResult?["is_error"] is JsonValue flag && flag.TryGetValue<bool>(out var failed) && failed;
                            hook["tool_result"] = new JsonObject
                            {
                                ["value"] = value,
                                ["is_error"] = isError,
                            };
                            hook["target"] = value?.DeepClone();
                        }

                        if (hook["tools"] is not JsonArray { Count: > 0 })
                        {
                            hook["tools"] = new JsonArray(ToolFromExtensions(hook, toolName!));
                        }

                        break;
                    }
            }

            ClampOtherContent(hook, maxCharacters);
            truncated = decisionTruncated;
            return hook;
        }

        /// <summary>
        /// Applies <see cref="DefenderRtpOptions.MaxContentCharacters"/> to the content <see cref="Prepare"/>
        /// does not already clamp with the point's field: message content, tool descriptions and schemas,
        /// extensions, and any member a host adds. Envelope fields (ids, names, roles, timestamps) are not
        /// truncated, so the request still validates and correlates.
        /// </summary>
        private static void ClampOtherContent(JsonObject hook, int maxCharacters)
        {
            ClampMember(hook, "extensions", maxCharacters);
            foreach (var message in (hook["messages"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            {
                ClampMember(message, "content", maxCharacters);
            }

            foreach (var tool in (hook["tools"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            {
                ClampMember(tool, "description", maxCharacters);
                ClampMember(tool, "schema", maxCharacters);
            }

            foreach (var key in hook.Select(property => property.Key).Where(key => !KnownMembers.Contains(key)).ToList())
            {
                ClampMember(hook, key, maxCharacters);
            }
        }

        private static void ClampMember(JsonObject owner, string key, int maxCharacters)
        {
            if (owner[key] is { } value)
            {
                owner[key] = Clamp(value, maxCharacters);
            }
        }

        /// <summary>
        /// Optional fields a host may fill loosely but Defender validates strictly (a 400 would leave the
        /// call unverified): extension namespaces, <c>model.id</c>, tool declarations, messages, actor.
        /// </summary>
        private static void DropInvalidOptionalFields(JsonObject hook)
        {
            if (hook["extensions"] is JsonObject extensions)
            {
                foreach (var key in extensions.Select(property => property.Key).Where(key => !ExtensionKeyPattern.IsMatch(key)).ToList())
                {
                    extensions.Remove(key);
                }

                if (extensions.Count == 0)
                {
                    hook.Remove("extensions");
                }
            }
            else if (hook.ContainsKey("extensions"))
            {
                hook.Remove("extensions");
            }

            if (hook.ContainsKey("model"))
            {
                if (ReadString(hook["model"]?["id"]) is { Length: > 0 } modelId)
                {
                    hook["model"] = new JsonObject { ["id"] = modelId };
                }
                else
                {
                    hook.Remove("model");
                }
            }

            if (hook.ContainsKey("tools"))
            {
                var tools = new JsonArray();
                foreach (var tool in (hook["tools"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                {
                    if (ReadString(tool["name"]) is not { Length: > 0 } toolName)
                    {
                        continue;
                    }

                    var declaration = new JsonObject { ["name"] = toolName };
                    if (ReadString(tool["description"]) is { } description)
                    {
                        declaration["description"] = description;
                    }

                    if (tool["schema"] is JsonObject schema)
                    {
                        declaration["schema"] = schema.DeepClone();
                    }

                    tools.Add(declaration);
                }

                if (tools.Count > 0)
                {
                    hook["tools"] = tools;
                }
                else
                {
                    hook.Remove("tools");
                }
            }

            if (hook.ContainsKey("messages"))
            {
                var valid = hook["messages"] is JsonArray messages
                    && messages.All(message => message is JsonObject item
                        && ReadString(item["role"]) is { Length: > 0 }
                        && item.ContainsKey("content"));
                if (!valid)
                {
                    hook.Remove("messages");
                }
            }

            if (hook.ContainsKey("actor"))
            {
                if (hook["actor"] is JsonObject actor)
                {
                    var prepared = new JsonObject();
                    if (ReadString(actor["id"]) is { Length: > 0 } actorId)
                    {
                        prepared["id"] = actorId;
                    }

                    if (ReadString(actor["kind"]) is { } kind && ActorKinds.Contains(kind))
                    {
                        prepared["kind"] = kind;
                    }

                    hook["actor"] = prepared;
                }
                else
                {
                    hook.Remove("actor");
                }
            }
        }

        private static JsonObject ToolFromExtensions(JsonObject hook, string toolName)
        {
            var declaration = new JsonObject { ["name"] = toolName };
            if (ReadString(hook["extensions"]?[A365Extension]?["tool"]?["description"]) is { Length: > 0 } description)
            {
                declaration["description"] = description;
            }

            return declaration;
        }

        private static JsonObject ToArguments(JsonNode? args, int maxCharacters, ref bool truncated)
        {
            var clamped = Clamp(args ?? new JsonObject(), maxCharacters, ref truncated);
            return clamped as JsonObject ?? new JsonObject { ["input"] = clamped };
        }

        // ---- transport -----------------------------------------------------------------------

        /// <summary>
        /// Posts the context; <paramref name="deadline"/> is the evaluation's one deadline, and
        /// <paramref name="truncated"/> says whether the content under decision was cut to fit
        /// <see cref="DefenderRtpOptions.MaxContentCharacters"/>.
        /// </summary>
        private async Task<DefenderRtpEvaluationResult> PostAsync(
            JsonObject hook,
            string point,
            string? sessionId,
            string accessToken,
            bool truncated,
            long started,
            CancellationToken deadline,
            CancellationToken cancellationToken)
        {
            var correlationId = _idFactory().ToString();

            // Options is mutable: read the endpoint once, and never send the token or content to one that is
            // not HTTPS.
            var endpoint = Options.Endpoint;
            if (!DefenderRtpOptions.IsHttpsUrl(endpoint))
            {
                return Failure(point, correlationId, sessionId, "endpoint is not an absolute https URL", null, started);
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(hook.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.TryAddWithoutValidation(CorrelationIdHeader, correlationId);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, deadline).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Failure(point, correlationId, sessionId, "request timeout", null, started);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Any transport failure, including resilience handlers' exceptions (for example a broken
                // circuit), is no verdict and follows the fail mode.
                return Failure(point, correlationId, sessionId, TransportError("request failed", ex), null, started);
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                string body;
                try
                {
                    body = await response.Content.ReadAsStringAsync(deadline).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return Failure(point, correlationId, sessionId, "request timeout", status, started);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    return Failure(point, correlationId, sessionId, TransportError("response body could not be read", ex), status, started);
                }

                if (!response.IsSuccessStatusCode)
                {
                    var detail = ErrorDetail(body);
                    return Failure(point, correlationId, sessionId, detail.Length > 0 ? $"http {status}: {detail}" : $"http {status}", status, started);
                }

                JsonNode? payload;
                try
                {
                    payload = JsonNode.Parse(body);
                }
                catch (JsonException)
                {
                    return Failure(point, correlationId, sessionId, "non-JSON response", status, started);
                }

                var verdict = ParseVerdict(payload);
                if (verdict == null)
                {
                    return Failure(point, correlationId, sessionId, "response contained no verdict", status, started);
                }

                var allowed = verdict.Decision == "allow";
                if (truncated && allowed)
                {
                    // Defender saw only a truncated copy, so its allow does not cover the content that was cut and
                    // follows the fail mode instead. A deny, or a transform (applied here as a block), stands.
                    return new DefenderRtpEvaluationResult
                    {
                        Allowed = !Options.FailClosed,
                        Evaluated = true,
                        Truncated = true,
                        InterceptionPoint = point,
                        CorrelationId = correlationId,
                        SessionId = sessionId,
                        Verdict = verdict,
                        HttpStatus = status,
                        Error = TruncatedContentError,
                        Latency = _timeProvider.GetElapsedTime(started),
                        BlockReason = Options.FailClosed
                            ? "The content is longer than Defender evaluates and this agent is configured to fail closed."
                            : null,
                    };
                }

                return new DefenderRtpEvaluationResult
                {
                    Allowed = allowed,
                    Evaluated = true,
                    Truncated = truncated,
                    InterceptionPoint = point,
                    CorrelationId = correlationId,
                    SessionId = sessionId,
                    Verdict = verdict,
                    HttpStatus = status,
                    Latency = _timeProvider.GetElapsedTime(started),
                    // transform also blocks: the rewrite cannot be applied here, and releasing the
                    // original content would defeat it.
                    BlockReason = allowed
                        ? null
                        : verdict.Decision == "transform"
                            ? "Microsoft Defender for AI asked to rewrite this content, which this SDK version does not apply yet."
                            : verdict.Message ?? "Blocked by Microsoft Defender for AI.",
                };
            }
        }

        private DefenderRtpEvaluationResult Failure(
            string point,
            string correlationId,
            string? sessionId,
            string error,
            int? httpStatus,
            long started) => new()
            {
                Allowed = !Options.FailClosed,
                Evaluated = false,
                InterceptionPoint = point,
                CorrelationId = correlationId,
                SessionId = sessionId,
                HttpStatus = httpStatus,
                Error = error,
                Latency = _timeProvider.GetElapsedTime(started),
                BlockReason = Options.FailClosed
                    ? "Security validation is unavailable and this agent is configured to fail closed."
                    : null,
            };

        /// <summary>A transport error: the exception type, never its message, which can echo request content.</summary>
        private static string TransportError(string error, Exception exception) =>
            exception is HttpRequestException ? error : $"{error} ({exception.GetType().Name})";

        private static DefenderRtpVerdict? ParseVerdict(JsonNode? payload)
        {
            if (payload is not JsonObject verdict || ReadString(verdict["decision"]) is not ("allow" or "deny" or "transform"))
            {
                return null;
            }

            static string? Text(JsonNode? node) => ReadString(node) is { Length: > 0 } text ? text : null;

            return new DefenderRtpVerdict
            {
                Decision = ReadString(verdict["decision"])!,
                Reason = Text(verdict["reason"]),
                Message = Text(verdict["message"]),
                Warnings = (verdict["warnings"] as JsonArray ?? new JsonArray())
                    .OfType<JsonObject>()
                    .Select(warning => new DefenderRtpWarning(Text(warning["reason"]), Text(warning["message"])))
                    .ToList(),
                ResultLabels = (verdict["result_labels"] as JsonArray ?? new JsonArray())
                    .Select(Text)
                    .OfType<string>()
                    .ToList(),
                TransformPath = Text(verdict["transform"]?["path"]),
            };
        }

        /// <summary>
        /// A short single-line detail from a ProblemDetails or Defender error body. For a validation error
        /// (400) it is the failed rules, which Defender reports in <c>diagnostics.validationErrors</c>.
        /// </summary>
        private static string ErrorDetail(string body)
        {
            JsonNode? payload;
            try
            {
                payload = JsonNode.Parse(body);
            }
            catch (JsonException)
            {
                return string.Empty;
            }

            if (payload is not JsonObject error)
            {
                return string.Empty;
            }

            var detail = ValidationErrors(error["diagnostics"])
                ?? ReadString(error["detail"])
                ?? ReadString(error["message"])
                ?? ReadString(error["title"])
                ?? string.Empty;
            detail = Regex.Replace(detail, @"\s+", " ").Trim();
            return detail.Length > MaxErrorDetailCharacters
                ? string.Concat(detail.AsSpan(0, MaxErrorDetailCharacters), "...")
                : detail;
        }

        private static string? ValidationErrors(JsonNode? diagnostics)
        {
            var parsed = diagnostics;
            if (ReadString(diagnostics) is { } serialized)
            {
                try
                {
                    parsed = JsonNode.Parse(serialized);
                }
                catch (JsonException)
                {
                    return null;
                }
            }

            var messages = (parsed?["validationErrors"] as JsonArray ?? new JsonArray())
                .OfType<JsonObject>()
                .Select(item => ReadString(item["message"]))
                .Where(message => !string.IsNullOrEmpty(message))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            return messages.Count > 0 ? "validation: " + string.Join("; ", messages) : null;
        }

        // ---- authentication ------------------------------------------------------------------

        private async Task<string> GetAccessTokenAsync(
            DefenderRtpAgentContext agent,
            DefenderRtpTokenResolver tokenResolver,
            CancellationToken cancellationToken)
        {
            var scope = Options.AuthenticationScope;
            var key = string.Join(":", agent.TenantId, agent.AgentId, scope);
            var now = _timeProvider.GetUtcNow();
            if (_tokens.TryGetValue(key, out var cached) && now < cached.ExpiresAt)
            {
                if (now >= cached.ExpiresAt - TokenRefreshSkew)
                {
                    // Refresh early in the background and keep using the cached token until it actually
                    // expires, as MSAL and Azure.Identity do; a failed refresh is retried by a later call.
                    _ = StartOrJoinAcquisition(key, agent, tokenResolver, scope).ContinueWith(
                        static task => { _ = task.Exception; },
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }

                return cached.Token;
            }

            return await StartOrJoinAcquisition(key, agent, tokenResolver, scope).WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// One acquisition per agent, tenant and scope, shared by concurrent callers. It is bounded by the
        /// configured timeout rather than by any caller's cancellation, and it leaves the in-flight map as it
        /// completes, so a cancelled caller cannot strand it and a failure is never handed to a later caller.
        /// </summary>
        private Task<string> StartOrJoinAcquisition(
            string key,
            DefenderRtpAgentContext agent,
            DefenderRtpTokenResolver tokenResolver,
            string scope)
        {
            while (true)
            {
                if (_inFlightTokens.TryGetValue(key, out var inFlight))
                {
                    return inFlight.Value;
                }

                Lazy<Task<string>>? acquisition = null;
                acquisition = new Lazy<Task<string>>(() => AcquireTokenAsync(key, acquisition!, agent, tokenResolver, scope));
                if (_inFlightTokens.TryAdd(key, acquisition))
                {
                    return acquisition.Value;
                }
            }
        }

        private async Task<string> AcquireTokenAsync(
            string key,
            Lazy<Task<string>> acquisition,
            DefenderRtpAgentContext agent,
            DefenderRtpTokenResolver tokenResolver,
            string scope)
        {
            try
            {
                return await ResolveAndCacheTokenAsync(key, agent, tokenResolver, scope).ConfigureAwait(false);
            }
            finally
            {
                // Runs before the acquisition's task completes, so no caller joins a finished or failed one.
                _inFlightTokens.TryRemove(new KeyValuePair<string, Lazy<Task<string>>>(key, acquisition));
            }
        }

        private async Task<string> ResolveAndCacheTokenAsync(
            string key,
            DefenderRtpAgentContext agent,
            DefenderRtpTokenResolver tokenResolver,
            string scope)
        {
            using var timeout = new CancellationTokenSource(Options.Timeout, _timeProvider);
            var token = await tokenResolver(agent.AgentId, agent.TenantId, new[] { scope }, timeout.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("The Defender token resolver returned no token.");
            }

            if (ReadExpiry(token!) is { } expiresAt)
            {
                if (expiresAt <= _timeProvider.GetUtcNow())
                {
                    throw new InvalidOperationException("The Defender token resolver returned an expired token.");
                }

                if (_tokens.Count >= MaxCachedTokens)
                {
                    var now = _timeProvider.GetUtcNow();
                    foreach (var stale in _tokens.Where(entry => entry.Value.ExpiresAt <= now).Select(entry => entry.Key).ToList())
                    {
                        _tokens.TryRemove(stale, out _);
                    }

                    if (_tokens.Count >= MaxCachedTokens && _tokens.OrderBy(entry => entry.Value.ExpiresAt).FirstOrDefault() is { Key: not null } oldest)
                    {
                        _tokens.TryRemove(oldest.Key, out _);
                    }
                }

                _tokens[key] = new CachedToken(token!, expiresAt);
            }

            return token!;
        }

        private static DateTimeOffset? ReadExpiry(string token)
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                return null;
            }

            try
            {
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
                if (JsonNode.Parse(Convert.FromBase64String(payload))?["exp"] is not JsonValue exp)
                {
                    return null;
                }

                if (exp.TryGetValue<long>(out var seconds))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(seconds);
                }

                return exp.TryGetValue<double>(out var fractional)
                    ? DateTimeOffset.FromUnixTimeSeconds((long)fractional)
                    : null;
            }
            catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
            {
                return null;
            }
        }

        // ---- helpers -------------------------------------------------------------------------

        private long NextSequence(string sessionId)
        {
            var next = _sequences.AddOrUpdate(sessionId, 1, (_, current) => current + 1);
            if (_sequences.Count > MaxTrackedSessions)
            {
                foreach (var key in _sequences.Keys.Where(key => key != sessionId).Take(_sequences.Count - MaxTrackedSessions).ToList())
                {
                    _sequences.TryRemove(key, out _);
                }
            }

            return next;
        }

        private string GeneratedToolCallId() => "tooluse_" + _idFactory().ToString("N").Substring(0, 12);

        private string UtcTimestamp(JsonNode? value)
        {
            var parsed = ReadString(value) is { } text
                && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)
                    ? timestamp
                    : _timeProvider.GetUtcNow();
            return parsed.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        private static string SanitizeFramework(string? framework)
        {
            var value = InvalidFrameworkCharacters.Replace((framework ?? string.Empty).Trim().ToLowerInvariant(), "-").Trim('-');
            return value.Length > 0 ? value : DefaultFramework;
        }

        private static JsonNode? Clamp(JsonNode? node, int maxCharacters)
        {
            var truncated = false;
            return Clamp(node, maxCharacters, ref truncated);
        }

        /// <summary>Clamps every string in the value; <paramref name="truncated"/> is set when any was cut.</summary>
        private static JsonNode? Clamp(JsonNode? node, int maxCharacters, ref bool truncated)
        {
            switch (node)
            {
                case null:
                    return null;
                case JsonValue value when value.TryGetValue<string>(out var text):
                    truncated |= text.Length > maxCharacters;
                    return JsonValue.Create(Truncate(text, maxCharacters));
                case JsonArray array:
                    var items = new JsonArray();
                    foreach (var item in array)
                    {
                        items.Add(Clamp(item, maxCharacters, ref truncated));
                    }

                    return items;
                case JsonObject obj:
                    var properties = new JsonObject();
                    foreach (var property in obj)
                    {
                        properties[property.Key] = Clamp(property.Value, maxCharacters, ref truncated);
                    }

                    return properties;
                default:
                    return node.DeepClone();
            }
        }

        private static string Truncate(string value, int maxCharacters)
        {
            if (value.Length <= maxCharacters)
            {
                return value;
            }

            // The marker counts toward the limit; it is sized for the most digits it can need, so the result
            // never exceeds maxCharacters. A limit too small to hold it cuts without a marker.
            var kept = maxCharacters - TruncationMarker(value.Length).Length;
            return kept > 0
                ? string.Concat(value.AsSpan(0, kept), TruncationMarker(value.Length - kept))
                : value.Substring(0, maxCharacters);
        }

        private static string TruncationMarker(int dropped) => $"...[truncated {dropped} chars]";

        private static string? ReadString(JsonNode? node) =>
            node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        private static bool IsNonNegativeInteger(JsonNode? node) =>
            node is JsonValue value && value.TryGetValue<long>(out var number) && number >= 0;

        private static string? FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        private static void RequireString(string? value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{name} is required.", name);
            }
        }

        private sealed record CachedToken(string Token, DateTimeOffset ExpiresAt);
    }
}
