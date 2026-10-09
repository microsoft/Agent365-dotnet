// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AgentHooks;
using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Protection.Defender;
using Microsoft.Agents.A365.Tooling.Protection.Purview;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentHooks.Tests;

/// <summary>
/// Runs the Purview interceptor under the real agent-hooks emitter (native core) against a fake Microsoft Graph
/// <c>processContent</c> endpoint.
/// </summary>
public class A365PurviewInterceptorTests
{
    private const string GraphBase = "https://graph.example.test/v1.0";
    private const string DefenderEndpoint = "https://prevention.example.test/v1/protection/evaluate";
    private const string AgentId = "aaaaaaaa-0000-4000-8000-000000000001";
    private const string TenantId = "bbbbbbbb-0000-4000-8000-000000000002";
    private const string BlueprintId = "cccccccc-0000-4000-8000-000000000003";
    private const string AgenticUserId = "dddddddd-0000-4000-8000-000000000004";
    private const string Payload = "BLOCK_ME";
    private const string Clean = """{"protectionScopeState":"modified","policyActions":[],"processingErrors":[]}""";
    private const string Blocked = """{"protectionScopeState":"modified","policyActions":[{"@odata.type":"#microsoft.graph.restrictAccessAction","action":"restrictAccess","restrictionAction":"block"}],"processingErrors":[]}""";
    private const string RequestBlocked = "The request was blocked by a Microsoft Purview data loss prevention policy.";

    private static readonly PurviewDlpTokenResolver Tokens = (_, _, _) => Task.FromResult<PurviewDlpToken?>(new PurviewDlpToken("graph-access-token"));

    [Fact]
    public async Task AllowsACleanPromptAndSendsItAsUploadText()
    {
        var harness = new Harness(_ => Graph(Clean));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "conversation:activity", "SampleAgent");
        var context = builder.Input(JsonValue.Create("Find 2 flights from Seattle to San Francisco next week")!);

        var record = await harness.Emitter.EmitUncheckedAsync(context, CancellationToken.None);

