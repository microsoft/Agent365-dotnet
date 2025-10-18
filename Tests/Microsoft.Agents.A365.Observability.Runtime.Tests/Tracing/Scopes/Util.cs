namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;

public static class Util
{
    public static AgentDetails GetAgentDetails() =>
        new AgentDetails("agentId", "Test Agent", "A test agent for unit testing.");

    public static TenantDetails GetTenantDetails() =>
        new TenantDetails(new Guid());
}
