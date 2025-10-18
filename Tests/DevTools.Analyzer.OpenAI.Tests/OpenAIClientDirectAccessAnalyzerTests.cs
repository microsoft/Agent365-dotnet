using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Constants;
using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Tests.Common;
using System.Threading.Tasks;
using Xunit;
using System.Linq;

namespace Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Tests;

/// <summary>
/// Unit tests for OpenAIClientDirectAccessAnalyzer.
/// Tests ensure proper detection of direct OpenAI client access patterns.
/// </summary>
public class OpenAIClientDirectAccessAnalyzerTests : AnalyzerTestBase<OpenAIClientDirectAccessAnalyzer>
{
    #region Core Analyzer Configuration Tests

    [Fact]
    public void Analyzer_HasCorrectDiagnosticIds()
    {
        var analyzer = CreateAnalyzer();
        
        Assert.Equal(2, analyzer.SupportedDiagnostics.Length);
        
        var diagnosticIds = analyzer.SupportedDiagnostics.Select(d => d.Id).ToArray();
        Assert.Contains(AnalyzerConstants.DiagnosticIds.ChatClientDirectAccess, diagnosticIds);
        Assert.Contains(AnalyzerConstants.DiagnosticIds.OpenAIClientDirectAccess, diagnosticIds);
    }

    [Fact]
    public void Analyzer_IsProperlyConfigured()
    {
        var analyzer = CreateAnalyzer();
        
        foreach (var diagnostic in analyzer.SupportedDiagnostics)
        {
            Assert.True(diagnostic.Id == AnalyzerConstants.DiagnosticIds.ChatClientDirectAccess || 
                       diagnostic.Id == AnalyzerConstants.DiagnosticIds.OpenAIClientDirectAccess);
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
        Assert.Equal(AnalyzerConstants.DiagnosticIds.ChatClientDirectAccess, OpenAIClientDirectAccessAnalyzer.ChatClientDiagnosticId);
        Assert.Equal(AnalyzerConstants.DiagnosticIds.OpenAIClientDirectAccess, OpenAIClientDirectAccessAnalyzer.OpenAIClientDiagnosticId);
        
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
    public void ChatClientDirectAccess_DiagnosticMessage_ContainsRequiredGuidance()
    {
        var analyzer = CreateAnalyzer();
        var diagnostic = analyzer.SupportedDiagnostics.First(d => d.Id == AnalyzerConstants.DiagnosticIds.ChatClientDirectAccess);
        var message = diagnostic.MessageFormat.ToString();
        
        // Test essential concepts are present (not specific formatting)
        Assert.Contains("IChatClientProvider", message);
        Assert.Contains("GetChatClient", message);
        Assert.Contains("tenantId", message);
        Assert.Contains("workerId", message);
        Assert.Contains(AnalyzerConstants.GuidanceSuffix, message);
        
        // Verify message is actionable
        Assert.True(message.Contains("Use") || message.Contains("instead") || message.Contains("Replace"));
    }

    [Fact]
    public void OpenAIClientDirectAccess_DiagnosticMessage_ContainsRequiredGuidance()
    {
        var analyzer = CreateAnalyzer();
        var diagnostic = analyzer.SupportedDiagnostics.First(d => d.Id == AnalyzerConstants.DiagnosticIds.OpenAIClientDirectAccess);
        var message = diagnostic.MessageFormat.ToString();
        
        // Test essential concepts are present (not specific formatting)
        Assert.Contains("IChatClientProvider", message);
        Assert.Contains("GetChatClient", message);
        Assert.Contains("tenantId", message);
        Assert.Contains("workerId", message);
        Assert.Contains(AnalyzerConstants.GuidanceSuffix, message);
        
        // Verify message is actionable
        Assert.True(message.Contains("Use") || message.Contains("instead") || message.Contains("Replace"));
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

    #region Analyzer Metadata Tests

    [Fact]
    public void Analyzer_MetadataIsComplete()
    {
        var analyzer = CreateAnalyzer();
        
        AssertAllDescriptors(analyzer);
    }

    [Fact] 
    public void Analyzer_DiagnosticIds_FollowConvention()
    {
        var analyzer = CreateAnalyzer();
        
        foreach (var diagnostic in analyzer.SupportedDiagnostics)
        {
            // Verify diagnostic IDs follow A365OAI#### pattern
            Assert.StartsWith("A365OAI", diagnostic.Id);
            Assert.Equal(11, diagnostic.Id.Length); // A365OAI + 4 digits
            
            // Verify the number part is valid
            var numberPart = diagnostic.Id.Substring(7);
            Assert.True(int.TryParse(numberPart, out var number));
            Assert.True(number >= 1 && number <= 9999);
        }
    }

    [Fact]
    public void Analyzer_Categories_AreConsistent()
    {
        var analyzer = CreateAnalyzer();
        
        foreach (var diagnostic in analyzer.SupportedDiagnostics)
        {
            // All OpenAI diagnostics should be Governance category
            Assert.Equal(AnalyzerConstants.Categories.Governance, diagnostic.Category);
        }
    }

    #endregion

    // Note: Specific syntax detection logic is tested in SyntaxAnalysisHelpersTests
    // Integration tests would require more complex test infrastructure for full compilation testing
}