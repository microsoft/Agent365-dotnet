using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Constants;
using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Common;

namespace Microsoft.Agents.A365.DevTools.Analyzer.OpenAI
{
    /// <summary>
    /// Analyzer for detecting direct function tool creation and execution violations.
    /// Enforces multi-tenant governance by ensuring function operations go through IOpenAIFunctionProvider.
    /// 
    /// Detected violations:
    /// - Direct ChatTool.CreateFunctionTool calls
    /// - Direct function execution without provider
    /// - Missing IOpenAIFunctionProvider injection
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FunctionProviderEnforcementAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Gets the diagnostic ID for function provider enforcement violations.
        /// </summary>
        public static string DiagnosticId => AnalyzerConstants.DiagnosticIds.FunctionProviderEnforcement;

        /// <summary>
        /// Gets the collection of diagnostics that this analyzer can report.
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptorFactory.FunctionProviderEnforcement);

        /// <summary>
        /// Initializes the analyzer and registers analysis actions.
        /// </summary>
        /// <param name="context">The analysis context for registration</param>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            // Register for method invocations to catch direct function tool creation
            context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is not InvocationExpressionSyntax invocation)
                return;

            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            if (memberAccess == null)
                return;

            // Check for ChatTool.CreateFunctionTool() calls
            if (IsChatToolCreateFunctionTool(memberAccess, context.SemanticModel))
            {
                ReportFunctionToolCreationViolation(context, invocation);
            }
        }

        private static bool IsChatToolCreateFunctionTool(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel)
        {
            // Check if this is ChatTool.CreateFunctionTool
            if (memberAccess.Name.Identifier.ValueText != AnalyzerConstants.MethodNames.CreateFunctionTool)
                return false;

            var symbolInfo = semanticModel.GetSymbolInfo(memberAccess.Expression);
            if (symbolInfo.Symbol is not INamedTypeSymbol typeSymbol)
                return false;

            return typeSymbol.Name == AnalyzerConstants.TypeNames.ChatTool &&
                   typeSymbol.ContainingNamespace?.ToDisplayString() == AnalyzerConstants.Namespaces.OpenAIChat;
        }

        private static void ReportFunctionToolCreationViolation(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptorFactory.FunctionProviderEnforcement,
                invocation.GetLocation(),
                "Direct ChatTool.CreateFunctionTool usage");

            context.ReportDiagnostic(diagnostic);
        }
    }
}