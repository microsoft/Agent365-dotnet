# Analyzers in Microsoft.Agents.A365.DevTools.Analyzer.OpenAI

This document provides a comprehensive guide for developers creating and maintaining analyzers in the Microsoft.Agents.A365.DevTools.Analyzer.OpenAI project. These analyzers enforce governance principles in multi-tenant environments and ensure proper OpenAI client management and tenant isolation.

## Analyzer Summary Table

| Analyzer ID | Analyzer Name | What It Detects | Why It's An Issue | How It's Fixed | Code Fix Provider | Status |
|-------------|---------------|-----------------|-------------------|----------------|-------------------|--------|
| A365OAI0001 | ChatClientDirectAccessAnalyzer | • Direct ChatClient injection in constructors<br/>• ChatClient field declarations<br/>• GetRequiredService<ChatClient>() calls<br/>• Direct _chatClient field usage | Violates A365 multi-tenant governance. Direct client access bypasses tenant isolation and governance controls. | Use IChatClientProvider instead of direct ChatClient access. Retrieve clients via provider.GetChatClient(tenantId, workerId) | ChatClientDirectAccessCodeFixProvider | ✅ |
| A365OAI0002 | OpenAIClientDirectAccessAnalyzer | • Direct OpenAIClient injection in constructors<br/>• OpenAIClient field declarations<br/>• GetRequiredService<OpenAIClient>() calls<br/>• Direct client field usage | Violates A365 multi-tenant governance. Direct client access bypasses tenant isolation and governance controls. | Use IChatClientProvider instead of direct OpenAIClient access. Retrieve clients via provider.GetChatClient(tenantId, workerId) | OpenAIClientDirectAccessCodeFixProvider | ✅ |
| A365OAI0004 | TenantWorkerIdAccessAnalyzer | • Direct tenant/worker ID extraction from headers<br/>• context.User.FindFirst("tenant_id")<br/>• context.Request.Headers["X-Tenant-Id"]<br/>• context.Items["tenant_id"] access | Bypasses centralized tenant context management and governance. Direct access can lead to security vulnerabilities and inconsistent tenant isolation. | Use TenantContextHelper or similar governance-approved methods for tenant/worker ID access | TenantWorkerIdAccessCodeFixProvider | ✅ |
| A365OAI0005 | ChatClientProviderUsageAnalyzer | • Missing IChatClientProvider registration<br/>• Improper provider configuration patterns | Direct registration bypasses governance controls and tenant-aware configuration. | Use approved governance delegate/template functions for provider registration | ChatClientProviderUsageCodeFixProvider | ✅ |
| A365OAI0006 | FunctionProviderEnforcementAnalyzer | • Direct function creation without provider<br/>• ChatTool.CreateFunctionTool() without tenant context<br/>• Function execution bypassing provider patterns | Direct function operations bypass multi-tenant governance and can cause cross-tenant data leakage. | Use IOpenAIFunctionProvider.GetAvailableTools() and ExecuteFunctionAsync() for tenant-isolated function operations | FunctionProviderEnforcementCodeFixProvider | ✅ |
| A365OAI0008 | ProviderRegistrationValidationAnalyzer | • Direct client registration in DI container<br/>• AddSingleton<ChatClient>() or AddSingleton<OpenAIClient>()<br/>• Non-delegate-based provider registration | Direct client registration bypasses governance-approved factory patterns and tenant isolation. | Register providers using delegate-based factories with proper tenant isolation | ProviderRegistrationValidationCodeFixProvider | ✅ |
| A365OAI0009 | HardcodedTenantWorkerAnalyzer | • Hardcoded tenant ID string literals<br/>• Hardcoded worker ID string literals<br/>• Static tenant/worker identification patterns | Hardcoded tenant/worker IDs bypass multi-tenant isolation and create security risks. | Extract tenant/worker IDs from HttpContext using TenantContextHelper methods | HardcodedTenantWorkerCodeFixProvider | ✅ |
| A365OAI0010 | CrossTenantDataAnalyzer | • Shared static collections across tenants<br/>• Singleton storage without tenant scoping<br/>• Global state that could leak across tenants | Shared storage without tenant isolation creates data leakage risks and violates multi-tenant governance. | Use tenant-scoped storage patterns with tenantId/workerId in keys or tenant-scoped DI containers | CrossTenantDataCodeFixProvider | ✅ |
| A365OAI0011 | AgentConstructionAnalyzer | • Agent constructors with direct ChatClient parameters<br/>• Agent constructors with direct OpenAIClient parameters<br/>• Direct client dependency injection in agents | Agent classes with direct client dependencies cannot support multi-tenancy and bypass governance controls. | Use provider-based dependency injection (IChatClientProvider, IOpenAIFunctionProvider) instead of direct clients | AgentConstructionCodeFixProvider | ✅ |

