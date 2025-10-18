# Analyzers in Microsoft.Kairo.Sdk.DevTools.Analyzer.SemanticKernel

This document provides a comprehensive guide for developers creating and maintaining analyzers in the Microsoft.Kairo.Sdk.DevTools.Analyzer.SemanticKernel project. These analyzers enforce governance principles in multi-tenant environments and ensure proper lifecycle and context management.

## Analyzer Summary Table

| Analyzer ID | Analyzer Name | What It Detects | Why It's An Issue | How It's Fixed | Code Fix Provider | Status |
|-------------|---------------|-----------------|-------------------|----------------|-------------------|--------|
| A365SK0001 | KernelRetrievalBeforeBuildAnalyzer | • GetRequiredService<Kernel>() in service registration lambdas<br/>• new MyAgent() instantiation before builder.Build()<br/>• Premature kernel access patterns | Kernel should only be retrieved after DI container is built and governance is applied. Early access bypasses governance setup. | Remove flagged error regions or nodes. Restructure code to retrieve kernel after builder.Build() | KernelRetrievalBeforeBuildCodeFixProvider | ✅ |
| A365SK0002 | KernelDirectAccessAnalyzer | • Direct Kernel injection in constructors<br/>• Kernel field declarations<br/>• GetRequiredService<Kernel>() calls<br/>• Direct _kernel field usage | Violates A365 multi-tenant governance. Direct kernel access bypasses tenant isolation and governance controls. | Use IKernelProvider instead of direct Kernel access. Retrieve kernels via kernelProvider.GetKernel(tenantId, workerId) | KernelDirectAccessCodeFixProvider | ✅ |
| A365SK0003 | KernelDirectAccessAnalyzer | • ImportPluginFromObject() calls<br/>• Unsafe plugin import patterns<br/>• Direct plugin registration bypassing governance | ImportPluginFromObject can cause 'key already added' exceptions when governance automatically registers plugins. | Use TryImportPluginFromObject instead of ImportPluginFromObject to prevent exceptions | KernelDirectAccessCodeFixProvider | ✅ |
| A365SK0004 | TenantWorkerIdAccessAnalyzer | • Direct tenant/worker ID extraction from headers<br/>• context.User.FindFirst("tenant_id")<br/>• context.Request.Headers["X-Tenant-Id"]<br/>• context.Items["tenant_id"] access | Bypasses centralized tenant context management and governance. Direct access can lead to security vulnerabilities and inconsistent tenant isolation. | Use TenantContextHelper or similar governance-approved methods for tenant/worker ID access | TenantWorkerIdAccessCodeFixProvider | ✅ |
| A365SK0005 | ChatCompletionServiceRegistrationAnalyzer | • Direct chat completion service registration<br/>• Non-governance-approved service registration patterns | Direct registration of chat completion service bypasses governance controls and tenant-aware configuration. | Use approved governance delegate/template functions for service registration | ChatCompletionServiceRegistrationCodeFixProvider | ✅ |

## Diagnostic ID System

The analyzers use a structured diagnostic ID format: **A365SK####**
- **A365**: Kairo prefix
- **SK**: Semantic Kernel orchestrator code  
- **####**: 4-digit sequence number

### Current Diagnostic IDs
- **A365SK0001**: KernelRetrievalBeforeBuild
- **A365SK0002**: KernelDirectAccess  
- **A365SK0003**: UnsafePluginImport
- **A365SK0004**: TenantWorkerIdAccess
- **A365SK0005**: ChatCompletionServiceRegistration

Future orchestrator codes: OI (OpenAI), CL (Claude), etc.

## Key Governance Principles Enforced

### Multi-tenant Isolation
- **Kernel Access**: Ensures kernels are accessed through governance-aware providers (`IKernelProvider`)
- **Tenant Context**: Requires centralized tenant context management via `TenantContextHelper`
- **Service Registration**: Enforces governance-approved service registration patterns

### Function Governance
- **Plugin Management**: Prevents unsafe plugin imports that can cause exceptions during auto-registration
- **Service Configuration**: Ensures chat completion services use approved registration methods

### Lifecycle Management
- **DI Container**: Enforces proper dependency injection container build sequence
- **Kernel Lifecycle**: Prevents premature kernel access before governance setup

### API Security
- **Endpoint Protection**: Ensures API endpoints have proper governance enforcement via ApplyGovernanceAsync
- **Context Validation**: Requires proper tenant context validation in controllers

## Project Structure for New Developers

