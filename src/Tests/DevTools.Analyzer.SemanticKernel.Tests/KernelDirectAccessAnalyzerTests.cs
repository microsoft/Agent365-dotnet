using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Constants;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Tests.Common;
using System.Threading.Tasks;
using Xunit;
using System.Linq;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Tests;

/// <summary>
/// Unit tests for KernelDirectAccessAnalyzer.
/// Tests ensure proper detection of direct Kernel access patterns and unsafe plugin imports.
/// </summary>
public class KernelDirectAccessAnalyzerTests : AnalyzerTestBase<KernelDirectAccessAnalyzer>
{
    #region Core Analyzer Configuration Tests

    [Fact]
    public void Analyzer_HasCorrectDiagnosticIds()
    {
        var analyzer = CreateAnalyzer();
        
        Assert.Equal(2, analyzer.SupportedDiagnostics.Length);
        
        var diagnosticIds = analyzer.SupportedDiagnostics.Select(d => d.Id).ToArray();
        Assert.Contains(AnalyzerConstants.DiagnosticIds.KernelDirectAccess, diagnosticIds);
        Assert.Contains(AnalyzerConstants.DiagnosticIds.UnsafePluginImport, diagnosticIds);
    }

    [Fact]
    public void Analyzer_IsProperlyConfigured()
    {
        var analyzer = CreateAnalyzer();
        
        foreach (var diagnostic in analyzer.SupportedDiagnostics)
        {
            Assert.True(diagnostic.Id == AnalyzerConstants.DiagnosticIds.KernelDirectAccess || 
                       diagnostic.Id == AnalyzerConstants.DiagnosticIds.UnsafePluginImport);
            Assert.Equal(AnalyzerConstants.Categories.Governance, diagnostic.Category);
            Assert.Equal(AnalyzerConstants.DefaultSeverity, diagnostic.DefaultSeverity);
            Assert.True(diagnostic.IsEnabledByDefault);
        }
    }

    [Fact]
    public void Analyzer_UsesConfigurationConstants()
    {
        var analyzer = CreateAnalyzer();
        
        // Verify diagnostic IDs come from constants (no hardcoded strings)
        Assert.Equal(AnalyzerConstants.DiagnosticIds.KernelDirectAccess, KernelDirectAccessAnalyzer.DiagnosticId);
        Assert.Equal(AnalyzerConstants.DiagnosticIds.UnsafePluginImport, KernelDirectAccessAnalyzer.UnsafeImportId);
        
        // Verify all diagnostics have proper help links
        foreach (var diagnostic in analyzer.SupportedDiagnostics)
        {
            Assert.NotNull(diagnostic.HelpLinkUri);
            Assert.Contains(AnalyzerConstants.HelpLinkBase, diagnostic.HelpLinkUri);
            Assert.Contains(diagnostic.Id, diagnostic.HelpLinkUri);
        }
    }

    #endregion

    #region Diagnostic Message Quality Tests

    [Fact]
    public void KernelDirectAccess_DiagnosticMessage_ContainsRequiredGuidance()
    {
        var analyzer = CreateAnalyzer();
        var diagnostic = analyzer.SupportedDiagnostics.First(d => d.Id == AnalyzerConstants.DiagnosticIds.KernelDirectAccess);
        var message = diagnostic.MessageFormat.ToString();
        
        // Test essential concepts are present (not specific formatting)
        Assert.Contains("IKernelProvider", message);
        Assert.Contains("GetKernel", message);
        Assert.Contains("tenantId", message);
        Assert.Contains("workerId", message);
        Assert.Contains(AnalyzerConstants.GuidanceSuffix, message);
        
        // Verify message is actionable
        Assert.True(message.Contains("Use") || message.Contains("instead") || message.Contains("Replace"));
    }

    [Fact]
    public void UnsafePluginImport_DiagnosticMessage_ContainsRequiredGuidance()
    {
        var analyzer = CreateAnalyzer();
        var diagnostic = analyzer.SupportedDiagnostics.First(d => d.Id == AnalyzerConstants.DiagnosticIds.UnsafePluginImport);
        var message = diagnostic.MessageFormat.ToString();
        
        // Test essential concepts are present (not specific formatting)
        Assert.Contains("TryImportPluginFromObject", message);
        Assert.Contains("ImportPluginFromObject", message);
        Assert.Contains("Extensions", message);
        Assert.Contains(AnalyzerConstants.GuidanceSuffix, message);
        
        // Verify message explains the problem
        Assert.True(message.Contains("prevent") || message.Contains("exception") || message.Contains("safe"));
    }

    [Fact]
    public void AllDiagnostics_FollowQualityStandards()
    {
        var analyzer = CreateAnalyzer();
        
        foreach (var diagnostic in analyzer.SupportedDiagnostics)
        {
            // Verify consistent structure from constants
            Assert.Equal(AnalyzerConstants.Categories.Governance, diagnostic.Category);
            Assert.Equal(AnalyzerConstants.DefaultSeverity, diagnostic.DefaultSeverity);
            Assert.True(diagnostic.IsEnabledByDefault);
            
            // Verify content quality
            Assert.NotNull(diagnostic.Title);
            Assert.True(diagnostic.Title.ToString().Length > 10, "Title should be meaningful");
            
            Assert.NotNull(diagnostic.Description);
            Assert.True(diagnostic.Description.ToString().Length > 20, "Description should be substantial");
            
            var message = diagnostic.MessageFormat.ToString();
            Assert.True(message.Length > 50, "Message should be detailed");
            Assert.Contains(AnalyzerConstants.GuidanceSuffix, message);
            
            // Verify help link format
            Assert.NotNull(diagnostic.HelpLinkUri);
            Assert.StartsWith(AnalyzerConstants.HelpLinkBase, diagnostic.HelpLinkUri);
            Assert.EndsWith($"{diagnostic.Id}.md", diagnostic.HelpLinkUri);
        }
    }

    #endregion

    // Note: Syntax analysis logic is tested in EssentialValidationTests
}
