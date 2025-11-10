# Microsoft.Agents.A365.DevTools.Analyzer.OpenAI

A Roslyn analyzer package that enforces Microsoft Agents 365 SDK compliance and governance rules for OpenAI-based agent projects.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.DevTools.Analyzer.OpenAI
```

The analyzer is automatically activated once the package is installed.

## Key Rules

### A365OAI0001: Use IChatClientProvider

```csharp
// ❌ Incorrect
private ChatClient _chatClient;

// ✅ Correct
private readonly IChatClientProvider _chatClientProvider;
```

### A365OAI0002: Use IOpenAIClientProvider

```csharp
// ❌ Incorrect
private OpenAIClient _openAIClient;

// ✅ Correct
private readonly IOpenAIClientProvider _openAIClientProvider;
```

### A365OAI0009: No Hardcoded IDs

```csharp
// ❌ Incorrect
var tenantId = "12345";

// ✅ Correct
var tenantId = context.GetTenantId();
```

## Support

For issues, questions, or feedback:

- File issues in the [GitHub Issues](https://github.com/microsoft/Agent365-dotnet/issues) section
- See the [main documentation](../../../../README.md) for more information

## License

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License - see the [LICENSE](../../../../LICENSE.md) file for details.
