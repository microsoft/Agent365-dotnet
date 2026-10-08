// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Web;
using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Protection.Defender;
using Microsoft.Agents.Authentication;
using Moq;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Core.Tests.Protection;

public class DefenderRtpTokenResolversTests
{
    private const string AgentId = "aaaaaaaa-0000-4000-8000-000000000001";
    private const string TenantId = "bbbbbbbb-0000-4000-8000-000000000002";
    private const string Assertion = "agent-identity-assertion";
    private static readonly string[] Scopes = { DefenderRtpOptions.DefaultAuthenticationScope };

    [Fact]
    public async Task ExchangesTheAgentIdentityAssertionForTheDefenderToken()
    {
        var connection = Connection(Assertion);
        var endpoint = new TokenEndpoint(_ => Json("""{"token_type":"Bearer","expires_in":3599,"access_token":"defender-token"}"""));
        using var httpClient = new HttpClient(endpoint);
        var resolver = DefenderRtpTokenResolvers.FromAgenticConnection(connection.Object, httpClient);

        var token = await resolver(AgentId, TenantId, Scopes, CancellationToken.None);

        token.Should().Be("defender-token");
        connection.Verify(provider => provider.GetAgenticApplicationTokenAsync(TenantId, AgentId, It.IsAny<CancellationToken>()), Times.Once);
        var request = endpoint.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.Should().Be(new Uri($"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token"));
        request.Form.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = AgentId,
            ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            ["client_assertion"] = Assertion,
            ["scope"] = "api://86a21212-634e-4553-b3d6-e477e4c9d9ec/.default",
        });
    }

    [Fact]
    public async Task UsesTheGivenAuthority()
    {
        var endpoint = new TokenEndpoint(_ => Json("""{"access_token":"defender-token"}"""));
        using var httpClient = new HttpClient(endpoint);
        var resolver = DefenderRtpTokenResolvers.FromAgenticConnection(Connection(Assertion).Object, httpClient, "https://login.example.test/");

        await resolver(AgentId, TenantId, Scopes, CancellationToken.None);

        endpoint.Requests.Single().Uri.Should().Be(new Uri($"https://login.example.test/{TenantId}/oauth2/v2.0/token"));
    }

    [Theory]
    [InlineData("http://login.example.test")]
    [InlineData("login.example.test")]
    [InlineData("")]
    public void RejectsAnAuthorityThatIsNotAbsoluteHttps(string authority)
    {
        using var httpClient = new HttpClient();
        var act = () => DefenderRtpTokenResolvers.FromAgenticConnection(Connection(Assertion).Object, httpClient, authority);

        act.Should().Throw<ArgumentException>().WithParameterName("authority");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task FailsWithoutAnAssertion(string? assertion)
    {
        var endpoint = new TokenEndpoint(_ => Json("""{"access_token":"defender-token"}"""));
        using var httpClient = new HttpClient(endpoint);
        var resolver = DefenderRtpTokenResolvers.FromAgenticConnection(Connection(assertion).Object, httpClient);

        var act = () => resolver(AgentId, TenantId, Scopes, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no agent identity assertion*");
        endpoint.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task FailsOnAnErrorStatusWithoutEchoingTheResponse()
    {
        var endpoint = new TokenEndpoint(_ => Json(
            $$"""{"error":"invalid_client","error_description":"Assertion {{Assertion}} was rejected."}""",
            HttpStatusCode.Unauthorized));
        using var httpClient = new HttpClient(endpoint);
        var resolver = DefenderRtpTokenResolvers.FromAgenticConnection(Connection(Assertion).Object, httpClient);

        var act = () => resolver(AgentId, TenantId, Scopes, CancellationToken.None);

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        failure.Which.Message.Should().Contain("HTTP 401").And.NotContain(Assertion).And.NotContain("invalid_client");
    }

    [Theory]
    [InlineData("not json", "*not valid JSON*")]
    [InlineData("""{"token_type":"Bearer"}""", "*no access_token*")]
    [InlineData("""{"access_token":42}""", "*no access_token*")]
    [InlineData("[]", "*no access_token*")]
    [InlineData("\"defender-token\"", "*no access_token*")]
    public async Task FailsOnAMalformedSuccessResponse(string body, string message)
    {
        var endpoint = new TokenEndpoint(_ => Json(body));
        using var httpClient = new HttpClient(endpoint);
        var resolver = DefenderRtpTokenResolvers.FromAgenticConnection(Connection(Assertion).Object, httpClient);

        var act = () => resolver(AgentId, TenantId, Scopes, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(message);
    }

    [Fact]
    public async Task StopsWhenCancelled()
    {
        var endpoint = new TokenEndpoint(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Json("""{"access_token":"defender-token"}""");
        });
        using var httpClient = new HttpClient(endpoint);
        var resolver = DefenderRtpTokenResolvers.FromAgenticConnection(Connection(Assertion).Object, httpClient);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var act = () => resolver(AgentId, TenantId, Scopes, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static Mock<IAgenticTokenProvider> Connection(string? assertion)
    {
        var connection = new Mock<IAgenticTokenProvider>();
        connection
            .Setup(provider => provider.GetAgenticApplicationTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(assertion!);
        return connection;
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
}

/// <summary>A fake token endpoint that records each form-encoded request.</summary>
internal sealed class TokenEndpoint : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

    public TokenEndpoint(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : this((request, _) => Task.FromResult(respond(request)))
    {
    }

    public TokenEndpoint(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    public List<TokenRequest> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var form = HttpUtility.ParseQueryString(body);
        Requests.Add(new TokenRequest(
            request.Method,
            request.RequestUri!,
            form.AllKeys.OfType<string>().ToDictionary(key => key, key => form[key] ?? string.Empty)));
        return await _respond(request, cancellationToken);
    }
}

internal sealed record TokenRequest(HttpMethod Method, Uri Uri, Dictionary<string, string> Form);
