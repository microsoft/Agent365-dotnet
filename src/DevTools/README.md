# Microsoft Agents A365 DevTools

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.svg?label=OpenAI%20Analyzer)](https://www.nuget.org/packages/Microsoft.Agents.A365.DevTools.Analyzer.OpenAI/)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.svg?label=SK%20Analyzer)](https://www.nuget.org/packages/Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel/)
[![Downloads](https://img.shields.io/nuget/dt/Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.svg)](https://www.nuget.org/packages/Microsoft.Agents.A365.DevTools.Analyzer.OpenAI/)

The Microsoft Agents A365 DevTools provide Roslyn analyzers and development tools for building robust AI agent applications. These analyzers help enforce best practices, detect potential issues at compile-time, and ensure proper usage of agent frameworks.

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

## License

This project is licensed under the MIT License - see the [LICENSE](../../LICENSE.md) file for details.