## Diagnostic ID System

The analyzers use a structured diagnostic ID format: **A365OAI####**
- **A365**: Microsoft Agents A365 prefix
- **OAI**: OpenAI orchestrator code  
- **####**: 4-digit sequence number

### Current Diagnostic IDs
- **A365OAI0001**: ChatClientDirectAccess
- **A365OAI0002**: OpenAIClientDirectAccess  
- **A365OAI0004**: TenantWorkerIdAccess
- **A365OAI0005**: ChatClientProviderUsage
- **A365OAI0006**: FunctionProviderEnforcement
- **A365OAI0008**: ProviderRegistrationValidation
- **A365OAI0009**: HardcodedTenantWorkerPrevention
- **A365OAI0010**: CrossTenantDataAccessPrevention
- **A365OAI0011**: AgentConstructionValidation

Future orchestrator codes: SK (Semantic Kernel), CL (Claude), etc.

## Key Governance Principles Enforced

### Multi-tenant Isolation
- **Client Access**: Ensures OpenAI clients are accessed through governance-aware providers (`IChatClientProvider`)
- **Tenant Context**: Requires centralized tenant context management via `TenantContextHelper`
- **Provider Registration**: Enforces governance-approved provider registration patterns

### Function Governance
- **Function Management**: Prevents direct function creation bypassing tenant-aware function providers
- **Tool Management**: Ensures ChatTool operations use proper tenant context
- **Function Execution**: Enforces tenant-isolated function execution via providers

### Lifecycle Management
- **DI Container**: Enforces proper dependency injection with provider-based patterns
- **Client Lifecycle**: Prevents direct client storage and promotes provider-based access

### API Security
- **Endpoint Protection**: Ensures API endpoints have proper governance enforcement
- **Context Validation**: Requires proper tenant context validation in controllers
- **Data Isolation**: Prevents cross-tenant data leakage through shared storage

## Project Structure for New Developers

### Core Directories
```
Microsoft.Agents.A365.DevTools.Analyzer.OpenAI/
├── Constants/
│   └── AnalyzerConstants.cs          # Central constants, diagnostic IDs, type names
├── Common/
│   ├── DiagnosticDescriptorFactory.cs # Standardized diagnostic creation
│   └── SyntaxAnalysisHelpers.cs      # Shared syntax analysis utilities
├── [Individual Analyzer Files]
│   ├── AgentConstructionAnalyzer.cs
│   ├── CrossTenantDataAnalyzer.cs
│   ├── FunctionProviderEnforcementAnalyzer.cs
│   ├── HardcodedTenantWorkerAnalyzer.cs
│   ├── OpenAIClientDirectAccessAnalyzer.cs
│   └── ProviderRegistrationAnalyzer.cs
└── AnalyzerDocumentation.md          # This file
```

### Test Project Structure
```
Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Tests/
├── Common/
│   ├── TestCodeSamples.cs            # Shared test code samples
│   └── AnalyzerTestBase.cs           # Base test infrastructure
├── [Individual Test Files]
│   ├── AgentConstructionAnalyzerTests.cs
│   ├── CrossTenantDataAnalyzerTests.cs
│   ├── FunctionProviderEnforcementAnalyzerTests.cs
│   ├── HardcodedTenantWorkerAnalyzerTests.cs
│   ├── OpenAIClientDirectAccessAnalyzerTests.cs
│   ├── ProviderRegistrationAnalyzerTests.cs
│   ├── TenantWorkerIdAccessAnalyzerTests.cs
│   ├── AnalyzerMetadataTests.cs      # Metadata validation tests
│   └── AnalyzerIntegrationTests.cs   # Cross-analyzer integration tests
└── AnalyzerDemoApp/                  # Real test application for integration testing
    ├── WeatherAgent.cs
    └── Program.cs
```

## Code Fix Capabilities

