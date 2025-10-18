using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Constants;
using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Common;

namespace Microsoft.Agents.A365.DevTools.Analyzer.OpenAI
{
    /// <summary>
    /// Analyzer for detecting cross-tenant data access risks.
    /// Enforces multi-tenant governance by identifying shared storage patterns that could leak data.
    /// 
    /// Detected violations:
    /// - Static fields that could store tenant data without isolation
    /// - Singleton collections without tenant scoping
    /// - Shared caches without tenant keys
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CrossTenantDataAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Gets the diagnostic ID for cross-tenant data access prevention violations.
        /// </summary>
        public static string DiagnosticId => AnalyzerConstants.DiagnosticIds.CrossTenantDataAccessPrevention;

        /// <summary>
        /// Gets the collection of diagnostics that this analyzer can report.
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptorFactory.CrossTenantDataAccessPrevention);

        /// <summary>
        /// Initializes the analyzer and registers analysis actions.
        /// </summary>
        /// <param name="context">The analysis context for registration</param>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            // Register for field declarations to check for risky storage patterns
            context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
        }

        private static void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is not FieldDeclarationSyntax fieldDeclaration)
                return;

            // Check if this is a static field that could store tenant data
            if (!fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
                return;

            var variableType = fieldDeclaration.Declaration.Type;
            if (variableType == null)
                return;

            // Check if this looks like a collection type that could store tenant data
            if (IsRiskyCollectionType(variableType, context.SemanticModel))
            {
                foreach (var variable in fieldDeclaration.Declaration.Variables)
                {
                    ReportCrossTenantDataViolation(context, variable, fieldDeclaration);
                }
            }
        }

        private static bool IsRiskyCollectionType(TypeSyntax typeSyntax, SemanticModel semanticModel)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(typeSyntax);
            if (symbolInfo.Symbol is not INamedTypeSymbol typeSymbol)
                return false;

            var typeName = typeSymbol.Name;
            
            // Check for common collection types that could store cross-tenant data
            var riskyTypes = new[]
            {
                "Dictionary", "ConcurrentDictionary", "List", "HashSet", "Queue", "Stack",
                "MemoryCache", "Cache", "CacheEntry"
            };

            if (riskyTypes.Any(risky => typeName.Contains(risky)))
            {
                // Additional check: see if the type parameters or field name suggest tenant data
                return ContainsTenantDataIndicators(typeSyntax, typeSymbol);
            }

            return false;
        }

        private static bool ContainsTenantDataIndicators(TypeSyntax typeSyntax, INamedTypeSymbol typeSymbol)
        {
            // Check generic type arguments for tenant-related data
            if (typeSyntax is GenericNameSyntax genericName)
            {
                foreach (var typeArg in genericName.TypeArgumentList.Arguments)
                {
                    var argText = typeArg.ToString().ToLowerInvariant();
                    if (ContainsTenantRelatedTerms(argText))
                        return true;
                }
            }

            // Check if the type itself suggests tenant data storage
            var typeText = typeSymbol.ToDisplayString().ToLowerInvariant();
            return ContainsTenantRelatedTerms(typeText);
        }

        private static bool ContainsTenantRelatedTerms(string text)
        {
            var tenantTerms = new[]
            {
                "chat", "message", "history", "conversation", "session", "user", "client",
                "agent", "request", "response", "context", "state", "data", "cache"
            };

            return tenantTerms.Any(term => text.Contains(term));
        }

        private static void ReportCrossTenantDataViolation(SyntaxNodeAnalysisContext context, 
            VariableDeclaratorSyntax variable, FieldDeclarationSyntax fieldDeclaration)
        {
            var fieldName = variable.Identifier.ValueText;
            var typeName = fieldDeclaration.Declaration.Type!.ToString();

            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptorFactory.CrossTenantDataAccessPrevention,
                variable.GetLocation(),
                $"Static field '{fieldName}' of type '{typeName}' may store cross-tenant data");

            context.ReportDiagnostic(diagnostic);
        }
    }
}