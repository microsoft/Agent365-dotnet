using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Common
{
    /// <summary>
    /// Validation utilities for OpenAI analyzer input parameters and safety checks.
    /// Ensures robust error handling throughout the analyzer infrastructure.
    /// </summary>
    public static class AnalyzerValidation
    {
        /// <summary>
        /// Validates that a syntax node is not null and handles potential exceptions.
        /// </summary>
        /// <typeparam name="T">The type of syntax node</typeparam>
        /// <param name="node">The syntax node to validate</param>
        /// <param name="context">The analysis context for error reporting</param>
        /// <param name="operation">Description of the operation being performed</param>
        /// <returns>True if validation passes, false if analysis should be skipped</returns>
        public static bool ValidateSyntaxNode<T>(T? node, Microsoft.CodeAnalysis.Diagnostics.SyntaxNodeAnalysisContext context, string operation) 
            where T : SyntaxNode
        {
            if (node == null)
            {
                // Log diagnostic info for debugging but don't fail analysis
                // This can happen with malformed or incomplete code during editing
                return false;
            }

            try
            {
                // Basic syntax tree validation
                _ = node.GetLocation();
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                // Handle cases where syntax tree is corrupted or incomplete
                // This can occur during live editing in the IDE
                return false;
            }
        }

        /// <summary>
        /// Safely extracts string representation from a type syntax node.
        /// </summary>
        /// <param name="typeSyntax">The type syntax node</param>
        /// <returns>String representation or empty string if extraction fails</returns>
        public static string SafeGetTypeString(TypeSyntax? typeSyntax)
        {
            if (typeSyntax == null)
                return string.Empty;

            try
            {
                return typeSyntax.ToString();
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                // Handle malformed syntax during live editing
                return string.Empty;
            }
        }

        /// <summary>
        /// Safely extracts identifier text from a syntax token.
        /// </summary>
        /// <param name="token">The syntax token</param>
        /// <returns>Identifier text or empty string if extraction fails</returns>
        public static string SafeGetIdentifierText(SyntaxToken token)
        {
            try
            {
                return token.Text ?? string.Empty;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Validates that a semantic model is available and functional.
        /// </summary>
        /// <param name="semanticModel">The semantic model to validate</param>
        /// <param name="node">The syntax node being analyzed</param>
        /// <returns>True if semantic model is usable, false otherwise</returns>
        public static bool ValidateSemanticModel(SemanticModel? semanticModel, SyntaxNode node)
        {
            if (semanticModel == null)
                return false;

            try
            {
                // Test basic semantic model functionality
                _ = semanticModel.GetTypeInfo(node);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                // Semantic model might be incomplete during compilation
                return false;
            }
        }

        /// <summary>
        /// Validates input parameters for null/empty/whitespace conditions with helpful error context.
        /// </summary>
        /// <param name="value">The string value to validate</param>
        /// <param name="parameterName">The name of the parameter being validated</param>
        /// <returns>True if validation passes</returns>
        public static bool ValidateStringParameter(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                // In production analyzers, we typically don't throw exceptions
                // but return false to skip analysis gracefully
                return false;
            }

            return true;
        }

        /// <summary>
        /// Safely attempts to get the location of a syntax node for diagnostic reporting.
        /// </summary>
        /// <param name="node">The syntax node</param>
        /// <returns>The location or a default location if extraction fails</returns>
        public static Location SafeGetLocation(SyntaxNode? node)
        {
            if (node == null)
                return Location.None;

            try
            {
                return node.GetLocation();
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                return Location.None;
            }
        }

        /// <summary>
        /// Checks if a compilation is in a valid state for analysis.
        /// </summary>
        /// <param name="compilation">The compilation to check</param>
        /// <returns>True if compilation is analyzable</returns>
        public static bool IsCompilationValid(Compilation? compilation)
        {
            return compilation != null && !compilation.HasErrors();
        }

        private static bool HasErrors(this Compilation compilation)
        {
            // Check for critical compilation errors that would prevent analysis
            foreach (var diagnostic in compilation.GetDiagnostics())
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    return true;
                }
            }
            return false;
        }
    }
}