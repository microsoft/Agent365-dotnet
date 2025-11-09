# Microsoft.Agents.A365.Observability - Core Package

The core observability package provides fundamental tracing, monitoring, and instrumentation capabilities for AI agent applications.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability
```

## Usage

### Basic Token Cache Configuration

```csharp
using Microsoft.Agents.A365.Observability;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Add agentic token handling for agent-to-agent scenarios
services.AddAgenticTracingExporter(clusterCategory: "production");

// OR add service token handling for service-to-service scenarios
services.AddServiceTracingExporter(clusterCategory: "production");

var serviceProvider = services.BuildServiceProvider();
```

### Agent Tracing Scopes

```csharp
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;

// Execute Agent Scope
var agentDetails = new AgentDetails(agentId: "my-agent");
var tenantDetails = new TenantDetails(tenantId: myTenantGuid);

using var agentScope = ExecuteAgentScope.Start(agentDetails, tenantDetails);
// Your agent logic here
agentScope.Complete();

// Execute Tool Scope  
var toolDetails = new ToolCallDetails(
    functionName: "GetWeather",
    functionArguments: "{\"location\":\"Seattle\"}",
    toolCallId: "call_123",
    modelId: "gpt-4",
    toolType: "function"
);

using var toolScope = ExecuteToolScope.Start(toolDetails, agentDetails, tenantDetails);
// Your tool execution logic
toolScope.Complete();
```

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../LICENSE.md) file for details.