### Core Directories
```
Microsoft.Kairo.Sdk.DevTools.Analyzer.SemanticKernel/
├── Constants/
│   └── AnalyzerConstants.cs          # Central constants, diagnostic IDs, type names
├── Common/
│   ├── DiagnosticDescriptorFactory.cs # Standardized diagnostic creation
│   └── SyntaxAnalysisHelpers.cs      # Shared syntax analysis utilities
├── ChatCompletionService/
│   ├── ChatCompletionServiceRegistrationAnalyzer.cs
│   └── ChatCompletionServiceRegistrationCodeFixProvider.cs
├── [Individual Analyzer Files]
│   ├── KernelDirectAccessAnalyzer.cs
│   ├── KernelDirectAccessCodeFixProvider.cs
│   ├── TenantWorkerIdAccessAnalyzer.cs
│   ├── TenantWorkerIdAccessCodeFixProvider.cs
│   ├── KernelRetrievalBeforeBuildAnalyzer.cs
│   └── KernelRetrievalBeforeBuildCodeFixProvider.cs
└── AnalyzerDocumentation.md          # This file
```

### Test Project Structure
```
Microsoft.Kairo.Sdk.DevTools.Analyzer.SemanticKernel.Tests/
├── Common/
│   ├── TestCodeSamples.cs            # Shared test code samples
│   └── AnalyzerTestsBase.cs          # Base test infrastructure
├── ChatCompletionService/
│   └── ChatCompletionServiceRegistrationAnalyzerTests.cs
├── [Individual Test Files]
│   ├── KernelDirectAccessAnalyzerTests.cs
│   ├── TenantWorkerIdAccessAnalyzerTests.cs
│   ├── KernelRetrievalBeforeBuildAnalyzerTests.cs
│   ├── AnalyzerMetadataTests.cs      # Metadata validation tests
│   └── AnalyzerIntegrationTests.cs   # Cross-analyzer integration tests
└── TestApp/                          # Real test application for integration testing
    ├── WeatherAgent.cs
    └── Program.cs
```

## Code Fix Capabilities

## Code Fix Capabilities

### Automated Code Fixes Available
- **KernelDirectAccessCodeFixProvider**: Automatically replaces direct kernel access with IKernelProvider patterns and unsafe plugin imports with safe alternatives
- **KernelRetrievalBeforeBuildCodeFixProvider**: Removes flagged error regions and restructures premature kernel access
- **TenantWorkerIdAccessCodeFixProvider**: Replaces direct header access with TenantContextHelper calls
- **ChatCompletionServiceRegistrationCodeFixProvider**: Updates service registration to use governance-approved methods

## Creating New Analyzers - Developer Guide

### Step 1: Update AnalyzerConstants.cs
Add your new diagnostic ID to the `DiagnosticIds` class:
```csharp
public const string YourNewAnalyzer = "A365SK0007"; // Next available ID
```

### Step 2: Create Diagnostic Descriptor
Add a new method to `DiagnosticDescriptorFactory.cs`:
```csharp
public static DiagnosticDescriptor YourNewAnalyzer => CreateDescriptor(
    AnalyzerConstants.DiagnosticIds.YourNewAnalyzer,
    "Brief title describing the issue",
    "Message format with fix guidance. " + AnalyzerConstants.GuidanceSuffix,
    AnalyzerConstants.Categories.Governance, // or Usage
    AnalyzerConstants.DefaultSeverity,
    isEnabledByDefault: true,
    description: "Detailed description explaining why this pattern is problematic.");
```

### Step 3: Implement the Analyzer
Create your analyzer class following the established pattern:
```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class YourNewAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = AnalyzerConstants.DiagnosticIds.YourNewAnalyzer;
    
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptorFactory.YourNewAnalyzer);
        
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.YourTargetSyntaxKind);
    }
    
    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        // Your analysis logic here
    }
}
```

### Step 4: Create Code Fix Provider (Optional)
If automatic fixes are possible, create a code fix provider:
```csharp
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(YourNewCodeFixProvider)), Shared]
public sealed class YourNewCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(AnalyzerConstants.DiagnosticIds.YourNewAnalyzer);
        
    // Implementation following established patterns
}
```

### Step 5: Write Comprehensive Tests
Create test file following the naming pattern:
```csharp
public class YourNewAnalyzerTests
{
    [Fact]
    public void AnalyzerMetadata_IsCorrect() { /* Test metadata */ }
    
    [Fact] 
    public void AnalyzesProblematicCode_ReportsExpectedDiagnostics() { /* Test detection */ }
    
    [Fact]
    public void AnalyzesCorrectCode_ReportsNoDiagnostics() { /* Test no false positives */ }
}
```

### Step 6: Integration Testing
Add your analyzer to the integration tests to ensure it works with the complete system.

