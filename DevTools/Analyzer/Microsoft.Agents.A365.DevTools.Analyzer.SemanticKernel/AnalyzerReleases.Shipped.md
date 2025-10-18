markdown; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SKGOV001 | Governance | Error | Direct Kernel access or storage is not allowed
AGENTS001 | Usage | Error | Kernel retrieval before builder.Build() and duplicate MyAgent registration
AGENTS002 | Usage | Error | Governance enforcement in endpoints
SKGOV002 | Governance | Error | Tenant/worker ID access enforcement
