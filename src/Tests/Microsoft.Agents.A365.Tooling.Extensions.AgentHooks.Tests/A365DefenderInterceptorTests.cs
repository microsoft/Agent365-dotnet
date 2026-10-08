// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AgentHooks;
using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Protection.Defender;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentHooks.Tests;

/// <summary>
/// Runs the Defender interceptor under the real agent-hooks emitter (native core) against a fake
/// prevention endpoint.
/// </summary>
public class A365DefenderInterceptorTests
{
    private const string Endpoint = "https://prevention.example.test/v1/protection/evaluate";
    private const string AgentId = "aaaaaaaa-0000-4000-8000-000000000001";
    private const string TenantId = "bbbbbbbb-0000-4000-8000-000000000002";
    private const string Payload = "BLOCK_ME";

    [Fact]
    public async Task ForwardsTheEmittedContextAndAllows()
    {
        var harness = new Harness(_ => Json(new { decision = "allow" }));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "conversation:activity", "SampleAgent");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("Find flights to Paris")!), CancellationToken.None);

        record.Proceeds.Should().BeTrue();
        record.Verdict.Decision.Should().Be(Decision.Allow);
        var body = harness.Bodies.Should().ContainSingle().Subject;
        body["spec"]!.GetValue<string>().Should().Be("agent-hooks/0.1");
        body["interception_point"]!.GetValue<string>().Should().Be("input");
        body["agent"]!["id"]!.GetValue<string>().Should().Be(AgentId);
        body["agent"]!["framework"]!.GetValue<string>().Should().Be("agent-framework");
        body["session"]!["id"]!.GetValue<string>().Should().Be("conversation:activity");
        body["tenant"]!["id"]!.GetValue<string>().Should().Be(TenantId);
        body["actor"]!.ToJsonString().Should().Be("""{"id":"user-object-id","kind":"human"}""");
        JsonNode.DeepEquals(body["target"], body["input"]).Should().BeTrue();
        harness.Evaluations.Should().ContainSingle().Which.CorrelationId.Should().Be(harness.CorrelationIds.Single());
    }

    [Fact]
    public async Task BlocksAToolCallDefenderDenies()
    {
        var harness = new Harness(body => body["interception_point"]!.GetValue<string>() == "pre_tool_call"
            ? Json(new { decision = "deny", reason = "prevention_blocked", message = "Known malicious URL.", result_labels = new[] { "MaliciousUrl" } })
            : Json(new { decision = "allow" }));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-1");

        var record = await harness.Emitter.EmitUncheckedAsync(
            builder.PreToolCall("call-1", "FetchTravelAdvisory", new JsonObject { ["url"] = "https://malicious.example.test" }),
            CancellationToken.None);

        record.Proceeds.Should().BeFalse();
        record.Verdict.Decision.Should().Be(Decision.Deny);
        record.Verdict.Reason.Should().Be("defender:block:prevention_blocked");
        record.Verdict.Message.Should().Be("Known malicious URL.");
        record.DecidedBy.Should().Be(0);
        var body = harness.Bodies.Single();
        body["tool_call"]!.ToJsonString().Should().Be("""{"id":"call-1","name":"FetchTravelAdvisory","args":{"url":"https://malicious.example.test"}}""");
        JsonNode.DeepEquals(body["target"], body["tool_call"]!["args"]).Should().BeTrue();
    }

    [Fact]
    public void MapsADefenderDenyToAnAgentHooksVerdictWithEvidenceAndLabels()
    {
        var verdict = A365DefenderInterceptor.ToVerdict(new DefenderRtpEvaluationResult
        {
            Allowed = false,
            Evaluated = true,
            InterceptionPoint = "input",
            CorrelationId = "cid-1",
            BlockReason = "Prompt injection detected.",
            Verdict = new DefenderRtpVerdict { Decision = "deny", Reason = "prevention_blocked", ResultLabels = new[] { "PromptInjection" } },
        });

        verdict.Decision.Should().Be(Decision.Deny);
        verdict.Reason.Should().Be("defender:block:prevention_blocked");
        verdict.ResultLabels.Should().Equal("PromptInjection");
        verdict.Evidence!.VerificationPointers!["correlation"].Should().Be("urn:a365:defender:cid-1");
    }

    [Fact]
    public void MapsToAVerdictThatDoesNotShareTheResultsLabels()
    {
        var labels = new List<string> { "PromptInjection" };
        var verdict = A365DefenderInterceptor.ToVerdict(new DefenderRtpEvaluationResult
        {
            Allowed = false,
            Evaluated = true,
            InterceptionPoint = "input",
            CorrelationId = "cid-2",
            Verdict = new DefenderRtpVerdict { Decision = "deny", ResultLabels = labels },
        });

        labels.Clear();

        verdict.ResultLabels.Should().Equal("PromptInjection");
    }

    [Fact]
    public async Task KeepsTheVerdictWhenTheEvaluationCallbackChangesTheResult()
    {
        var harness = new Harness(
            _ => Json(new
            {
                decision = "allow",
                warnings = new[] { new { reason = "prevention_annotated", message = "Suspicious but allowed." } },
                result_labels = new[] { "MaliciousContentPropagation" },
            }),
            onEvaluated: result =>
            {
                ((IList<DefenderRtpWarning>)result.Verdict!.Warnings).Clear();
                ((IList<string>)result.Verdict.ResultLabels).Clear();
            });
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-9");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);

        record.Proceeds.Should().BeTrue();
        record.Verdict.Warnings.Should().ContainSingle(warning => warning.Reason == "prevention_annotated");
        record.Verdict.ResultLabels.Should().Equal("MaliciousContentPropagation");
    }

    [Fact]
    public async Task AllowsWithAWarningWhenFailOpenDefenderIsUnavailable()
    {
        var harness = new Harness(_ => Json(new { title = "Forbidden", detail = "The caller is not authorized for real-time protection." }, HttpStatusCode.Forbidden));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-2");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Output(JsonValue.Create("Here are three flights.")!), CancellationToken.None);

        record.Proceeds.Should().BeTrue();
        record.Verdict.Warnings.Should().ContainSingle(warning => warning.Reason == "defender:unverified"
            && warning.Message != null && warning.Message.Contains("not authorized for real-time protection"));
    }

    [Theory]
    [InlineData("allow", true)]
    [InlineData("deny", false)]
    public async Task KeepsTheVerdictWhenTheEvaluationCallbackThrows(string decision, bool proceeds)
    {
        var harness = new Harness(
            _ => Json(new { decision, reason = "prevention_blocked" }),
            onEvaluated: _ => throw new InvalidOperationException("telemetry is unavailable"));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-8");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);

        record.Proceeds.Should().Be(proceeds);
        if (proceeds)
        {
            record.Verdict.Decision.Should().Be(Decision.Allow);
        }
        else
        {
            record.Verdict.Reason.Should().Be("defender:block:prevention_blocked", "a failing callback is not a host error");
        }
    }

    [Fact]
    public async Task ResolvesTheCallOnlyForPointsDefenderEvaluatesAndOnlyWhenEnabled()
    {
        var calls = 0;
        DefenderRtpTokenResolver tokens = (_, _, _, _) => Task.FromResult<string?>(Token);
        A365DefenderCall? Resolve(AgentContext _)
        {
            calls++;
            return new A365DefenderCall(new DefenderRtpAgentContext { AgentId = AgentId, TenantId = TenantId }, tokens);
        }

        var enabled = new Harness(_ => Json(new { decision = "allow" }), resolveCall: Resolve);
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-9");
        await enabled.Emitter.EmitUncheckedAsync(builder.AgentStartup(new[] { "SearchFlights" }), CancellationToken.None);
        calls.Should().Be(0, "agent_startup is not evaluated by Defender");
        await enabled.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);
        calls.Should().Be(1);

        var disabled = new Harness(_ => Json(new { decision = "deny" }), resolveCall: Resolve, enabled: false);
        var record = await disabled.Emitter.EmitUncheckedAsync(
            new AgentContextBuilder(AgentId, "agent-framework", "s-10").Input(JsonValue.Create("hello")!),
            CancellationToken.None);

        record.Proceeds.Should().BeTrue();
        calls.Should().Be(1, "a disabled client never resolves the call");
        disabled.Bodies.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FollowsTheFailModeWhenResolvingTheCallFails(bool failClosed)
    {
        var harness = new Harness(
            _ => Json(new { decision = "allow" }),
            failClosed: failClosed,
            resolveCall: _ => throw new InvalidOperationException("no turn is available"));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-11");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);

        AssertFollowsTheFailMode(record, failClosed);
        harness.Bodies.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FollowsTheFailModeWhenTheTokenResolverFails(bool failClosed)
    {
        var harness = new Harness(
            _ => Json(new { decision = "allow" }),
            failClosed: failClosed,
            tokenResolver: (_, _, _, _) => throw new InvalidOperationException("no credential"));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-12");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);

        AssertFollowsTheFailMode(record, failClosed);
        harness.Bodies.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FollowsTheFailModeWhenDefenderAllowsATruncatedCopy(bool failClosed)
    {
        var harness = new Harness(DeniesThePayload, failClosed: failClosed);
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-13");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create(new string('a', 20000) + Payload)!), CancellationToken.None);

        AssertFollowsTheFailMode(record, failClosed);
        record.Verdict.Warnings.Should().Contain(warning => warning.Reason == "defender:unverified" && warning.Message == DefenderRtpClient.TruncatedContentError);
    }

    [Fact]
    public async Task BlocksWhenDefenderDeniesATruncatedCopy()
    {
        var harness = new Harness(DeniesThePayload);
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-14");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create(Payload + new string('a', 20000))!), CancellationToken.None);

        record.Proceeds.Should().BeFalse();
        record.Verdict.Reason.Should().Be("defender:block:prevention_blocked");
    }

    [Fact]
    public async Task AllowsContentWithinTheLimit()
    {
        var harness = new Harness(DeniesThePayload, failClosed: true);
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-15");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create(new string('a', 20000))!), CancellationToken.None);

        record.Proceeds.Should().BeTrue();
        record.Verdict.Decision.Should().Be(Decision.Allow);
        (record.Verdict.Warnings ?? Array.Empty<Warning>()).Should().NotContain(warning => warning.Reason == "defender:unverified");
    }

    [Fact]
    public async Task AppliesTheFailModeBeforeTheInterceptorTimeout()
    {
        var options = new DefenderRtpOptions { Enabled = true, Endpoint = new Uri(Endpoint), Timeout = TimeSpan.FromSeconds(1) };
        var client = new DefenderRtpClient(options, new HttpClient(new HangingEndpoint()));
        DefenderRtpTokenResolver slowToken = async (_, _, _, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(900), cancellationToken);
            return Token;
        };
        var agent = new DefenderRtpAgentContext { AgentId = AgentId, TenantId = TenantId };

        // 0.9 s for the token plus a fresh 1 s for the request would outlast this 1.6 s interceptor timeout.
        var emitter = A365AgentHooks.CreateProtectionEmitter(TimeSpan.FromMilliseconds(1600), options)
            .AddA365Defender(new A365DefenderInterceptor(client, _ => new A365DefenderCall(agent, slowToken)));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-7");

        var record = await emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);

        record.Proceeds.Should().BeTrue("Defender is configured to fail open");
        record.Verdict.Warnings.Should().ContainSingle(warning => warning.Reason == "defender:unverified");
    }

    [Fact]
    public async Task BlocksAsUnverifiedNotAsADetectionWhenFailClosedDefenderIsUnavailable()
    {
        var harness = new Harness(_ => Json(new { title = "Service Unavailable" }, HttpStatusCode.ServiceUnavailable), failClosed: true);
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-3");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);

        record.Proceeds.Should().BeFalse();
        record.Verdict.Reason.Should().Be("runtime_error:defender_unverified");
    }

    [Fact]
    public async Task FollowsTheFailModeWhenTheIdentityIsInvalid()
    {
        var harness = new Harness(_ => Json(new { decision = "allow" }), failClosed: true, agentId: " ");
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-4");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);

        record.Proceeds.Should().BeFalse();
        record.Verdict.Reason.Should().Be("runtime_error:defender_unverified");
        harness.Bodies.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotCallDefenderForPointsItDoesNotEvaluate()
    {
        var harness = new Harness(_ => Json(new { decision = "deny" }));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-5");

        var startup = await harness.Emitter.EmitUncheckedAsync(builder.AgentStartup(new[] { "SearchFlights" }), CancellationToken.None);
        var modelCall = await harness.Emitter.EmitUncheckedAsync(
            builder.PreModelCall("gpt-4o", new JsonArray(new JsonObject { ["role"] = "user", ["content"] = "hi" }), new JsonArray(), null),
            CancellationToken.None);

        startup.Proceeds.Should().BeTrue();
        modelCall.Proceeds.Should().BeTrue();
        harness.Bodies.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FollowsTheFailModeWhenNoIdentityIsResolved(bool failClosed)
    {
        var harness = new Harness(_ => Json(new { decision = "allow" }), failClosed: failClosed, resolveNothing: true);
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-6");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);

        AssertFollowsTheFailMode(record, failClosed);
        harness.Bodies.Should().BeEmpty();
        harness.Evaluations.Should().ContainSingle().Which.Error.Should().Be("no agent identity was resolved");
    }

    /// <summary>A fake Defender that denies only content containing <see cref="Payload"/>.</summary>
    private static HttpResponseMessage DeniesThePayload(JsonObject body) =>
        body.ToJsonString().Contains(Payload, StringComparison.Ordinal)
            ? Json(new { decision = "deny", reason = "prevention_blocked", message = "Blocked payload." })
            : Json(new { decision = "allow" });

    private static void AssertFollowsTheFailMode(InterceptionRecord record, bool failClosed)
    {
        record.Proceeds.Should().Be(!failClosed);
        if (failClosed)
        {
            record.Verdict.Reason.Should().Be("runtime_error:defender_unverified", "a failure is not a host error");
        }
        else
        {
            record.Verdict.Warnings.Should().ContainSingle(warning => warning.Reason == "defender:unverified");
        }
    }

    private static string Token
    {
        get
        {
            static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return $"{Encode("""{"alg":"none"}""")}.{Encode($$"""{"exp":{{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}""")}.sig";
        }
    }

    private static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };

    private sealed class Harness
    {
        public Harness(
            Func<JsonObject, HttpResponseMessage> respond,
            bool failClosed = false,
            string agentId = AgentId,
            bool resolveNothing = false,
            Action<DefenderRtpEvaluationResult>? onEvaluated = null,
            Func<AgentContext, A365DefenderCall?>? resolveCall = null,
            DefenderRtpTokenResolver? tokenResolver = null,
            bool enabled = true)
        {
            var handler = new FakeEndpoint(respond, Bodies, CorrelationIds);
            var options = new DefenderRtpOptions { Enabled = enabled, Endpoint = new Uri(Endpoint), FailClosed = failClosed };
            var client = new DefenderRtpClient(options, new HttpClient(handler));
            DefenderRtpTokenResolver tokens = tokenResolver ?? ((_, _, _, _) => Task.FromResult<string?>(Token));
            var agent = new DefenderRtpAgentContext { AgentId = agentId, TenantId = TenantId, UserId = "user-object-id" };
            Emitter = A365AgentHooks.CreateProtectionEmitter(defender: options)
                .AddA365Defender(new A365DefenderInterceptor(
                    client,
                    resolveCall ?? (_ => resolveNothing ? null : new A365DefenderCall(agent, tokens)),
                    onEvaluated ?? Evaluations.Add));
        }

        public InterceptionEmitter Emitter { get; }

        public List<JsonObject> Bodies { get; } = new();

        public List<string> CorrelationIds { get; } = new();

        public List<DefenderRtpEvaluationResult> Evaluations { get; } = new();
    }

    /// <summary>An endpoint that never answers, so only a deadline ends the request.</summary>
    private sealed class HangingEndpoint : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class FakeEndpoint : HttpMessageHandler
    {
        private readonly Func<JsonObject, HttpResponseMessage> _respond;
        private readonly List<JsonObject> _bodies;
        private readonly List<string> _correlationIds;

        public FakeEndpoint(Func<JsonObject, HttpResponseMessage> respond, List<JsonObject> bodies, List<string> correlationIds)
        {
            _respond = respond;
            _bodies = bodies;
            _correlationIds = correlationIds;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
            _bodies.Add(body);
            _correlationIds.Add(request.Headers.GetValues(DefenderRtpClient.CorrelationIdHeader).Single());
            return _respond(body);
        }
    }
}
