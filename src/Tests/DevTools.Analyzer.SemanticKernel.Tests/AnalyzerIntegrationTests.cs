using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Constants;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Tests;

/// <summary>
/// Integration tests that verify multiple analyzers working together on complex scenarios.
/// These tests focus on the integration concepts rather than complex source analysis.
/// </summary>
public class AnalyzerIntegrationTests
{
    [Fact]
    public void AllAnalyzers_HaveDistinctDiagnosticIds()
    {
        // Test that all analyzer diagnostic IDs are unique across the system
        var analyzers = new DiagnosticAnalyzer[]
        {
            new KernelDirectAccessAnalyzer(),
            new KernelRetrievalBeforeBuildAnalyzer(),
            new TenantWorkerIdAccessAnalyzer()
        };

        var allDiagnosticIds = analyzers
            .SelectMany(a => a.SupportedDiagnostics)
            .Select(d => d.Id)
            .ToList();

        var uniqueIds = allDiagnosticIds.Distinct().ToList();
        
        Assert.Equal(allDiagnosticIds.Count, uniqueIds.Count);
        
        // Verify expected IDs are present
        Assert.Contains(AnalyzerConstants.DiagnosticIds.KernelDirectAccess, uniqueIds); // KernelDirectAccessAnalyzer main rule
        Assert.Contains(AnalyzerConstants.DiagnosticIds.UnsafePluginImport, uniqueIds); // KernelDirectAccessAnalyzer unsafe import rule
        Assert.Contains(AnalyzerConstants.DiagnosticIds.KernelRetrievalBeforeBuild, uniqueIds); // KernelRetrievalBeforeBuildAnalyzer
        Assert.Contains(AnalyzerConstants.DiagnosticIds.TenantWorkerIdAccess, uniqueIds); // TenantWorkerIdAccessAnalyzer
    }

    [Fact]
    public void MultipleAnalyzers_CanBeInitializedTogether()
    {
        // Test that all analyzers can be instantiated and initialized without conflicts
        var analyzers = new DiagnosticAnalyzer[]
        {
            new KernelDirectAccessAnalyzer(),
            new KernelRetrievalBeforeBuildAnalyzer(),
            new TenantWorkerIdAccessAnalyzer()
        };

        foreach (var analyzer in analyzers)
        {
            // Verify each analyzer is properly configured
            Assert.NotNull(analyzer);
            Assert.True(analyzer.SupportedDiagnostics.Length > 0);
            
            // Verify each analyzer's diagnostics are properly configured
            foreach (var diagnostic in analyzer.SupportedDiagnostics)
            {
                Assert.False(string.IsNullOrEmpty(diagnostic.Id));
                Assert.Equal(DiagnosticSeverity.Error, diagnostic.DefaultSeverity);
                Assert.True(diagnostic.IsEnabledByDefault);
            }
        }
    }

    [Fact]
    public void AnalyzerCombination_CoversAllGovernanceAreas()
    {
        // Test that the combination of analyzers covers all expected governance areas
        var kernelDirectAnalyzer = new KernelDirectAccessAnalyzer();
        var kernelRetrievalAnalyzer = new KernelRetrievalBeforeBuildAnalyzer();
        var tenantAnalyzer = new TenantWorkerIdAccessAnalyzer();

        // Verify KernelDirectAccessAnalyzer covers direct access patterns
        var kernelDirectRules = kernelDirectAnalyzer.SupportedDiagnostics;
        Assert.Contains(kernelDirectRules, d => d.Id == AnalyzerConstants.DiagnosticIds.KernelDirectAccess); // Direct access rule
        Assert.Contains(kernelDirectRules, d => d.Id == AnalyzerConstants.DiagnosticIds.UnsafePluginImport); // Unsafe import rule

        // Verify KernelRetrievalBeforeBuildAnalyzer covers MyAgent constructor patterns
        var kernelRetrievalRules = kernelRetrievalAnalyzer.SupportedDiagnostics;
        Assert.Contains(kernelRetrievalRules, d => d.Id == AnalyzerConstants.DiagnosticIds.KernelRetrievalBeforeBuild); // MyAgent constructor rule

        // Verify TenantWorkerIdAccessAnalyzer covers tenant access patterns
        var tenantRules = tenantAnalyzer.SupportedDiagnostics;
        Assert.Contains(tenantRules, d => d.Id == AnalyzerConstants.DiagnosticIds.TenantWorkerIdAccess); // Tenant access rule

        // Verify rule messages contain expected governance guidance
        var allRules = kernelDirectRules.Concat(kernelRetrievalRules).Concat(tenantRules);
        
        var hasKernelProviderGuidance = allRules.Any(r => r.MessageFormat.ToString().Contains("KernelProvider"));
        var hasTenantHelperGuidance = allRules.Any(r => r.MessageFormat.ToString().Contains("TenantContextHelper"));
        var hasSafeImportGuidance = allRules.Any(r => r.MessageFormat.ToString().Contains("TryImportPluginFromObject"));

        Assert.True(hasKernelProviderGuidance, "Should provide KernelProvider guidance");
        Assert.True(hasTenantHelperGuidance, "Should provide TenantContextHelper guidance");
        Assert.True(hasSafeImportGuidance, "Should provide safe import guidance");
    }

