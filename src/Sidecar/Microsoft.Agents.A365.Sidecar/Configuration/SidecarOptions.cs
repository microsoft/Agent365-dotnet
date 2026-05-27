// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Sidecar.Configuration;

/// <summary>
/// Root configuration options for the Agent365 Sidecar.
/// </summary>
public sealed class SidecarOptions
{
    /// <summary>
    /// Configuration section name in appsettings / YAML.
    /// </summary>
    public const string SectionName = "A365Sidecar";

    /// <summary>
    /// Agent identity configuration.
    /// </summary>
    public AgentOptions Agent { get; set; } = new();

    /// <summary>
    /// Tenant configuration.
    /// </summary>
    public TenantOptions Tenant { get; set; } = new();

    /// <summary>
    /// Authentication configuration.
    /// </summary>
    public AuthOptions Auth { get; set; } = new();

    /// <summary>
    /// Activity Protocol messaging configuration.
    /// </summary>
    public MessagingOptions Messaging { get; set; } = new();

    /// <summary>
    /// Observability (OTLP receiver + A365 exporter) configuration.
    /// </summary>
    public ObservabilityOptions Observability { get; set; } = new();

    /// <summary>
    /// MCP Tooling configuration.
    /// </summary>
    public ToolingOptions Tooling { get; set; } = new();

    /// <summary>
    /// Notification relay configuration.
    /// </summary>
    public NotificationsOptions Notifications { get; set; } = new();

    /// <summary>
    /// Server binding configuration.
    /// </summary>
    public ServerOptions Server { get; set; } = new();
}

/// <summary>
/// Agent identity options.
/// </summary>
public sealed class AgentOptions
{
    /// <summary>
    /// The agent instance ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The agent display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Tenant configuration options.
/// </summary>
public sealed class TenantOptions
{
    /// <summary>
    /// Where to extract tenant ID from: "header", "config", or "token".
    /// </summary>
    public string ExtractFrom { get; set; } = "config";
}

/// <summary>
/// Authentication configuration options.
/// </summary>
public sealed class AuthOptions
{
    /// <summary>
    /// Auth mode: "managed-identity", "client-credentials", or "fmi".
    /// </summary>
    public string Mode { get; set; } = "client-credentials";

    /// <summary>
    /// Entra app client ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Entra app client secret.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Entra tenant ID.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;
}

/// <summary>
/// Activity Protocol messaging options.
/// </summary>
public sealed class MessagingOptions
{
    /// <summary>
    /// Whether messaging (Activity Protocol proxy) is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The customer's webhook URL where activities are delivered.
    /// </summary>
    public string CustomerWebhook { get; set; } = "http://localhost:8080/agent/turn";

    /// <summary>
    /// Streaming configuration.
    /// </summary>
    public StreamingOptions Streaming { get; set; } = new();
}

/// <summary>
/// Streaming response options.
/// </summary>
public sealed class StreamingOptions
{
    /// <summary>
    /// Whether streaming (SSE) responses are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Silence timeout in seconds. If no SSE event is received within this period, the turn fails.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Observability pipeline options.
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>
    /// Whether the OTLP receiver and A365 exporter are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// OTLP gRPC receiver port.
    /// </summary>
    public int OtlpGrpcPort { get; set; } = 4317;

    /// <summary>
    /// OTLP HTTP receiver port.
    /// </summary>
    public int OtlpHttpPort { get; set; } = 4318;

    /// <summary>
    /// Agent365 exporter domain.
    /// </summary>
    public string ExporterDomain { get; set; } = "agent365.svc.cloud.microsoft";
}

/// <summary>
/// MCP Tooling options.
/// </summary>
public sealed class ToolingOptions
{
    /// <summary>
    /// Whether the Tooling API is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Tooling Gateway endpoint URL.
    /// </summary>
    public string GatewayEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// OAuth scope for the Tooling Gateway (used to acquire the gateway auth token).
    /// </summary>
    public string GatewayScope { get; set; } = string.Empty;

    /// <summary>
    /// Auth token for accessing MCP tool servers (if pre-configured, bypasses token acquisition).
    /// </summary>
    public string? AuthToken { get; set; }
}

/// <summary>
/// Notification relay options.
/// </summary>
public sealed class NotificationsOptions
{
    /// <summary>
    /// Whether notification relay is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Server binding options.
/// </summary>
public sealed class ServerOptions
{
    /// <summary>
    /// REST API port.
    /// </summary>
    public int Port { get; set; } = 5365;

    /// <summary>
    /// Bind address (default localhost-only for security).
    /// </summary>
    public string BindAddress { get; set; } = "127.0.0.1";
}
