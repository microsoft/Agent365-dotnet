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
    /// Analyzer for detecting direct OpenAI client access violations.
    /// Enforces multi-tenant governance by ensuring OpenAI clients are accessed only via providers.
    /// 
    /// Detected violations:
    /// - Direct ChatClient injection in constructors
    /// - Direct OpenAIClient injection in constructors
    /// - ChatClient/OpenAIClient fields or properties
    /// - GetRequiredService&lt;ChatClient&gt; calls
    /// - GetRequiredService&lt;OpenAIClient&gt; calls
    /// 
    /// NOTE: IOpenAIFunctionManager detection removed - use IOpenAIFunctionProvider instead.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class OpenAIClientDirectAccessAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Gets the diagnostic ID for direct ChatClient access violations.
        /// </summary>
        public static string ChatClientDiagnosticId => AnalyzerConstants.DiagnosticIds.ChatClientDirectAccess;

        /// <summary>
        /// Gets the diagnostic ID for direct OpenAIClient access violations.
        /// </summary>
        public static string OpenAIClientDiagnosticId => AnalyzerConstants.DiagnosticIds.OpenAIClientDirectAccess;

        // NOTE: FunctionManagerDiagnosticId removed - deprecated abstraction

        /// <summary>
        /// Gets the collection of diagnostics that this analyzer can report.
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            DiagnosticDescriptorFactory.GetOpenAIAccessDiagnostics();

        /// <summary>
        /// Initializes the analyzer and registers analysis actions.
        /// </summary>
        /// <param name="context">The analysis context for registration</param>
        public override void Initialize(AnalysisContext context)
        {
            // Configure analyzer behavior for optimal performance and reliability
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            
            // Register syntax node analysis for specific patterns
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeClientFieldUsage, SyntaxKind.IdentifierName);
        }

        /// <summary>
        /// Analyzes method invocations for direct OpenAI client access.
        /// Detects GetRequiredService&lt;ChatClient&gt; and GetRequiredService&lt;OpenAIClient&gt; patterns.
        /// </summary>
        /// <param name="context">The analysis context containing the invocation node</param>
        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            if (!AnalyzerValidation.ValidateSyntaxNode(context.Node, context, nameof(AnalyzeInvocation)))
                return;

            var invocation = (InvocationExpressionSyntax)context.Node;

            // Check for direct ChatClient retrieval from dependency injection
            if (SyntaxAnalysisHelpers.IsGetRequiredServiceForChatClient(invocation))
            {
                ReportChatClientDirectAccess(context, invocation);
                return; // Early return to avoid double reporting
            }

            // Check for direct OpenAIClient retrieval from dependency injection
            if (SyntaxAnalysisHelpers.IsGetRequiredServiceForOpenAIClient(invocation))
            {
                ReportOpenAIClientDirectAccess(context, invocation);
                return; // Early return to avoid double reporting
            }

            // NOTE: IOpenAIFunctionManager detection removed as the abstraction is deprecated
            // in favor of direct OpenAI function calling patterns
        }

        /// <summary>
        /// Analyzes field declarations for direct OpenAI client type usage.
        /// Prevents storing OpenAI client instances as class fields.
        /// </summary>
        /// <param name="context">The analysis context containing the field declaration node</param>
        private static void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (!AnalyzerValidation.ValidateSyntaxNode(context.Node, context, nameof(AnalyzeFieldDeclaration)))
                return;

            var fieldDecl = (FieldDeclarationSyntax)context.Node;
            var typeString = AnalyzerValidation.SafeGetTypeString(fieldDecl.Declaration.Type);
            
            if (SyntaxAnalysisHelpers.IsDirectChatClientType(typeString))
            {
                ReportChatClientDirectAccess(context, fieldDecl);
            }
            else if (SyntaxAnalysisHelpers.IsDirectOpenAIClientType(typeString))
            {
                ReportOpenAIClientDirectAccess(context, fieldDecl);
            }
            // NOTE: IOpenAIFunctionManager field detection removed as the abstraction is deprecated
        }

        /// <summary>
        /// Analyzes property declarations for direct OpenAI client type usage.
        /// Prevents storing OpenAI client instances as class properties.
        /// </summary>
        /// <param name="context">The analysis context containing the property declaration node</param>
        private static void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (!AnalyzerValidation.ValidateSyntaxNode(context.Node, context, nameof(AnalyzePropertyDeclaration)))
                return;

            var propertyDecl = (PropertyDeclarationSyntax)context.Node;
            var typeString = AnalyzerValidation.SafeGetTypeString(propertyDecl.Type);
            
            if (SyntaxAnalysisHelpers.IsDirectChatClientType(typeString))
            {
                ReportChatClientDirectAccess(context, propertyDecl);
            }
            else if (SyntaxAnalysisHelpers.IsDirectOpenAIClientType(typeString))
            {
                ReportOpenAIClientDirectAccess(context, propertyDecl);
            }
            // NOTE: IOpenAIFunctionManager property detection removed as the abstraction is deprecated
        }

        /// <summary>
        /// Analyzes constructor parameters for direct OpenAI client type injection.
        /// Prevents dependency injection of OpenAI client instances.
        /// </summary>
        /// <param name="context">The analysis context containing the constructor declaration node</param>
        private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
        {
            if (!AnalyzerValidation.ValidateSyntaxNode(context.Node, context, nameof(AnalyzeConstructor)))
                return;

            var ctorDecl = (ConstructorDeclarationSyntax)context.Node;
            
            foreach (var param in ctorDecl.ParameterList.Parameters)
            {
                var paramTypeString = AnalyzerValidation.SafeGetTypeString(param.Type);
                
                if (SyntaxAnalysisHelpers.IsDirectChatClientType(paramTypeString))
                {
                    ReportChatClientDirectAccess(context, param);
                }
                else if (SyntaxAnalysisHelpers.IsDirectOpenAIClientType(paramTypeString))
                {
                    ReportOpenAIClientDirectAccess(context, param);
                }
                // NOTE: IOpenAIFunctionManager constructor parameter detection removed as the abstraction is deprecated
            }
        }

        /// <summary>
        /// Analyzes identifier usage for direct OpenAI client field access patterns.
        /// Detects common field naming patterns like '_chatClient', '_openAIClient'.
        /// </summary>
        /// <param name="context">The analysis context containing the identifier node</param>
        private static void AnalyzeClientFieldUsage(SyntaxNodeAnalysisContext context)
        {
            if (!AnalyzerValidation.ValidateSyntaxNode(context.Node, context, nameof(AnalyzeClientFieldUsage)))
                return;

            var identifier = (IdentifierNameSyntax)context.Node;
            var identifierText = AnalyzerValidation.SafeGetIdentifierText(identifier.Identifier);
            
            if (identifierText == AnalyzerConstants.MemberNames.ChatClientField)
            {
                ReportChatClientDirectAccess(context, identifier);
            }
            else if (identifierText == AnalyzerConstants.MemberNames.OpenAIClientField)
            {
                ReportOpenAIClientDirectAccess(context, identifier);
            }
        }

        /// <summary>
        /// Reports a ChatClient direct access violation with consistent diagnostic creation.
        /// </summary>
        /// <param name="context">The analysis context</param>
        /// <param name="node">The violating syntax node</param>
        private static void ReportChatClientDirectAccess(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            var location = AnalyzerValidation.SafeGetLocation(node);
            if (location == Location.None) return;

            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptorFactory.ChatClientDirectAccess,
                location);
            
            context.ReportDiagnostic(diagnostic);
        }

        /// <summary>
        /// Reports an OpenAIClient direct access violation with consistent diagnostic creation.
        /// </summary>
        /// <param name="context">The analysis context</param>
        /// <param name="node">The violating syntax node</param>
        private static void ReportOpenAIClientDirectAccess(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            var location = AnalyzerValidation.SafeGetLocation(node);
            if (location == Location.None) return;

            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptorFactory.OpenAIClientDirectAccess,
                location);
            
            context.ReportDiagnostic(diagnostic);
        }

        // NOTE: ReportFunctionManagerDirectAccess method removed - deprecated abstraction
    }
}