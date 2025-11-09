# Microsoft.Agents.A365.Observability.Hosting

The Observability Hosting package provides ETW (Event Tracing for Windows) integration for Microsoft Agents A365 Observability. This package enables high-performance event tracing on Windows platforms for production monitoring scenarios.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Observability.Hosting
```

## Usage

### Basic ETW Configuration

```csharp
using Microsoft.Agents.A365.Observability.Hosting.Etw;

var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry tracing with ETW support
builder.Services.AddTracingWithEtw();

var app = builder.Build();
app.Run();
```

### Collecting ETW Events

```powershell
# Using PerfView
PerfView.exe collect -AcceptEula -NoGui -NoNGenRundown

# Using logman (Windows built-in)
logman create trace AgentTrace -p "Microsoft-Agents-A365-Observability" -o trace.etl
logman start AgentTrace
# ... run your application ...
logman stop AgentTrace
```

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../LICENSE.md) file for details.
