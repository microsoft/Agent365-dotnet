; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
A365OAI0001 | Governance | Error | Direct ChatClient access or storage is not allowed
A365OAI0002 | Governance | Error | Direct OpenAIClient access or storage is not allowed
A365OAI0004 | Governance | Error | Tenant/worker ID access enforcement
A365OAI0005 | Usage | Error | ChatClient provider must be configured properly for multi-tenant scenarios
A365OAI0006 | Governance | Error | Functions must be accessed via IOpenAIFunctionProvider for tenant isolation
A365OAI0008 | Usage | Error | Providers must be registered with delegate-based configuration
A365OAI0009 | Governance | Error | Tenant and Worker IDs must not be hardcoded
A365OAI0010 | Governance | Error | Data storage must be tenant-isolated to prevent cross-tenant access
A365OAI0011 | Governance | Error | Agent classes must use providers instead of direct OpenAI clients