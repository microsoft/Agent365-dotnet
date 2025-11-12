## Building and Testing with Local Analyzer NuGet Package

To build the analyzer NuGet package and test governance failures in AnalyzerDemoApp on a new machine:

1. **Restore and build the analyzer project:**
	```pwsh
	dotnet restore ../../Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel/Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.csproj
	dotnet build ../../Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel/Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.csproj -c Release
	```

2. **Pack the analyzer into a NuGet package:**
	```pwsh
	dotnet pack ../../Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel/Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.csproj -c Release -o ../../artifacts
	```

3. **Build AnalyzerDemoApp (which references the local NuGet package):**
	```pwsh
	dotnet restore AnalyzerDemoApp.csproj
	dotnet build AnalyzerDemoApp.csproj -c Release
	```

This will surface all analyzer failures in the build output. Ensure the `artifacts` folder is present and referenced in the `.csproj` as a NuGet source.
# Analyzer Demo Application

This is a **full-fledged ASP.NET Core application** that deliberately contains realistic analyzer violations. Unlike simple code snippets, this demonstrates how violations might actually occur in real-world applications.

## ?? Purpose

1. **Real-World Testing** - Tests analyzers against complete, realistic application patterns
2. **Visual Studio Integration** - Verify analyzer behavior in actual IDE environment  
3. **Documentation** - Shows developers realistic examples of what NOT to do
4. **Regression Testing** - Catches analyzer issues in complete build scenarios
5. **Training Material** - Helps teams understand governance violations in context

## ?? Violations Demonstrated

### **A365SK0001 - Kernel Retrieval Before Build**
- `GetRequiredService<Kernel>()` calls in service registration
- Agent instantiation before `builder.Build()`
- Premature kernel access patterns

### **A365SK0002 - Direct Kernel Access**
- Direct `Kernel` injection in controllers and agents
- `context.RequestServices.GetRequiredService<Kernel>()` calls
- Kernel fields in classes that should use `IKernelProvider`

### **A365SK0003 - Unsafe Plugin Import**
- `kernel.ImportPluginFromObject()` calls in startup
- Plugin imports in controllers and services
- Should use `kernel.TryImportPluginFromObject()` instead

### **A365SK0004 - Tenant/Worker ID Access**
- Direct `context.User.FindFirst("tenant_id")` calls
- Direct header access `context.Request.Headers["X-Worker-Id"]`
- Direct Items access `context.Items["worker_id"]`

### **A365SK0005 - Chat Completion Service Registration**
- Direct chat completion service registration
- Non-governance-approved service registration patterns
- Should use governance-approved delegates for service registration

## ?? How to Use for Testing

### **?? Visual Studio 2022 Testing**

The best way to test analyzers in a realistic environment:

```bash
# Build and see analyzer violations
dotnet build AnalyzerDemoApp.csproj

# Or run the application (will show runtime governance errors)
dotnet run --project AnalyzerDemoApp.csproj
```

**What you'll see in VS2022:**
- ? **Real-time red squiggles** under problematic code
- ? **Error List** with all analyzer diagnostics  
- ? **IntelliSense warnings** as you type
- ? **Hover tooltips** with violation messages

## ?? Files Structure

- **`RealWorldAgentExamples.cs`** - Realistic agents, controllers, and services showing common patterns
- **`Program.cs`** - Typical ASP.NET Core startup configuration  
- **`AnalyzerDemoApp.csproj`** - Project file with analyzer integration
- **`README.md`** - This file

## ? Benefits Over Unit Tests

1. **Realistic Context** - Shows how violations occur in real applications
2. **Complete Build Testing** - Tests entire compilation pipeline
3. **IDE Integration** - Verifies IntelliSense and error highlighting
4. **Maintenance Scenarios** - Tests analyzer performance on larger codebases
5. **Team Training** - Provides realistic examples for education

## ?? Developer Workflow

When developing new analyzers:

1. **Add pattern examples** to `RealWorldAgentExamples.cs`
2. **Update analyzer constants** and implement the analyzer logic
3. **Add unit tests** for the new analyzer in the main test project
4. **Test in Visual Studio** using this demo application
5. **Verify IDE integration** works correctly

## ?? Expected Analyzer Violations

When you build this project, you should see numerous analyzer violations demonstrating common governance issues that developers might encounter in real-world Agent 365 applications.