### Automated Code Fixes Available
- **ChatClientDirectAccessCodeFixProvider**: Automatically replaces direct ChatClient access with IChatClientProvider patterns
- **OpenAIClientDirectAccessCodeFixProvider**: Automatically replaces direct OpenAIClient access with IChatClientProvider patterns
- **TenantWorkerIdAccessCodeFixProvider**: Replaces direct header access with TenantContextHelper calls
- **FunctionProviderEnforcementCodeFixProvider**: Updates function operations to use IOpenAIFunctionProvider
- **ProviderRegistrationValidationCodeFixProvider**: Updates service registration to use governance-approved provider patterns
- **HardcodedTenantWorkerCodeFixProvider**: Replaces hardcoded tenant/worker IDs with context extraction
- **CrossTenantDataCodeFixProvider**: Updates storage patterns to use tenant-scoped approaches
- **AgentConstructionCodeFixProvider**: Updates agent constructors to use provider-based dependency injection

## Creating New Analyzers - Developer Guide

### Step 1: Update AnalyzerConstants.cs
Add your new diagnostic ID to the `DiagnosticIds` class:
```csharp
public const string YourNewAnalyzer = "A365OAI0012"; // Next available ID
```

### Step 2: Create Diagnostic Descriptor
Add a new method to `DiagnosticDescriptorFactory.cs`:
```csharp
public static DiagnosticDescriptor YourNewAnalyzer => CreateDescriptor(
    AnalyzerConstants.DiagnosticIds.YourNewAnalyzer,
    "Title describing the governance rule",
    "Message format with fix guidance. Fix: 1) Step one, 2) Step two. " + 
    AnalyzerConstants.GuidanceSuffix,
    AnalyzerConstants.Categories.Governance, // or Usage
    AnalyzerConstants.DefaultSeverity,
    isEnabledByDefault: true,
    description: "Detailed description explaining why this pattern is problematic and the governance principles it violates.");
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
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression); // or appropriate syntax kind
    }
    
    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        // Implementation following established patterns
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
public class YourNewAnalyzerTests : AnalyzerTestBase<YourNewAnalyzer>
{
    [Fact]
    public async Task AnalyzesProblematicCode_ReportsExpectedDiagnostic() { /* Test detects violations */ }
    
    [Fact]
    public async Task AnalyzesCorrectCode_ReportsNoDiagnostics() { /* Test no false positives */ }
}
```

### Step 6: Integration Testing
Add your analyzer to the integration tests to ensure it works with the complete system.

## Analyzer Architecture

### Constants Management
- **AnalyzerConstants.cs**: Single source of truth for all constants to eliminate hardcoded strings
  - `DiagnosticIds`: All diagnostic IDs with A365OAI#### pattern
  - `TypeNames`: OpenAI-specific type names (ChatClient, OpenAIClient, IChatClientProvider, etc.)
  - `MethodNames`: Common method names used in analysis (GetChatClient, GetAvailableTools, etc.)
  - `TenantWorkerIds`: Tenant/worker ID patterns for detection
  - `HelpLinkBase`: Base URL for help documentation

### Diagnostic Creation
- **DiagnosticDescriptorFactory.cs**: Centralized diagnostic descriptor creation
  - Consistent help link generation
  - Standardized message formats with fix guidance
  - Multi-rule analyzer support via `GetOpenAIAccessDiagnostics()` and `GetAllOpenAIDiagnostics()`

### Common Utilities
- **SyntaxAnalysisHelpers.cs**: Shared syntax analysis utilities for detecting OpenAI-specific patterns

### Diagnostic Categories
- **Governance**: Multi-tenant isolation, security, and compliance (A365OAI0001, A365OAI0002, A365OAI0004, A365OAI0006, A365OAI0009, A365OAI0010, A365OAI0011)
- **Usage**: Lifecycle management and proper API usage (A365OAI0005, A365OAI0008)

## Best Practices for Analyzer Development

### 1. Follow the Constants Pattern
- Always use `AnalyzerConstants` instead of hardcoded strings
- Add new constants to appropriate sections
- Use `nameof()` for compile-time safety where applicable

### 2. Consistent Diagnostic Creation
- Use `DiagnosticDescriptorFactory` for all descriptors
- Include fix guidance in message format with numbered steps
- Append `AnalyzerConstants.GuidanceSuffix` to user messages
- Provide detailed descriptions explaining governance principles

