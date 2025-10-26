# Microsoft Agents A365 Observability SDK for .NET

## 🚀 Features

- **🔍 Agent Monitoring**: Specialized tracing for AI agent invocations with detailed telemetry
- **🛠️ Tool Execution Tracking**: Monitor tool executions and function calls with comprehensive metrics
- **📊 OpenTelemetry Integration**: Built-in OpenTelemetry tracing for standardized observability
- **☁️ Azure Monitor Support**: Seamless integration with Azure Monitor for cloud-based monitoring

### 🚀 Quick Start

#### .NET Quick Start

1. **Install the package**:
   ```bash
   dotnet add package Microsoft.Agents.A365
   ```

2. **Configure in your application**:
   ```csharp
   using Microsoft.Agents.A365;
   
   var builder = WebApplication.CreateBuilder(args);
   
   // Configure Microsoft Agents A365 with Azure Monitor
   builder.Services.AddTracing();
   
   ...
   ```

3. **Add agent tracing**:
   ```csharp
   using Microsoft.Agents.A365.Tracing;
   
   using var agentScope = ExecuteAgentScope.Start(AgentId);
   // Your agent logic here
   ```

### 🛠️ Sample Applications

#### .NET Samples
- **Basic Sample**: [`/dotnet/samples/basic_agent/`](../samples/basic_agent/) - ASP.NET Core web application with Microsoft Agents A365 integration
- **Custom Engine**: [`/dotnet/samples/agent_with_custom_engine/`](../samples/agent_with_custom_engine/) - Advanced agent implementation with custom engines
- **Hello World Agent**: [`/dotnet/samples/hello_world_a365_agent/`](../samples/hello_world_a365_agent/) - Simple getting started example
- **Devin Agent**: [`/dotnet/samples/devin_agent/`](../samples/devin_agent/) - Advanced AI agent implementation
- **Semantic Kernel Multiturn**: [`/dotnet/samples/semantic-kernel-multiturn/`](../samples/semantic-kernel-multiturn/) - C# Semantic Kernel sample
