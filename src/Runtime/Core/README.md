# Microsoft.Agent.A365.Runtime

Runtime integration helpers for the Microsoft Agent 365 SDK, providing HttpContext-based tenant and worker ID extraction for multi-tenant agent applications.

## Features

- **Tenant Context Extraction**: Extract tenant IDs from HttpContext using standardized patterns
- **Worker Context Extraction**: Extract worker IDs from HttpContext for multi-worker scenarios
- **Multiple Source Support**: Checks user claims, request headers, and request items
- **Null Safety**: Proper null handling and validation
- **Performance Optimized**: Minimal overhead for context extraction

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Runtime
```

## Documentation

For detailed usage information, configuration examples, and best practices, see the [Microsoft Agents 365 Developer documentation](https://review.learn.microsoft.com/en-us/microsoft-agent-365/developer/observability?tabs=dotnet).

## Context Sources

The helper checks for tenant/worker IDs in this priority order:

1. **User Claims**: `tenant_id`, `worker_id`
2. **Request Headers**: `X-Tenant-Id`, `X-Worker-Id`
3. **Request Items**: `TenantId`, `WorkerId`

## Why Separate Package?

This package is separate from framework-specific integrations to:

- **Avoid Unnecessary Dependencies**: Core runtime utilities don't need framework-specific dependencies
- **Enable Flexible Deployment**: Console apps, background services don't need web dependencies
- **Follow Single Responsibility**: Each package has a focused purpose
- **Reduce Package Size**: Consumers only get what they need

## Related Documentation

- [Runtime Module Overview](../README.md)
- [Microsoft Agent 365 Observability SDK](../../Observability/README.md)
- [Microsoft Agent 365 Developer Tools](../../DevTools/README.md)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../README.md) for more information

## 📋 **Telemetry**
 
Data Collection. The software may collect information about you and your use of the software and send it to Microsoft. Microsoft may use this information to provide services and improve our products and services. You may turn off the telemetry as described in the repository. There are also some features in the software that may enable you and Microsoft to collect data from users of your applications. If you use these features, you must comply with applicable law, including providing appropriate notices to users of your applications together with a copy of Microsoft's privacy statement. Our privacy statement is located at https://go.microsoft.com/fwlink/?LinkID=824704. You can learn more about data collection and use in the help documentation and our privacy statement. Your use of the software operates as your consent to these practices.
 
## Trademarks
 
*Microsoft, Windows, Microsoft Azure and/or other Microsoft products and services referenced in the documentation may be either trademarks or registered trademarks of Microsoft in the United States and/or other countries. The licenses for this project do not grant you rights to use any Microsoft names, logos, or trademarks. Microsoft's general trademark guidelines can be found at http://go.microsoft.com/fwlink/?LinkID=254653.*

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../LICENSE.md) file for details.
