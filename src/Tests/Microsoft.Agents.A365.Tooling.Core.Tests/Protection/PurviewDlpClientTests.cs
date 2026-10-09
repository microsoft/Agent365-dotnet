// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Protection.Purview;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Core.Tests.Protection;

public class PurviewDlpClientTests
{
    private const string GraphBase = "https://graph.example.test/v1.0";
    private const string ProcessContentUrl = GraphBase + "/me/dataSecurityAndGovernance/processContent";
    private const string AgentId = "aaaaaaaa-0000-4000-8000-000000000001";
    private const string TenantId = "bbbbbbbb-0000-4000-8000-000000000002";
    private const string BlueprintId = "cccccccc-0000-4000-8000-000000000003";
    private const string AgenticUserId = "dddddddd-0000-4000-8000-000000000004";
    private const string AccessToken = "graph-access-token";
    private const string Payload = "BLOCK_ME";

    internal const string Clean = """{"protectionScopeState":"modified","policyActions":[],"processingErrors":[]}""";

    internal const string Blocked = """
        {"protectionScopeState":"modified","policyActions":[{"@odata.type":"#microsoft.graph.restrictAccessAction","action":"restrictAccess","restrictionAction":"block"}],"processingErrors":[]}
        """;

    internal const string ProcessingError = """
        {"protectionScopeState":"notModified","policyActions":[],"processingErrors":[{"code":"BadRequest","message":"Invalid request field. The provided data for Name is invalid.","target":null,"errorType":"permanent","innerError":null,"details":[]}]}
        """;

    private static readonly PurviewDlpTokenResolver Tokens = (_, _, _) => Task.FromResult<PurviewDlpToken?>(new PurviewDlpToken(AccessToken));

