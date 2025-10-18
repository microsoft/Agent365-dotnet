using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Constants;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Common;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel
{
    /// <summary>
    /// Analyzer for detecting direct tenant/worker ID access violations.
    /// Enforces the use of TenantContextHelper for consistent tenant/worker ID extraction.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TenantWorkerIdAccessAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = AnalyzerConstants.DiagnosticIds.TenantWorkerIdAccess;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            ImmutableArray.Create(DiagnosticDescriptorFactory.TenantWorkerIdAccess);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeElementAccessExpression, SyntaxKind.ElementAccessExpression);
        }

        private void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            
            // Check for context.User.FindFirst("tenant_id") or context.User.FindFirst("worker_id")
            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess) || 
                memberAccess.Name.Identifier.Text != AnalyzerConstants.MethodNames.FindFirst)
                return;

            var arguments = invocation.ArgumentList?.Arguments;
            if (arguments?.Count > 0)
            {
                var literal = SyntaxAnalysisHelpers.GetFirstLiteralArgument(arguments.Value);
                if (literal != null && SyntaxAnalysisHelpers.ContainsTenantWorkerIdentifier(literal))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptorFactory.TenantWorkerIdAccess, 
                        invocation.GetLocation()));
                }
            }
        }

        private void AnalyzeElementAccessExpression(SyntaxNodeAnalysisContext context)
        {
            var elementAccess = (ElementAccessExpressionSyntax)context.Node;
            
            // Check for context.Request.Headers["X-Tenant-Id"] or context.Items["tenant_id"] etc.
            if (!(elementAccess.Expression is MemberAccessExpressionSyntax memberAccess) ||
                !SyntaxAnalysisHelpers.IsHeadersOrItemsAccess(memberAccess))
                return;

            var arguments = elementAccess.ArgumentList?.Arguments;
            if (arguments?.Count > 0)
            {
                var literal = SyntaxAnalysisHelpers.GetFirstLiteralArgument(arguments.Value);
                if (literal != null && SyntaxAnalysisHelpers.ContainsTenantWorkerIdentifier(literal))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptorFactory.TenantWorkerIdAccess, 
                        elementAccess.GetLocation()));
                }
            }
        }
    }
}
