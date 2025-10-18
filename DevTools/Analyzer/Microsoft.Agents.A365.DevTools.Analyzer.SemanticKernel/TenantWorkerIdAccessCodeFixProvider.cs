using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TenantWorkerIdAccessCodeFixProvider)), Shared]
    public class TenantWorkerIdAccessCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(TenantWorkerIdAccessAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics[0];
            var node = root.FindNode(diagnostic.Location.SourceSpan) as MemberAccessExpressionSyntax;
            if (node == null) return;

            context.RegisterCodeFix(
                Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                    "Use TenantContextHelper.GetTenantId/GetWorkerId(HttpContext) instead",
                    ct => ReplaceWithHelperAsync(context.Document, node, ct),
                    nameof(TenantWorkerIdAccessCodeFixProvider)),
                diagnostic);
        }

        private async Task<Document> ReplaceWithHelperAsync(Document document, MemberAccessExpressionSyntax node, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
            // Replace with a template call to TenantContextHelper
            var newExpr = SyntaxFactory.ParseExpression("TenantContextHelper.GetTenantId(context)");
            if (node.ToString().Contains("worker_id") || node.ToString().Contains("X-Worker-Id"))
            {
                newExpr = SyntaxFactory.ParseExpression("TenantContextHelper.GetWorkerId(context)");
            }
            editor.ReplaceNode(node, newExpr);
            return editor.GetChangedDocument();
        }
    }
}
