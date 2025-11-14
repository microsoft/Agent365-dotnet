# Microsoft Agent 365 Developer Tools

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.svg?label=OpenAI%20Analyzer)](https://www.nuget.org/packages/Microsoft.Agents.A365.DevTools.Analyzer.OpenAI/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.svg?label=SK%20Analyzer)](https://www.nuget.org/packages/Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel/)
[![Downloads](https://img.shields.io/nuget/dt/Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.svg)](https://www.nuget.org/packages/Microsoft.Agents.A365.DevTools.Analyzer.OpenAI/)

The Microsoft Agent 365 Developer Tools provide Roslyn analyzers and development tools for building robust AI agent applications. These analyzers help enforce best practices, detect potential issues at compile-time, and ensure proper usage of agent frameworks.

## Overview

Building reliable AI agents requires careful attention to API usage patterns, error handling, and framework-specific guidelines. The DevTools package provides compile-time analysis and guidance to help developers:

- Detect common mistakes before runtime
- Enforce best practices for AI framework usage
- Identify potential security and performance issues
- Provide automated code fixes for common problems
- Ensure consistent coding patterns across teams

## Features

- **Compile-Time Analysis**: Catch issues during development, not in production
- **Framework-Specific Analyzers**: Specialized analysis for OpenAI and Semantic Kernel
- **Code Fixes**: Automated suggestions and fixes for detected issues
- **Best Practice Enforcement**: Ensure adherence to recommended patterns
- **IDE Integration**: Seamless integration with Visual Studio and VS Code

## Installation

```bash
# For OpenAI analyzer
dotnet add package Microsoft.Agents.A365.DevTools.Analyzer.OpenAI

# For Semantic Kernel analyzer
dotnet add package Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel
```

Analyzers are automatically activated once installed and will provide warnings and suggestions during development.

## Package Structure

The DevTools module provides Roslyn analyzers for enforcing best practices:

### Analyzers

- **[Microsoft.Agents.A365.DevTools.Analyzer.OpenAI](Analyzer/Microsoft.Agents.A365.DevTools.Analyzer.OpenAI/README.md)** - Roslyn analyzers for OpenAI API usage and governance
- **[Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel](Analyzer/Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel/README.md)** - Roslyn analyzers for Semantic Kernel integration

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

This project is licensed under the MIT License - see the [LICENSE](../../LICENSE.md) file for details.