    // ─── request ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReturnsNullWithoutCallsWhenDisabled()
    {
        var handler = new GraphHandler(_ => Graph(Clean));
        var resolved = 0;
        var client = new PurviewDlpClient(new PurviewDlpOptions { Enabled = false, GraphBaseUrl = new Uri(GraphBase) }, new HttpClient(handler));

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), (_, _, _) =>
        {
            resolved++;
            return Task.FromResult<PurviewDlpToken?>(new PurviewDlpToken(AccessToken));
        });

        result.Should().BeNull();
        handler.Calls.Should().BeEmpty();
        resolved.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n\t ")]
    public async Task ReturnsNullWithoutCallsForEmptyText(string? text)
    {
        var (client, handler) = Create(_ => Graph(Clean));

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, text, Agent(), Tokens);

        result.Should().BeNull();
        handler.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task SendsTheTextAsAConversationEntryWithAUniqueClientRequestId()
    {
        var clock = new ManualClock { Now = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.FromHours(2)) };
        var handler = new GraphHandler(_ => Graph(Clean));
        var client = new PurviewDlpClient(Options(), new HttpClient(handler), timeProvider: clock);

        var first = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "Find flights to Paris", Agent(), Tokens);
        var second = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "And a hotel", Agent(), Tokens);

        handler.Calls.Should().HaveCount(2);
        var call = handler.Calls[0];
        call.Method.Should().Be(HttpMethod.Post);
        call.Uri.Should().Be(new Uri(ProcessContentUrl));
        call.Authorization.Should().Be("Bearer " + AccessToken);
        call.ContentType.Should().Be("application/json");
        Guid.TryParse(call.ClientRequestId, out _).Should().BeTrue();
        handler.Calls[1].ClientRequestId.Should().NotBe(call.ClientRequestId);
        first!.CorrelationId.Should().Be(call.ClientRequestId);
        second!.CorrelationId.Should().Be(handler.Calls[1].ClientRequestId);

        var entry = call.Entry;
        Guid.TryParse(entry["identifier"]!.GetValue<string>(), out _).Should().BeTrue();
        entry["identifier"]!.GetValue<string>().Should().NotBe(call.ClientRequestId);
        var expected = JsonNode.Parse($$"""
            {
              "contentToProcess": {
                "contentEntries": [{
                  "@odata.type": "microsoft.graph.processConversationMetadata",
                  "identifier": "{{entry["identifier"]!.GetValue<string>()}}",
                  "content": { "@odata.type": "microsoft.graph.textContent", "data": "Find flights to Paris" },
                  "agents": [{ "@odata.type": "microsoft.graph.aiAgentInfo", "blueprintId": "{{BlueprintId}}", "identifier": "{{AgentId}}", "name": "SampleAgent", "version": "1.0" }],
                  "name": "SampleAgent uploadText",
                  "correlationId": "conversation-1",
                  "sequenceNumber": 7,
                  "isTruncated": false,
                  "createdDateTime": "2026-10-07T10:00:00.000Z",
                  "modifiedDateTime": "2026-10-07T10:00:00.000Z",
                  "contentCategory": "ai"
                }],
                "activityMetadata": { "activity": "uploadText" },
                "integratedAppMetadata": { "name": "SampleAgent", "version": "1.0" },
                "protectedAppMetadata": {
                  "name": "SampleAgent", "version": "1.0",
                  "applicationLocation": { "@odata.type": "microsoft.graph.policyLocationApplication", "value": "{{BlueprintId}}" }
                }
              }
            }
            """);
        JsonNode.DeepEquals(call.Body, expected).Should().BeTrue("the request body is {0}", call.Body.ToJsonString());

        first.Should().BeEquivalentTo(new
        {
            Allowed = true,
            Evaluated = true,
            Truncated = false,
            Activity = PurviewDlpActivity.UploadText,
            SessionId = "conversation-1",
            ProtectionScopeState = "modified",
            HttpStatus = 200,
            Error = (string?)null,
            BlockReason = (string?)null,
            Decision = new { BlockAction = false, RestrictionAction = (string?)null, ActionCount = 0 },
        });
    }

    [Fact]
    public async Task ReportsTheReplyAsDownloadText()
    {
        var (client, handler) = Create(_ => Graph(Clean));

        var result = await client.EvaluateAsync(PurviewDlpActivity.DownloadText, "Here are three flights.", Agent(), Tokens);

        result!.Activity.Should().Be(PurviewDlpActivity.DownloadText);
        var call = handler.Calls.Single();
        call.Body["contentToProcess"]!["activityMetadata"]!["activity"]!.GetValue<string>().Should().Be("downloadText");
        call.Entry["name"]!.GetValue<string>().Should().Be("SampleAgent downloadText");
    }

    [Theory]
    [InlineData("app-id", BlueprintId, AgentId, "app-id")]
    [InlineData(null, BlueprintId, AgentId, BlueprintId)]
    [InlineData(" ", null, AgentId, AgentId)]
    public async Task ScopesThePolicyLocationToTheApplicationThenTheBlueprintThenTheAgent(
        string? applicationId,
        string? blueprintId,
        string? agentId,
        string expected)
    {
        var (client, handler) = Create(_ => Graph(Clean));
        var agent = Agent();
        agent.ApplicationId = applicationId;
        agent.BlueprintId = blueprintId;
        agent.AgentId = agentId;

        await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", agent, Tokens);

        var body = handler.Calls.Single().Body;
        body["contentToProcess"]!["protectedAppMetadata"]!["applicationLocation"]!["value"]!.GetValue<string>().Should().Be(expected);
    }

    [Fact]
    public async Task DeclaresTheAgentWithoutABlueprintItDoesNotKnow()
    {
        var (client, handler) = Create(_ => Graph(Clean));
        var agent = Agent();
        agent.BlueprintId = null;

        await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", agent, Tokens);

        handler.Calls.Single().Entry["agents"]!.ToJsonString().Should().Be(
            $$"""[{"@odata.type":"microsoft.graph.aiAgentInfo","identifier":"{{AgentId}}","name":"SampleAgent","version":"1.0"}]""");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task NamesTheEntryEvenWithoutAnAgentName(string? agentName)
    {
        var (client, handler) = Create(_ => Graph(Clean));
        var agent = Agent();
        agent.AgentName = agentName;

        await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", agent, Tokens);

        var body = handler.Calls.Single().Body;
        body["contentToProcess"]!["contentEntries"]![0]!["name"]!.GetValue<string>().Should().Be("agent365-agent uploadText", "Graph rejects an entry without a name");
        body["contentToProcess"]!["protectedAppMetadata"]!["name"]!.GetValue<string>().Should().Be(PurviewDlpClient.DefaultAgentName);
    }

    [Fact]
    public async Task NumbersEachSessionsEvaluationsWhenNoSequenceIsGiven()
    {
        var (client, handler) = Create(_ => Graph(Clean));

        foreach (var session in new[] { "s-1", "s-1", "s-2", "s-1" })
        {
            var agent = Agent(sequence: null);
            agent.SessionId = session;
            await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", agent, Tokens);
        }

        handler.Calls.Select(call => call.Entry["sequenceNumber"]!.GetValue<long>()).Should().Equal(0, 1, 0, 2);
    }

    [Fact]
    public async Task KeepsTheSequenceIncreasingForASessionThatWasDropped()
    {
        var handler = new GraphHandler(_ => Graph(Clean));
        var client = new PurviewDlpClient(Options(), new HttpClient(handler)) { MaxTrackedSessions = 2 };
        async Task<long> Next(string session)
        {
            var agent = Agent(sequence: null);
            agent.SessionId = session;
            await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", agent, Tokens);
            return handler.Calls[^1].Entry["sequenceNumber"]!.GetValue<long>();
        }

        var first = await Next("s-1");
        await Next("s-1");
        await Next("s-2");
        await Next("s-3");
        await Next("s-4");
        var returned = await Next("s-1");

        first.Should().Be(0);
        returned.Should().BeGreaterThan(1, "a session that was dropped never repeats or goes back");
    }

    [Fact]
    public async Task UsesTheGivenSequenceAndIgnoresANegativeOne()
    {
        var (client, handler) = Create(_ => Graph(Clean));

        await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(sequence: 42), Tokens);
        await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(sequence: -1), Tokens);

        handler.Calls.Select(call => call.Entry["sequenceNumber"]!.GetValue<long>()).Should().Equal(42, 0);
    }

    [Fact]
    public async Task RequiresASessionAndAnApplication()
    {
        var (client, handler) = Create(_ => Graph(Clean));
        var withoutSession = Agent();
        withoutSession.SessionId = " ";
        var withoutApplication = new PurviewDlpAgentContext { SessionId = "s-1" };

        var noSession = () => client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", withoutSession, Tokens);
        var noApplication = () => client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", withoutApplication, Tokens);

        (await noSession.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("SessionId");
        (await noApplication.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("ApplicationId");
        handler.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluatesForTheUserTheTokenNames()
    {
        var handler = new GraphHandler(_ => Graph(Clean));
        var client = new PurviewDlpClient(Options(new PurviewDlpOptions { GraphBaseUrl = new Uri("https://graph.example.test/beta/") }), new HttpClient(handler));
        PurviewDlpTokenResolver appOnly = (_, _, _) => Task.FromResult<PurviewDlpToken?>(new PurviewDlpToken(AccessToken, "user/../admin@contoso.example"));

        await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), appOnly);

        handler.Calls.Single().Uri.AbsoluteUri.Should().Be(
            "https://graph.example.test/beta/users/user%2F..%2Fadmin%40contoso.example/dataSecurityAndGovernance/processContent");
    }

    [Fact]
    public async Task ReplacesLoneSurrogatesSoGraphCanParseTheRequest()
    {
        var (client, handler) = Create(_ => Graph(Clean));
        var agent = Agent();
        agent.AgentName = "Agent\uD800";

        await client.EvaluateAsync(PurviewDlpActivity.UploadText, "a\uDC00b \uD83D\uDE00 c\uD800", agent, Tokens);

        var call = handler.Calls.Single();
        call.Text.Should().Be("a\uFFFDb \uD83D\uDE00 c\uFFFD", "valid pairs are kept");
        call.Entry["name"]!.GetValue<string>().Should().Be("Agent\uFFFD uploadText");
    }

    // ─── verdict ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task BlocksWhenAPolicyActionRestrictsWithBlock()
    {
        var (client, _) = Create(_ => Graph(Blocked));

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "card number", Agent(), Tokens);

        result.Should().BeEquivalentTo(new
        {
            Allowed = false,
            Evaluated = true,
            Truncated = false,
            HttpStatus = 200,
            Error = (string?)null,
            BlockReason = "The request was blocked by a Microsoft Purview data loss prevention policy.",
            Decision = new { BlockAction = true, RestrictionAction = "block", ActionCount = 1 },
        });
    }

    [Fact]
    public async Task BlocksAReplyWithItsOwnMessage()
    {
        var (client, _) = Create(_ => Graph(Blocked));

        var result = await client.EvaluateAsync(PurviewDlpActivity.DownloadText, "card number", Agent(), Tokens);

        result!.Allowed.Should().BeFalse();
        result.BlockReason.Should().Be("The response was blocked by a Microsoft Purview data loss prevention policy.");
    }

    [Fact]
    public async Task MatchesTheBlockActionInAnyCase()
    {
        var (client, _) = Create(_ => Graph("""{"policyActions":[{"action":"restrictAccess","restrictionAction":"warn"},{"action":"restrictAccess","restrictionAction":"BLOCK"}]}"""));

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens);

        result!.Allowed.Should().BeFalse();
        result.Decision.Should().BeEquivalentTo(new { BlockAction = true, RestrictionAction = "BLOCK", ActionCount = 2 });
    }

    [Fact]
    public async Task AllowsActionsThatDoNotBlockAndCountsThem()
    {
        var (client, _) = Create(_ => Graph("""
            {"policyActions":[{"action":"restrictAccess","restrictionAction":"audit"},{"action":"restrictAccess","restrictionAction":"warn"},{"action":"notifyUser"}],"processingErrors":[]}
            """));

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens);

        result!.Allowed.Should().BeTrue();
        result.Evaluated.Should().BeTrue();
        result.Decision.Should().BeEquivalentTo(new { BlockAction = false, RestrictionAction = (string?)null, ActionCount = 3 });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TreatsProcessingErrorsAsNoVerdict(bool failClosed)
    {
        var (client, _) = Create(_ => Graph(ProcessingError), new PurviewDlpOptions { FailClosed = failClosed });

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens);

        result!.Evaluated.Should().BeFalse("a permanent BadRequest is reported with HTTP 200 and no policy actions");
        result.Allowed.Should().Be(!failClosed);
        result.HttpStatus.Should().Be(200);
        result.ProtectionScopeState.Should().Be("notModified");
        result.Error.Should().Be("processing errors: 1 (BadRequest, permanent)", "only identifier-like codes are kept, never the message");
        result.BlockReason.Should().Be(failClosed ? "Data loss prevention validation is unavailable and this agent is configured to fail closed." : null);
    }

    [Fact]
    public async Task KeepsABlockReportedWithProcessingErrors()
    {
        var (client, _) = Create(_ => Graph("""
            {"policyActions":[{"restrictionAction":"block"}],"processingErrors":[{"code":"PartialContent","message":"Some content was skipped."}]}
            """));

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens);

        result!.Evaluated.Should().BeTrue();
        result.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task ReportsProcessingErrorsWithoutTheirText()
    {
        var (client, _) = Create(_ => Graph("""
            {"policyActions":[],"processingErrors":[{"code":"has spaces and secrets","errorType":"transient"},"unexpected",{"code":"BadRequest","code":"Repeated"}]}
            """));

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens);

        result!.Evaluated.Should().BeFalse();
        result.Error.Should().Be("processing errors: 3 (transient)", "an error whose properties repeat still counts, unread");
    }

    [Theory]
    [InlineData("""{"policyActions":[{"restrictionAction":"block"},"unexpected"]}""")]
    [InlineData("""{"policyActions":["unexpected",{"restrictionAction":"Block"}]}""")]
    [InlineData("""{"policyActions":[{"restrictionAction":{"value":"audit"}},{"restrictionAction":"block"}]}""")]
    [InlineData("""{"policyActions":[{"action":"a","action":"b"},{"restrictionAction":"block"}]}""")]
    public async Task KeepsABlockWhateverShapeTheOtherActionsHave(string body)
    {
        var (client, _) = Create(_ => Graph(body));

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens);

        result!.Evaluated.Should().BeTrue();
        result.Allowed.Should().BeFalse("an action that blocks is never outweighed by one Purview did not send in its usual shape");
        result.Decision.BlockAction.Should().BeTrue();
        result.Decision.ActionCount.Should().Be(2);
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task TreatsAnAcceptedRequestWithoutAVerdictAsAnAllow(HttpStatusCode status)
    {
        var (client, _) = Create(_ => new HttpResponseMessage(status));

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens);

        result!.Evaluated.Should().BeTrue();
        result.Allowed.Should().BeTrue();
        result.HttpStatus.Should().Be((int)status);
        result.Decision.ActionCount.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FollowsTheFailModeOnAnHttpErrorWithoutReadingItsBody(bool failClosed)
    {
        var (client, _) = Create(
            _ => Graph("""{"error":{"code":"Forbidden","message":"Echo: card 4111 and token graph-access-token"}}""", HttpStatusCode.Forbidden),
            new PurviewDlpOptions { FailClosed = failClosed });

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens);

        result!.Evaluated.Should().BeFalse();
        result.Allowed.Should().Be(!failClosed);
        result.HttpStatus.Should().Be(403);
        result.Error.Should().Be("http 403");
    }

    [Theory]
    [InlineData("[]", "response contained no verdict")]
    [InlineData("\"allow\"", "response contained no verdict")]
    [InlineData("{}", "response contained no verdict")]
    [InlineData("""{"value":[]}""", "response contained no verdict")]
    [InlineData("""{"error":{"code":"InternalServerError"}}""", "response contained no verdict")]
    [InlineData("""{"policyActions":null,"processingErrors":[]}""", "response contained no verdict")]
    [InlineData("""{"policyActions":{"restrictionAction":"block"}}""", "response contained unexpected policyActions")]
    [InlineData("""{"policyActions":["block"]}""", "response contained unexpected policyActions")]
    [InlineData("""{"policyActions":[{"restrictionAction":{"value":"block"}}]}""", "response contained unexpected policyActions")]
    [InlineData("""{"policyActions":[{"restrictionAction":"audit","restrictionAction":"block"}]}""", "response contained unexpected policyActions")]
    [InlineData("""{"policyActions":[],"processingErrors":{"code":"BadRequest"}}""", "response contained unexpected processingErrors")]
    [InlineData("""{"policyActions":[{"restrictionAction":"block"}],"policyActions":[]}""", "response contained duplicate properties")]
    [InlineData("not json", "non-JSON response")]
    [InlineData("", "non-JSON response")]
    public async Task TreatsAResponseOfAnotherShapeAsNoVerdict(string body, string error)
    {
        var (client, _) = Create(_ => Graph(body), new PurviewDlpOptions { FailClosed = true });

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens);

        result!.Evaluated.Should().BeFalse();
        result.Allowed.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    [Fact]
    public async Task ReadsAResponseWithoutOptionalMembers()
    {
        var (client, _) = Create(_ => Graph("""{"policyActions":[],"protectionScopeState":7}"""));

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens);

        result!.Evaluated.Should().BeTrue();
        result.Allowed.Should().BeTrue();
        result.ProtectionScopeState.Should().BeNull();
    }

    // ─── truncation ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DoesNotLetAnAllowOfATruncatedCopyAuthorizeTheText(bool failClosed)
    {
        var (client, handler) = Create(DeniesThePayload, new PurviewDlpOptions { MaxContentCharacters = 100, FailClosed = failClosed });

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, new string('a', 100) + Payload, Agent(), Tokens);

        var call = handler.Calls.Single();
        call.Text.Should().Be(new string('a', 100));
        call.Entry["isTruncated"]!.GetValue<bool>().Should().BeTrue();
        result!.Evaluated.Should().BeTrue();
        result.Truncated.Should().BeTrue();
        result.Allowed.Should().Be(!failClosed);
        result.Error.Should().Be(PurviewDlpClient.TruncatedContentError);
        result.BlockReason.Should().Be(failClosed ? "Microsoft Purview could not evaluate all of the content and this agent is configured to fail closed." : null);
    }

    [Fact]
    public async Task KeepsABlockOfATruncatedCopy()
    {
        var (client, _) = Create(DeniesThePayload, new PurviewDlpOptions { MaxContentCharacters = 100 });

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, Payload + new string('a', 200), Agent(), Tokens);

        result!.Truncated.Should().BeTrue();
        result.Allowed.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task AllowsTextWithinTheLimitNormally()
    {
        var (client, handler) = Create(DeniesThePayload, new PurviewDlpOptions { MaxContentCharacters = 100, FailClosed = true });

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, new string('a', 100), Agent(), Tokens);

        handler.Calls.Single().Entry["isTruncated"]!.GetValue<bool>().Should().BeFalse();
        result!.Truncated.Should().BeFalse();
        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task DoesNotSplitASurrogatePairWhenTruncating()
    {
        var (client, handler) = Create(_ => Graph(Clean), new PurviewDlpOptions { MaxContentCharacters = 4 });

        await client.EvaluateAsync(PurviewDlpActivity.UploadText, "abc\uD83D\uDE00def", Agent(), Tokens);

        handler.Calls.Single().Text.Should().Be("abc");
    }

    // ─── transport and deadline ──────────────────────────────────────────────

    [Fact]
    public async Task ReportsATimeout()
    {
        var handler = new GraphHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return Graph(Clean);
        });
        var client = new PurviewDlpClient(Options(new PurviewDlpOptions { Timeout = TimeSpan.FromMilliseconds(100) }), new HttpClient(handler));

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens);

        result!.Evaluated.Should().BeFalse();
        result.Allowed.Should().BeTrue();
        result.Error.Should().Be("request timeout");
    }

    [Fact]
    public async Task SharesOneDeadlineBetweenTokenAcquisitionAndTheRequest()
    {
        var requestWait = TimeSpan.Zero;
        var handler = new GraphHandler(async (_, cancellationToken) =>
        {
            var waiting = Stopwatch.StartNew();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            finally
            {
                requestWait = waiting.Elapsed;
            }

            return Graph(Clean);
        });
        var client = new PurviewDlpClient(Options(new PurviewDlpOptions { Timeout = TimeSpan.FromSeconds(1) }), new HttpClient(handler));
        PurviewDlpTokenResolver slow = async (_, _, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken);
            return new PurviewDlpToken(AccessToken);
        };

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), slow);

        result!.Evaluated.Should().BeFalse();
        result.Error.Should().BeOneOf("request timeout", "entra token timeout");
        requestWait.Should().BeLessThan(TimeSpan.FromMilliseconds(700), "the request only gets what is left of the one deadline");
    }

    [Fact]
    public async Task ReleasesAResolverThatIgnoresTheDeadline()
    {
        var (client, handler) = Create(_ => Graph(Clean), new PurviewDlpOptions { Timeout = TimeSpan.FromMilliseconds(200) });
        var never = new TaskCompletionSource<PurviewDlpToken?>();
        var watch = Stopwatch.StartNew();

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), (_, _, _) => never.Task);

        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        result!.Evaluated.Should().BeFalse();
        result.Error.Should().Be("entra token timeout");
        handler.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task BoundsAResolverThatBlocksBeforeReturningItsTaskAndSendsNothingAfterTheDeadline()
    {
        // A handler that ignores cancellation, so only the client can keep a late token from being used.
        var handler = new GraphHandler(async (_, _) =>
        {
            await Task.Yield();
            return Graph(Clean);
        });
        var client = new PurviewDlpClient(Options(new PurviewDlpOptions { Timeout = TimeSpan.FromMilliseconds(300) }), new HttpClient(handler));
        var watch = Stopwatch.StartNew();

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), (_, _, _) =>
        {
            Thread.Sleep(1500);
            return Task.FromResult<PurviewDlpToken?>(new PurviewDlpToken(AccessToken));
        });
        watch.Stop();
        await Task.Delay(1500);

        watch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(1200), "the deadline bounds the resolver's synchronous work too");
        result!.Evaluated.Should().BeFalse();
        result.Error.Should().Be("entra token timeout");
        handler.Calls.Should().BeEmpty("a token that arrives after the deadline is never used");
    }

    [Fact]
    public async Task TreatsAResolverThatReturnsNoTaskAsNoToken()
    {
        var (client, handler) = Create(_ => Graph(Clean));

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), (_, _, _) => null!);

        result!.Evaluated.Should().BeFalse();
        result.Error.Should().Be("entra token unavailable (InvalidOperationException)");
        handler.Calls.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FollowsTheFailModeOnAnyTransportException(bool failClosed)
    {
        var (client, _) = Create(_ => throw new CircuitOpenException(), new PurviewDlpOptions { FailClosed = failClosed });

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens);

        result!.Evaluated.Should().BeFalse();
        result.Allowed.Should().Be(!failClosed);
        result.Error.Should().Be("request failed (CircuitOpenException)");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TreatsARedirectedRequestAsNoVerdict(bool failClosed)
    {
        var (client, _) = Create(
            request =>
            {
                // What a client that follows redirects does: the request ends at another host, which answers.
                request.RequestUri = new Uri("https://elsewhere.example.test/v1.0/me/dataSecurityAndGovernance/processContent");
                return Graph(Clean);
            },
            new PurviewDlpOptions { FailClosed = failClosed });

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens);

        result!.Evaluated.Should().BeFalse();
        result.Allowed.Should().Be(!failClosed);
        result.Error.Should().Be("request was redirected");
    }

    [Fact]
    public async Task TreatsARedirectResponseAsNoVerdict()
    {
        var (client, _) = Create(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
            response.Headers.Location = new Uri("https://elsewhere.example.test/");
            return response;
        });

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens);

        result!.Evaluated.Should().BeFalse();
        result.Error.Should().Be("http 307");
    }

    [Fact]
    public async Task DoesNotSendToABaseUrlChangedToHttpAfterConstruction()
    {
        var (client, handler) = Create(_ => Graph(Clean));
        client.Options.GraphBaseUrl = new Uri("http://graph.example.test/v1.0");

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens);

        result!.Evaluated.Should().BeFalse();
        result.Error.Should().Be("graph base URL is not an absolute https URL");
        handler.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task LetsTheCallersCancellationThrough()
    {
        var (client, _) = Create(_ => Graph(Clean));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var act = () => client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), Tokens, cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ─── authentication ──────────────────────────────────────────────────────

    [Fact]
    public async Task AsksTheResolverForTheConfiguredScopeOnEveryEvaluation()
    {
        var (client, handler) = Create(_ => Graph(Clean), new PurviewDlpOptions { AuthenticationScope = "https://graph.example.test/.default" });
        var requests = new List<string>();
        PurviewDlpTokenResolver resolver = (agent, scope, _) =>
        {
            requests.Add($"{agent.AgenticUserId}|{scope}");
            return Task.FromResult<PurviewDlpToken?>(new PurviewDlpToken(AccessToken));
        };

        await client.EvaluateAsync(PurviewDlpActivity.UploadText, "one", Agent(), resolver);
        await client.EvaluateAsync(PurviewDlpActivity.DownloadText, "two", Agent(), resolver);

        requests.Should().Equal(Enumerable.Repeat($"{AgenticUserId}|https://graph.example.test/.default", 2), "the resolver owns caching");
        handler.Calls.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FollowsTheFailModeWhenNoTokenIsResolved(bool failClosed)
    {
        var (client, handler) = Create(_ => Graph(Clean), new PurviewDlpOptions { FailClosed = failClosed });

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), (_, _, _) => Task.FromResult<PurviewDlpToken?>(null));

        result!.Evaluated.Should().BeFalse();
        result.Allowed.Should().Be(!failClosed);
        result.Error.Should().Be("entra token unavailable");
        handler.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordsOnlyTheTypeWhenTheResolverFails()
    {
        const string Secret = "client_secret=do-not-leak";
        var (client, handler) = Create(_ => Graph(Clean));

        var result = await client.EvaluateAsync(PurviewDlpActivity.UploadText, "hello", Agent(), (_, _, _) => throw new InvalidOperationException(Secret));

        result!.Evaluated.Should().BeFalse();
        result.Error.Should().Be("entra token unavailable (InvalidOperationException)");
        handler.Calls.Should().BeEmpty();
    }

    // ─── options ─────────────────────────────────────────────────────────────

    [Fact]
    public void ReadsOptionsFromTheEnvironment()
    {
        var variables = new Dictionary<string, string>
        {
            ["ENABLE_A365_PURVIEW_DLP"] = " TRUE ",
            ["A365_PURVIEW_DLP_GRAPH_BASE_URL"] = "https://graph.example.test/beta",
            ["A365_PURVIEW_DLP_AUTHENTICATION_SCOPE"] = "https://graph.example.test/.default",
            ["A365_PURVIEW_DLP_FAIL_MODE"] = "Closed",
            ["A365_PURVIEW_DLP_TIMEOUT_MILLISECONDS"] = "2500",
            ["A365_PURVIEW_DLP_MAX_CONTENT_CHARACTERS"] = "5000",
            ["A365_PURVIEW_DLP_RESPONSE_MODE"] = "ENFORCE",
        };

        var options = PurviewDlpOptions.FromEnvironment(name => variables.TryGetValue(name, out var value) ? value : null);

        options.Should().BeEquivalentTo(new
        {
            Enabled = true,
            GraphBaseUrl = new Uri("https://graph.example.test/beta"),
            AuthenticationScope = "https://graph.example.test/.default",
            FailClosed = true,
            Timeout = TimeSpan.FromMilliseconds(2500),
            MaxContentCharacters = 5000,
            ResponseMode = PurviewDlpResponseMode.Enforce,
        });
    }

    [Fact]
    public void DefaultsToDisabledAuditAndFailOpen()
    {
        var options = PurviewDlpOptions.FromEnvironment(_ => null);

        options.Should().BeEquivalentTo(new
        {
            Enabled = false,
            GraphBaseUrl = new Uri("https://graph.microsoft.com/v1.0"),
            AuthenticationScope = "https://graph.microsoft.com/.default",
            FailClosed = false,
            Timeout = TimeSpan.FromSeconds(10),
            MaxContentCharacters = 100000,
            ResponseMode = PurviewDlpResponseMode.Audit,
        });
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("Yes", true)]
    [InlineData("on", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    [InlineData("OFF", false)]
    [InlineData("", false)]
    public void ReadsWhetherPurviewIsEnabled(string value, bool enabled)
    {
        PurviewDlpOptions.FromEnvironment(name => name == "ENABLE_A365_PURVIEW_DLP" ? value : null).Enabled.Should().Be(enabled);
    }

    [Theory]
    [InlineData("ENABLE_A365_PURVIEW_DLP", "enabled")]
    [InlineData("A365_PURVIEW_DLP_FAIL_MODE", "close")]
    [InlineData("A365_PURVIEW_DLP_RESPONSE_MODE", "block")]
    [InlineData("A365_PURVIEW_DLP_TIMEOUT_MILLISECONDS", "0")]
    [InlineData("A365_PURVIEW_DLP_TIMEOUT_MILLISECONDS", "1.5")]
    [InlineData("A365_PURVIEW_DLP_MAX_CONTENT_CHARACTERS", "-1")]
    [InlineData("A365_PURVIEW_DLP_GRAPH_BASE_URL", "http://graph.example.test/v1.0")]
    [InlineData("A365_PURVIEW_DLP_GRAPH_BASE_URL", "graph.example.test/v1.0")]
    [InlineData("A365_PURVIEW_DLP_GRAPH_BASE_URL", "https://graph.example.test/v1.0?api=1")]
    [InlineData("A365_PURVIEW_DLP_GRAPH_BASE_URL", "https://graph.example.test/v1.0#me")]
    public void RejectsAValueAVariableDoesNotAcceptRatherThanFallingBack(string name, string value)
    {
        var read = () => PurviewDlpOptions.FromEnvironment(variable => variable == name ? value : null);

        read.Should().Throw<InvalidOperationException>().WithMessage($"*{name}*");
    }

    [Fact]
    public void RejectsOptionsAClientCannotUse()
    {
        var http = () => new PurviewDlpClient(new PurviewDlpOptions { GraphBaseUrl = new Uri("http://graph.example.test/v1.0") });
        var timeout = () => new PurviewDlpClient(new PurviewDlpOptions { Timeout = TimeSpan.Zero });
        var maximum = () => new PurviewDlpClient(new PurviewDlpOptions { MaxContentCharacters = 0 });
        var scope = () => new PurviewDlpClient(new PurviewDlpOptions { AuthenticationScope = " " });
        var mode = () => new PurviewDlpClient(new PurviewDlpOptions { ResponseMode = (PurviewDlpResponseMode)7 });

        foreach (var create in new[] { http, timeout, maximum, scope, mode })
        {
            create.Should().Throw<InvalidOperationException>();
        }
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    internal static PurviewDlpAgentContext Agent(long? sequence = 7) => new()
    {
        AgentId = AgentId,
        TenantId = TenantId,
        AgenticUserId = AgenticUserId,
        BlueprintId = BlueprintId,
        AgentName = "SampleAgent",
        SessionId = "conversation-1",
        SequenceNumber = sequence,
    };

    private static PurviewDlpOptions Options(PurviewDlpOptions? overrides = null)
    {
        var options = overrides ?? new PurviewDlpOptions();
        options.Enabled = true;

        // Tests that do not choose a Graph base use the fake one.
        if (options.GraphBaseUrl == new Uri(PurviewDlpOptions.DefaultGraphBaseUrl))
        {
            options.GraphBaseUrl = new Uri(GraphBase);
        }

        return options;
    }

    /// <summary>A fake Graph that blocks only text containing <see cref="Payload"/>.</summary>
    private static HttpResponseMessage DeniesThePayload(HttpRequestMessage request)
    {
        var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        return Graph(body.Contains(Payload, StringComparison.Ordinal) ? Blocked : Clean);
    }

    private static (PurviewDlpClient Client, GraphHandler Handler) Create(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        PurviewDlpOptions? options = null)
    {
        var handler = new GraphHandler(respond);
        return (new PurviewDlpClient(Options(options), new HttpClient(handler)), handler);
    }

    internal static HttpResponseMessage Graph(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
}

/// <summary>Captures each Graph request (headers and body are read before the client disposes the request).</summary>
internal sealed class GraphHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

    public GraphHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : this((request, _) => Task.FromResult(respond(request)))
    {
    }

    public GraphHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    public List<GraphCall> Calls { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content == null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
        var call = new GraphCall(
            request.Method,
            request.RequestUri!,
            request.Headers.Authorization?.ToString(),
            request.Headers.TryGetValues(PurviewDlpClient.ClientRequestIdHeader, out var values) ? values.Single() : null,
            request.Content?.Headers.ContentType?.MediaType,
            JsonNode.Parse(body)!.AsObject());

        // Some tests send concurrently.
        lock (Calls)
        {
            Calls.Add(call);
        }

        return await _respond(request, cancellationToken);
    }
}

internal sealed record GraphCall(HttpMethod Method, Uri Uri, string? Authorization, string? ClientRequestId, string? ContentType, JsonObject Body)
{
    public JsonObject Entry => Body["contentToProcess"]!["contentEntries"]![0]!.AsObject();

    public string Text => Entry["content"]!["data"]!.GetValue<string>();
}
