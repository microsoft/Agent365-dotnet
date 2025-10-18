using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Constants;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Common
{
    /// <summary>
    /// Reusable syntax analysis utilities focused on specific detection patterns.
    /// Each method has a single, well-defined responsibility.
    /// </summary>
    public static class SyntaxAnalysisHelpers
    {
        #region Type Analysis Methods

        /// <summary>
        /// Checks if a type string represents a direct Kernel type (not a provider type).
        /// </summary>
        /// <param name="typeString">The type string to check</param>
        /// <returns>True if it's a direct Kernel type that should be avoided</returns>
        public static bool IsDirectKernelType(string typeString)
        {
            if (string.IsNullOrEmpty(typeString)) 
                return false;

            var isKernelType = typeString == AnalyzerConstants.TypeNames.Kernel || 
                              typeString.EndsWith($".{AnalyzerConstants.TypeNames.Kernel}");
            
            var isProviderType = typeString.Contains(AnalyzerConstants.TypeNames.IKernelProvider) ||
                                typeString.Contains(AnalyzerConstants.TypeNames.KernelProvider);

            return isKernelType && !isProviderType;
        }

        /// <summary>
        /// Checks if a type string represents an allowed provider type.
        /// </summary>
        /// <param name="typeString">The type string to check</param>
        /// <returns>True if it's an allowed provider type</returns>
        public static bool IsProviderType(string typeString)
        {
            if (string.IsNullOrEmpty(typeString)) 
                return false;

            return typeString.Contains(AnalyzerConstants.TypeNames.IKernelProvider) ||
                   typeString.Contains(AnalyzerConstants.TypeNames.KernelProvider);
        }

        #endregion

        #region Invocation Analysis Methods

        /// <summary>
        /// Checks if an invocation is a GetRequiredService call specifically for Kernel type.
        /// </summary>
        /// <param name="invocation">The invocation expression to check</param>
        /// <returns>True if it's a GetRequiredService&lt;Kernel&gt; call</returns>
        public static bool IsGetRequiredServiceForKernel(InvocationExpressionSyntax invocation)
        {
            if (!IsGetRequiredServiceCall(invocation))
                return false;

            return HasKernelGenericArgument(invocation);
        }

        /// <summary>
        /// Checks if an invocation is an unsafe plugin import call.
        /// </summary>
        /// <param name="invocation">The invocation expression to check</param>
        /// <returns>True if it's an ImportPluginFromObject call</returns>
        public static bool IsUnsafePluginImport(InvocationExpressionSyntax invocation)
        {
            return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                   memberAccess.Name.Identifier.Text == AnalyzerConstants.MethodNames.ImportPluginFromObject;
        }

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
        /// Checks if an invocation has Kernel as a generic type argument.
        /// </summary>
        /// <param name="invocation">The invocation expression to check</param>
        /// <returns>True if it has Kernel as generic argument</returns>
        private static bool HasKernelGenericArgument(InvocationExpressionSyntax invocation)
        {
            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess) ||
                !(memberAccess.Name is GenericNameSyntax genericName) ||
                genericName.TypeArgumentList.Arguments.Count != 1)
                return false;

            var argType = genericName.TypeArgumentList.Arguments[0].ToString();
            return IsDirectKernelType(argType);
        }

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

        #region Compilation Analysis Methods

        /// <summary>
        /// Gets all types in the compilation that inherit from AgentApplication.
        /// Provides a solution-wide view of all agent types.
        /// </summary>
        /// <param name="compilation">The compilation to search</param>
        /// <returns>A collection of type symbols that inherit from AgentApplication</returns>
        public static IEnumerable<INamedTypeSymbol> GetAllAgentApplicationTypes(Compilation compilation)
        {
            var agentApplicationTypes = new List<INamedTypeSymbol>();
            var allTypes = GetAllTypesInAssembly(compilation.Assembly);
            
            foreach (var type in allTypes)
            {
                if (InheritsFromAgentApplication(type))
                {
                    agentApplicationTypes.Add(type);
                }
            }

            return agentApplicationTypes;
        }

        /// <summary>
        /// Gets all named types defined in an assembly.
        /// </summary>
        /// <param name="assembly">The assembly to search</param>
        /// <returns>All named types in the assembly</returns>
        private static IEnumerable<INamedTypeSymbol> GetAllTypesInAssembly(IAssemblySymbol assembly)
        {
            var types = new List<INamedTypeSymbol>();
            CollectTypesFromNamespace(assembly.GlobalNamespace, types);
            return types;
        }

        /// <summary>
        /// Recursively collects all types from a namespace and its nested namespaces.
        /// </summary>
        /// <param name="namespaceSymbol">The namespace to search</param>
        /// <param name="types">The collection to add types to</param>
        private static void CollectTypesFromNamespace(INamespaceSymbol namespaceSymbol, List<INamedTypeSymbol> types)
        {
            // Add all types in this namespace
            foreach (var type in namespaceSymbol.GetTypeMembers())
            {
                types.Add(type);
                CollectNestedTypes(type, types);
            }

            // Recursively process nested namespaces
            foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
            {
                CollectTypesFromNamespace(nestedNamespace, types);
            }
        }

        /// <summary>
        /// Recursively collects all nested types from a parent type.
        /// </summary>
        /// <param name="parentType">The parent type</param>
        /// <param name="types">The collection to add nested types to</param>
        private static void CollectNestedTypes(INamedTypeSymbol parentType, List<INamedTypeSymbol> types)
        {
            foreach (var nestedType in parentType.GetTypeMembers())
            {
                types.Add(nestedType);
                CollectNestedTypes(nestedType, types);
            }
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

        #region Deprecated Methods (Backward Compatibility)

        /// <summary>
        /// [Deprecated] Use IsAgentApplicationCreation with SemanticModel instead.
        /// Checks if an object creation is for MyAgent type (hardcoded check).
        /// </summary>
        /// <param name="creation">The object creation expression</param>
        /// <returns>True if it's creating MyAgent</returns>
        [System.Obsolete("Use IsAgentApplicationCreation with SemanticModel for proper inheritance checking")]
        public static bool IsMyAgentCreation(ObjectCreationExpressionSyntax creation)
        {
            return creation.Type.ToString().Contains("MyAgent");
        }

        /// <summary>
        /// [Deprecated] Use IsAgentApplicationConstructor with SemanticModel instead.
        /// Checks if a constructor declaration is for MyAgent (hardcoded check).
        /// </summary>
        /// <param name="constructor">The constructor declaration</param>
        /// <returns>True if it's MyAgent constructor</returns>
        [System.Obsolete("Use IsAgentApplicationConstructor with SemanticModel for proper inheritance checking")]
        public static bool IsMyAgentConstructor(ConstructorDeclarationSyntax constructor)
        {
            return constructor.Identifier.Text == "MyAgent";
        }

        #endregion
    }
}
