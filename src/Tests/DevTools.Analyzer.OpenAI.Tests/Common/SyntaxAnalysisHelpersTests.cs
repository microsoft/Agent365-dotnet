using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Common;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Tests.Common
{
    /// <summary>
    /// Tests for SyntaxAnalysisHelpers utility methods to ensure correct detection patterns.
    /// </summary>
    public class SyntaxAnalysisHelpersTests
    {
        #region Type Detection Tests

        [Theory]
        [InlineData("ChatClient", true)]
        [InlineData("OpenAI.Chat.ChatClient", true)]
        [InlineData("IOpenAIClientProvider", false)]
        [InlineData("OpenAIClientProvider", false)]
        [InlineData("SomeOtherType", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsDirectChatClientType_DetectsCorrectly(string? typeString, bool expected)
        {
            var result = SyntaxAnalysisHelpers.IsDirectChatClientType(typeString ?? string.Empty);
            
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("OpenAIClient", true)]
        [InlineData("OpenAI.OpenAIClient", true)]
        [InlineData("IOpenAIClientProvider", false)]
        [InlineData("OpenAIClientProvider", false)]
        [InlineData("ChatClient", false)]
        [InlineData("SomeOtherType", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsDirectOpenAIClientType_DetectsCorrectly(string? typeString, bool expected)
        {
            var result = SyntaxAnalysisHelpers.IsDirectOpenAIClientType(typeString ?? string.Empty);
            
            Assert.Equal(expected, result);
        }



        [Theory]
        [InlineData("IChatClientProvider", true)]
        [InlineData("ChatClientProvider", true)]
        [InlineData("IOpenAIFunctionProvider", true)]
        [InlineData("OpenAIFunctionProvider", true)]
        [InlineData("ChatClient", false)]
        [InlineData("OpenAIClient", false)]
        [InlineData("SomeOtherType", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsProviderType_DetectsCorrectly(string? typeString, bool expected)
        {
            var result = SyntaxAnalysisHelpers.IsProviderType(typeString ?? string.Empty);
            
            Assert.Equal(expected, result);
        }

        #endregion

        #region GetRequiredService Detection Tests

        [Fact]
        public void IsGetRequiredServiceForChatClient_DetectsCorrectPattern()
        {
            var code = @"
                public void Test(HttpContext context)
                {
                    var client = context.RequestServices.GetRequiredService<ChatClient>();
                }";
            
            var tree = CSharpSyntaxTree.ParseText(code);
            var invocation = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
            
            var result = SyntaxAnalysisHelpers.IsGetRequiredServiceForChatClient(invocation);
            
            Assert.True(result);
        }

        [Fact]
        public void IsGetRequiredServiceForOpenAIClient_DetectsCorrectPattern()
        {
            var code = @"
                public void Test(HttpContext context)
                {
                    var client = context.RequestServices.GetRequiredService<OpenAIClient>();
                }";
            
            var tree = CSharpSyntaxTree.ParseText(code);
            var invocation = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
            
            var result = SyntaxAnalysisHelpers.IsGetRequiredServiceForOpenAIClient(invocation);
            
            Assert.True(result);
        }



        [Fact]
        public void GetRequiredServiceDetection_IgnoresAllowedTypes()
        {
            var code = @"
                public void Test(HttpContext context)
                {
                    var provider = context.RequestServices.GetRequiredService<IOpenAIClientProvider>();
                }";
            
            var tree = CSharpSyntaxTree.ParseText(code);
            var invocation = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
            
            // Should not detect provider types as violations
            Assert.False(SyntaxAnalysisHelpers.IsGetRequiredServiceForChatClient(invocation));
            Assert.False(SyntaxAnalysisHelpers.IsGetRequiredServiceForOpenAIClient(invocation));
        }

        #endregion

        #region Tenant/Worker ID Tests

        [Theory]
        [InlineData("\"tenant_id\"", true)]
        [InlineData("\"worker_id\"", true)]
        [InlineData("\"X-Tenant-Id\"", true)]
        [InlineData("\"X-Worker-Id\"", true)]
        [InlineData("\"some_other_id\"", false)]
        [InlineData("\"random\"", false)]
        public void ContainsTenantWorkerIdentifier_DetectsCorrectly(string literalValue, bool expected)
        {
            var tree = CSharpSyntaxTree.ParseText($"var x = {literalValue};");
            var literal = tree.GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>().First();
            
            var result = SyntaxAnalysisHelpers.ContainsTenantWorkerIdentifier(literal);
            
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("context.Request.Headers", "Headers", true)]
        [InlineData("context.Items", "Items", true)]
        [InlineData("context.Request.Query", "Query", false)]
        [InlineData("context.Request.Form", "Form", false)]
        public void IsHeadersOrItemsAccess_DetectsCorrectly(string expression, string memberName, bool expected)
        {
            var code = $"var x = {expression}[\"key\"];";
            var tree = CSharpSyntaxTree.ParseText(code);
            var memberAccess = tree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                .FirstOrDefault(m => m.Name.Identifier.Text == memberName);
            
            if (memberAccess != null)
            {
                var result = SyntaxAnalysisHelpers.IsHeadersOrItemsAccess(memberAccess);
                Assert.Equal(expected, result);
            }
            else if (expected)
            {
                Assert.Fail($"Expected to find member access for {memberName}");
            }
        }

        #endregion

        #region Utility Methods Tests

        [Fact]
        public void GetFirstLiteralArgument_WithLiteralArgument_ReturnsLiteral()
        {
            var code = "Method(\"test\", 42);";
            var tree = CSharpSyntaxTree.ParseText(code);
            var invocation = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
            
            var result = SyntaxAnalysisHelpers.GetFirstLiteralArgument(invocation.ArgumentList.Arguments);
            
            Assert.NotNull(result);
            Assert.Equal("\"test\"", result.ToString());
        }

        [Fact]
        public void GetFirstLiteralArgument_WithNoArguments_ReturnsNull()
        {
            var code = "Method();";
            var tree = CSharpSyntaxTree.ParseText(code);
            var invocation = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
            
            var result = SyntaxAnalysisHelpers.GetFirstLiteralArgument(invocation.ArgumentList.Arguments);
            
            Assert.Null(result);
        }

        [Fact]
        public void GetFirstLiteralArgument_WithNonLiteralArgument_ReturnsNull()
        {
            var code = "Method(variable);";
            var tree = CSharpSyntaxTree.ParseText(code);
            var invocation = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
            
            var result = SyntaxAnalysisHelpers.GetFirstLiteralArgument(invocation.ArgumentList.Arguments);
            
            Assert.Null(result);
        }

        #endregion
    }
}