// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Contracts.Details;

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;


public static class Util
{
    public static AgentDetails GetAgentDetails() =>
        new AgentDetails("agentId", "Test Agent", "A test agent for unit testing.");

    public static TenantDetails GetTenantDetails() =>
        new TenantDetails(new Guid());
}
