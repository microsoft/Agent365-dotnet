// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Microsoft.Agents.A365.Sidecar.Tests;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> for sidecar integration tests.
/// Skips Agent SDK registration to avoid MSAL assembly loading in test environments.
/// </summary>
public class SidecarTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("A365_SKIP_AGENT_REGISTRATION", "true");
        Environment.SetEnvironmentVariable("A365_AGENT_ID", "test-agent-id");
        Environment.SetEnvironmentVariable("A365_AUTH__TenantId", "test-tenant-id");
        Environment.SetEnvironmentVariable("A365_AUTH__ClientId", "test-blueprint-id");
        Environment.SetEnvironmentVariable("A365_CUSTOMER_WEBHOOK", "http://localhost:9999/webhook");
        Environment.SetEnvironmentVariable("A365_TOOLING_GATEWAY_ENDPOINT", "http://localhost:9998/tooling");
        builder.UseEnvironment("Development");
        builder.UseSetting("Testing:SkipAgentRegistration", "true");
    }
}
