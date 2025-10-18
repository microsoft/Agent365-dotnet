using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Common;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Tests.Common
{
    /// <summary>
    /// Tests for AnalyzerValidation utility methods to ensure robust error handling.
    /// </summary>
    public class AnalyzerValidationTests
    {
        #region SafeGetTypeString Tests

        [Fact]
        public void SafeGetTypeString_WithValidType_ReturnsCorrectString()
        {
            var code = "public class TestClass { public ChatClient Client { get; set; } }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var property = tree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>().First();
            
            var result = AnalyzerValidation.SafeGetTypeString(property.Type);
            
            Assert.Equal("ChatClient", result);
        }

        [Fact]
        public void SafeGetTypeString_WithNullType_ReturnsEmptyString()
        {
            var result = AnalyzerValidation.SafeGetTypeString(null);
            
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void SafeGetTypeString_WithComplexType_ReturnsFullString()
        {
            var code = "public class TestClass { public Microsoft.Kairo.Sdk.Runtime.OpenAI.IOpenAIClientProvider Provider { get; set; } }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var property = tree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>().First();
            
            var result = AnalyzerValidation.SafeGetTypeString(property.Type);
            
            Assert.Equal("Microsoft.Kairo.Sdk.Runtime.OpenAI.IOpenAIClientProvider", result);
        }

        #endregion

        #region SafeGetIdentifierText Tests

        [Fact]
        public void SafeGetIdentifierText_WithValidToken_ReturnsText()
        {
            var code = "var _chatClient = new ChatClient();";
            var tree = CSharpSyntaxTree.ParseText(code);
            var identifier = tree.GetRoot().DescendantTokens().First(t => t.IsKind(SyntaxKind.IdentifierToken) && t.ValueText == "_chatClient");
            
            var result = AnalyzerValidation.SafeGetIdentifierText(identifier);
            
            Assert.Equal("_chatClient", result);
        }

        [Fact]
        public void SafeGetIdentifierText_WithInvalidToken_ReturnsEmptyString()
        {
            var result = AnalyzerValidation.SafeGetIdentifierText(default);
            
            Assert.Equal(string.Empty, result);
        }

        #endregion

        #region ValidateStringParameter Tests

        [Fact]
        public void ValidateStringParameter_WithValidString_ReturnsTrue()
        {
            var result = AnalyzerValidation.ValidateStringParameter("ValidString", "paramName");
            
            Assert.True(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void ValidateStringParameter_WithInvalidString_ReturnsFalse(string? value)
        {
            var result = AnalyzerValidation.ValidateStringParameter(value, "paramName");
            
            Assert.False(result);
        }

        #endregion

        #region SafeGetLocation Tests

        [Fact]
        public void SafeGetLocation_WithValidNode_ReturnsLocation()
        {
            var code = "public class TestClass { }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var classNode = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            
            var result = AnalyzerValidation.SafeGetLocation(classNode);
            
            Assert.NotEqual(Location.None, result);
            Assert.Equal(tree, result.SourceTree);
        }

        [Fact]
        public void SafeGetLocation_WithNullNode_ReturnsLocationNone()
        {
            var result = AnalyzerValidation.SafeGetLocation(null);
            
            Assert.Equal(Location.None, result);
        }

        #endregion

        #region IsCompilationValid Tests

        [Fact]
        public void IsCompilationValid_WithValidCompilation_ReturnsTrue()
        {
            var code = "public class TestClass { }";
            var tree = CSharpSyntaxTree.ParseText(code);
            
            // Add comprehensive references for a valid compilation
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location)
            };
            
            // Create a library compilation (not an executable) to avoid needing Main method
            var compilation = CSharpCompilation.Create(
                assemblyName: "TestAssembly",
                syntaxTrees: new[] { tree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            
            var result = AnalyzerValidation.IsCompilationValid(compilation);
            
            Assert.True(result);
        }

        [Fact]
        public void IsCompilationValid_WithNullCompilation_ReturnsFalse()
        {
            var result = AnalyzerValidation.IsCompilationValid(null);
            
            Assert.False(result);
        }

        [Fact]
        public void IsCompilationValid_WithErrorCompilation_ReturnsFalse()
        {
            var code = "public class TestClass { undefined_type field; }"; // This will cause compilation error
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create("TestAssembly")
                .AddSyntaxTrees(tree)
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            
            var result = AnalyzerValidation.IsCompilationValid(compilation);
            
            Assert.False(result);
        }

        #endregion
    }
}