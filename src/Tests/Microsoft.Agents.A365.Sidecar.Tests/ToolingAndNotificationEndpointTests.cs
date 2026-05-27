// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace Microsoft.Agents.A365.Sidecar.Tests;

/// <summary>
/// Integration tests for the Tooling and Notifications API endpoints.
/// </summary>
public class ToolingAndNotificationEndpointTests : IClassFixture<SidecarTestFactory>
{
    private readonly HttpClient _client;

    public ToolingAndNotificationEndpointTests(SidecarTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListTools_ReturnsDiscoveryInfo()
    {
        var response = await _client.GetAsync("/api/v1/tools");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("version").GetString().Should().Be("v2");
        doc.RootElement.GetProperty("endpoints").EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListNotificationChannels_ReturnsKnownChannels()
    {
        var response = await _client.GetAsync("/api/v1/notifications/channels");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var channels = doc.RootElement.EnumerateArray().ToList();

        channels.Should().HaveCountGreaterThanOrEqualTo(4);
        channels.Select(c => c.GetProperty("id").GetString())
            .Should().Contain(new[] { "email", "word", "excel", "powerpoint" });
    }

    [Fact]
    public async Task GetNotificationSchemas_ReturnsPayloadShape()
    {
        var response = await _client.GetAsync("/api/v1/notifications/schemas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("payloadShape").EnumerateObject().Should().NotBeEmpty();
        doc.RootElement.GetProperty("models").EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetNotificationStatus_ReturnsEnabled()
    {
        var response = await _client.GetAsync("/api/v1/notifications/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("enabled").GetBoolean().Should().BeTrue();
    }
}
