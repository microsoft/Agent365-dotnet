# Microsoft Agents A365 Notification

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

## Getting Started

For detailed implementation examples, configuration options, and advanced usage patterns, see the [Notifications package documentation](./Microsoft.Agents.A365.Notifications/README.md).

Quick overview of key capabilities:

- **Email Notifications**: Respond when the agent receives email
- **Document Comments**: Handle @-mentions in Word document comments
- **Route Ranking**: Control handler priority with First/Normal/Last ranking
- **Error Handling**: Built-in support for exception handling and logging

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../README.md) for more information

## Contributing

This project welcomes contributions and suggestions. See the [Contributing Guide](../../README.md#contributing) for details.

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../LICENSE.md) file for details.