    [Fact]
    public void GovernanceRules_HaveCorrectCategories()
    {
        // Test that governance rules are properly categorized
        var analyzers = new DiagnosticAnalyzer[]
        {
            new KernelDirectAccessAnalyzer(),
            new KernelRetrievalBeforeBuildAnalyzer(),
            new TenantWorkerIdAccessAnalyzer()
        };

        var allDiagnostics = analyzers.SelectMany(a => a.SupportedDiagnostics).ToList();
        
        // Governance rules should use appropriate categories
        var governanceRules = allDiagnostics.Where(d => 
            d.Id == AnalyzerConstants.DiagnosticIds.KernelDirectAccess ||
            d.Id == AnalyzerConstants.DiagnosticIds.UnsafePluginImport ||
            d.Id == AnalyzerConstants.DiagnosticIds.TenantWorkerIdAccess).ToList();
        var usageRules = allDiagnostics.Where(d => 
            d.Id == AnalyzerConstants.DiagnosticIds.KernelRetrievalBeforeBuild).ToList();
        
        Assert.True(governanceRules.Count >= 3, "Should have at least 3 governance rules");
        Assert.True(usageRules.Count >= 1, "Should have at least 1 usage rule");
        
        // Check that governance rules use "Governance" category
        foreach (var rule in governanceRules)
        {
            Assert.Equal("Governance", rule.Category);
        }
        
        // Check that usage rules use "Usage" category
        foreach (var rule in usageRules)
        {
            Assert.Equal("Usage", rule.Category);
        }
    }

    [Fact]
    public void ComplexScenario_ConceptualValidation()
    {
        // This test verifies the conceptual coverage of the governance rules
        // without requiring complex source code analysis framework setup
        
        var scenarios = new[]
        {
            new { Description = "Direct Kernel injection", CoveredBy = "A365SK0002" },
            new { Description = "Kernel field storage", CoveredBy = "A365SK0002" },
            new { Description = "Unsafe plugin import", CoveredBy = "A365SK0003" },
            new { Description = "MyAgent with Kernel constructor", CoveredBy = "A365SK0001" },
            new { Description = "Direct tenant ID access", CoveredBy = "A365SK0004" }
        };

        var analyzers = new DiagnosticAnalyzer[]
        {
            new KernelDirectAccessAnalyzer(),
            new KernelRetrievalBeforeBuildAnalyzer(),
            new TenantWorkerIdAccessAnalyzer()
        };

        var allSupportedIds = analyzers
            .SelectMany(a => a.SupportedDiagnostics)
            .Select(d => d.Id)
            .ToHashSet();

        foreach (var scenario in scenarios)
        {
            Assert.True(allSupportedIds.Contains(scenario.CoveredBy), 
                $"Scenario '{scenario.Description}' should be covered by rule '{scenario.CoveredBy}'");
        }
    }

    [Fact]
    public void ErrorMessages_ProvideActionableGuidance()
    {
        // Test that error messages provide clear, actionable guidance
        var analyzers = new DiagnosticAnalyzer[]
        {
            new KernelDirectAccessAnalyzer(),
            new KernelRetrievalBeforeBuildAnalyzer(),
            new TenantWorkerIdAccessAnalyzer()
        };

        var allDiagnostics = analyzers.SelectMany(a => a.SupportedDiagnostics).ToList();
        
        foreach (var diagnostic in allDiagnostics)
        {
            var message = diagnostic.MessageFormat.ToString();
            var description = diagnostic.Description?.ToString() ?? "";
            
            // Should provide clear guidance (either what not to do OR what to do)
            var hasActionableGuidance = message.Contains("not") || 
                                      message.Contains("instead") || 
                                      message.Contains("Use") ||
                                      message.Contains("Access") ||
                                      message.Contains("using");
            
            Assert.True(hasActionableGuidance, 
                $"Rule {diagnostic.Id} should provide clear guidance. Message: '{message}'");
            
            // Should mention specific alternatives or solutions
            var hasSpecificGuidance = message.Contains("KernelProvider") || 
                                    message.Contains("TenantContextHelper") || 
                                    message.Contains("TryImportPluginFromObject") ||
                                    message.Contains("builder.Build()");
            
            Assert.True(hasSpecificGuidance, 
                $"Rule {diagnostic.Id} should provide specific guidance about the correct approach. Message: '{message}'");
        }
    }
}
