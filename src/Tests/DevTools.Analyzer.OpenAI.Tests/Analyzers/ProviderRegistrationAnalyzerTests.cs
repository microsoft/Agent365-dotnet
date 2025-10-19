using Microsoft.CodeAnalysis;
using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Common;
using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Tests.Common;
using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Constants;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Tests.Analyzers
{
    /// <summary>
    /// Test class for ProviderRegistrationAnalyzer.
    /// </summary>
    public class ProviderRegistrationAnalyzerTests : AnalyzerTestBase<ProviderRegistrationAnalyzer>
    {
        [Fact]
        public void Analyzer_HasCorrectDiagnosticIds()
        {
            var analyzer = CreateAnalyzer();
            
            Assert.Single(analyzer.SupportedDiagnostics);
            
            var diagnosticId = analyzer.SupportedDiagnostics.First().Id;
            Assert.Equal(AnalyzerConstants.DiagnosticIds.ProviderRegistrationValidation, diagnosticId);
        }

        [Fact]
        public void Analyzer_IsProperlyConfigured()
        {
            var analyzer = CreateAnalyzer();
            AssertAllDescriptors(analyzer);
        }

        [Fact]
        public void Descriptor_HasCorrectMetadata()
        {
            var descriptor = DiagnosticDescriptorFactory.ProviderRegistrationValidation;
            AssertDescriptorMetadata(descriptor);
        }
    }
}