# Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel

This package contains custom Roslyn analyzers for enforcing Microsoft Agents A365 SDK compliance and governance in Semantic Kernel-based agent projects.

## Included Rules
- A365SK0001: Direct Kernel access or storage is not allowed
- A365SK0002: Kernel retrieval before builder.Build() and duplicate agent registration
- A365SK0003: Chat completion service registration enforcement
- A365SK0004: Tenant/worker ID access enforcement

See AnalyzerDocumentation.md for details on rule enforcement and usage.

## 📦 Creating a Local NuGet Package

To build and generate a local NuGet package for this project:

1. **Ensure prerequisites:**
   - .NET SDK 8.0 or later installed
   - This repository cloned locally

2. **Build and pack the project:**
   - Open a terminal in the project directory:
     ```pwsh
     cd ./Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel
     dotnet build -c Release
     ```
   - The build will automatically generate the NuGet package in `nupkgs/`.

3. **Verify the package:**
   - Check for the `.nupkg` file in:
     ```
     ./Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel/nupkgs/
     ```

4. **Consume the package locally:**
   - In your agent project, add the following to your `.csproj`:
     ```xml
     <PropertyGroup>
       <RestoreSources>$(RestoreSources);../Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel/nupkgs</RestoreSources>
     </PropertyGroup>
     <ItemGroup>
       <PackageReference Include="Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel" Version="1.0.0" />
     </ItemGroup>
     ```
   - Run `dotnet restore` in your agent project to use the local package.


## Local Development Note
If you want analyzer diagnostics to run immediately during development, add the analyzer DLL directly in your agent project's `.csproj`:

```xml
<ItemGroup>
  <Analyzer Include="..\Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel\bin\Debug\netstandard2.0\Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.dll" />
</ItemGroup>
```
This ensures analyzers run on every build, even if the NuGet package is not published or restored.

## Troubleshooting
- If the package is not generated, ensure `<IsPackable>true</IsPackable>` and `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` are set in the `.csproj`.
- If you change the version, rebuild to update the `.nupkg` file.

## License
Copyright (c) Microsoft. All rights reserved.
