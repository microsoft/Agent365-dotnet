using Microsoft.CodeAnalysis;
using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Common;
using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Tests.Common;
using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Constants;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Tests.Analyzers
{
    /// <summary>
    /// Test class for FunctionProviderEnforcementAnalyzer.
    /// Tests detection of direct function tool creation violations.
    /// </summary>
    public class FunctionProviderEnforcementAnalyzerTests : AnalyzerTestBase<FunctionProviderEnforcementAnalyzer>
    {
        [Fact]
        public void Analyzer_HasCorrectDiagnosticIds()
        {
            var analyzer = CreateAnalyzer();
            
            Assert.Single(analyzer.SupportedDiagnostics);
            
            var diagnosticId = analyzer.SupportedDiagnostics.First().Id;
            Assert.Equal(AnalyzerConstants.DiagnosticIds.FunctionProviderEnforcement, diagnosticId);
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
            var descriptor = DiagnosticDescriptorFactory.FunctionProviderEnforcement;
            AssertDescriptorMetadata(descriptor);
        }
    }
}