## Analyzer Architecture

## Analyzer Architecture

### Constants Management
- **AnalyzerConstants.cs**: Single source of truth for all constants to eliminate hardcoded strings
  - `DiagnosticIds`: All diagnostic IDs with A365SK#### pattern
  - `Categories`: Governance and Usage categories
  - `TypeNames`: Common type names used across analyzers
  - `MethodNames`: Method names for semantic analysis
  - `MemberNames`: Property/field names with compile-time safety
  - `TenantWorkerIds`: Tenant/worker ID patterns and headers
  - `HelpLinkBase`: Base URL for help documentation

### Diagnostic Creation
- **DiagnosticDescriptorFactory.cs**: Centralized diagnostic descriptor creation
  - Consistent help link generation
  - Standardized message formatting
  - Unified severity and category handling
  - Multi-rule analyzer support via `GetKernelAccessDiagnostics()`

### Common Utilities
- **SyntaxAnalysisHelpers.cs**: Shared syntax analysis utilities for detecting problematic patterns

### Diagnostic Categories
- **Governance**: Multi-tenant isolation, security, and compliance (A365SK0002, A365SK0003, A365SK0004, A365SK0005)
- **Usage**: Lifecycle management and proper API usage (A365SK0001)

## Best Practices for Analyzer Development

### 1. Follow the Constants Pattern
- Always use `AnalyzerConstants` instead of hardcoded strings
- Add new constants to appropriate sections
- Use `nameof()` for compile-time safety where applicable

### 2. Consistent Diagnostic Creation
- Use `DiagnosticDescriptorFactory` for all descriptors
- Include fix guidance in message format
- Append `AnalyzerConstants.GuidanceSuffix` to user messages
- Provide detailed descriptions explaining why patterns are problematic

### 3. File Organization
- One analyzer per file with matching filename
- Group related analyzers in subdirectories (e.g., ChatCompletionService/)
- Place code fix providers next to their analyzers
- Mirror directory structure in test project

### 4. Testing Standards
- Test analyzer metadata correctness
- Test positive cases (detects problematic code)
- Test negative cases (no false positives)
- Include integration tests for multi-analyzer scenarios
- Use `TestCodeSamples` for shared test code

### 5. Error Handling
- Handle syntax edge cases gracefully
- Avoid throwing exceptions in analyzers
- Use semantic model safely with null checks

## Integration and Compatibility

### Target Frameworks
- **.NET Standard 2.0**: Maximum compatibility across .NET implementations
- **C# Language Version**: Latest for enhanced analyzer capabilities
- **Roslyn Version**: Compatible with Visual Studio 2019+ and .NET SDK 6.0+

### Packaging
- **NuGet Analyzer Package**: Build-time enforcement through diagnostic errors
- **IDE Integration**: Real-time feedback in Visual Studio and VS Code
- **CI/CD Integration**: Automated enforcement in build pipelines via MSBuild

### Help Documentation
- **Help Links**: Auto-generated as `{HelpLinkBase}/{DiagnosticId}.md`
- **Base URL**: `https://github.com/microsoft/Kairo/tree/main/docs/analyzers`
- **Format**: Each analyzer has dedicated documentation page

## Usage Examples

### Before (Problematic Code)
```csharp
// ❌ A365SK0002: Direct Kernel access
public class MyAgent 
{
    private readonly Kernel _kernel;
    
    public MyAgent(Kernel kernel) // Direct injection
    {
        _kernel = kernel;
    }
}

// ❌ A365SK0001: Premature kernel retrieval
var kernel = builder.Services.GetRequiredService<Kernel>(); // Before Build()
var app = builder.Build();

// ❌ A365SK0004: Direct tenant ID access
var tenantId = context.User.FindFirst("tenant_id")?.Value;
var workerId = context.Request.Headers["X-Worker-Id"].FirstOrDefault();

// ❌ A365SK0003: Unsafe plugin import
kernel.ImportPluginFromObject(new WeatherPlugin()); // Can throw exceptions
```

