using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Constants;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Common;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel
{
    /// <summary>
    /// Analyzer for detecting direct Kernel access violations and unsafe plugin imports.
    /// Enforces multi-tenant governance by ensuring Kernel is accessed only via KernelProvider.
    /// 
    /// Detected violations:
    /// - Direct Kernel injection in constructors
    /// - Kernel fields or properties
    /// - GetRequiredService&lt;Kernel&gt; calls
    /// - ImportPluginFromObject usage (should use TryImportPluginFromObject)
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class KernelDirectAccessAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Gets the diagnostic ID for direct Kernel access violations.
        /// </summary>
        public static string DiagnosticId => AnalyzerConstants.DiagnosticIds.KernelDirectAccess;

        /// <summary>
        /// Gets the diagnostic ID for unsafe plugin import violations.
        /// </summary>
        public static string UnsafeImportId => AnalyzerConstants.DiagnosticIds.UnsafePluginImport;

        /// <summary>
        /// Gets the collection of diagnostics that this analyzer can report.
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            DiagnosticDescriptorFactory.GetKernelAccessDiagnostics();

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
            context.RegisterSyntaxNodeAction(AnalyzeKernelFieldUsage, SyntaxKind.IdentifierName);
        }

        /// <summary>
        /// Analyzes method invocations for direct Kernel access and unsafe plugin imports.
        /// Detects GetRequiredService&lt;Kernel&gt; and ImportPluginFromObject patterns.
        /// </summary>
        /// <param name="context">The analysis context containing the invocation node</param>
        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            if (!AnalyzerValidation.ValidateSyntaxNode(context.Node, context, nameof(AnalyzeInvocation)))
                return;

            var invocation = (InvocationExpressionSyntax)context.Node;

            // Check for unsafe plugin imports first (higher priority violation)
            if (SyntaxAnalysisHelpers.IsUnsafePluginImport(invocation))
            {
                ReportUnsafePluginImport(context, invocation);
                return; // Early return to avoid double reporting
            }

            // Check for direct Kernel retrieval from dependency injection
            if (SyntaxAnalysisHelpers.IsGetRequiredServiceForKernel(invocation))
            {
                ReportKernelDirectAccess(context, invocation);
            }
        }

        /// <summary>
        /// Analyzes field declarations for direct Kernel type usage.
        /// Prevents storing Kernel instances as class fields.
        /// </summary>
        /// <param name="context">The analysis context containing the field declaration node</param>
        private static void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (!AnalyzerValidation.ValidateSyntaxNode(context.Node, context, nameof(AnalyzeFieldDeclaration)))
                return;

            var fieldDecl = (FieldDeclarationSyntax)context.Node;
            var typeString = AnalyzerValidation.SafeGetTypeString(fieldDecl.Declaration.Type);
            
            if (SyntaxAnalysisHelpers.IsDirectKernelType(typeString))
            {
                ReportKernelDirectAccess(context, fieldDecl);
            }
        }

        /// <summary>
        /// Analyzes property declarations for direct Kernel type usage.
        /// Prevents storing Kernel instances as class properties.
        /// </summary>
        /// <param name="context">The analysis context containing the property declaration node</param>
        private static void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (!AnalyzerValidation.ValidateSyntaxNode(context.Node, context, nameof(AnalyzePropertyDeclaration)))
                return;

            var propertyDecl = (PropertyDeclarationSyntax)context.Node;
            var typeString = AnalyzerValidation.SafeGetTypeString(propertyDecl.Type);
            
            if (SyntaxAnalysisHelpers.IsDirectKernelType(typeString))
            {
                ReportKernelDirectAccess(context, propertyDecl);
            }
        }

        /// <summary>
        /// Analyzes constructor parameters for direct Kernel type injection.
        /// Prevents dependency injection of Kernel instances.
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
                if (SyntaxAnalysisHelpers.IsDirectKernelType(paramTypeString))
                {
                    ReportKernelDirectAccess(context, param);
                }
            }
        }

        /// <summary>
        /// Analyzes identifier usage for direct Kernel field access patterns.
        /// Detects common field naming patterns like '_kernel'.
        /// </summary>
        /// <param name="context">The analysis context containing the identifier node</param>
        private static void AnalyzeKernelFieldUsage(SyntaxNodeAnalysisContext context)
        {
            if (!AnalyzerValidation.ValidateSyntaxNode(context.Node, context, nameof(AnalyzeKernelFieldUsage)))
                return;

            var identifier = (IdentifierNameSyntax)context.Node;
            var identifierText = AnalyzerValidation.SafeGetIdentifierText(identifier.Identifier);
            
            if (identifierText == AnalyzerConstants.MemberNames.KernelField)
            {
                ReportKernelDirectAccess(context, identifier);
            }
        }

        /// <summary>
        /// Reports a kernel direct access violation with consistent diagnostic creation.
        /// </summary>
        /// <param name="context">The analysis context</param>
        /// <param name="node">The violating syntax node</param>
        private static void ReportKernelDirectAccess(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            var location = AnalyzerValidation.SafeGetLocation(node);
            if (location == Location.None) return;

            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptorFactory.KernelDirectAccess,
                location);
            
            context.ReportDiagnostic(diagnostic);
        }

        /// <summary>
        /// Reports an unsafe plugin import violation with consistent diagnostic creation.
        /// </summary>
        /// <param name="context">The analysis context</param>
        /// <param name="node">The violating syntax node</param>
        private static void ReportUnsafePluginImport(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            var location = AnalyzerValidation.SafeGetLocation(node);
            if (location == Location.None) return;

            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptorFactory.UnsafePluginImport,
                location);
            
            context.ReportDiagnostic(diagnostic);
        }
    }
}
