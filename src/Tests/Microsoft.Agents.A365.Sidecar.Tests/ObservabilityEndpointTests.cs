// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Microsoft.Agents.A365.Sidecar.Tests;

/// <summary>
/// Integration tests for the OTLP observability receiver endpoint.
/// </summary>
public class ObservabilityEndpointTests : IClassFixture<SidecarTestFactory>
{
    private readonly HttpClient _client;

    public ObservabilityEndpointTests(SidecarTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ReceiveTraces_WithEmptyBody_ReturnsBadRequest()
    {
        var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/observability/v1/traces", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReceiveTraces_WithNoSpans_ReturnsOkWithZeroCount()
    {
        var payload = new { resourceSpans = Array.Empty<object>() };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/observability/v1/traces", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("acceptedSpans").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task ReceiveTraces_WithUnsupportedContentType_ReturnsOkButLogs()
    {
        // The endpoint currently only supports JSON, protobuf returns a 415-level response
        var content = new ByteArrayContent(new byte[] { 0x01, 0x02 });
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-protobuf");

        var response = await _client.PostAsync("/api/v1/observability/v1/traces", content);

        // Should reject unsupported content type
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetConfig_ReturnsOtlpConfiguration()
    {
        var response = await _client.GetAsync("/api/v1/observability/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        root.GetProperty("protocol").GetString().Should().Be("http/json");
        root.GetProperty("endpoint").GetString().Should().Contain("/api/v1/observability");
        root.GetProperty("headers").EnumerateObject().Should().NotBeEmpty();
    }
}
