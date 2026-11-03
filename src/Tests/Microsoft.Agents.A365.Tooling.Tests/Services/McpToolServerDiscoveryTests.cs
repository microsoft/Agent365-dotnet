// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using FluentAssertions;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Microsoft.Agents.A365.Tooling.Tests.Services;

[CollectionDefinition("Tool discovery", DisableParallelization = true)]
public class ToolDiscoveryCollection
{
}

[Collection("Tool discovery")]
public class McpToolServerDiscoveryTests : IDisposable
{
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();
    private readonly string _manifestDirectory = Path.Combine(Path.GetTempPath(), Path.GetFileName(Guid.NewGuid().ToString()));
    private readonly Mock<ILogger<IMcpToolServerConfigurationService>> _logger = new();
    private readonly Mock<HttpMessageHandler> _handler = new();
    private readonly HttpClient _httpClient;

    public McpToolServerDiscoveryTests()
    {
        Directory.CreateDirectory(_manifestDirectory);
        Directory.SetCurrentDirectory(_manifestDirectory);
        _httpClient = new HttpClient(_handler.Object);
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"mcpServerName":"gateway-server","url":"https://gateway.example.com/mcp"}]""")
            });
    }

    [Theory]
    [InlineData("ASPNETCORE_ENVIRONMENT", "Development", null)]
    [InlineData("DOTNET_ENVIRONMENT", "Development", "false")]
    [InlineData("ASPNETCORE_ENVIRONMENT", "Production", null)]
    [InlineData("DOTNET_ENVIRONMENT", "Staging", "invalid")]
    [InlineData("ASPNETCORE_ENVIRONMENT", null, null)]
    public async Task ListToolServersAsync_ManifestExists_LoadsManifestAndLogsWarning(
        string environmentKey, string? environment, string? useToolingService)
    {
        WriteManifest();
        var service = CreateService(environmentKey, environment, useToolingService);

        var servers = await service.ListToolServersAsync("agent-id", "test-token");

        servers.Should().ContainSingle().Which.mcpServerName.Should().Be("manifest-server");
        VerifyGatewayCall(Times.Never());
        VerifyManifestWarning(Times.Once());
    }

    [Theory]
    [InlineData("ASPNETCORE_ENVIRONMENT", "Development", null)]
    [InlineData("DOTNET_ENVIRONMENT", "Development", "false")]
    [InlineData("ASPNETCORE_ENVIRONMENT", "Production", null)]
    [InlineData("ASPNETCORE_ENVIRONMENT", null, null)]
    public async Task ListToolServersAsync_ManifestMissing_UsesToolingService(
        string environmentKey, string? environment, string? useToolingService)
    {
        var service = CreateService(environmentKey, environment, useToolingService);

        var servers = await service.ListToolServersAsync("agent-id", "test-token", new ToolOptions());

        servers.Should().ContainSingle().Which.mcpServerName.Should().Be("gateway-server");
        VerifyGatewayCall(Times.Once());
        VerifyManifestWarning(Times.Never());
        _httpClient.DefaultRequestHeaders.Authorization!.Scheme.Should().Be("Bearer");
        _httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be("test-token");
    }

    [Theory]
    [InlineData("ASPNETCORE_ENVIRONMENT", "Development", "true")]
    [InlineData("DOTNET_ENVIRONMENT", "Development", "TRUE")]
    [InlineData("ASPNETCORE_ENVIRONMENT", "Production", "true")]
    public async Task ListToolServersAsync_UseToolingService_BypassesExistingManifest(
        string environmentKey, string environment, string useToolingService)
    {
        File.WriteAllText(Path.Join(_manifestDirectory, "ToolingManifest.json"), "invalid json");
        var service = CreateService(environmentKey, environment, useToolingService);

        var servers = await service.ListToolServersAsync("agent-id", "test-token");

        servers.Should().ContainSingle().Which.mcpServerName.Should().Be("gateway-server");
        VerifyGatewayCall(Times.Once());
        VerifyManifestWarning(Times.Never());
    }

    [Fact]
    public async Task ListToolServersAsync_EmptyManifest_DoesNotFallBackToToolingService()
    {
        File.WriteAllText(Path.Join(_manifestDirectory, "ToolingManifest.json"), """{"mcpServers":[]}""");
        var service = CreateService("ASPNETCORE_ENVIRONMENT", "Development", null);

        var servers = await service.ListToolServersAsync("agent-id", "test-token");

        servers.Should().BeEmpty();
        VerifyGatewayCall(Times.Never());
        VerifyManifestWarning(Times.Once());
    }

    [Fact]
    public async Task ListToolServersAsync_InvalidManifest_ThrowsWithoutCallingToolingService()
    {
        File.WriteAllText(Path.Combine(_manifestDirectory, "ToolingManifest.json"), "invalid json");
        var service = CreateService("ASPNETCORE_ENVIRONMENT", "Development", null);

        Func<Task> act = () => service.ListToolServersAsync("agent-id", "test-token");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to read MCP servers from ToolingManifest.json:*");
        VerifyGatewayCall(Times.Never());
        VerifyManifestWarning(Times.Once());
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_manifestDirectory, recursive: true);
    }

    private void WriteManifest() =>
        File.WriteAllText(Path.Combine(_manifestDirectory, "ToolingManifest.json"),
            """{"mcpServers":[{"mcpServerName":"manifest-server","url":"https://manifest.example.com/mcp"}]}""");

    private McpToolServerConfigurationService CreateService(
        string environmentKey, string? environment, string? useToolingService)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [environmentKey] = environment,
            ["A365_USE_TOOLING_SERVICE"] = useToolingService,
            ["MCP_PLATFORM_ENDPOINT"] = "https://gateway.example.com"
        }).Build();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        return new McpToolServerConfigurationService(
            _logger.Object, configuration, Mock.Of<IServiceProvider>(), factory.Object);
    }

    private void VerifyGatewayCall(Times times) =>
        _handler.Protected().Verify("SendAsync", times,
            ItExpr.Is<HttpRequestMessage>(request =>
                request.Method == HttpMethod.Get &&
                request.RequestUri!.AbsoluteUri == "https://gateway.example.com/agents/v2/agent-id/mcpServers"),
            ItExpr.IsAny<CancellationToken>());

    private void VerifyManifestWarning(Times times) =>
        _logger.Verify(logger => logger.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("ToolingManifest.json")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);
}
