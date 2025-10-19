using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Common;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Tests.Common
{
    /// <summary>
    /// Base class for OpenAI analyzer test configuration to eliminate duplication.
    /// Provides consistent test setup across all analyzer tests.
    /// </summary>
    public abstract class AnalyzerTestBase<TAnalyzer> where TAnalyzer : DiagnosticAnalyzer, new()
    {
        /// <summary>
        /// Creates a diagnostic result for the analyzer with the specified location.
        /// </summary>
        /// <param name="diagnosticId">The diagnostic ID</param>
        /// <param name="location">The location marker (e.g., 0 for {|#0:|})</param>
        /// <returns>A configured DiagnosticResult</returns>
        protected static (string DiagnosticId, int Location) CreateDiagnostic(string diagnosticId, int location) =>
            (diagnosticId, location);
        
        /// <summary>
        /// Creates an analyzer instance for testing.
        /// </summary>
        /// <returns>A new analyzer instance</returns>
        protected static TAnalyzer CreateAnalyzer() => new TAnalyzer();

        protected void AssertDescriptorMetadata(DiagnosticDescriptor descriptor)
        {
            Assert.False(string.IsNullOrEmpty(descriptor.Id));
            Assert.False(string.IsNullOrEmpty(descriptor.Title?.ToString()));
            Assert.False(string.IsNullOrEmpty(descriptor.MessageFormat?.ToString()));
            Assert.False(string.IsNullOrEmpty(descriptor.Category));
            Assert.NotEqual(DiagnosticSeverity.Info, descriptor.DefaultSeverity);
            Assert.True(descriptor.IsEnabledByDefault);
            Assert.False(string.IsNullOrEmpty(descriptor.HelpLinkUri), $"Descriptor {descriptor.Id} must provide a HelpLinkUri for traceability");
        }

        protected void AssertAllDescriptors(TAnalyzer analyzer)
        {
            var descriptors = analyzer.SupportedDiagnostics;
            Assert.NotEmpty(descriptors);

            var ids = descriptors.Select(d => d.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());

            foreach (var d in descriptors)
            {
                AssertDescriptorMetadata(d);
            }
        }
    }
}