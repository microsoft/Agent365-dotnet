
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Code fix provider for KernelRetrievalBeforeBuildAnalyzer.
/// Removes flagged error regions or nodes.
/// </summary>

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(KernelRetrievalBeforeBuildCodeFixProvider)), Shared]
    public class KernelRetrievalBeforeBuildCodeFixProvider : CodeFixProvider
    {
        /// <inheritdoc />
        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(KernelRetrievalBeforeBuildAnalyzer.DiagnosticId);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        /// <inheritdoc />
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics[0];
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the node flagged by the analyzer (could be invocation or object creation)
            var flaggedNode = root.FindNode(diagnosticSpan);
            if (flaggedNode == null)
                return;

            // Try to find the surrounding error region
            var region = flaggedNode.FirstAncestorOrSelf<RegionDirectiveTriviaSyntax>();
            if (region != null)
            {
                context.RegisterCodeFix(
                    Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                        title: "Remove flagged error region (A365 DI Kernel)",
                        createChangedDocument: c => RemoveErrorRegionAsync(context.Document, region, c),
                        equivalenceKey: "RemoveErrorRegionA365DIKernel"),
                    diagnostic);
            }
            else if (flaggedNode is InvocationExpressionSyntax || flaggedNode is ObjectCreationExpressionSyntax)
            {
                // Remove only invocation or object creation nodes
                context.RegisterCodeFix(
                    Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                        title: "Remove flagged code (A365 DI Kernel)",
                        createChangedDocument: c => RemoveFlaggedNodeAsync(context.Document, flaggedNode, c),
                        equivalenceKey: "RemoveFlaggedNodeA365DIKernel"),
                    diagnostic);
            }
        }

        /// <summary>
        /// Removes all nodes within the flagged region.
        /// </summary>
        private async Task<Document> RemoveErrorRegionAsync(Document document, RegionDirectiveTriviaSyntax region, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var parent = region.Parent;
            if (parent == null) return document;
            var nodesToRemove = parent.DescendantNodes().Where(n => n.SpanStart >= region.SpanStart && n.Span.End <= region.Span.End).ToList();
            var newRoot = root.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepNoTrivia);
            return document.WithSyntaxRoot(newRoot);
        }

        /// <summary>
        /// Removes the flagged node from the syntax tree.
        /// </summary>
        private async Task<Document> RemoveFlaggedNodeAsync(Document document, SyntaxNode flaggedNode, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.RemoveNode(flaggedNode, SyntaxRemoveOptions.KeepNoTrivia);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
