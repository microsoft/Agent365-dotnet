# Microsoft Agent 365 Notifications SDK

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.Notifications.svg)](https://www.nuget.org/packages/Microsoft.Agents.A365.Notifications/)
[![Downloads](https://img.shields.io/nuget/dt/Microsoft.Agents.A365.Notifications.svg)](https://www.nuget.org/packages/Microsoft.Agents.A365.Notifications/)

The Notification module provides a comprehensive framework for handling agent notification events in Microsoft 365 environments. It enables agents to respond to email notifications, document mentions, and other M365 notification types with type-safe, event-driven architectures.

## Overview

The Notification module offers:

- **Event-Driven Architecture**: Handle notifications asynchronously with pattern-based routing
- **Multiple Notification Types**: Support for email notifications, Word document @-mentions, and more
- **Type-Safe Models**: Strongly-typed notification models and enums for reliable development
- **Flexible Handler Registration**: Easy-to-use `OnAgentNotification` extension method with route ranking
- **Auto Sign-In Support**: Built-in authentication handler integration for secure notification processing

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Notifications
```

## Package Structure

- **[Microsoft.Agents.A365.Notifications](./Microsoft.Agents.A365.Notifications/README.md)** - Notification handling framework with event routing, type-safe models, and authentication support

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../README.md) for more information

## Contributing

This project welcomes contributions and suggestions. See the [Contributing Guide](../../README.md#contributing) for details.

## 📋 **Telemetry**
 
Data Collection. The software may collect information about you and your use of the software and send it to Microsoft. Microsoft may use this information to provide services and improve our products and services. You may turn off the telemetry as described in the repository. There are also some features in the software that may enable you and Microsoft to collect data from users of your applications. If you use these features, you must comply with applicable law, including providing appropriate notices to users of your applications together with a copy of Microsoft's privacy statement. Our privacy statement is located at https://go.microsoft.com/fwlink/?LinkID=824704. You can learn more about data collection and use in the help documentation and our privacy statement. Your use of the software operates as your consent to these practices.
 
## Trademarks
 
*Microsoft, Windows, Microsoft Azure and/or other Microsoft products and services referenced in the documentation may be either trademarks or registered trademarks of Microsoft in the United States and/or other countries. The licenses for this project do not grant you rights to use any Microsoft names, logos, or trademarks. Microsoft's general trademark guidelines can be found at http://go.microsoft.com/fwlink/?LinkID=254653.*

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../LICENSE.md) file for details.

