using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Constants;
using System.Reflection;
using Xunit;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Tests;

/// <summary>
/// Unit tests for analyzer metadata, configuration, and assembly-level properties.
/// Verifies that analyzers are properly configured and discoverable.
/// </summary>
public class AnalyzerMetadataTests
{
    [Fact]
    public void KernelDirectAccessAnalyzer_HasCorrectMetadata()
    {
        var analyzer = new KernelDirectAccessAnalyzer();
        
        Assert.Equal(2, analyzer.SupportedDiagnostics.Length);
        
        var mainRule = analyzer.SupportedDiagnostics.First(d => d.Id == KernelDirectAccessAnalyzer.DiagnosticId);
        Assert.Equal(AnalyzerConstants.DiagnosticIds.KernelDirectAccess, mainRule.Id);
        Assert.Equal("Governance", mainRule.Category);
        Assert.Equal(DiagnosticSeverity.Error, mainRule.DefaultSeverity);
        Assert.True(mainRule.IsEnabledByDefault);
        Assert.Contains("KernelProvider", mainRule.MessageFormat.ToString());
        
        var importRule = analyzer.SupportedDiagnostics.First(d => d.Id == KernelDirectAccessAnalyzer.UnsafeImportId);
        Assert.Equal(AnalyzerConstants.DiagnosticIds.UnsafePluginImport, importRule.Id);
        Assert.Equal("Governance", importRule.Category);
        Assert.Equal(DiagnosticSeverity.Error, importRule.DefaultSeverity);
        Assert.True(importRule.IsEnabledByDefault);
        Assert.Contains("TryImportPluginFromObject", importRule.MessageFormat.ToString());
        Assert.Contains("Microsoft.Agents.A365.Tools.SemanticKernel.Extensions", importRule.MessageFormat.ToString());
    }

    [Fact]
    public void KernelRetrievalBeforeBuildAnalyzer_HasCorrectMetadata()
    {
        var analyzer = new KernelRetrievalBeforeBuildAnalyzer();
        
        Assert.Single(analyzer.SupportedDiagnostics);
        
        var rule = analyzer.SupportedDiagnostics[0];
        Assert.Equal(AnalyzerConstants.DiagnosticIds.KernelRetrievalBeforeBuild, rule.Id);
        Assert.Equal("Usage", rule.Category);
        Assert.Equal(DiagnosticSeverity.Error, rule.DefaultSeverity);
        Assert.True(rule.IsEnabledByDefault);
        Assert.Contains("builder.Build()", rule.MessageFormat.ToString());
        Assert.Contains("AgentApplication", rule.MessageFormat.ToString());
    }

    [Fact]
    public void TenantWorkerIdAccessAnalyzer_HasCorrectMetadata()
    {
        var analyzer = new TenantWorkerIdAccessAnalyzer();
        
        Assert.Single(analyzer.SupportedDiagnostics);
        
        var rule = analyzer.SupportedDiagnostics[0];
        Assert.Equal(AnalyzerConstants.DiagnosticIds.TenantWorkerIdAccess, rule.Id);
        Assert.Equal("Governance", rule.Category);
        Assert.Equal(DiagnosticSeverity.Error, rule.DefaultSeverity);
        Assert.True(rule.IsEnabledByDefault);
        Assert.Contains("TenantContextHelper", rule.MessageFormat.ToString());
    }

    [Fact]
    public void AllAnalyzers_HaveUniqueIds()
    {
        var analyzers = new DiagnosticAnalyzer[]
        {
            new KernelDirectAccessAnalyzer(),
            new KernelRetrievalBeforeBuildAnalyzer(),
            new TenantWorkerIdAccessAnalyzer()
        };

        var allIds = analyzers
            .SelectMany(a => a.SupportedDiagnostics)
            .Select(d => d.Id)
            .ToList();

        var uniqueIds = allIds.Distinct().ToList();
        
        Assert.Equal(allIds.Count, uniqueIds.Count);
        Assert.Contains(AnalyzerConstants.DiagnosticIds.KernelDirectAccess, uniqueIds);
        Assert.Contains(AnalyzerConstants.DiagnosticIds.TenantWorkerIdAccess, uniqueIds);
        Assert.Contains(AnalyzerConstants.DiagnosticIds.UnsafePluginImport, uniqueIds);
        Assert.Contains(AnalyzerConstants.DiagnosticIds.KernelRetrievalBeforeBuild, uniqueIds);
    }

