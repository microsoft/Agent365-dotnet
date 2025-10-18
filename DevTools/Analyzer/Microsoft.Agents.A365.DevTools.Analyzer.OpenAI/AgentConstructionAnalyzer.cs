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
    /// Analyzer for detecting improper Agent class construction patterns.
    /// Enforces multi-tenant governance by ensuring Agent classes use providers instead of direct clients.
    /// 
    /// Detected violations:
    /// - ChatClient constructor parameters in Agent classes
    /// - OpenAIClient constructor parameters in Agent classes
    /// - ChatClient/OpenAIClient field assignments in Agent constructors
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AgentConstructionAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Gets the diagnostic ID for agent construction validation violations.
        /// </summary>
        public static string DiagnosticId => AnalyzerConstants.DiagnosticIds.AgentConstructionValidation;

        /// <summary>
        /// Gets the collection of diagnostics that this analyzer can report.
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptorFactory.AgentConstructionValidation);

        /// <summary>
        /// Initializes the analyzer and registers analysis actions.
        /// </summary>
        /// <param name="context">The analysis context for registration</param>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            // Register for constructor declarations to check Agent class patterns
            context.RegisterSyntaxNodeAction(AnalyzeConstructorDeclaration, SyntaxKind.ConstructorDeclaration);
        }

        private static void AnalyzeConstructorDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is not ConstructorDeclarationSyntax constructor)
                return;

            // Get the containing class
            var classDeclaration = constructor.Parent as ClassDeclarationSyntax;
            if (classDeclaration == null)
                return;

            // Check if this is an Agent class (inherits from AgentApplication or has "Agent" in name)
            if (!IsAgentClass(classDeclaration, context.SemanticModel))
                return;

            // Check constructor parameters for direct client dependencies
            foreach (var parameter in constructor.ParameterList.Parameters)
            {
                if (IsDirectClientParameter(parameter, context.SemanticModel))
                {
                    ReportAgentConstructionViolation(context, parameter);
                }
            }

            // Check constructor body for direct client field assignments
            if (constructor.Body != null)
            {
                foreach (var statement in constructor.Body.Statements)
                {
                    if (statement is ExpressionStatementSyntax expressionStatement &&
                        expressionStatement.Expression is AssignmentExpressionSyntax assignment)
                    {
                        CheckFieldAssignment(context, assignment);
                    }
                }
            }
        }

        private static bool IsAgentClass(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
        {
            // Check if class name contains "Agent"
            if (classDeclaration.Identifier.ValueText.Contains("Agent"))
                return true;

            // Check if class inherits from AgentApplication
            if (classDeclaration.BaseList?.Types.Any(baseType =>
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(baseType.Type);
                    if (symbolInfo.Symbol is INamedTypeSymbol typeSymbol)
                    {
                        return typeSymbol.Name == AnalyzerConstants.TypeNames.AgentApplication ||
                               IsInheritedFromAgentApplication(typeSymbol);
                    }
                    return false;
                }) == true)
            {
                return true;
            }

            return false;
        }

        private static bool IsInheritedFromAgentApplication(INamedTypeSymbol typeSymbol)
        {
            var baseType = typeSymbol.BaseType;
            while (baseType != null)
            {
                if (baseType.Name == AnalyzerConstants.TypeNames.AgentApplication)
                    return true;
                baseType = baseType.BaseType;
            }
            return false;
        }

        private static bool IsDirectClientParameter(ParameterSyntax parameter, SemanticModel semanticModel)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(parameter.Type!);
            if (symbolInfo.Symbol is not INamedTypeSymbol typeSymbol)
                return false;

            var typeName = typeSymbol.Name;
            var namespaceName = typeSymbol.ContainingNamespace?.ToDisplayString();

            return (typeName == AnalyzerConstants.TypeNames.ChatClient && 
                    namespaceName == AnalyzerConstants.Namespaces.OpenAIChat) ||
                   (typeName == AnalyzerConstants.TypeNames.OpenAIClient && 
                    namespaceName == AnalyzerConstants.Namespaces.OpenAI);
        }

        private static void CheckFieldAssignment(SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignment)
        {
            // Check if this is assigning to a field that looks like a client field
            if (assignment.Left is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is ThisExpressionSyntax)
            {
                var fieldName = memberAccess.Name.Identifier.ValueText;
                if (fieldName == AnalyzerConstants.MemberNames.ChatClientField ||
                    fieldName == AnalyzerConstants.MemberNames.OpenAIClientField ||
                    fieldName.Contains("ChatClient") ||
                    fieldName.Contains("OpenAIClient"))
                {
                    ReportFieldAssignmentViolation(context, assignment, fieldName);
                }
            }
        }

        private static void ReportAgentConstructionViolation(SyntaxNodeAnalysisContext context, ParameterSyntax parameter)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptorFactory.AgentConstructionValidation,
                parameter.GetLocation(),
                $"Agent constructor parameter '{parameter.Identifier.ValueText}' uses direct client dependency");

            context.ReportDiagnostic(diagnostic);
        }

        private static void ReportFieldAssignmentViolation(SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignment, string fieldName)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptorFactory.AgentConstructionValidation,
                assignment.GetLocation(),
                $"Agent constructor assigns direct client to field '{fieldName}'");

            context.ReportDiagnostic(diagnostic);
        }
    }
}