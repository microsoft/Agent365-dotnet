using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Common;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Constants;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Tests.Common
{
    /// <summary>
    /// Unit tests for SyntaxAnalysisHelpers utility methods.
    /// Tests core detection logic used by multiple analyzers.
    /// </summary>
    public class SyntaxAnalysisHelpersTests
    {
        #region Type Detection Tests

        [Theory]
        [InlineData("Kernel")]
        [InlineData("Microsoft.SemanticKernel.Kernel")]
        public void IsDirectKernelType_WithKernelTypes_ReturnsTrue(string typeString)
        {
            var result = SyntaxAnalysisHelpers.IsDirectKernelType(typeString);
            
            Assert.True(result);
        }

        [Theory]
        [InlineData("IKernelProvider")]
        [InlineData("KernelProvider")]
        [InlineData("Microsoft.Agents.A365.IKernelProvider")]
        [InlineData("string")]
        [InlineData("")]
        public void IsDirectKernelType_WithNonKernelTypes_ReturnsFalse(string? typeString)
        {
            var result = SyntaxAnalysisHelpers.IsDirectKernelType(typeString ?? string.Empty);
            
            Assert.False(result);
        }

        [Fact]
        public void IsDirectKernelType_WithNullString_ReturnsFalse()
        {
            var result = SyntaxAnalysisHelpers.IsDirectKernelType(null!);
            
            Assert.False(result);
        }

        [Theory]
        [InlineData("IKernelProvider")]
        [InlineData("KernelProvider")]
        [InlineData("Microsoft.Agents.A365.IKernelProvider")]
        public void IsProviderType_WithProviderTypes_ReturnsTrue(string typeString)
        {
            var result = SyntaxAnalysisHelpers.IsProviderType(typeString);
            
            Assert.True(result);
        }

        [Theory]
        [InlineData("Kernel")]
        [InlineData("string")]
        [InlineData("")]
        public void IsProviderType_WithNonProviderTypes_ReturnsFalse(string? typeString)
        {
            var result = SyntaxAnalysisHelpers.IsProviderType(typeString ?? string.Empty);
            
            Assert.False(result);
        }

        [Fact]
        public void IsProviderType_WithNullString_ReturnsFalse()
        {
            var result = SyntaxAnalysisHelpers.IsProviderType(null!);
            
            Assert.False(result);
        }

        #endregion

        #region Invocation Detection Tests

        [Fact]
        public void IsGetRequiredServiceForKernel_WithKernelServiceCall_ReturnsTrue()
        {
            var invocation = ParseInvocation("context.RequestServices.GetRequiredService<Kernel>()");
            
            var result = SyntaxAnalysisHelpers.IsGetRequiredServiceForKernel(invocation);
            
            Assert.True(result);
        }

        [Fact]
        public void IsGetRequiredServiceForKernel_WithNonKernelServiceCall_ReturnsFalse()
        {
            var invocation = ParseInvocation("context.RequestServices.GetRequiredService<ILogger>()");
            
            var result = SyntaxAnalysisHelpers.IsGetRequiredServiceForKernel(invocation);
            
            Assert.False(result);
        }

        [Fact]
        public void IsUnsafePluginImport_WithImportPluginFromObject_ReturnsTrue()
        {
            var invocation = ParseInvocation("kernel.ImportPluginFromObject(plugin)");
            
            var result = SyntaxAnalysisHelpers.IsUnsafePluginImport(invocation);
            
            Assert.True(result);
        }

        [Fact]
        public void IsUnsafePluginImport_WithTryImportPluginFromObject_ReturnsFalse()
        {
            var invocation = ParseInvocation("kernel.TryImportPluginFromObject(plugin)");
            
            var result = SyntaxAnalysisHelpers.IsUnsafePluginImport(invocation);
            
            Assert.False(result);
        }

        #endregion

        #region Tenant/Worker ID Tests

        [Theory]
        [InlineData("tenant_id")]
        [InlineData("worker_id")]
        [InlineData("X-Tenant-Id")]
        [InlineData("X-Worker-Id")]
        public void ContainsTenantWorkerIdentifier_WithValidIdentifiers_ReturnsTrue(string identifier)
        {
            var literal = SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(identifier));
            
            var result = SyntaxAnalysisHelpers.ContainsTenantWorkerIdentifier(literal);
            
            Assert.True(result);
        }

        [Fact]
        public void ContainsTenantWorkerIdentifier_WithOtherString_ReturnsFalse()
        {
            var literal = SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal("other_value"));
            
            var result = SyntaxAnalysisHelpers.ContainsTenantWorkerIdentifier(literal);
            
            Assert.False(result);
        }

        [Fact]
        public void TenantWorkerIdDetection_MatchesConstants()
        {
            // Verify detection logic aligns with defined constants
            var allIdentifiers = AnalyzerConstants.TenantWorkerIds.AllIdentifiers;
            
            foreach (var identifier in allIdentifiers)
            {
                var literal = SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(identifier));
                
                Assert.True(SyntaxAnalysisHelpers.ContainsTenantWorkerIdentifier(literal),
                    $"Should detect identifier: {identifier}");
            }
        }

        #endregion

        #region Helper Methods

        private static InvocationExpressionSyntax ParseInvocation(string sourceCode)
        {
            var fullSource = $@"
using System;
class Test 
{{ 
    void Method() 
    {{ 
        {sourceCode}; 
    }} 
}}";
            var tree = CSharpSyntaxTree.ParseText(fullSource);
            return tree.GetRoot()
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .First();
        }

        #endregion
    }
}