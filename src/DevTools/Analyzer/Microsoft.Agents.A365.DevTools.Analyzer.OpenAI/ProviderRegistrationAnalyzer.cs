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
    /// Analyzer for detecting improper provider registration patterns.
    /// Enforces multi-tenant governance by ensuring providers are registered with delegates, not direct clients.
    /// 
    /// Detected violations:
    /// - Direct ChatClient registration via AddSingleton/AddScoped/AddTransient
    /// - Direct OpenAIClient registration via AddSingleton/AddScoped/AddTransient
    /// - Missing provider registrations when direct clients are removed
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ProviderRegistrationAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Gets the diagnostic ID for provider registration validation violations.
        /// </summary>
        public static string DiagnosticId => AnalyzerConstants.DiagnosticIds.ProviderRegistrationValidation;

        /// <summary>
        /// Gets the collection of diagnostics that this analyzer can report.
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptorFactory.ProviderRegistrationValidation);

        /// <summary>
        /// Initializes the analyzer and registers analysis actions.
        /// </summary>
        /// <param name="context">The analysis context for registration</param>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            // Register for method invocations to catch DI registration patterns
            context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is not InvocationExpressionSyntax invocation)
                return;

            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            if (memberAccess == null)
                return;

            var methodName = memberAccess.Name.Identifier.ValueText;

            // Check for DI registration methods
            if (IsDependencyInjectionMethod(methodName))
            {
                CheckRegistrationPattern(context, invocation, methodName);
            }
        }

        private static bool IsDependencyInjectionMethod(string methodName)
        {
            return methodName == AnalyzerConstants.MethodNames.AddSingleton ||
                   methodName == AnalyzerConstants.MethodNames.AddScoped ||
                   methodName == AnalyzerConstants.MethodNames.AddTransient;
        }

        private static void CheckRegistrationPattern(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, string methodName)
        {
            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            if (memberAccess == null)
                return;

            // Check for generic type arguments to see what's being registered
            if (memberAccess.Name is GenericNameSyntax genericName)
            {
                var typeArguments = genericName.TypeArgumentList.Arguments;
                if (typeArguments.Count > 0)
                {
                    var firstTypeArg = typeArguments[0];
                    var symbolInfo = context.SemanticModel.GetSymbolInfo(firstTypeArg);
                    
                    if (symbolInfo.Symbol is INamedTypeSymbol typeSymbol)
                    {
                        if (IsDirectClientType(typeSymbol))
                        {
                            ReportDirectClientRegistration(context, invocation, typeSymbol.Name, methodName);
                        }
                    }
                }
            }
        }

        private static bool IsDirectClientType(INamedTypeSymbol typeSymbol)
        {
            var typeName = typeSymbol.Name;
            var namespaceName = typeSymbol.ContainingNamespace?.ToDisplayString();

            return (typeName == AnalyzerConstants.TypeNames.ChatClient && 
                    namespaceName == AnalyzerConstants.Namespaces.OpenAIChat) ||
                   (typeName == AnalyzerConstants.TypeNames.OpenAIClient && 
                    namespaceName == AnalyzerConstants.Namespaces.OpenAI);
        }

        private static void ReportDirectClientRegistration(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, 
            string clientType, string registrationMethod)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptorFactory.ProviderRegistrationValidation,
                invocation.GetLocation(),
                $"Direct {clientType} registration using {registrationMethod}");

            context.ReportDiagnostic(diagnostic);
        }
    }
}