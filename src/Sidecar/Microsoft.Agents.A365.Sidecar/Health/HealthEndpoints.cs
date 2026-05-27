// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Sidecar.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Agents.A365.Sidecar.Health;

/// <summary>
/// Extension methods for mapping health check endpoints.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Maps health check endpoints: /healthz, /readyz, and /api/v1/status.
    /// </summary>
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }))
            .WithName("Liveness")
            .WithTags("Health");

        app.MapGet("/readyz", (IOptions<SidecarOptions> options) =>
        {
            // Basic readiness: check that configuration is valid
            var config = options.Value;
            var issues = new List<string>();

            if (string.IsNullOrEmpty(config.Agent.Id))
            {
                issues.Add("Agent ID not configured");
            }

            if (config.Messaging.Enabled && string.IsNullOrEmpty(config.Messaging.CustomerWebhook))
            {
                issues.Add("Messaging enabled but customer webhook not configured");
            }

            if (config.Tooling.Enabled && string.IsNullOrEmpty(config.Tooling.GatewayEndpoint))
            {
                issues.Add("Tooling enabled but gateway endpoint not configured");
            }

            if (issues.Count > 0)
            {
                return Results.Json(new { status = "not_ready", issues }, statusCode: 503);
            }

            return Results.Ok(new { status = "ready" });
        })
        .WithName("Readiness")
        .WithTags("Health");

        app.MapGet("/api/v1/status", (IOptions<SidecarOptions> options) =>
        {
            var config = options.Value;
            return Results.Ok(new
            {
                status = "running",
                agent = new
                {
                    id = config.Agent.Id,
                    name = config.Agent.Name,
                    blueprintId = config.Auth.ClientId,
                },
                modules = new
                {
                    messaging = new { enabled = config.Messaging.Enabled },
                    observability = new { enabled = config.Observability.Enabled },
                    tooling = new { enabled = config.Tooling.Enabled },
                    notifications = new { enabled = config.Notifications.Enabled },
                },
                server = new
                {
                    port = config.Server.Port,
                    bindAddress = config.Server.BindAddress,
                },
            });
        })
        .WithName("Status")
        .WithTags("Health");

        return app;
    }
}