### After (Corrected Code)
```csharp
// ✅ Proper IKernelProvider usage
public class MyAgent 
{
    private readonly IKernelProvider _kernelProvider;
    
    public MyAgent(IKernelProvider kernelProvider) // Governance-approved
    {
        _kernelProvider = kernelProvider;
    }
    
    public async Task<string> GetWeatherAsync(string tenantId, string workerId)
    {
        var kernel = await _kernelProvider.GetKernelAsync(tenantId, workerId);
        // Use kernel for tenant-isolated operations
    }
}

// ✅ Proper build sequence
var app = builder.Build(); // Build first
var kernel = app.Services.GetRequiredService<Kernel>(); // Then retrieve

// ✅ Centralized tenant context access
using Microsoft.Kairo.Sdk.AspNetCore;
var tenantId = TenantContextHelper.GetTenantId(context);
var workerId = TenantContextHelper.GetWorkerId(context);

// ✅ Safe plugin import
using Microsoft.Agents.A365.Tools.SemanticKernel.Extensions;
var success = kernel.TryImportPluginFromObject(new WeatherPlugin()); // Safe, idempotent

// ✅ Proper governance enforcement
app.MapPost("/weather", async (HttpContext context) => {
    await context.Services.ApplyGovernanceAsync(context.Logger); // First operation
    return Results.Ok("Weather data");
});
```

## Quick Reference for Developers

### Common Analyzer Patterns

#### Detecting Method Calls
```csharp
// Check for specific method calls like GetRequiredService<T>()
private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
{
    var invocation = (InvocationExpressionSyntax)context.Node;
    var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
    
    if (memberAccess?.Name.Identifier.ValueText == AnalyzerConstants.MethodNames.GetRequiredService)
    {
        // Analyze the invocation
    }
}
```

#### Detecting Type Usage
```csharp
// Check for field/property declarations of specific types
private static void AnalyzeField(SyntaxNodeAnalysisContext context)
{
    var field = (FieldDeclarationSyntax)context.Node;
    var typeName = field.Declaration.Type.ToString();
    
    if (typeName.Contains(AnalyzerConstants.TypeNames.Kernel))
    {
        // Report diagnostic
    }
}
```

#### Semantic Model Usage
```csharp
// Use semantic model for accurate type checking
var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
if (symbolInfo.Symbol is IMethodSymbol method)
{
    var containingType = method.ContainingType?.Name;
    if (containingType == AnalyzerConstants.TypeNames.Kernel)
    {
        // Accurate type-based analysis
    }
}
```

### Testing Patterns

#### Basic Analyzer Test
```csharp
[Fact]
public async Task AnalyzeProblematicCode_ReportsExpectedDiagnostic()
{
    var code = """
        public class TestClass 
        {
            private readonly Kernel _kernel; // Should trigger A365SK0002
        }
        """;
        
    var expected = DiagnosticResult
        .CompilerError(AnalyzerConstants.DiagnosticIds.KernelDirectAccess)
        .WithSpan(3, 9, 3, 45); // Line and column numbers
        
    await VerifyAnalyzerAsync(code, expected);
}
```

#### Integration Test Pattern
```csharp
[Fact] 
public void AllAnalyzers_HaveUniqueIds()
{
    var analyzers = GetAllAnalyzers();
    var diagnosticIds = analyzers.SelectMany(a => a.SupportedDiagnostics)
                               .Select(d => d.Id)
                               .ToList();
                               
    Assert.Equal(diagnosticIds.Count, diagnosticIds.Distinct().Count());
}
```

### Code Fix Provider Patterns

#### Simple Text Replacement
```csharp
public override async Task<Solution> GetFixAsync(Document document, Diagnostic diagnostic)
{
    var root = await document.GetSyntaxRootAsync();
    var problematicNode = root.FindNode(diagnostic.Location.SourceSpan);
    
    var replacement = SyntaxFactory.ParseExpression("TryImportPluginFromObject");
    var newRoot = root.ReplaceNode(problematicNode, replacement);
    
    return document.WithSyntaxRoot(newRoot).Project.Solution;
}
```

## Troubleshooting Guide

### Common Issues

1. **Analyzer Not Running**
   - Verify `.csproj` includes `<Analyzer Include="..." />`
   - Check target framework compatibility
   - Ensure no compilation errors in analyzer project

2. **False Positives**
   - Use semantic model for accurate type checking
   - Check for generated code exclusions
   - Validate syntax node types carefully

3. **Performance Issues**
   - Register for specific syntax kinds only
   - Avoid expensive operations in hot paths
   - Use concurrent execution: `context.EnableConcurrentExecution()`

4. **Test Failures**
   - Verify exact line/column numbers in expected diagnostics
   - Check for platform-specific path separators
   - Ensure test code compiles without syntax errors

### Debugging Tips

1. **Use Output Window**: Add `System.Diagnostics.Debug.WriteLine()` statements
2. **Debugger Attachment**: Attach debugger to compiler process for step-through debugging
3. **Syntax Visualizer**: Use Roslyn Syntax Visualizer in Visual Studio for syntax tree exploration
4. **Unit Test First**: Write failing tests before implementing analyzer logic

---

*Last Updated: September 12, 2025*  
*For questions or contributions, see the project repository documentation.*