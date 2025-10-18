using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Constants;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Tests.Common;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Tests;

/// <summary>
/// Unit tests for KernelRetrievalBeforeBuildAnalyzer to ensure proper detection of 
/// premature kernel retrieval and AgentApplication registration patterns.
/// </summary>
public class KernelRetrievalBeforeBuildAnalyzerTests : AnalyzerTestBase<KernelRetrievalBeforeBuildAnalyzer>
{
    [Fact]
    public void Analyzer_HasCorrectDiagnosticIds()
    {
        var analyzer = CreateAnalyzer();
        
        Assert.Single(analyzer.SupportedDiagnostics);
        Assert.Equal(AnalyzerConstants.DiagnosticIds.KernelRetrievalBeforeBuild, analyzer.SupportedDiagnostics[0].Id);
    }

    [Fact]
    public void Analyzer_IsProperlyConfigured()
    {
        var analyzer = CreateAnalyzer();
        var diagnostic = analyzer.SupportedDiagnostics[0];
        
        Assert.Equal(AnalyzerConstants.DiagnosticIds.KernelRetrievalBeforeBuild, diagnostic.Id);
        Assert.Equal("Usage", diagnostic.Category);
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, diagnostic.DefaultSeverity);
        Assert.True(diagnostic.IsEnabledByDefault);
        Assert.Contains("builder.Build()", diagnostic.MessageFormat.ToString());
        Assert.Contains("AgentApplication", diagnostic.MessageFormat.ToString());
    }

    [Fact]
    public void Analyzer_ProvidesActionableGuidance()
    {
        var analyzer = CreateAnalyzer();
        var diagnostic = analyzer.SupportedDiagnostics[0];
        var message = diagnostic.MessageFormat.ToString();
        
        // Should provide clear guidance
        Assert.True(message.Contains("not") || message.Contains("instead") || message.Contains("Use"));
        
        // Should mention specific alternatives
        Assert.True(message.Contains("builder.Build()") || message.Contains("AgentApplication"));
    }

    // Note: Complex source code analysis tests would require the full testing framework
    // These simplified tests verify the analyzer configuration and metadata
}
