using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.ChatCompletionService;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Constants;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Tests.Common;
using Xunit;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Tests.ChatCompletionService
{
    /// <summary>
    /// Unit tests for ChatCompletionServiceRegistrationAnalyzer.
    /// Tests detection of direct chat completion service registrations.
    /// </summary>
    public class ChatCompletionServiceRegistrationAnalyzerTests : AnalyzerTestBase<ChatCompletionServiceRegistrationAnalyzer>
    {
        [Fact]
        public void Analyzer_HasCorrectConfiguration()
        {
            var analyzer = CreateAnalyzer();
            
            Assert.Single(analyzer.SupportedDiagnostics);
            Assert.Equal(AnalyzerConstants.DiagnosticIds.ChatCompletionServiceRegistration, analyzer.SupportedDiagnostics[0].Id);
            Assert.Equal("Governance", analyzer.SupportedDiagnostics[0].Category);
        }

        [Fact]
        public void Analyzer_UsesConfigurationConstants()
        {
            var analyzer = CreateAnalyzer();
            
            // Verify diagnostic ID comes from constants (no hardcoded strings)
            Assert.Equal(AnalyzerConstants.DiagnosticIds.ChatCompletionServiceRegistration, 
                        ChatCompletionServiceRegistrationAnalyzer.DiagnosticId);
        }
    }
}