        record.Proceeds.Should().BeTrue();
        record.Verdict.Decision.Should().Be(Decision.Allow);
        (record.Verdict.Warnings ?? Array.Empty<Warning>()).Should().BeEmpty();
        harness.Requests.Should().ContainSingle().Which.Uri.Should().Be(new Uri(GraphBase + "/me/dataSecurityAndGovernance/processContent"));
        var body = harness.Bodies.Should().ContainSingle().Subject;
        var entry = Entry(body);
        entry["content"]!["data"]!.GetValue<string>().Should().Be("Find 2 flights from Seattle to San Francisco next week");
        entry["correlationId"]!.GetValue<string>().Should().Be("conversation:activity", "the content is grouped by the agent-hooks session");
        entry["sequenceNumber"]!.GetValue<long>().Should().Be(long.Parse(context.Json["sequence"]!.ToJsonString()), "the agent-hooks sequence orders it");
        body["contentToProcess"]!["activityMetadata"]!["activity"]!.GetValue<string>().Should().Be("uploadText");
        body["contentToProcess"]!["protectedAppMetadata"]!["applicationLocation"]!["value"]!.GetValue<string>().Should().Be(BlueprintId);
        var evaluation = (await harness.EvaluationsAsync()).Should().ContainSingle().Subject;
        evaluation.Activity.Should().Be(PurviewDlpActivity.UploadText);
        evaluation.CorrelationId.Should().Be(harness.RequestIds.Single());
    }

    [Fact]
    public async Task BlocksAPromptAPurviewPolicyBlocks()
    {
        var harness = new Harness(BlocksThePayload);
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-1", "SampleAgent");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create($"Please charge my card {Payload}")!), CancellationToken.None);

        record.Proceeds.Should().BeFalse();
        record.Verdict.Decision.Should().Be(Decision.Deny);
        record.Verdict.Reason.Should().Be("purview:block");
        record.Verdict.Message.Should().Be(RequestBlocked);
        record.Verdict.Evidence!.Artefact.Should().Be("purview-verdict");
        record.Verdict.Evidence.VerificationPointers!["correlation"].Should().Be($"urn:a365:purview:{harness.RequestIds.Single()}");
        record.DecidedBy.Should().Be(0);
    }

    [Fact]
    public async Task AuditsTheReplyWithoutWaitingAndNeverBlocksIt()
    {
        var answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var harness = new Harness(
            async (_, _) =>
            {
                await answer.Task;
                return Graph(Blocked);
            },
            failClosed: true);
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-2", "SampleAgent");

        var emitting = harness.Emitter.EmitUncheckedAsync(builder.Output(JsonValue.Create($"Your card {Payload} is on file.")!), CancellationToken.None).AsTask();
        (await Task.WhenAny(emitting, Task.Delay(TimeSpan.FromSeconds(10)))).Should().BeSameAs(emitting, "the reply does not wait for Purview");
        var record = await emitting;
        answer.SetResult();

        record.Proceeds.Should().BeTrue("Purview DLP restricts prompts, so replies are audited");
        (record.Verdict.Warnings ?? Array.Empty<Warning>()).Should().BeEmpty();
        var evaluation = (await harness.EvaluationsAsync()).Should().ContainSingle().Subject;
        evaluation.Activity.Should().Be(PurviewDlpActivity.DownloadText);
        evaluation.Evaluated.Should().BeTrue();
        evaluation.Allowed.Should().BeFalse("the callback still reports what Purview decided");
        Entry(harness.Bodies.Single())["content"]!["data"]!.GetValue<string>().Should().Be($"Your card {Payload} is on file.");
        harness.Bodies.Single()["contentToProcess"]!["activityMetadata"]!["activity"]!.GetValue<string>().Should().Be("downloadText");
    }

    [Fact]
    public async Task KeepsAuditingTheReplyAfterTheInterceptionIsCancelled()
    {
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var evaluated = new TaskCompletionSource<PurviewDlpEvaluationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var graph = new FakeGraph(
            async (_, cancellationToken) =>
            {
                requested.TrySetResult();

                // Ends early only if the request's token is cancelled.
                await answer.Task.WaitAsync(cancellationToken);
                return Graph(Clean);
            },
            new List<JsonObject>(),
            new List<(Uri Uri, string RequestId)>());
        var client = new PurviewDlpClient(new PurviewDlpOptions { Enabled = true, GraphBaseUrl = new Uri(GraphBase) }, new HttpClient(graph));
        var interceptor = new A365PurviewInterceptor(client, _ => new A365PurviewCall(Agent(), Tokens), result => evaluated.TrySetResult(result));
        var context = new AgentContextBuilder(AgentId, "agent-framework", "s-3", "SampleAgent").Output(JsonValue.Create("Here are three flights.")!);
        using var interception = new CancellationTokenSource();

        // Called directly: the emitter hands each interceptor a token of its own, which it stops using on return.
        var verdict = await interceptor.InterceptAsync(context, interception.Token);
        await requested.Task.WaitAsync(TimeSpan.FromSeconds(10));
        interception.Cancel();
        answer.SetResult();

        verdict.Decision.Should().Be(Decision.Allow);
        var evaluation = await evaluated.Task.WaitAsync(TimeSpan.FromSeconds(10));
        evaluation.Evaluated.Should().BeTrue("the audit is bounded by the client's deadline, not the interception's token");
        evaluation.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task EnforcesTheReplyWhenConfigured()
    {
        var harness = new Harness(BlocksThePayload, responseMode: PurviewDlpResponseMode.Enforce);
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-4", "SampleAgent");

        var clean = await harness.Emitter.EmitUncheckedAsync(builder.Output(JsonValue.Create("Here are three flights.")!), CancellationToken.None);
        var blocked = await harness.Emitter.EmitUncheckedAsync(builder.Output(JsonValue.Create($"Your card {Payload} is on file.")!), CancellationToken.None);

        clean.Proceeds.Should().BeTrue();
        blocked.Proceeds.Should().BeFalse();
        blocked.Verdict.Reason.Should().Be("purview:block");
        blocked.Verdict.Message.Should().Be("The response was blocked by a Microsoft Purview data loss prevention policy.");
        harness.Bodies.Should().HaveCount(2).And.OnlyContain(body => body["contentToProcess"]!["activityMetadata"]!["activity"]!.GetValue<string>() == "downloadText");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FollowsTheFailModeWhenNoIdentityIsResolved(bool failClosed)
    {
        var harness = new Harness(_ => Graph(Clean), failClosed: failClosed, resolveCall: _ => null);
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-5");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);

        AssertFollowsTheFailMode(record, failClosed);
        harness.Bodies.Should().BeEmpty();
        (await harness.EvaluationsAsync()).Should().ContainSingle().Which.Error.Should().Be("no agent identity was resolved");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FollowsTheFailModeWhenResolvingTheCallFails(bool failClosed)
    {
        var harness = new Harness(_ => Graph(Clean), failClosed: failClosed, resolveCall: _ => throw new InvalidOperationException("no turn is available"));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-6");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);

        AssertFollowsTheFailMode(record, failClosed);
        harness.Bodies.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordsOnlyTheExceptionTypeWhenTheEvaluationFails()
    {
        const string Secret = "client_secret=do-not-leak";
        var harness = new Harness(_ => Graph(Clean), resolveCall: _ => throw new InvalidOperationException(Secret));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-7");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);

        AssertFollowsTheFailMode(record, failClosed: false);
        (await harness.EvaluationsAsync()).Should().ContainSingle().Which.Error.Should().Be("evaluation failed (InvalidOperationException)");
        record.Verdict.Warnings.Should().OnlyContain(warning => warning.Message == null || !warning.Message.Contains(Secret));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FollowsTheFailModeWhenTheTokenResolverFails(bool failClosed)
    {
        var harness = new Harness(
            _ => Graph(Clean),
            failClosed: failClosed,
            tokenResolver: (_, _, _) => throw new InvalidOperationException("no credential"));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-8");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);

        AssertFollowsTheFailMode(record, failClosed);
        harness.Bodies.Should().BeEmpty();
        (await harness.EvaluationsAsync()).Should().ContainSingle().Which.Error.Should().Be("entra token unavailable (InvalidOperationException)");
    }

    [Fact]
    public async Task ReportsAReplyAuditThatCouldNotStartWithoutBlockingTheReply()
    {
        var calls = 0;
        var harness = new Harness(
            _ => Graph(Clean),
            failClosed: true,
            resolveCall: _ => ++calls == 1 ? throw new InvalidOperationException("no turn is available") : (A365PurviewCall?)null);
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-9");

        var failed = await harness.Emitter.EmitUncheckedAsync(builder.Output(JsonValue.Create("Here are three flights.")!), CancellationToken.None);
        var unresolved = await harness.Emitter.EmitUncheckedAsync(builder.Output(JsonValue.Create("Anything else?")!), CancellationToken.None);

        failed.Proceeds.Should().BeTrue("a reply audited in the background is never blocked, whatever the fail mode");
        unresolved.Proceeds.Should().BeTrue();
        harness.Bodies.Should().BeEmpty();
        (await harness.EvaluationsAsync(2)).Select(evaluation => evaluation.Error)
            .Should().BeEquivalentTo("evaluation failed (InvalidOperationException)", "no agent identity was resolved");
    }

    [Fact]
    public async Task DoesNotCallPurviewAtOtherPoints()
    {
        var resolved = 0;
        var harness = new Harness(_ => Graph(Blocked), resolveCall: _ =>
        {
            resolved++;
            return new A365PurviewCall(Agent(), Tokens);
        });
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-10");

        var records = new[]
        {
            await harness.Emitter.EmitUncheckedAsync(builder.AgentStartup(new[] { "SearchFlights" }), CancellationToken.None),
            await harness.Emitter.EmitUncheckedAsync(
                builder.PreModelCall("gpt-4o", new JsonArray(new JsonObject { ["role"] = "user", ["content"] = Payload }), new JsonArray(), null),
                CancellationToken.None),
            await harness.Emitter.EmitUncheckedAsync(
                builder.PreToolCall("call-1", "SearchFlights", new JsonObject { ["query"] = Payload }),
                CancellationToken.None),
            await harness.Emitter.EmitUncheckedAsync(
                builder.PostToolCall("call-1", "SearchFlights", new JsonObject { ["query"] = "SEA" }, JsonValue.Create(Payload)!),
                CancellationToken.None),
            await harness.Emitter.EmitUncheckedAsync(builder.AgentShutdown("completed"), CancellationToken.None),
        };

        records.Should().OnlyContain(record => record.Proceeds);
        harness.Bodies.Should().BeEmpty();
        resolved.Should().Be(0, "the call is resolved only for the points Purview evaluates");
    }

    [Fact]
    public async Task DoesNotCallPurviewForContentWithoutText()
    {
        var resolved = 0;
        var harness = new Harness(_ => Graph(Blocked), resolveCall: _ =>
        {
            resolved++;
            return new A365PurviewCall(Agent(), Tokens);
        });
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-11");

        var blank = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("  \n ")!), CancellationToken.None);
        var empty = await harness.Emitter.EmitUncheckedAsync(builder.Input(new JsonObject { ["attachments"] = new JsonArray() }), CancellationToken.None);

        blank.Proceeds.Should().BeTrue();
        empty.Proceeds.Should().BeTrue();
        harness.Bodies.Should().BeEmpty();
        resolved.Should().Be(0);
    }

    [Fact]
    public async Task DoesNothingWhenDisabled()
    {
        var resolved = 0;
        var harness = new Harness(_ => Graph(Blocked), enabled: false, resolveCall: _ =>
        {
            resolved++;
            return new A365PurviewCall(Agent(), Tokens);
        });
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-12");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create(Payload)!), CancellationToken.None);

        record.Proceeds.Should().BeTrue();
        harness.Bodies.Should().BeEmpty();
        resolved.Should().Be(0);
    }

    [Fact]
    public async Task SendsEveryStringAndNumberOfStructuredContent()
    {
        var harness = new Harness(_ => Graph(Clean));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-13");
        var content = new JsonArray(
            new JsonObject { ["type"] = "text", ["text"] = "Book the hotel" },
            new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = "https://images.example.test/a.png", ["detail"] = null } },
            new JsonObject { ["card"] = 4111111111111111, ["confirmed"] = true });

        await harness.Emitter.EmitUncheckedAsync(builder.Input(content), CancellationToken.None);

        Entry(harness.Bodies.Single())["content"]!["data"]!.GetValue<string>()
            .Should().Be("text\nBook the hotel\nimage_url\nhttps://images.example.test/a.png\n4111111111111111");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FollowsTheFailModeWhenPurviewAllowsATruncatedCopy(bool failClosed)
    {
        var harness = new Harness(BlocksThePayload, failClosed: failClosed, maxContentCharacters: 100);
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-14");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create(new string('a', 100) + Payload)!), CancellationToken.None);

        AssertFollowsTheFailMode(record, failClosed);
        record.Verdict.Warnings.Should().Contain(warning => warning.Reason == "purview:unverified" && warning.Message == PurviewDlpClient.TruncatedContentError);
    }

    [Fact]
    public async Task BlocksWhenPurviewBlocksATruncatedCopy()
    {
        var harness = new Harness(BlocksThePayload, maxContentCharacters: 100);
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-15");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create(Payload + new string('a', 200))!), CancellationToken.None);

        record.Proceeds.Should().BeFalse();
        record.Verdict.Reason.Should().Be("purview:block");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeepsTheVerdictWhenTheEvaluationCallbackThrows(bool block)
    {
        var harness = new Harness(_ => Graph(block ? Blocked : Clean), onEvaluated: _ => throw new InvalidOperationException("telemetry is unavailable"));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-16");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);

        record.Proceeds.Should().Be(!block);
        if (block)
        {
            record.Verdict.Reason.Should().Be("purview:block", "a failing callback is not a host error");
        }
    }

    [Fact]
    public async Task DoesNotLetASlowEvaluationCallbackDecideTheVerdict()
    {
        using var release = new ManualResetEventSlim();
        var callbackDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var harness = new Harness(
            _ => Graph(Clean),
            timeout: TimeSpan.FromMilliseconds(500),
            onEvaluated: _ =>
            {
                release.Wait(TimeSpan.FromSeconds(30));
                callbackDone.TrySetResult();
            },
            tokenResolver: async (_, _, _) =>
            {
                // Asynchronous like a real call, so the emitter is already timing the interceptor when the callback runs.
                await Task.Delay(10).ConfigureAwait(false);
                return new PurviewDlpToken("graph-access-token");
            });
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-17");

        var record = await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);
        release.Set();
        await callbackDone.Task.WaitAsync(TimeSpan.FromSeconds(10));

        record.Proceeds.Should().BeTrue("the callback runs after the verdict, outside the emitter's interceptor timeout");
        record.Verdict.Decision.Should().Be(Decision.Allow);
    }

    [Fact]
    public async Task DoesNotChangeTheAgentContextTheFactoryReturns()
    {
        var agent = Agent();
        agent.SessionId = "turn-session";
        var harness = new Harness(_ => Graph(Clean), resolveCall: _ => new A365PurviewCall(agent, Tokens));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "hook-session");

        await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("one")!), CancellationToken.None);
        await harness.Emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("two")!), CancellationToken.None);

        agent.SessionId.Should().Be("turn-session");
        agent.SequenceNumber.Should().BeNull();
        harness.Bodies.Select(body => Entry(body)["correlationId"]!.GetValue<string>()).Should().Equal("hook-session", "hook-session");
        var sequences = harness.Bodies.Select(body => Entry(body)["sequenceNumber"]!.GetValue<long>()).ToList();
        sequences[1].Should().BeGreaterThan(sequences[0]);
    }

    [Fact]
    public async Task AppliesTheFailModeBeforeTheInterceptorTimeout()
    {
        var options = new PurviewDlpOptions { Enabled = true, GraphBaseUrl = new Uri(GraphBase), Timeout = TimeSpan.FromSeconds(1) };
        var client = new PurviewDlpClient(options, new HttpClient(new HangingEndpoint()));
        PurviewDlpTokenResolver slowToken = async (_, _, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(900), cancellationToken);
            return new PurviewDlpToken("graph-access-token");
        };

        // 0.9 s for the token plus a fresh 1 s for the request would outlast this 1.6 s interceptor timeout.
        var emitter = A365AgentHooks.CreateProtectionEmitter(TimeSpan.FromMilliseconds(1600), purview: options)
            .AddA365Purview(new A365PurviewInterceptor(client, _ => new A365PurviewCall(Agent(), slowToken)));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-18");

        var record = await emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);

        record.Proceeds.Should().BeTrue("Purview is configured to fail open");
        record.Verdict.Warnings.Should().ContainSingle(warning => warning.Reason == "purview:unverified");
    }

    [Fact]
    public async Task SizesTheInterceptorTimeoutForTheSlowerProvider()
    {
        var defender = new DefenderRtpOptions { Enabled = false, Endpoint = new Uri(DefenderEndpoint), Timeout = TimeSpan.FromMilliseconds(100) };
        var purview = new PurviewDlpOptions { Enabled = true, GraphBaseUrl = new Uri(GraphBase), Timeout = TimeSpan.FromMilliseconds(2500) };
        var client = new PurviewDlpClient(purview, new HttpClient(new HangingEndpoint()));

        // Sized from Defender alone, the emitter would give up after 2.1 s, before Purview's 2.5 s deadline.
        var emitter = A365AgentHooks.CreateProtectionEmitter(defender: defender, purview: purview)
            .AddA365Purview(new A365PurviewInterceptor(client, _ => new A365PurviewCall(Agent(), Tokens)));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-19");

        var record = await emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("hello")!), CancellationToken.None);

        record.Proceeds.Should().BeTrue("Purview's own deadline applies its fail-open mode before the interceptor timeout");
        record.Verdict.Warnings.Should().ContainSingle(warning => warning.Reason == "purview:unverified" && warning.Message == "request timeout");
    }

    [Fact]
    public async Task ComposesDefenderAndPurviewOnOneEmitter()
    {
        var endpoints = new RoutingEndpoint(
            defender: body => body["interception_point"]!.GetValue<string>() == "pre_tool_call" && body.ToJsonString().Contains("malicious.example.test", StringComparison.Ordinal)
                ? Json(new { decision = "deny", reason = "prevention_blocked", message = "Known malicious URL." })
                : Json(new { decision = "allow" }),
            graph: body => Graph(body.ToJsonString().Contains(Payload, StringComparison.Ordinal) ? Blocked : Clean));
        var defenderOptions = new DefenderRtpOptions { Enabled = true, Endpoint = new Uri(DefenderEndpoint) };
        var purviewOptions = new PurviewDlpOptions { Enabled = true, GraphBaseUrl = new Uri(GraphBase) };
        var defender = new DefenderRtpClient(defenderOptions, new HttpClient(endpoints));
        var purview = new PurviewDlpClient(purviewOptions, new HttpClient(endpoints));
        var defenderAgent = new DefenderRtpAgentContext { AgentId = AgentId, TenantId = TenantId };
        var emitter = A365AgentHooks.CreateProtectionEmitter(defender: defenderOptions, purview: purviewOptions)
            .AddA365Defender(new A365DefenderInterceptor(defender, _ => new A365DefenderCall(defenderAgent, (_, _, _, _) => Task.FromResult<string?>("defender-token"))))
            .AddA365Purview(new A365PurviewInterceptor(purview, _ => new A365PurviewCall(Agent(), Tokens)));
        var builder = new AgentContextBuilder(AgentId, "agent-framework", "s-20", "SampleAgent");

        var clean = await emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create("Find 2 flights from Seattle to San Francisco")!), CancellationToken.None);
        var card = await emitter.EmitUncheckedAsync(builder.Input(JsonValue.Create($"Please charge my card {Payload}")!), CancellationToken.None);
        var tool = await emitter.EmitUncheckedAsync(
            builder.PreToolCall("call-1", "FetchTravelAdvisory", new JsonObject { ["url"] = "https://malicious.example.test" }),
            CancellationToken.None);

        clean.Proceeds.Should().BeTrue("both providers allow a clean turn");
        card.Proceeds.Should().BeFalse();
        card.Verdict.Reason.Should().Be("purview:block");
        card.DecidedBy.Should().Be(1, "Purview, registered second, decided");
        tool.Proceeds.Should().BeFalse();
        tool.Verdict.Reason.Should().Be("defender:block:prevention_blocked");
        endpoints.DefenderPoints.Should().Equal("input", "input", "pre_tool_call");
        endpoints.GraphActivities.Should().Equal("uploadText", "uploadText");
    }

    [Fact]
    public void MapsAPurviewResultToAnAgentHooksVerdict()
    {
        var blocked = A365PurviewInterceptor.ToVerdict(new PurviewDlpEvaluationResult
        {
            Allowed = false,
            Evaluated = true,
            CorrelationId = "cid/1",
            BlockReason = RequestBlocked,
            Decision = new PurviewDlpDecision { BlockAction = true, RestrictionAction = "block", ActionCount = 1 },
        });
        var unverifiedOpen = A365PurviewInterceptor.ToVerdict(new PurviewDlpEvaluationResult { Allowed = true, Evaluated = false, Error = "http 403" });
        var unverifiedClosed = A365PurviewInterceptor.ToVerdict(new PurviewDlpEvaluationResult { Allowed = false, Evaluated = false, Error = "http 503" });

        blocked.Decision.Should().Be(Decision.Deny);
        blocked.Reason.Should().Be("purview:block");
        blocked.Evidence!.VerificationPointers!["correlation"].Should().Be("urn:a365:purview:cid%2F1");
        unverifiedOpen.Decision.Should().Be(Decision.Allow);
        unverifiedOpen.Warnings.Should().ContainSingle(warning => warning.Reason == "purview:unverified" && warning.Message == "http 403");
        unverifiedClosed.Decision.Should().Be(Decision.Deny);
        unverifiedClosed.Reason.Should().Be("runtime_error:purview_unverified");
        unverifiedClosed.Message.Should().Be("Data loss prevention validation is unavailable and this agent is configured to fail closed.");
    }

    [Fact]
    public void RegistersOnlyAnInterceptor()
    {
        var emitter = A365AgentHooks.CreateProtectionEmitter();
        var register = () => emitter.AddA365Purview(null!);
        var withoutEmitter = () => A365AgentHooks.AddA365Purview(null!, new A365PurviewInterceptor(new PurviewDlpClient(new PurviewDlpOptions()), _ => null));

        register.Should().Throw<ArgumentNullException>().WithParameterName("interceptor");
        withoutEmitter.Should().Throw<ArgumentNullException>().WithParameterName("emitter");
    }

    private static JsonObject Entry(JsonObject body) => body["contentToProcess"]!["contentEntries"]![0]!.AsObject();

    private static PurviewDlpAgentContext Agent() => new()
    {
        AgentId = AgentId,
        TenantId = TenantId,
        AgenticUserId = AgenticUserId,
        BlueprintId = BlueprintId,
        AgentName = "SampleAgent",
    };

    /// <summary>A fake Graph that blocks only text containing <see cref="Payload"/>.</summary>
    private static HttpResponseMessage BlocksThePayload(JsonObject body) =>
        Graph(Entry(body)["content"]!["data"]!.GetValue<string>().Contains(Payload, StringComparison.Ordinal) ? Blocked : Clean);

    private static void AssertFollowsTheFailMode(InterceptionRecord record, bool failClosed)
    {
        record.Proceeds.Should().Be(!failClosed);
        if (failClosed)
        {
            record.Verdict.Reason.Should().Be("runtime_error:purview_unverified", "a failure is not a host error");
        }
        else
        {
            record.Verdict.Warnings.Should().ContainSingle(warning => warning.Reason == "purview:unverified");
        }
    }

    private static HttpResponseMessage Graph(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };

    private sealed class Harness
    {
        private readonly List<PurviewDlpEvaluationResult> _evaluations = new();
        private readonly SemaphoreSlim _evaluated = new(0);

        public Harness(
            Func<JsonObject, HttpResponseMessage> respond,
            bool failClosed = false,
            PurviewDlpResponseMode responseMode = PurviewDlpResponseMode.Audit,
            Action<PurviewDlpEvaluationResult>? onEvaluated = null,
            Func<AgentContext, A365PurviewCall?>? resolveCall = null,
            PurviewDlpTokenResolver? tokenResolver = null,
            bool enabled = true,
            TimeSpan? timeout = null,
            int? maxContentCharacters = null)
            : this((body, _) => Task.FromResult(respond(body)), failClosed, responseMode, onEvaluated, resolveCall, tokenResolver, enabled, timeout, maxContentCharacters)
        {
        }

        public Harness(
            Func<JsonObject, CancellationToken, Task<HttpResponseMessage>> respond,
            bool failClosed = false,
            PurviewDlpResponseMode responseMode = PurviewDlpResponseMode.Audit,
            Action<PurviewDlpEvaluationResult>? onEvaluated = null,
            Func<AgentContext, A365PurviewCall?>? resolveCall = null,
            PurviewDlpTokenResolver? tokenResolver = null,
            bool enabled = true,
            TimeSpan? timeout = null,
            int? maxContentCharacters = null)
        {
            var handler = new FakeGraph(respond, Bodies, Requests);
            var options = new PurviewDlpOptions
            {
                Enabled = enabled,
                GraphBaseUrl = new Uri(GraphBase),
                FailClosed = failClosed,
                ResponseMode = responseMode,
            };
            if (timeout is { } deadline)
            {
                options.Timeout = deadline;
            }

            if (maxContentCharacters is { } maximum)
            {
                options.MaxContentCharacters = maximum;
            }

            var client = new PurviewDlpClient(options, new HttpClient(handler));
            var tokens = tokenResolver ?? Tokens;
            Emitter = A365AgentHooks.CreateProtectionEmitter(purview: options)
                .AddA365Purview(new A365PurviewInterceptor(
                    client,
                    resolveCall ?? (_ => new A365PurviewCall(Agent(), tokens)),
                    onEvaluated ?? Record));
        }

        public InterceptionEmitter Emitter { get; }

        public List<JsonObject> Bodies { get; } = new();

        public List<(Uri Uri, string RequestId)> Requests { get; } = new();

        public IEnumerable<string> RequestIds => Requests.Select(request => request.RequestId);

        /// <summary>Waits for the evaluations the callback receives, which runs on the thread pool after the verdict.</summary>
        public async Task<List<PurviewDlpEvaluationResult>> EvaluationsAsync(int count = 1)
        {
            for (var received = 0; received < count; received++)
            {
                (await _evaluated.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeTrue("the callback runs soon after the verdict");
            }

            lock (_evaluations)
            {
                return _evaluations.ToList();
            }
        }

        private void Record(PurviewDlpEvaluationResult result)
        {
            lock (_evaluations)
            {
                _evaluations.Add(result);
            }

            _evaluated.Release();
        }
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

    private sealed class FakeGraph : HttpMessageHandler
    {
        private readonly Func<JsonObject, CancellationToken, Task<HttpResponseMessage>> _respond;
        private readonly List<JsonObject> _bodies;
        private readonly List<(Uri Uri, string RequestId)> _requests;

        public FakeGraph(
            Func<JsonObject, CancellationToken, Task<HttpResponseMessage>> respond,
            List<JsonObject> bodies,
            List<(Uri Uri, string RequestId)> requests)
        {
            _respond = respond;
            _bodies = bodies;
            _requests = requests;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
            lock (_bodies)
            {
                _bodies.Add(body);
                _requests.Add((request.RequestUri!, request.Headers.GetValues(PurviewDlpClient.ClientRequestIdHeader).Single()));
            }

            return await _respond(body, cancellationToken);
        }
    }

    /// <summary>Answers for the fake Defender endpoint and the fake Graph, recording what each received.</summary>
    private sealed class RoutingEndpoint : HttpMessageHandler
    {
        private readonly Func<JsonObject, HttpResponseMessage> _defender;
        private readonly Func<JsonObject, HttpResponseMessage> _graph;

        public RoutingEndpoint(Func<JsonObject, HttpResponseMessage> defender, Func<JsonObject, HttpResponseMessage> graph)
        {
            _defender = defender;
            _graph = graph;
        }

        public List<string> DefenderPoints { get; } = new();

        public List<string> GraphActivities { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
            if (request.RequestUri == new Uri(DefenderEndpoint))
            {
                lock (DefenderPoints)
                {
                    DefenderPoints.Add(body["interception_point"]!.GetValue<string>());
                }

                return _defender(body);
            }

            lock (GraphActivities)
            {
                GraphActivities.Add(body["contentToProcess"]!["activityMetadata"]!["activity"]!.GetValue<string>());
            }

            return _graph(body);
        }
    }
}
