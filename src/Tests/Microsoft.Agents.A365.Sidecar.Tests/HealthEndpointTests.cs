// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace Microsoft.Agents.A365.Sidecar.Tests;

/// <summary>
/// Integration tests for the sidecar health and status endpoints.
/// </summary>
public class HealthEndpointTests : IClassFixture<SidecarTestFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(SidecarTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Healthz_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/healthz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString().Should().Be("healthy");
    }

    [Fact]
    public async Task Readyz_ReturnsReady()
    {
        var response = await _client.GetAsync("/readyz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString().Should().Be("ready");
    }

    [Fact]
    public async Task Status_ReturnsRunningWithModules()
    {
        var response = await _client.GetAsync("/api/v1/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("running");
        root.GetProperty("modules").GetProperty("messaging").GetProperty("enabled").GetBoolean().Should().BeTrue();
        root.GetProperty("modules").GetProperty("observability").GetProperty("enabled").GetBoolean().Should().BeTrue();
        root.GetProperty("modules").GetProperty("tooling").GetProperty("enabled").GetBoolean().Should().BeTrue();
        root.GetProperty("modules").GetProperty("notifications").GetProperty("enabled").GetBoolean().Should().BeTrue();
    }
}
