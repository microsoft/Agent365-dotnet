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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(KernelDirectAccessCodeFixProvider)), Shared]
    public class KernelDirectAccessCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            KernelDirectAccessAnalyzer.DiagnosticId,
            KernelDirectAccessAnalyzer.UnsafeImportId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics[0];
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            if (node == null) return;

            if (diagnostic.Id == KernelDirectAccessAnalyzer.UnsafeImportId && node is InvocationExpressionSyntax importInvocation)
            {
                string title = "Replace ImportPluginFromObject with safe TryImportPluginFromObject";
                context.RegisterCodeFix(
                    Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                        title,
                        ct => ReplaceImportPluginFromObjectAsync(context.Document, importInvocation, ct),
                        nameof(KernelDirectAccessCodeFixProvider) + "_SafeImport"),
                    diagnostic);
                return;
            }

            string defaultTitle = "Use KernelProvider.GetKernel(context) for multi-tenant governance";

            if (node is InvocationExpressionSyntax invocation)
            {
                context.RegisterCodeFix(
                    Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                        defaultTitle,
                        ct => ReplaceWithKernelProviderAsync(context.Document, invocation, ct),
                        nameof(KernelDirectAccessCodeFixProvider)),
                    diagnostic);
            }
            else if (node is ParameterSyntax param)
            {
                context.RegisterCodeFix(
                    Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                        "Remove Kernel parameter (use KernelProvider instead)",
                        ct => RemoveParameterAsync(context.Document, param, ct),
                        nameof(KernelDirectAccessCodeFixProvider)),
                    diagnostic);
            }
            else if (node is FieldDeclarationSyntax field)
            {
                context.RegisterCodeFix(
                    Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                        "Remove Kernel field (use KernelProvider instead)",
                        ct => RemoveFieldAsync(context.Document, field, ct),
                        nameof(KernelDirectAccessCodeFixProvider)),
                    diagnostic);
            }
            else if (node is IdentifierNameSyntax identifier && identifier.Identifier.Text == "_kernel")
            {
                context.RegisterCodeFix(
                    Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                        "Replace _kernel with KernelProvider.GetKernel(...)",
                        ct => ReplaceKernelIdentifierAsync(context.Document, identifier, ct),
                        nameof(KernelDirectAccessCodeFixProvider)),
                    diagnostic);
            }
            }

        private async Task<Document> ReplaceImportPluginFromObjectAsync(Document document, InvocationExpressionSyntax node, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
            
            // Extract the original arguments
            var originalArgs = node.ArgumentList.Arguments;
            
            // Build new invocation: kernel.TryImportPluginFromObject(plugin, name, logger)
            var memberAccess = node.Expression as MemberAccessExpressionSyntax;
            if (memberAccess != null)
            {
                var newMemberAccess = memberAccess.WithName(SyntaxFactory.IdentifierName("TryImportPluginFromObject"));
                
                // Add app.Logger as third argument if not already present
                var newArgs = originalArgs;
                if (originalArgs.Count == 2)
                {
                    var loggerArg = SyntaxFactory.Argument(SyntaxFactory.ParseExpression("app.Logger"));
                    newArgs = originalArgs.Add(loggerArg);
                }
                
                var newInvocation = node
                    .WithExpression(newMemberAccess)
                    .WithArgumentList(SyntaxFactory.ArgumentList(newArgs));
                    
                editor.ReplaceNode(node, newInvocation);
            }
            
            return editor.GetChangedDocument();
        }

        private async Task<Document> ReplaceKernelIdentifierAsync(Document document, IdentifierNameSyntax node, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
            var newExpr = SyntaxFactory.ParseExpression("kernelProvider.GetKernel(context)");
            editor.ReplaceNode(node, newExpr);
            return editor.GetChangedDocument();
        }

        private async Task<Document> ReplaceWithKernelProviderAsync(Document document, InvocationExpressionSyntax node, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
            var newExpr = SyntaxFactory.ParseExpression("kernelProvider.GetKernel(context)");
            editor.ReplaceNode(node, newExpr);
            return editor.GetChangedDocument();
        }

        private async Task<Document> RemoveParameterAsync(Document document, ParameterSyntax param, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
            editor.RemoveNode(param);
            return editor.GetChangedDocument();
        }

        private async Task<Document> RemoveFieldAsync(Document document, FieldDeclarationSyntax field, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
            editor.RemoveNode(field);
            return editor.GetChangedDocument();
        }
    }
}