    [Fact]
    public void AnalyzersAssembly_HasCorrectAttributes()
    {
        var assembly = typeof(KernelDirectAccessAnalyzer).Assembly;
        
        Assert.NotNull(assembly);
        Assert.Contains("SemanticKernel", assembly.FullName);
        Assert.Contains("Analyzer", assembly.FullName);
        
        // Just verify the analyzer types we know exist (without GetTypes() which can fail with missing dependencies)
        Assert.NotNull(typeof(KernelDirectAccessAnalyzer));
        Assert.NotNull(typeof(KernelRetrievalBeforeBuildAnalyzer));
        Assert.NotNull(typeof(TenantWorkerIdAccessAnalyzer));
        
        // Verify they are analyzers
        Assert.True(typeof(KernelDirectAccessAnalyzer).IsSubclassOf(typeof(DiagnosticAnalyzer)));
        Assert.True(typeof(KernelRetrievalBeforeBuildAnalyzer).IsSubclassOf(typeof(DiagnosticAnalyzer)));
        Assert.True(typeof(TenantWorkerIdAccessAnalyzer).IsSubclassOf(typeof(DiagnosticAnalyzer)));
    }

    [Fact]
    public void DiagnosticIds_FollowNamingConvention()
    {
        var analyzers = new DiagnosticAnalyzer[]
        {
            new KernelDirectAccessAnalyzer(),
            new KernelRetrievalBeforeBuildAnalyzer(),
            new TenantWorkerIdAccessAnalyzer()
        };

        foreach (var analyzer in analyzers)
        {
            foreach (var diagnostic in analyzer.SupportedDiagnostics)
            {
                var id = diagnostic.Id;
                
                // Should follow the orchestrator-based pattern: A365 + 2 uppercase letters + 4 digits
                var pattern = System.Text.RegularExpressions.Regex.Match(id, "^A365[A-Z]{2}\\d{4}$");
                Assert.True(pattern.Success, $"Diagnostic ID '{id}' does not follow the required pattern A365<XX><NNNN>");
            }
        }
    }

    [Fact]
    public void AllAnalyzers_AreProperlyConfigured()
    {
        var analyzers = new DiagnosticAnalyzer[]
        {
            new KernelDirectAccessAnalyzer(),
            new KernelRetrievalBeforeBuildAnalyzer(),
            new TenantWorkerIdAccessAnalyzer()
        };

        foreach (var analyzer in analyzers)
        {
            // Each analyzer should have at least one supported diagnostic
            Assert.True(analyzer.SupportedDiagnostics.Length > 0);
            
            // Each diagnostic should be properly configured
            foreach (var diagnostic in analyzer.SupportedDiagnostics)
            {
                Assert.False(string.IsNullOrEmpty(diagnostic.Id));
                Assert.False(string.IsNullOrEmpty(diagnostic.Title.ToString()));
                Assert.False(string.IsNullOrEmpty(diagnostic.MessageFormat.ToString()));
                Assert.False(string.IsNullOrEmpty(diagnostic.Category));
                Assert.True(diagnostic.IsEnabledByDefault);
                Assert.Equal(DiagnosticSeverity.Error, diagnostic.DefaultSeverity);
                
                // Verify help link for traceability
                Assert.False(string.IsNullOrEmpty(diagnostic.HelpLinkUri), 
                    $"Diagnostic {diagnostic.Id} must have a help link for traceability");
                Assert.True(diagnostic.HelpLinkUri.StartsWith(AnalyzerConstants.HelpLinkBase),
                    $"Help link for {diagnostic.Id} should start with the base URL");
                Assert.True(diagnostic.HelpLinkUri.EndsWith($"{diagnostic.Id}.md"),
                    $"Help link for {diagnostic.Id} should end with the diagnostic ID and .md extension");
            }
        }
    }

    [Theory]
    [InlineData(typeof(KernelDirectAccessAnalyzer))]
    [InlineData(typeof(KernelRetrievalBeforeBuildAnalyzer))]
    [InlineData(typeof(TenantWorkerIdAccessAnalyzer))]
    public void Analyzer_HasDiagnosticAnalyzerAttribute(Type analyzerType)
    {
        var attribute = analyzerType.GetCustomAttribute<DiagnosticAnalyzerAttribute>();
        Assert.NotNull(attribute);
        Assert.Contains(LanguageNames.CSharp, attribute.Languages);
    }
}
