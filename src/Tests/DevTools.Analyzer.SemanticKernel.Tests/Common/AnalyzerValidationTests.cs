using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Tests.Common
{
    /// <summary>
    /// Unit tests for AnalyzerValidation utility methods.
    /// Tests essential error handling and input validation scenarios.
    /// </summary>
    public class AnalyzerValidationTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateStringParameter_WithInvalidInput_ReturnsFalse(string? input)
        {
            var result = AnalyzerValidation.ValidateStringParameter(input, "test");
            
            Assert.False(result);
        }

        [Fact]
        public void ValidateStringParameter_WithValidInput_ReturnsTrue()
        {
            var result = AnalyzerValidation.ValidateStringParameter("valid", "test");
            
            Assert.True(result);
        }

        [Fact]
        public void SafeGetLocation_WithNullNode_ReturnsLocationNone()
        {
            var location = AnalyzerValidation.SafeGetLocation(null);
            
            Assert.Equal(Location.None, location);
        }

        [Fact]
        public void SafeGetLocation_WithValidNode_ReturnsValidLocation()
        {
            var validNode = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression);
            
            var location = AnalyzerValidation.SafeGetLocation(validNode);
            
            Assert.NotEqual(Location.None, location);
        }

        [Fact]
        public void SafeGetTypeString_WithNullTypeSyntax_ReturnsEmptyString()
        {
            var result = AnalyzerValidation.SafeGetTypeString(null);
            
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void SafeGetTypeString_WithValidTypeSyntax_ReturnsTypeString()
        {
            var typeNode = SyntaxFactory.IdentifierName("string");
            
            var result = AnalyzerValidation.SafeGetTypeString(typeNode);
            
            Assert.Equal("string", result);
        }
    }
}