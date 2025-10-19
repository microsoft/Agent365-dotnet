using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.ChatCompletionService
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ChatCompletionServiceRegistrationCodeFixProvider)), Shared]
    public class ChatCompletionServiceRegistrationCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ChatCompletionServiceRegistrationAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics[0];
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            if (node is InvocationExpressionSyntax invocation)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Refactor to use configuration delegate/template function",
                        c => RefactorToDelegateAsync(context.Document, invocation, c),
                        equivalenceKey: "RefactorToDelegate"),
                    diagnostic);
            }
        }

        private async Task<Document> RefactorToDelegateAsync(Document document, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
        {
            var newInvocation = SyntaxFactory.ParseExpression("configChatCompletion(kernel)");
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(invocation, newInvocation);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