### 3. File Organization
- One analyzer per file with matching filename
- Place related analyzers in logical groups
- Place code fix providers next to their analyzers
- Mirror directory structure in test project

### 4. Testing Standards
- Extend `AnalyzerTestBase<T>` for consistent test infrastructure
- Test analyzer metadata correctness
- Test positive cases (detects problematic code)
- Test negative cases (no false positives)
- Include integration tests for multi-analyzer scenarios

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
- **Base URL**: `https://github.com/microsoft/Agent365/tree/main/docs/analyzers`
- **Format**: Each analyzer has dedicated documentation page

## Usage Examples

### Before (Problematic Code)
```csharp
// ❌ A365OAI0002: Direct OpenAIClient access
public class MyAgent 
{
    private readonly OpenAIClient _openAIClient;
    
    public MyAgent(OpenAIClient openAIClient) // Direct injection
    {
        _openAIClient = openAIClient;
    }
}

// ❌ A365OAI0001: Direct ChatClient access
public class WeatherAgent
{
    private readonly ChatClient _chatClient;
    
    public WeatherAgent(ChatClient chatClient) // Direct injection
    {
        _chatClient = chatClient;
    }
}

// ❌ A365OAI0004: Direct tenant ID access
var tenantId = context.User.FindFirst("tenant_id")?.Value;
var workerId = context.Request.Headers["X-Worker-Id"].FirstOrDefault();

// ❌ A365OAI0006: Direct function creation
var weatherTool = ChatTool.CreateFunctionTool("GetWeather", "Gets weather data"); // No tenant context

// ❌ A365OAI0008: Direct client registration
services.AddSingleton<ChatClient>(provider => new ChatClient()); // Bypasses governance

// ❌ A365OAI0009: Hardcoded tenant/worker IDs
var client = provider.GetChatClient("hardcoded-tenant", "hardcoded-worker"); // Security risk

// ❌ A365OAI0010: Shared storage across tenants
private static readonly Dictionary<string, object> _sharedCache = new(); // Data leakage risk
```

### After (Corrected Code)
```csharp
// ✅ Proper IChatClientProvider usage
public class MyAgent 
{
    private readonly IChatClientProvider _chatClientProvider;
    private readonly IOpenAIFunctionProvider _functionProvider;
    
    public MyAgent(IChatClientProvider chatClientProvider, IOpenAIFunctionProvider functionProvider)
    {
        _chatClientProvider = chatClientProvider;
        _functionProvider = functionProvider;
    }
    
    public async Task ProcessAsync(HttpContext context)
    {
        var tenantId = TenantContextHelper.GetTenantId(context);
        var workerId = TenantContextHelper.GetWorkerId(context);
        var client = await _chatClientProvider.GetChatClient(tenantId, workerId);
        // Use client for tenant-isolated operations
    }
}

// ✅ Proper provider-based agent construction
public class WeatherAgent
{
    private readonly IChatClientProvider _chatClientProvider;
    
    public WeatherAgent(IChatClientProvider chatClientProvider)
    {
        _chatClientProvider = chatClientProvider;
    }
}

// ✅ Centralized tenant context access
using Microsoft.Agents.A365.Runtime.Common.AspNetCore;
var tenantId = TenantContextHelper.GetTenantId(context);
var workerId = TenantContextHelper.GetWorkerId(context);

// ✅ Tenant-aware function creation
var tools = await functionProvider.GetAvailableTools(tenantId, workerId);
var result = await functionProvider.ExecuteFunctionAsync(tenantId, workerId, "GetWeather", args);

// ✅ Proper provider registration
services.AddSingleton<IChatClientProvider>(provider => 
{
    // Delegate-based factory with governance controls
    return new ChatClientProvider(/* proper configuration */);
});

// ✅ Dynamic tenant/worker ID extraction
var tenantId = TenantContextHelper.GetTenantId(context);
var workerId = TenantContextHelper.GetWorkerId(context);
var client = await provider.GetChatClient(tenantId, workerId);

// ✅ Tenant-scoped storage
private readonly ConcurrentDictionary<string, object> _cache = new();

public void StoreData(string tenantId, string workerId, object data)
{
    var key = $"{tenantId}:{workerId}:data";
    _cache[key] = data; // Tenant-isolated storage
}
```

