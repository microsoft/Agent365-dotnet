// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using Microsoft.Agents.A365.Sidecar.Auth;
using Microsoft.Agents.A365.Sidecar.Configuration;
using Microsoft.Agents.A365.Sidecar.Health;
using Microsoft.Agents.A365.Sidecar.Messaging;
using Microsoft.Agents.A365.Sidecar.Notifications;
using Microsoft.Agents.A365.Sidecar.Observability;
using Microsoft.Agents.A365.Sidecar.Tooling;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Load configuration from YAML/JSON/env vars
var configPath = Environment.GetEnvironmentVariable("A365_SIDECAR_CONFIG");
if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
{
    builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: true);
}

// Bind environment variable overrides
builder.Configuration.AddEnvironmentVariables("A365_");

// Register sidecar options
builder.Services.Configure<SidecarOptions>(options =>
{
    builder.Configuration.GetSection(SidecarOptions.SectionName).Bind(options);

    // Environment variable overrides take precedence
    var agentId = Environment.GetEnvironmentVariable("A365_AGENT_ID");
    if (!string.IsNullOrEmpty(agentId)) options.Agent.Id = agentId;

    var authMode = Environment.GetEnvironmentVariable("A365_AUTH_MODE");
    if (!string.IsNullOrEmpty(authMode)) options.Auth.Mode = authMode;

    var authClientId = Environment.GetEnvironmentVariable("A365_AUTH__ClientId");
    if (!string.IsNullOrEmpty(authClientId)) options.Auth.ClientId = authClientId;

    var authClientSecret = Environment.GetEnvironmentVariable("A365_AUTH__ClientSecret");
    if (!string.IsNullOrEmpty(authClientSecret)) options.Auth.ClientSecret = authClientSecret;

    var authTenantId = Environment.GetEnvironmentVariable("A365_AUTH__TenantId");
    if (!string.IsNullOrEmpty(authTenantId)) options.Auth.TenantId = authTenantId;

    var webhook = Environment.GetEnvironmentVariable("A365_CUSTOMER_WEBHOOK");
    if (!string.IsNullOrEmpty(webhook)) options.Messaging.CustomerWebhook = webhook;

    var port = Environment.GetEnvironmentVariable("A365_SIDECAR_PORT");
    if (int.TryParse(port, out var portValue)) options.Server.Port = portValue;

    var bindAddress = Environment.GetEnvironmentVariable("A365_BIND_ADDRESS");
    if (!string.IsNullOrEmpty(bindAddress)) options.Server.BindAddress = bindAddress;

    var streamingTimeout = Environment.GetEnvironmentVariable("A365_STREAMING_TIMEOUT");
    if (int.TryParse(streamingTimeout, out var timeout)) options.Messaging.Streaming.TimeoutSeconds = timeout;

    var gatewayEndpoint = Environment.GetEnvironmentVariable("A365_TOOLING_GATEWAY_ENDPOINT");
    if (!string.IsNullOrEmpty(gatewayEndpoint)) options.Tooling.GatewayEndpoint = gatewayEndpoint;

    var gatewayScope = Environment.GetEnvironmentVariable("A365_TOOLING__GatewayScope");
    if (!string.IsNullOrEmpty(gatewayScope)) options.Tooling.GatewayScope = gatewayScope;
});

// Register HttpClient factories for outbound calls
builder.Services.AddHttpClient("CustomerWebhook");
builder.Services.AddHttpClient("ToolingGateway");
builder.Services.AddHttpClient("ObservabilityExporter");

// Register messaging services
builder.Services.AddSingleton<TurnManager>();
builder.Services.AddSingleton<StreamingHandler>();

// Register observability services
builder.Services.AddSingleton<ExportFormatter>();
builder.Services.AddSingleton<Agent365ExporterCore>();
builder.Services.AddSingleton<OtlpTraceReceiver>();
builder.Services.AddSingleton<SidecarTokenProvider>();
builder.Services.Configure<Agent365ExporterOptions>(opts =>
{
    opts.UseS2SEndpoint = builder.Configuration.GetValue("Observability:UseS2SEndpoint", false);
});
builder.Services.AddSingleton<IConfigureOptions<Agent365ExporterOptions>>(sp =>
{
    var tokenProvider = sp.GetRequiredService<SidecarTokenProvider>();
    return new ConfigureOptions<Agent365ExporterOptions>(opts =>
    {
        opts.TokenResolver = tokenProvider.ResolveObservabilityTokenAsync;
    });
});

// Register tooling services
builder.Services.AddSingleton<IMcpToolServerConfigurationService, McpToolServerConfigurationService>();
builder.Services.AddSingleton<SidecarMcpTokenProvider>();

// Register Agents SDK for Activity Protocol handling
builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

if (!builder.Configuration.GetValue("Testing:SkipAgentRegistration", false)
    && Environment.GetEnvironmentVariable("A365_SKIP_AGENT_REGISTRATION") != "true")
{
    builder.AddAgent<SidecarAgent>();
}

var app = builder.Build();

// Authentication & authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map health endpoints
app.MapHealthEndpoints();

// Map Activity Protocol endpoint (receives from M365 channels)
if (Environment.GetEnvironmentVariable("A365_SKIP_AGENT_REGISTRATION") != "true")
{
    app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());
}

// Map outbound turn API (customer calls these to send replies)
app.MapTurnEndpoints();

// Map Tooling API
app.MapToolingEndpoints();

// Map Notifications API
app.MapNotificationEndpoints();

// Map Observability OTLP receiver
app.MapObservabilityEndpoints();

app.Run();

/// <summary>
/// Partial class marker to enable WebApplicationFactory-based testing.
/// </summary>
public partial class Program { }
