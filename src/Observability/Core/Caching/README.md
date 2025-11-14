# Microsoft.Agents.A365.Observability - Caching

Secure token caching with built-in expiration and invalidation features for Microsoft Agent 365 Observability exporters.

## Overview

`ServiceTokenCache` is a reference implementation of `IExporterTokenCache<string>` that provides secure token caching with built-in expiration and invalidation features for observability exporters.

## Features

### Security Features

- **Automatic Token Expiration**: Tokens expire after a configurable time period (default: 1 hour)
- **Automatic Cleanup**: Expired tokens are automatically removed on access
- **Manual Invalidation**: Support for explicit token removal (individual or all)
- **Thread-Safe Operations**: All operations are thread-safe using `ConcurrentDictionary`

### Configuration

- **Default Expiration**: Configurable default expiration time for all tokens
- **Per-Token Expiration**: Ability to override expiration on a per-token basis
- **Validation**: Comprehensive input validation with descriptive error messages

## Installation

This functionality is included in the core observability package:

```bash
dotnet add package Microsoft.Agents.A365.Observability
```

## Documentation

For detailed usage information, best practices, and examples, see the [Microsoft Agents 365 Observability documentation](https://learn.microsoft.com/microsoft-agent-365/developer/observability?tabs=dotnet).

## Related Documentation

- [IExporterTokenCache Interface](../Core/Caching/IExporterTokenCache.cs)
- [AgenticTokenCache](../Core/Caching/AgenticTokenCache.cs) - Alternative implementation for agentic scenarios
- [Observability Core Package](../README.md)
- [Observability Module Overview](../../README.md)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## 📋 **Telemetry**
 
Data Collection. The software may collect information about you and your use of the software and send it to Microsoft. Microsoft may use this information to provide services and improve our products and services. You may turn off the telemetry as described in the repository. There are also some features in the software that may enable you and Microsoft to collect data from users of your applications. If you use these features, you must comply with applicable law, including providing appropriate notices to users of your applications together with a copy of Microsoft's privacy statement. Our privacy statement is located at https://go.microsoft.com/fwlink/?LinkID=824704. You can learn more about data collection and use in the help documentation and our privacy statement. Your use of the software operates as your consent to these practices.
 
## Trademarks
 
*Microsoft, Windows, Microsoft Azure and/or other Microsoft products and services referenced in the documentation may be either trademarks or registered trademarks of Microsoft in the United States and/or other countries. The licenses for this project do not grant you rights to use any Microsoft names, logos, or trademarks. Microsoft's general trademark guidelines can be found at http://go.microsoft.com/fwlink/?LinkID=254653.*

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.

