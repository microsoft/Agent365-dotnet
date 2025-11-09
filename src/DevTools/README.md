# Microsoft Agents A365 DevTools for .NET

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

## Package Structure

The DevTools module provides Roslyn analyzers for enforcing best practices:

### Analyzers

- **[Microsoft.Agents.A365.DevTools.Analyzer.OpenAI](Analyzer/Microsoft.Agents.A365.DevTools.Analyzer.OpenAI/README.md)** - Roslyn analyzers for OpenAI API usage and governance
- **[Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel](Analyzer/Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel/README.md)** - Roslyn analyzers for Semantic Kernel integration

## Key Capabilities

### OpenAI Analyzer

Enforces governance and best practices for OpenAI-based agent applications:

- Direct client access prevention (use provider pattern)
- Hardcoded tenant/worker ID detection
- Multi-tenant configuration validation
- Provider registration pattern enforcement
- See [OpenAI Analyzer README](Analyzer/Microsoft.Agents.A365.DevTools.Analyzer.OpenAI/README.md) for complete rule list and examples

### Semantic Kernel Analyzer

Enforces governance and best practices for Semantic Kernel applications:

- Direct kernel access prevention (use provider pattern)
- Proper function registration validation
- Memory store usage enforcement
- Multi-tenant isolation checks
- See [Semantic Kernel Analyzer README](Analyzer/Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel/README.md) for complete rule list and examples

## Getting Started

Analyzers activate automatically when installed and provide real-time feedback in your IDE:

1. Install the appropriate analyzer package
2. Analyzer rules appear as warnings or errors in Visual Studio/VS Code
3. Review the package-specific README for detailed rule descriptions
4. Configure severity levels via `.editorconfig` if needed

For detailed examples of warnings and fixes, see the individual analyzer package READMEs.

## Configuration

### .editorconfig

Configure analyzer severity and behavior:

```ini
[*.cs]

# OpenAI Analyzer Rules
dotnet_diagnostic.A365OA001.severity = error  # Hardcoded API keys
dotnet_diagnostic.A365OA002.severity = warning # Missing error handling
dotnet_diagnostic.A365OA003.severity = suggestion # Token optimization

# Semantic Kernel Analyzer Rules
dotnet_diagnostic.A365SK001.severity = error  # Improper kernel configuration
dotnet_diagnostic.A365SK002.severity = warning # Function registration issues
dotnet_diagnostic.A365SK003.severity = suggestion # Memory management
```

### Suppressing Warnings

For legitimate cases where analyzer warnings need to be suppressed:

```csharp
#pragma warning disable A365OA001 // Legitimate reason for suppression
var testClient = new OpenAIClient("test-key-for-unit-tests");
#pragma warning restore A365OA001
```

## IDE Integration

### Visual Studio

Analyzers appear in:

- Error List window
- Code editor (squiggly underlines)
- Quick Actions menu (Ctrl+.)

### Visual Studio Code

With the C# extension, analyzers provide:

- Inline diagnostics
- Code action suggestions
- Problems panel integration

## Best Practices

1. **Enable All Analyzers**: Include both OpenAI and Semantic Kernel analyzers in your projects
2. **Treat Warnings as Errors**: Configure critical rules as errors in CI/CD pipelines
3. **Regular Updates**: Keep analyzer packages up-to-date for latest rules and fixes
4. **Team Consistency**: Share .editorconfig across the team for consistent enforcement
5. **Document Suppressions**: Always document why analyzer warnings are suppressed

## Development Guidelines

### Adding Custom Rules

To extend the analyzers with custom rules:

1. Reference the analyzer project
2. Implement `DiagnosticAnalyzer`
3. Register the analyzer in the analyzer package
4. Add tests for the new rule

See the [Analyzer Development Guide](Analyzer/README.md) for details.

## Testing Your Code

The DevTools include demo applications for testing analyzer behavior:

- **OpenAI Analyzer Demo**: [DevTools.Analyzer.OpenAI.Tests/AnalyzerDemoApp](../Tests/DevTools.Analyzer.OpenAI.Tests/AnalyzerDemoApp/)
- **Semantic Kernel Analyzer Demo**: [DevTools.Analyzer.SemanticKernel.Tests/AnalyzerDemoApp](../Tests/DevTools.Analyzer.SemanticKernel.Tests/AnalyzerDemoApp/)

## Related Packages

- [Microsoft.Agents.A365.Runtime](../Runtime/README.md) - Core runtime utilities for agents
- [Microsoft.Agents.A365.Observability](../Observability/README.md) - Monitoring and tracing
- [Microsoft.Agents.A365.Tooling](../Tooling/README.md) - Developer tools and utilities

## Documentation

- [OpenAI Analyzer Documentation](Analyzer/Microsoft.Agents.A365.DevTools.Analyzer.OpenAI/README.md)
- [Semantic Kernel Analyzer Documentation](Analyzer/Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel/README.md)

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../README.md) for more information

## Contributing

This project welcomes contributions and suggestions. See the [Contributing Guide](../../README.md#contributing) for details.

Contributions for new analyzer rules are especially welcome! Please ensure:

- New rules have clear diagnostic IDs
- Rules include code fix providers where applicable
- Comprehensive tests cover all scenarios
- Documentation explains the rule and its rationale

## License

This project is licensed under the MIT License - see the [LICENSE](../../LICENSE.md) file for details.
