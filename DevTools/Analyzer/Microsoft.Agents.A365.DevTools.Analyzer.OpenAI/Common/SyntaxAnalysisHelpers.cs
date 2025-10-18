using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Constants;

namespace Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Common
{
    /// <summary>
    /// Reusable syntax analysis utilities focused on OpenAI-specific detection patterns.
    /// Each method has a single, well-defined responsibility.
    /// </summary>
    public static class SyntaxAnalysisHelpers
    {
        #region Type Analysis Methods

        /// <summary>
        /// Checks if a type string represents a direct ChatClient type (not a provider type).
        /// </summary>
        /// <param name="typeString">The type string to check</param>
        /// <returns>True if it's a direct ChatClient type that should be avoided</returns>
        public static bool IsDirectChatClientType(string typeString)
        {
            if (string.IsNullOrEmpty(typeString)) 
                return false;

            var isChatClientType = typeString == AnalyzerConstants.TypeNames.ChatClient || 
                                  typeString.EndsWith($".{AnalyzerConstants.TypeNames.ChatClient}");
            
            var isProviderType = typeString.Contains(AnalyzerConstants.TypeNames.IChatClientProvider) ||
                                typeString.Contains(AnalyzerConstants.TypeNames.ChatClientProvider);

            return isChatClientType && !isProviderType;
        }

        /// <summary>
        /// Checks if a type string represents a direct OpenAIClient type (not a provider type).
        /// </summary>
        /// <param name="typeString">The type string to check</param>
        /// <returns>True if it's a direct OpenAIClient type that should be avoided</returns>
        public static bool IsDirectOpenAIClientType(string typeString)
        {
            if (string.IsNullOrEmpty(typeString)) 
                return false;

            var isOpenAIClientType = typeString == AnalyzerConstants.TypeNames.OpenAIClient || 
                                    typeString.EndsWith($".{AnalyzerConstants.TypeNames.OpenAIClient}");
            
            var isProviderType = typeString.Contains(AnalyzerConstants.TypeNames.IChatClientProvider) ||
                                typeString.Contains(AnalyzerConstants.TypeNames.ChatClientProvider);

            return isOpenAIClientType && !isProviderType;
        }

        // NOTE: IsDirectFunctionManagerType method removed - deprecated abstraction
        /// <summary>
        /// Deprecated method - kept for compatibility but always returns false.
        /// </summary>
        private static bool IsDirectFunctionManagerType(string typeString) => false;

        /// <summary>
        /// Checks if a type string represents an allowed provider type.
        /// </summary>
        /// <param name="typeString">The type string to check</param>
        /// <returns>True if it's an allowed provider type</returns>
        public static bool IsProviderType(string typeString)
        {
            if (string.IsNullOrEmpty(typeString)) 
                return false;

            return typeString.Contains(AnalyzerConstants.TypeNames.IChatClientProvider) ||
                   typeString.Contains(AnalyzerConstants.TypeNames.ChatClientProvider) ||
                   typeString.Contains(AnalyzerConstants.TypeNames.IOpenAIFunctionProvider) ||
                   typeString.Contains(AnalyzerConstants.TypeNames.OpenAIFunctionProvider);
        }

        #endregion

        #region Invocation Analysis Methods

        /// <summary>
        /// Checks if an invocation is a GetRequiredService call specifically for ChatClient type.
        /// </summary>
        /// <param name="invocation">The invocation expression to check</param>
        /// <returns>True if it's a GetRequiredService&lt;ChatClient&gt; call</returns>
        public static bool IsGetRequiredServiceForChatClient(InvocationExpressionSyntax invocation)
        {
            if (!IsGetRequiredServiceCall(invocation))
                return false;

            return HasChatClientGenericArgument(invocation);
        }

        /// <summary>
        /// Checks if an invocation is a GetRequiredService call specifically for OpenAIClient type.
        /// </summary>
        /// <param name="invocation">The invocation expression to check</param>
        /// <returns>True if it's a GetRequiredService&lt;OpenAIClient&gt; call</returns>
        public static bool IsGetRequiredServiceForOpenAIClient(InvocationExpressionSyntax invocation)
        {
            if (!IsGetRequiredServiceCall(invocation))
                return false;

            return HasOpenAIClientGenericArgument(invocation);
        }

        // NOTE: IsGetRequiredServiceForFunctionManager method removed - deprecated abstraction

        /// <summary>
        /// Checks if an invocation is a GetRequiredService call (any type).
        /// </summary>
        /// <param name="invocation">The invocation expression to check</param>
        /// <returns>True if it's a GetRequiredService call</returns>
        private static bool IsGetRequiredServiceCall(InvocationExpressionSyntax invocation)
        {
            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess) ||
                memberAccess.Name.Identifier.Text != AnalyzerConstants.MethodNames.GetRequiredService)
                return false;

