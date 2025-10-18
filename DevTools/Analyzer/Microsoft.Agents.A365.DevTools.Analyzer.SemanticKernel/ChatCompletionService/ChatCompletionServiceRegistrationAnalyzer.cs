using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Constants;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.ChatCompletionService
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ChatCompletionServiceRegistrationAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = AnalyzerConstants.DiagnosticIds.ChatCompletionServiceRegistration;
        private static readonly LocalizableString Title = "Direct chat completion service registration is not allowed";
        private static readonly LocalizableString MessageFormat = "Direct registration of chat completion service should use the approved delegate/template function";
        private static readonly LocalizableString Description = "All chat completion service registrations must use the configuration delegate/template function for governance.";
        private const string Category = "Governance";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var expr = invocation.Expression.ToString();
            if (expr.Contains("AddService<IChatCompletionService>") || expr.Contains("AddChatCompletionService"))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
            }
        }
    }
}
