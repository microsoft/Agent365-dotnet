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

        private readonly HttpClient _httpClient;
        private readonly Func<Guid> _idFactory;
        private readonly TimeProvider _timeProvider;
        private readonly ConcurrentDictionary<string, CachedToken> _tokens = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _inFlightTokens = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, long> _sequences = new(StringComparer.Ordinal);

        /// <summary>Initializes a new instance of the <see cref="DefenderRtpClient"/> class.</summary>
        /// <param name="options">The Defender configuration, for example <see cref="DefenderRtpOptions.FromEnvironment()"/>.</param>
        /// <param name="httpClient">The HTTP client; a pooled client is recommended.</param>
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
            _httpClient = httpClient ?? new HttpClient();
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
            var hook = Prepare(context, agent);
            var sessionId = ReadString(hook["session"]?["id"]);
            var started = _timeProvider.GetTimestamp();

            string? token;
            try
            {
                token = await GetAccessTokenAsync(agent, tokenResolver, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                token = null;
            }

            if (string.IsNullOrEmpty(token))
            {
                return Unavailable(point!, "entra token unavailable", sessionId, null, _timeProvider.GetElapsedTime(started));
            }

            return await PostAsync(hook, point!, sessionId, token!, started, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Acquires and caches the agent identity's Defender token without evaluating anything, so the
        /// first evaluation does not wait for Entra. A cached token is reused until five minutes before it
        /// expires. Throws when no token can be acquired.
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
        /// field, <c>tool_call</c> and <c>tool_result</c> carry only spec members, content is clamped, the
        /// timestamp is UTC, and loosely filled optional fields are repaired or dropped.
        /// </summary>
        private JsonObject Prepare(JsonObject context, DefenderRtpAgentContext agent)
        {
            var hook = (JsonObject)context.DeepClone();
            var maxCharacters = Options.MaxContentCharacters;
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

            if (string.IsNullOrEmpty(ReadString(hook["tenant"]?["id"])))
            {
                var tenant = hook["tenant"] as JsonObject ?? new JsonObject();
                tenant["id"] = agent.TenantId;
                hook["tenant"] = tenant;
            }

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
                            ["content"] = Clamp(input?["content"], maxCharacters) ?? JsonValue.Create(string.Empty),
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
                            ["content"] = Clamp((hook["output"] as JsonObject)?["content"], maxCharacters) ?? JsonValue.Create(string.Empty),
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
                        var args = ToArguments(toolCall?["args"], maxCharacters);
                        hook["tool_call"] = new JsonObject
                        {
                            ["id"] = FirstNonEmpty(ReadString(toolCall?["id"])) ?? GeneratedToolCallId(),
                            ["name"] = toolName,
                            ["args"] = args,
                        };

                        if (ReadString(hook["interception_point"]) == "pre_tool_call")
                        {
                            hook["target"] = args.DeepClone();
                        }
                        else
                        {
                            var toolResult = hook["tool_result"] as JsonObject;
                            var value = Clamp(toolResult?["value"], maxCharacters);
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

            return hook;
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

        private static JsonObject ToArguments(JsonNode? args, int maxCharacters)
        {
            var clamped = Clamp(args ?? new JsonObject(), maxCharacters);
            return clamped as JsonObject ?? new JsonObject { ["input"] = clamped };
        }

        // ---- transport -----------------------------------------------------------------------

        private async Task<DefenderRtpEvaluationResult> PostAsync(
            JsonObject hook,
            string point,
            string? sessionId,
            string accessToken,
            long started,
            CancellationToken cancellationToken)
        {
            var correlationId = _idFactory().ToString();
            using var request = new HttpRequestMessage(HttpMethod.Post, Options.Endpoint)
            {
                Content = new StringContent(hook.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.TryAddWithoutValidation(CorrelationIdHeader, correlationId);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Options.Timeout);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Failure(point, correlationId, sessionId, "request timeout", null, started);
            }
            catch (HttpRequestException)
            {
                return Failure(point, correlationId, sessionId, "request failed", null, started);
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                string body;
                try
                {
                    body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return Failure(point, correlationId, sessionId, "request timeout", status, started);
                }
                catch (HttpRequestException)
                {
                    return Failure(point, correlationId, sessionId, "response body could not be read", status, started);
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
                return new DefenderRtpEvaluationResult
                {
                    Allowed = allowed,
                    Evaluated = true,
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
            if (_tokens.TryGetValue(key, out var cached) && _timeProvider.GetUtcNow() < cached.ExpiresAt - TokenRefreshSkew)
            {
                return cached.Token;
            }

            // One acquisition per agent, tenant and scope; it is bounded by the configured timeout and
            // not tied to any single caller's cancellation.
            var acquisition = _inFlightTokens.GetOrAdd(
                key,
                _ => new Lazy<Task<string>>(() => AcquireTokenAsync(key, agent, tokenResolver, scope)));
            try
            {
                return await acquisition.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (acquisition.IsValueCreated && acquisition.Value.IsCompleted)
                {
                    _inFlightTokens.TryRemove(new KeyValuePair<string, Lazy<Task<string>>>(key, acquisition));
                }
            }
        }

        private async Task<string> AcquireTokenAsync(
            string key,
            DefenderRtpAgentContext agent,
            DefenderRtpTokenResolver tokenResolver,
            string scope)
        {
            using var timeout = new CancellationTokenSource(Options.Timeout);
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
            switch (node)
            {
                case null:
                    return null;
                case JsonValue value when value.TryGetValue<string>(out var text):
                    return JsonValue.Create(Truncate(text, maxCharacters));
                case JsonArray array:
                    var items = new JsonArray();
                    foreach (var item in array)
                    {
                        items.Add(Clamp(item, maxCharacters));
                    }

                    return items;
                case JsonObject obj:
                    var properties = new JsonObject();
                    foreach (var property in obj)
                    {
                        properties[property.Key] = Clamp(property.Value, maxCharacters);
                    }

                    return properties;
                default:
                    return node.DeepClone();
            }
        }

        private static string Truncate(string value, int maxCharacters) =>
            value.Length <= maxCharacters
                ? value
                : string.Concat(value.AsSpan(0, maxCharacters), $"...[truncated {value.Length - maxCharacters} chars]");

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