            // Verify it's accessing RequestServices
            return memberAccess.Expression is MemberAccessExpressionSyntax innerExpr &&
                   innerExpr.Name.Identifier.Text == AnalyzerConstants.MemberNames.RequestServices;
        }

        /// <summary>
        /// Checks if an invocation has ChatClient as a generic type argument.
        /// </summary>
        /// <param name="invocation">The invocation expression to check</param>
        /// <returns>True if it has ChatClient as generic argument</returns>
        private static bool HasChatClientGenericArgument(InvocationExpressionSyntax invocation)
        {
            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess) ||
                !(memberAccess.Name is GenericNameSyntax genericName) ||
                genericName.TypeArgumentList.Arguments.Count != 1)
                return false;

            var argType = genericName.TypeArgumentList.Arguments[0].ToString();
            return IsDirectChatClientType(argType);
        }

        /// <summary>
        /// Checks if an invocation has OpenAIClient as a generic type argument.
        /// </summary>
        /// <param name="invocation">The invocation expression to check</param>
        /// <returns>True if it has OpenAIClient as generic argument</returns>
        private static bool HasOpenAIClientGenericArgument(InvocationExpressionSyntax invocation)
        {
            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess) ||
                !(memberAccess.Name is GenericNameSyntax genericName) ||
                genericName.TypeArgumentList.Arguments.Count != 1)
                return false;

            var argType = genericName.TypeArgumentList.Arguments[0].ToString();
            return IsDirectOpenAIClientType(argType);
        }

        // NOTE: HasFunctionManagerGenericArgument method removed - deprecated abstraction

        #endregion

        #region Tenant/Worker ID Analysis Methods

        /// <summary>
        /// Checks if a literal expression contains any tenant/worker ID identifiers.
        /// </summary>
        /// <param name="literal">The literal expression to check</param>
        /// <returns>True if it contains tenant/worker ID identifiers</returns>
        public static bool ContainsTenantWorkerIdentifier(LiteralExpressionSyntax literal)
        {
            var literalValue = literal.Token.ValueText;
            return AnalyzerConstants.TenantWorkerIds.AllIdentifiers.Contains(literalValue);
        }

        /// <summary>
        /// Checks if a member access is for Headers or Items indexer.
        /// </summary>
        /// <param name="memberAccess">The member access expression</param>
        /// <returns>True if it's Headers or Items access</returns>
        public static bool IsHeadersOrItemsAccess(MemberAccessExpressionSyntax memberAccess)
        {
            var memberName = memberAccess.Name.Identifier.Text;
            return memberName == AnalyzerConstants.MemberNames.Headers ||
                   memberName == AnalyzerConstants.MemberNames.Items;
        }

        #endregion

        #region AgentApplication Analysis Methods

        /// <summary>
        /// Checks if an object creation is for a type that inherits from AgentApplication.
        /// Uses semantic analysis for accurate inheritance checking.
        /// </summary>
        /// <param name="creation">The object creation expression</param>
        /// <param name="semanticModel">The semantic model for type analysis</param>
        /// <returns>True if it's creating an AgentApplication-derived type</returns>
        public static bool IsAgentApplicationCreation(ObjectCreationExpressionSyntax creation, SemanticModel semanticModel)
        {
            var typeInfo = semanticModel.GetTypeInfo(creation.Type);
            return typeInfo.Type != null && InheritsFromAgentApplication(typeInfo.Type);
        }

        /// <summary>
        /// Checks if a constructor declaration is for a type that inherits from AgentApplication.
        /// Uses semantic analysis for accurate inheritance checking.
        /// </summary>
        /// <param name="constructor">The constructor declaration</param>
        /// <param name="semanticModel">The semantic model for type analysis</param>
        /// <returns>True if it's an AgentApplication-derived constructor</returns>
        public static bool IsAgentApplicationConstructor(ConstructorDeclarationSyntax constructor, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(constructor);
            return symbol?.ContainingType != null && InheritsFromAgentApplication(symbol.ContainingType);
        }

        /// <summary>
        /// Checks if a specific type inherits from AgentApplication.
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <param name="compilation">The compilation context (unused but kept for API compatibility)</param>
        /// <returns>True if the type inherits from AgentApplication</returns>
        public static bool IsAgentApplicationType(ITypeSymbol type, Compilation compilation)
        {
            return InheritsFromAgentApplication(type);
        }

        /// <summary>
        /// Checks if a type inherits from AgentApplication by walking the inheritance chain.
        /// </summary>
        /// <param name="type">The type symbol to check</param>
        /// <returns>True if the type inherits from AgentApplication</returns>
        private static bool InheritsFromAgentApplication(ITypeSymbol type)
        {
            var current = type;
            while (current != null)
            {
                if (current.Name == AnalyzerConstants.TypeNames.AgentApplication)
                    return true;

                current = current.BaseType;
            }

            return false;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Extracts the first literal argument from an argument list.
        /// </summary>
        /// <param name="arguments">The argument list</param>
        /// <returns>The literal expression if found, null otherwise</returns>
        public static LiteralExpressionSyntax? GetFirstLiteralArgument(SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            return arguments.Count > 0 ? arguments[0].Expression as LiteralExpressionSyntax : null;
        }

        #endregion
    }
}