## Quick Reference for Developers

### Common Analyzer Patterns

#### Detecting Method Calls
```csharp
// Check for specific method calls like GetRequiredService<T>()
private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
{
    var invocation = (InvocationExpressionSyntax)context.Node;
    var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
    
    if (symbolInfo.Symbol is IMethodSymbol method && 
        method.Name == AnalyzerConstants.MethodNames.GetRequiredService)
    {
        // Check if it's requesting ChatClient or OpenAIClient
        var typeArg = method.TypeArguments.FirstOrDefault();
        if (typeArg?.Name == AnalyzerConstants.TypeNames.ChatClient ||
            typeArg?.Name == AnalyzerConstants.TypeNames.OpenAIClient)
        {
            // Report diagnostic
        }
    }
}
```

#### Detecting Type Usage
```csharp
// Check for field/property declarations of specific types
private static void AnalyzeField(SyntaxNodeAnalysisContext context)
{
    var field = (FieldDeclarationSyntax)context.Node;
    var typeInfo = context.SemanticModel.GetTypeInfo(field.Declaration.Type);
    
    if (typeInfo.Type?.Name == AnalyzerConstants.TypeNames.ChatClient ||
        typeInfo.Type?.Name == AnalyzerConstants.TypeNames.OpenAIClient)
    {
        // Report diagnostic for direct client storage
    }
}
```

#### Detecting Constructor Parameters
```csharp
// Check for constructor parameters with direct client types
private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
{
    var constructor = (ConstructorDeclarationSyntax)context.Node;
    
    foreach (var parameter in constructor.ParameterList.Parameters)
    {
        var paramType = context.SemanticModel.GetTypeInfo(parameter.Type!);
        if (paramType.Type?.Name == AnalyzerConstants.TypeNames.ChatClient ||
            paramType.Type?.Name == AnalyzerConstants.TypeNames.OpenAIClient)
        {
            // Report diagnostic for direct client injection
        }
    }
}
```

### Testing Patterns

#### Basic Analyzer Test
```csharp
[Fact]
public async Task AnalyzeProblematicCode_ReportsExpectedDiagnostic()
{
    const string testCode = @"
using OpenAI.Chat;

public class TestClass
{
    private readonly ChatClient _chatClient;
    
    public TestClass(ChatClient chatClient)
    {
        _chatClient = chatClient;
    }
}";

    var expected = new DiagnosticResult(DiagnosticDescriptorFactory.ChatClientDirectAccess)
        .WithSpan(8, 21, 8, 31) // Constructor parameter location
        .WithArguments("ChatClient");
        
    await VerifyAnalyzerAsync(testCode, expected);
}
```

#### Code Fix Test
```csharp
[Fact]
public async Task CodeFix_ReplacesDirectClientWithProvider()
{
    const string testCode = @"
using OpenAI.Chat;

public class TestClass
{
    private readonly ChatClient _chatClient;
    
    public TestClass(ChatClient chatClient)
    {
        _chatClient = chatClient;
    }
}";

    const string fixedCode = @"
using OpenAI.Chat;
using Microsoft.Agents.A365.Runtime.OpenAI;

public class TestClass
{
    private readonly IChatClientProvider _chatClientProvider;
    
    public TestClass(IChatClientProvider chatClientProvider)
    {
        _chatClientProvider = chatClientProvider;
    }
}";

    await VerifyCodeFixAsync(testCode, fixedCode);
}
```

## Troubleshooting

### Common Issues
1. **False Positives**: Ensure semantic model checks for exact type matches
2. **Missing Diagnostics**: Verify syntax kind registration matches target constructs
3. **Performance**: Use concurrent execution and avoid expensive operations in tight loops
4. **Code Fixes**: Ensure fix providers handle all edge cases and maintain code formatting

### Debugging Tips
- Use `DiagnosticAnalyzer.SupportedDiagnostics` to verify rule registration
- Test with minimal repro cases before complex scenarios
- Use semantic model carefully - always check for null
- Validate fix providers don't introduce compilation errors

## Contributing

When contributing new analyzers:
1. Follow the established patterns and naming conventions
2. Add comprehensive tests for both positive and negative cases
3. Update this documentation with new analyzer details
4. Ensure help links are properly configured
5. Test integration with existing analyzers

*For questions or contributions, see the project repository documentation.*