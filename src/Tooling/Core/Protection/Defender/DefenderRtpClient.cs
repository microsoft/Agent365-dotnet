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

        /// <summary>
        /// The <see cref="DefenderRtpEvaluationResult.Error"/> of a result that Defender allowed at a tool point after
        /// the called tool's description or schema had to be cut: Defender evaluated a truncated copy, so the result
        /// follows <see cref="DefenderRtpOptions.FailClosed"/>.
        /// </summary>
        public const string TruncatedToolDeclarationError =
            "the called tool's declaration exceeded MaxContentCharacters; Defender evaluated a truncated copy";

        /// <summary>
        /// The <see cref="DefenderRtpEvaluationResult.Error"/> of a result that Defender allowed at a tool point when
        /// the list of tool declarations is longer than 10,000 and the called tool was not among the first 10,000:
        /// Defender evaluated without its declaration, so the result follows <see cref="DefenderRtpOptions.FailClosed"/>.
        /// </summary>
        public const string UnseenToolDeclarationError =
            "the called tool was not among the first 10000 tool declarations; Defender evaluated without its declaration";

        private const string DefaultFramework = "agent365";
        private const string A365Extension = "a365";
        private const int MaxErrorDetailCharacters = 200;
        private const int MaxCachedTokens = 100;

        // A fitted copy carries at most this many times MaxContentCharacters of content in all.
        private const int ContentBudgetFactor = 4;

        // Content nested deeper than this is cut like content over the size limit, so the copy stays within common
        // JSON parser depth limits and the walk over the host's JSON stays shallow however deep it is.
        private const int MaxContentDepth = 32;

        // How many tool declarations a name-only scan reads to find the called tool's.
        private const int MaxCalledToolScan = 10_000;
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

        // Used when the caller passes no HttpClient. Pooled connections are recycled so DNS changes are picked up, and
        // redirects are never followed: a 307 or 308 would replay the token, the client assertion or the agent's
        // content to another host.
        internal static readonly SocketsHttpHandler SharedHandler = new()
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AllowAutoRedirect = false,
        };

        internal static readonly HttpClient SharedHttpClient = new(SharedHandler);

        private readonly HttpClient _httpClient;
        private readonly Func<Guid> _idFactory;
        private readonly TimeProvider _timeProvider;
        private readonly ConcurrentDictionary<TokenCacheKey, CachedToken> _tokens = new();

        // Serializes writes to _tokens, so concurrent acquisitions cannot push it past MaxCachedTokens.
        private readonly object _tokenCacheLock = new();
        private readonly ConcurrentDictionary<TokenCacheKey, Lazy<Task<string>>> _inFlightTokens = new();
        private readonly ConcurrentDictionary<string, long> _sequences = new(StringComparer.Ordinal);

        // The highest sequence given to a session that is no longer tracked; a session that comes back resumes above
        // it, so its sequence keeps increasing.
        private long _sequenceFloor;

        /// <summary>Initializes a new instance of the <see cref="DefenderRtpClient"/> class.</summary>
        /// <param name="options">The Defender configuration, for example <see cref="DefenderRtpOptions.FromEnvironment()"/>.</param>
        /// <param name="httpClient">
        /// The HTTP client, for example from <c>IHttpClientFactory</c>; defaults to a shared client that does not
        /// follow redirects. A client passed here must not follow them either: a redirected request is treated as a
        /// failure, since the context has been sent to another host.
        /// </param>
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
        /// When the content under decision is longer than <see cref="DefenderRtpOptions.MaxContentCharacters"/>, or at
        /// a tool point the called tool's declaration had to be cut or was not among the first 10,000 declarations,
        /// Defender evaluates a truncated copy: its deny stands, but an allow does not cover what it did not see, so
        /// the result is marked <see cref="DefenderRtpEvaluationResult.Truncated"/> and follows
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
            var hook = Prepare(context, agent, out var truncation);
            var sessionId = ReadString((hook["session"] as JsonObject)?["id"]);
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

            return await PostAsync(hook, point!, sessionId, token!, truncation, started, deadline.Token, cancellationToken).ConfigureAwait(false);
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
        /// Builds the copy of the context Defender evaluates without cloning the host's content. The envelope is
        /// rebuilt from its spec fields so the request meets Defender's validation; the content under decision at the
        /// point (the field <c>target</c> mirrors) is clamped first, then the called tool's declaration at a tool
        /// point, and then the tool call's arguments at <c>post_tool_call</c>, the other tool declarations, the newest
        /// message history, extensions and any other member share what remains of <see cref="ContentBudgetFactor"/>
        /// times <see cref="DefenderRtpOptions.MaxContentCharacters"/>. Lone surrogates are replaced, since Defender
        /// cannot parse them. <paramref name="truncation"/> says why Defender cannot see all of what its verdict would
        /// authorize (the content under decision or the called tool's declaration was cut, or the declaration was not
        /// among those scanned), or is null when it can.
        /// </summary>
        private JsonObject Prepare(JsonObject context, DefenderRtpAgentContext agent, out string? truncation)
        {
            var maxCharacters = Options.MaxContentCharacters;
            var budget = new ContentBudget((int)Math.Min(int.MaxValue, (long)ContentBudgetFactor * maxCharacters));
            var point = ReadString(context["interception_point"]);
            var hook = PrepareEnvelope(context, agent, point);

            // The content under decision is sent twice, as the point's field and as target, so it may use half of
            // the budget; the rest of the context shares what it leaves.
            var decisionBudget = new ContentBudget(budget.Remaining / 2);
            var decisionTruncated = false;
            string? toolName = null;
            switch (point)
            {
                case "input":
                    {
                        var input = context["input"] as JsonObject;
                        var role = ReadString(input?["role"]);
                        var prepared = new JsonObject
                        {
                            ["content"] = Clamp(input?["content"], maxCharacters, decisionBudget, ref decisionTruncated) ?? JsonValue.Create(string.Empty),
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
                            ["content"] = Clamp((context["output"] as JsonObject)?["content"], maxCharacters, decisionBudget, ref decisionTruncated) ?? JsonValue.Create(string.Empty),
                        };
                        hook["output"] = prepared;
                        hook["target"] = prepared.DeepClone();
                        break;
                    }

                case "pre_tool_call":
                case "post_tool_call":
                    {
                        var toolCall = context["tool_call"] as JsonObject;
                        toolName = ReadString(toolCall?["name"]);
                        RequireString(toolName, "tool_call.name");
                        var preparedCall = new JsonObject
                        {
                            ["id"] = FirstNonEmpty(ReadString(toolCall?["id"])) ?? GeneratedToolCallId(),
                            ["name"] = toolName,
                        };
                        hook["tool_call"] = preparedCall;
                        if (point == "pre_tool_call")
                        {
                            var args = ToArguments(toolCall?["args"], maxCharacters, decisionBudget, ref decisionTruncated);
                            preparedCall["args"] = args;
                            hook["target"] = args.DeepClone();
                        }
                        else
                        {
                            var toolResult = context["tool_result"] as JsonObject;
                            var value = Clamp(toolResult?["value"], maxCharacters, decisionBudget, ref decisionTruncated);
                            var isError = toolResult?["is_error"] is JsonValue flag && flag.TryGetValue<bool>(out var failed) && failed;
                            hook["tool_result"] = new JsonObject
                            {
                                ["value"] = value,
                                ["is_error"] = isError,
                            };
                            hook["target"] = value?.DeepClone();
                        }

                        break;
                    }
            }

            budget.Spend(2 * decisionBudget.Used);

            // At a tool point the called tool's declaration comes next, ahead of the rest of the context: Defender
            // evaluates the call against it, so a declaration it cannot see in full leaves the verdict unverified, like
            // cut content under decision.
            var tools = new JsonArray();
            JsonObject? calledToolDeclaration = null;
            var calledToolTruncation = toolName == null
                ? null
                : AddCalledTool(tools, context, toolName, maxCharacters, budget, out calledToolDeclaration);

            // The rest of the context. What is cut here does not change the authority of the verdict.
            var contextCut = false;
            if (point == "post_tool_call" && hook["tool_call"] is JsonObject calledTool)
            {
                calledTool["args"] = ToArguments((context["tool_call"] as JsonObject)?["args"], maxCharacters, budget, ref contextCut);
            }

            AddOtherTools(tools, context, calledToolDeclaration, maxCharacters, budget);
            if (tools.Count > 0)
            {
                hook["tools"] = tools;
            }

            AddMessages(hook, context, maxCharacters, budget);
            AddExtensions(hook, context, maxCharacters, budget);
            foreach (var property in context.Where(property => !KnownMembers.Contains(property.Key)))
            {
                if (!AddMember(hook, property.Key, property.Value, maxCharacters, budget, ref contextCut))
                {
                    break;
                }
            }

            truncation = decisionTruncated ? TruncatedContentError : calledToolTruncation;
            return (JsonObject)ReplaceLoneSurrogates(hook)!;
        }

        /// <summary>
        /// Builds the envelope from its spec fields alone, repairing what a host may fill loosely but Defender
        /// validates strictly (a 400 would leave the call unverified). Nothing else a host puts in an envelope object
        /// is copied, so it cannot escape the content budget; ids and names are not truncated.
        /// </summary>
        private JsonObject PrepareEnvelope(JsonObject context, DefenderRtpAgentContext agent, string? point)
        {
            var agentNode = context["agent"] as JsonObject;
            var agentId = FirstNonEmpty(agent.AgentObjectId, ReadString(agentNode?["id"]), agent.AgentId);
            RequireString(agentId, "agent.id");
            var sessionNode = context["session"] as JsonObject;
            var sessionId = ReadString(sessionNode?["id"]);
            RequireString(sessionId, "session.id");

            var hook = new JsonObject
            {
                ["spec"] = AgentHooksSpec,
                ["interception_point"] = point,
                ["timestamp"] = UtcTimestamp(context["timestamp"]),
                ["sequence"] = IsNonNegativeInteger(context["sequence"])
                    ? context["sequence"]!.DeepClone()
                    : JsonValue.Create(NextSequence(sessionId!)),
            };

            if (FirstNonEmpty(ReadString(context["request_id"]), agent.RequestId) is { } requestId)
            {
                hook["request_id"] = requestId;
            }

            var session = new JsonObject { ["id"] = sessionId };
            if (ReadString(sessionNode?["started_at"]) is { } startedAt
                && DateTimeOffset.TryParse(startedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var started))
            {
                session["started_at"] = FormatUtc(started);
            }

            if (sessionNode?["turn"] is { } turn && IsNonNegativeInteger(turn))
            {
                session["turn"] = turn.DeepClone();
            }

            hook["session"] = session;

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
            var tenant = new JsonObject { ["id"] = agent.TenantId };
            if (ReadString((context["tenant"] as JsonObject)?["name"]) is { Length: > 0 } tenantName)
            {
                tenant["name"] = tenantName;
            }

            hook["tenant"] = tenant;

            // An actor or model the host set is repaired rather than replaced by the turn's.
            var actorNode = context["actor"] ?? (string.IsNullOrEmpty(agent.UserId)
                ? null
                : new JsonObject { ["id"] = agent.UserId, ["kind"] = agent.ActorKind ?? "human" });
            if (actorNode is JsonObject actor)
            {
                var preparedActor = new JsonObject();
                if (ReadString(actor["id"]) is { Length: > 0 } actorId)
                {
                    preparedActor["id"] = actorId;
                }

                if (ReadString(actor["kind"]) is { } kind && ActorKinds.Contains(kind))
                {
                    preparedActor["kind"] = kind;
                }

                hook["actor"] = preparedActor;
            }

            var modelId = context["model"] is { } model ? ReadString((model as JsonObject)?["id"]) : agent.ModelName;
            if (!string.IsNullOrEmpty(modelId))
            {
                hook["model"] = new JsonObject { ["id"] = modelId };
            }

            if (context["trace"] is JsonObject traceNode)
            {
                var trace = new JsonObject();
                if (ReadString(traceNode["trace_id"]) is { Length: > 0 } traceId)
                {
                    trace["trace_id"] = traceId;
                }

                if (ReadString(traceNode["span_id"]) is { Length: > 0 } spanId)
                {
                    trace["span_id"] = spanId;
                }

                if (trace.Count > 0)
                {
                    hook["trace"] = trace;
                }
            }

            return hook;
        }

        /// <summary>
        /// Declares the called tool. A name-only scan of the first <see cref="MaxCalledToolScan"/> entries of
        /// <c>tools</c> finds its declaration, whose description and schema use the budget; the name, the tool's
        /// identity, is copied whole and not charged. Without declarations at all, the <c>a365</c> extension's
        /// description is used. Returns why Defender cannot see the tool's full declaration (its description or schema
        /// had to be cut, or the list is longer than the scan and the tool was not among the entries scanned), or null.
        /// A tool absent from a list scanned to the end is declared from its name, and that is not a cut.
        /// </summary>
        private static string? AddCalledTool(
            JsonArray tools,
            JsonObject context,
            string toolName,
            int maxCharacters,
            ContentBudget budget,
            out JsonObject? found)
        {
            var declared = context["tools"] as JsonArray;
            found = declared?.Take(MaxCalledToolScan).OfType<JsonObject>().FirstOrDefault(tool => ReadString(tool["name"]) == toolName);

            // An extension namespace may hold any JSON value, so each level's shape is checked before it is read.
            var source = found
                ?? (declared is { Count: > 0 } ? null : ((context["extensions"] as JsonObject)?[A365Extension] as JsonObject)?["tool"] as JsonObject);
            var cut = false;
            var declaration = new JsonObject { ["name"] = toolName };
            if (source?["description"] is JsonValue description && ReadString(description) != null)
            {
                AddMember(declaration, "description", description, maxCharacters, budget, ref cut);
            }

            if (found?["schema"] is JsonObject schema)
            {
                AddMember(declaration, "schema", schema, maxCharacters, budget, ref cut);
            }

            tools.Add(declaration);
            if (cut)
            {
                return TruncatedToolDeclarationError;
            }

            return found == null && declared?.Count > MaxCalledToolScan ? UnseenToolDeclarationError : null;
        }

        /// <summary>
        /// Fills what remains of the budget with the other valid tool declarations (a name, and a string description
        /// and object schema when present) in the host's order, skipping the called tool's, which is declared already.
        /// Only as many entries as the budget can hold are read: each entry costs its element, and a copied declaration
        /// its name and members too.
        /// </summary>
        private static void AddOtherTools(JsonArray tools, JsonObject context, JsonObject? calledTool, int maxCharacters, ContentBudget budget)
        {
            var cut = false;
            foreach (var entry in context["tools"] as JsonArray ?? new JsonArray())
            {
                if (budget.Remaining == 0)
                {
                    break;
                }

                // The called tool's entry, or one Defender would reject, is skipped, but reading it still costs its
                // element.
                if (ReferenceEquals(entry, calledTool) || entry is not JsonObject tool || ReadString(tool["name"]) is not { Length: > 0 } declaredName)
                {
                    budget.Spend(1);
                    continue;
                }

                var cost = 1 + MemberCost("name", declaredName);
                if (budget.Remaining < cost)
                {
                    break;
                }

                budget.Spend(cost);
                var declaration = new JsonObject { ["name"] = declaredName };
                if (tool["description"] is JsonValue description && ReadString(description) != null)
                {
                    AddMember(declaration, "description", description, maxCharacters, budget, ref cut);
                }

                if (tool["schema"] is JsonObject schema)
                {
                    AddMember(declaration, "schema", schema, maxCharacters, budget, ref cut);
                }

                tools.Add(declaration);
            }
        }

        /// <summary>
        /// Copies the message history within the budget, newest first, so older messages are dropped before newer
        /// ones. Each message is charged for its element, role and member names as well as its content, so even empty
        /// messages are bounded. Copying stops at a message without a role or content, which Defender rejects, so
        /// only the messages after it are sent.
        /// </summary>
        private static void AddMessages(JsonObject hook, JsonObject context, int maxCharacters, ContentBudget budget)
        {
            if (context["messages"] is not JsonArray messages)
            {
                return;
            }

            var cut = false;
            var kept = new List<JsonNode?>();
            for (var index = messages.Count - 1; index >= 0; index--)
            {
                if (messages[index] is not JsonObject message
                    || ReadString(message["role"]) is not { Length: > 0 } role
                    || !message.TryGetPropertyValue("content", out var content))
                {
                    break;
                }

                var cost = 1 + MemberCost("role", role) + MemberCost("content", string.Empty);
                if (budget.Remaining < cost)
                {
                    break;
                }

                budget.Spend(cost);
                var copy = new JsonObject
                {
                    ["role"] = role,
                    ["content"] = Clamp(content, maxCharacters, budget, ref cut),
                };
                foreach (var property in message.Where(property => property.Key is not ("role" or "content")))
                {
                    if (!AddMember(copy, property.Key, property.Value, maxCharacters, budget, ref cut))
                    {
                        break;
                    }
                }

                kept.Add(copy);
            }

            if (kept.Count > 0)
            {
                kept.Reverse();
                hook["messages"] = new JsonArray(kept.ToArray());
            }
        }

        /// <summary>
        /// Copies the extension namespaces Defender accepts within the budget, reading only as many as it can hold: a
        /// namespace Defender would reject is skipped but still costs a character.
        /// </summary>
        private static void AddExtensions(JsonObject hook, JsonObject context, int maxCharacters, ContentBudget budget)
        {
            if (context["extensions"] is not JsonObject extensions)
            {
                return;
            }

            var cut = false;
            var kept = new JsonObject();
            foreach (var property in extensions)
            {
                if (!ExtensionKeyPattern.IsMatch(property.Key))
                {
                    if (budget.Remaining == 0)
                    {
                        break;
                    }

                    budget.Spend(1);
                    continue;
                }

                if (!AddMember(kept, property.Key, property.Value, maxCharacters, budget, ref cut))
                {
                    break;
                }
            }

            if (kept.Count > 0)
            {
                hook["extensions"] = kept;
            }
        }

        /// <summary>
        /// Adds a member within the budget: its name is charged as <see cref="Clamp"/> charges an object's property
        /// names, and its value is clamped. Returns false, setting <paramref name="cut"/>, when the name does not fit.
        /// </summary>
        private static bool AddMember(JsonObject target, string name, JsonNode? value, int maxCharacters, ContentBudget budget, ref bool cut)
        {
            if (budget.Remaining < name.Length + 1)
            {
                cut = true;
                return false;
            }

            budget.Spend(name.Length + 1);
            target[name] = Clamp(value, maxCharacters, budget, ref cut);
            return true;
        }

        /// <summary>What a member whose value is never truncated (a name or role) costs: its name, and its value.</summary>
        private static int MemberCost(string name, string value) => name.Length + 1 + value.Length;

        private static JsonObject ToArguments(JsonNode? args, int maxCharacters, ContentBudget budget, ref bool truncated)
        {
            var clamped = Clamp(args ?? new JsonObject(), maxCharacters, budget, ref truncated);
            return clamped as JsonObject ?? new JsonObject { ["input"] = clamped };
        }

        // ---- transport -----------------------------------------------------------------------

        /// <summary>
        /// Posts the context; <paramref name="deadline"/> is the evaluation's one deadline, and
        /// <paramref name="truncation"/> says why Defender's allow would not cover all of what is under decision, or
        /// is null when it would.
        /// </summary>
        private async Task<DefenderRtpEvaluationResult> PostAsync(
            JsonObject hook,
            string point,
            string? sessionId,
            string accessToken,
            string? truncation,
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

                // A client that follows redirects has sent the context to another host; what came back is no verdict.
                if (request.RequestUri != endpoint)
                {
                    return Failure(point, correlationId, sessionId, "request was redirected", status, started);
                }

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
                if (truncation != null && allowed)
                {
                    // Defender saw only a truncated copy, so its allow does not cover what it did not see and follows
                    // the fail mode instead. A deny, or a transform (applied here as a block), stands.
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
                        Error = truncation,
                        Latency = _timeProvider.GetElapsedTime(started),
                        BlockReason = Options.FailClosed
                            ? "Defender could not evaluate all of the content and this agent is configured to fail closed."
                            : null,
                    };
                }

                return new DefenderRtpEvaluationResult
                {
                    Allowed = allowed,
                    Evaluated = true,
                    Truncated = truncation != null,
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

            // Read-only, so no observer of the result can change what the verdict says.
            return new DefenderRtpVerdict
            {
                Decision = ReadString(verdict["decision"])!,
                Reason = Text(verdict["reason"]),
                Message = Text(verdict["message"]),
                Warnings = (verdict["warnings"] as JsonArray ?? new JsonArray())
                    .OfType<JsonObject>()
                    .Select(warning => new DefenderRtpWarning(Text(warning["reason"]), Text(warning["message"])))
                    .ToList()
                    .AsReadOnly(),
                ResultLabels = (verdict["result_labels"] as JsonArray ?? new JsonArray())
                    .Select(Text)
                    .OfType<string>()
                    .ToList()
                    .AsReadOnly(),
                // Read only from an object, so unexpected transform metadata never costs Defender its decision.
                TransformPath = Text((verdict["transform"] as JsonObject)?["path"]),
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

            var messages = ((parsed as JsonObject)?["validationErrors"] as JsonArray ?? new JsonArray())
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
            var key = TokenKey(agent, scope);
            var now = _timeProvider.GetUtcNow();
            if (_tokens.TryGetValue(key, out var cached) && now < cached.ExpiresAt)
            {
                if (now >= cached.ExpiresAt - TokenRefreshSkew)
                {
                    // Refresh early in the background and keep using the cached token until it actually
                    // expires, as MSAL and Azure.Identity do; a failed refresh is retried by a later call.
                    ObserveFailure(StartOrJoinAcquisition(key, agent, tokenResolver, scope));
                }

                return cached.Token;
            }

            return await StartOrJoinAcquisition(key, agent, tokenResolver, scope).WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// The token acquisition in flight for the agent, if any; for tests. Its task completes only after the
        /// acquisition has left the in-flight map, so awaiting it orders a later call after it.
        /// </summary>
        internal Task? PendingTokenAcquisition(DefenderRtpAgentContext agent) =>
            _inFlightTokens.TryGetValue(TokenKey(agent, Options.AuthenticationScope), out var acquisition) ? acquisition.Value : null;

        /// <summary>The number of cached tokens; for tests.</summary>
        internal int CachedTokenCount => _tokens.Count;

        /// <summary>How many sessions' sequences are tracked; settable for tests, so eviction needs few sessions.</summary>
        internal int MaxTrackedSessions { get; init; } = 1000;

        private static TokenCacheKey TokenKey(DefenderRtpAgentContext agent, string scope) =>
            new(agent.TenantId, agent.AgentId, scope);

        /// <summary>Observes the failure of a task no caller awaits, so it is not reported as unobserved.</summary>
        private static void ObserveFailure(Task task) =>
            _ = task.ContinueWith(
                static completed => { _ = completed.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

        /// <summary>
        /// One acquisition per agent, tenant and scope, shared by concurrent callers. It is bounded by the
        /// configured timeout rather than by any caller's cancellation, and it leaves the in-flight map as it
        /// completes, so a cancelled caller cannot strand it and a failure is never handed to a later caller.
        /// </summary>
        private Task<string> StartOrJoinAcquisition(
            TokenCacheKey key,
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
            TokenCacheKey key,
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
                _inFlightTokens.TryRemove(new KeyValuePair<TokenCacheKey, Lazy<Task<string>>>(key, acquisition));
            }
        }

        private async Task<string> ResolveAndCacheTokenAsync(
            TokenCacheKey key,
            DefenderRtpAgentContext agent,
            DefenderRtpTokenResolver tokenResolver,
            string scope)
        {
            using var timeout = new CancellationTokenSource(Options.Timeout, _timeProvider);
            var resolving = tokenResolver(agent.AgentId, agent.TenantId, new[] { scope }, timeout.Token);
            string? token;
            try
            {
                // The timeout bounds the resolver's task as well as asking it to stop, so a resolver that ignores
                // cancellation cannot hold the identity's acquisition, and every later evaluation, forever.
                token = await resolving.WaitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                ObserveFailure(resolving);
                throw;
            }

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

                CacheToken(key, new CachedToken(token!, expiresAt));
            }

            return token!;
        }

        /// <summary>
        /// Caches a token within <see cref="MaxCachedTokens"/>: at capacity, expired tokens go first, then the one
        /// closest to expiry. Writers are serialized, so concurrent acquisitions cannot each evict the same entry and
        /// then all insert.
        /// </summary>
        private void CacheToken(TokenCacheKey key, CachedToken token)
        {
            lock (_tokenCacheLock)
            {
                if (_tokens.Count >= MaxCachedTokens)
                {
                    var now = _timeProvider.GetUtcNow();
                    foreach (var stale in _tokens.Where(entry => entry.Value.ExpiresAt <= now).Select(entry => entry.Key).ToList())
                    {
                        _tokens.TryRemove(stale, out _);
                    }

                    if (_tokens.Count >= MaxCachedTokens && _tokens.OrderBy(entry => entry.Value.ExpiresAt).FirstOrDefault() is { Value: not null } oldest)
                    {
                        _tokens.TryRemove(oldest.Key, out _);
                    }
                }

                _tokens[key] = token;
            }
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
                if ((JsonNode.Parse(Convert.FromBase64String(payload)) as JsonObject)?["exp"] is not JsonValue exp)
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
            // A session the client does not track starts above every sequence given to a session it dropped, so a
            // session that comes back never repeats or goes back.
            var next = _sequences.AddOrUpdate(sessionId, _ => Interlocked.Read(ref _sequenceFloor) + 1, (_, current) => current + 1);
            if (_sequences.Count > MaxTrackedSessions)
            {
                foreach (var entry in _sequences.Where(entry => entry.Key != sessionId).Take(_sequences.Count - MaxTrackedSessions).ToList())
                {
                    // The floor rises before the session is dropped, and the session is dropped only if it was not
                    // numbered since, so a concurrent call for it cannot fall below the floor.
                    RaiseSequenceFloor(entry.Value);
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

        private string GeneratedToolCallId() => "tooluse_" + _idFactory().ToString("N").Substring(0, 12);

        private string UtcTimestamp(JsonNode? value)
        {
            var parsed = ReadString(value) is { } text
                && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)
                    ? timestamp
                    : _timeProvider.GetUtcNow();
            return FormatUtc(parsed);
        }

        private static string FormatUtc(DateTimeOffset value) =>
            value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        private static string SanitizeFramework(string? framework)
        {
            var value = InvalidFrameworkCharacters.Replace((framework ?? string.Empty).Trim().ToLowerInvariant(), "-").Trim('-');
            return value.Length > 0 ? value : DefaultFramework;
        }

        /// <summary>
        /// Copies a value with every string clamped to <paramref name="maxCharacters"/> and the whole copy kept within
        /// <paramref name="budget"/>. Strings cost their characters, numbers and Booleans their JSON text, and each
        /// null, array element and property name at least one character, so the copy stays bounded however the value
        /// is shaped; arrays and objects nested deeper than <see cref="MaxContentDepth"/> are cut, so the recursion
        /// stays shallow. <paramref name="truncated"/> is set when anything was cut.
        /// </summary>
        private static JsonNode? Clamp(JsonNode? node, int maxCharacters, ContentBudget budget, ref bool truncated, int depth = 0)
        {
            switch (node)
            {
                case null:
                    // A null costs a character like any copied value, so padding with nulls is bounded too.
                    budget.Spend(1);
                    return null;
                case JsonValue value when value.TryGetValue<string>(out var text):
                    var limit = Math.Min(maxCharacters, budget.Remaining);
                    truncated |= text.Length > limit;
                    var clamped = Truncate(text, limit);
                    budget.Spend(clamped.Length);
                    return JsonValue.Create(clamped);
                case JsonArray or JsonObject when depth >= MaxContentDepth:
                    truncated = true;
                    return null;
                case JsonArray array:
                    var items = new JsonArray();
                    foreach (var item in array)
                    {
                        if (budget.Remaining == 0)
                        {
                            truncated = true;
                            break;
                        }

                        budget.Spend(1);
                        items.Add(Clamp(item, maxCharacters, budget, ref truncated, depth + 1));
                    }

                    return items;
                case JsonObject obj:
                    var properties = new JsonObject();
                    foreach (var property in obj)
                    {
                        if (budget.Remaining < property.Key.Length + 1)
                        {
                            truncated = true;
                            break;
                        }

                        budget.Spend(property.Key.Length + 1);
                        properties[property.Key] = Clamp(property.Value, maxCharacters, budget, ref truncated, depth + 1);
                    }

                    return properties;
                default:
                    // Numbers and Booleans cost their JSON text, as strings cost their characters.
                    var literal = node.ToJsonString();
                    if (literal.Length > budget.Remaining)
                    {
                        truncated = true;
                        return null;
                    }

                    budget.Spend(literal.Length);
                    return node.DeepClone();
            }
        }

        /// <summary>
        /// A copy of the value with lone surrogates replaced by U+FFFD in every string and property name: they are
        /// not valid UTF-16, so Defender cannot parse a request that carries them. Valid surrogate pairs are kept.
        /// Keys stay distinct, so no value is dropped: a renamed key that would collide with another gets a numbered
        /// suffix. It only walks the fitted copy, whose depth <see cref="Clamp"/> bounds.
        /// </summary>
        private static JsonNode? ReplaceLoneSurrogates(JsonNode? node)
        {
            switch (node)
            {
                case null:
                    return null;
                case JsonValue value when value.TryGetValue<string>(out var text):
                    return JsonValue.Create(ReplaceLoneSurrogates(text));
                case JsonArray array:
                    var items = new JsonArray();
                    foreach (var item in array)
                    {
                        items.Add(ReplaceLoneSurrogates(item));
                    }

                    return items;
                case JsonObject obj:
                    // Keys that are valid UTF-16 keep their names; only the renamed ones can need a suffix.
                    var taken = new HashSet<string>(
                        obj.Select(property => property.Key).Where(key => ReplaceLoneSurrogates(key) == key),
                        StringComparer.Ordinal);
                    var properties = new JsonObject();
                    foreach (var property in obj)
                    {
                        var name = ReplaceLoneSurrogates(property.Key);
                        if (name != property.Key)
                        {
                            var unique = name;
                            for (var suffix = 2; !taken.Add(unique); suffix++)
                            {
                                unique = $"{name}~{suffix}";
                            }

                            name = unique;
                        }

                        properties[name] = ReplaceLoneSurrogates(property.Value);
                    }

                    return properties;
                default:
                    return node.DeepClone();
            }
        }

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

        private static string Truncate(string value, int maxCharacters)
        {
            if (value.Length <= maxCharacters)
            {
                return value;
            }

            // The marker counts toward the limit; it is sized for the most digits it can need, so the result
            // never exceeds maxCharacters. A limit too small to hold it cuts without a marker.
            var kept = maxCharacters - TruncationMarker(value.Length).Length;
            if (kept <= 0)
            {
                return value.Substring(0, KeepSurrogatePairs(value, maxCharacters));
            }

            kept = KeepSurrogatePairs(value, kept);
            return string.Concat(value.AsSpan(0, kept), TruncationMarker(value.Length - kept));
        }

        /// <summary>Moves a cut back one code unit when it would split a surrogate pair, which cannot be serialized.</summary>
        private static int KeepSurrogatePairs(string value, int cut) =>
            cut > 0 && cut < value.Length && char.IsHighSurrogate(value[cut - 1]) && char.IsLowSurrogate(value[cut]) ? cut - 1 : cut;

        private static string TruncationMarker(int dropped) => $"...[truncated {dropped} chars]";

        private static string? ReadString(JsonNode? node) =>
            node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        // Reads the number's JSON text, so a value a host built from any integer type counts, not only a long.
        private static bool IsNonNegativeInteger(JsonNode? node) =>
            node is JsonValue value
            && value.GetValueKind() == JsonValueKind.Number
            && long.TryParse(value.ToJsonString(), NumberStyles.None, CultureInfo.InvariantCulture, out _);

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

        // Identifies an agent identity's token by its parts, so no two identities can share a key.
        private readonly record struct TokenCacheKey(string TenantId, string AgentId, string Scope);

        /// <summary>The characters of content a fitted copy may still carry.</summary>
        private sealed class ContentBudget
        {
            public ContentBudget(int characters)
            {
                Remaining = Math.Max(0, characters);
            }

            public int Remaining { get; private set; }

            public int Used { get; private set; }

            public void Spend(int characters)
            {
                var spent = Math.Min(Remaining, Math.Max(0, characters));
                Remaining -= spent;
                Used += spent;
            }
        }
    }
}
