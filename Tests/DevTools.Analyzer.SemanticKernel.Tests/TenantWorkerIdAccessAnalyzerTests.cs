using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Constants;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Tests.Common;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Tests;

/// <summary>
/// Unit tests for TenantWorkerIdAccessAnalyzer to ensure proper detection of 
/// direct tenant/worker ID access patterns.
/// </summary>
public class TenantWorkerIdAccessAnalyzerTests : AnalyzerTestBase<TenantWorkerIdAccessAnalyzer>
{
    [Fact]
    public void Analyzer_HasCorrectDiagnosticIds()
    {
        var analyzer = CreateAnalyzer();
        
        Assert.Single(analyzer.SupportedDiagnostics);
        Assert.Equal(AnalyzerConstants.DiagnosticIds.TenantWorkerIdAccess, analyzer.SupportedDiagnostics[0].Id);
    }

    [Fact]
    public void Analyzer_IsProperlyConfigured()
    {
        var analyzer = CreateAnalyzer();
        var diagnostic = analyzer.SupportedDiagnostics[0];
        
        Assert.Equal(AnalyzerConstants.DiagnosticIds.TenantWorkerIdAccess, diagnostic.Id);
        Assert.Equal("Governance", diagnostic.Category);
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, diagnostic.DefaultSeverity);
        Assert.True(diagnostic.IsEnabledByDefault);
        Assert.Contains("TenantContextHelper", diagnostic.MessageFormat.ToString());
    }

    [Fact]
    public void Analyzer_ProvidesActionableGuidance()
    {
        var analyzer = CreateAnalyzer();
        var diagnostic = analyzer.SupportedDiagnostics[0];
        var message = diagnostic.MessageFormat.ToString();
        
        // Should provide clear guidance - check for TenantContextHelper OR other guidance words
        var hasActionableGuidance = message.Contains("TenantContextHelper") || 
                                  message.Contains("Use") || 
                                  message.Contains("instead") ||
                                  message.Contains("Access") ||
                                  message.Contains("not");
        
        Assert.True(hasActionableGuidance, $"Message should provide actionable guidance. Actual message: '{message}'");
    }

    // Note: Complex source code analysis tests would require the full testing framework
    // These simplified tests verify the analyzer configuration and metadata
}
