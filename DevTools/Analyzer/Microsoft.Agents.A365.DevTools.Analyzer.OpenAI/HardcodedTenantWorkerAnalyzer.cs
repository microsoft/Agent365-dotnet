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
    /// Analyzer for detecting hardcoded tenant/worker ID violations.
    /// Enforces multi-tenant governance by ensuring tenant and worker IDs are extracted from context.
    /// 
    /// Detected violations:
    /// - Hardcoded strings passed to GetChatClient(tenantId, workerId)
    /// - Hardcoded strings passed to GetAvailableTools(tenantId, workerId)
    /// - Hardcoded strings passed to ExecuteFunctionAsync(tenantId, workerId, ...)
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class HardcodedTenantWorkerAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Gets the diagnostic ID for hardcoded tenant/worker prevention violations.
        /// </summary>
        public static string DiagnosticId => AnalyzerConstants.DiagnosticIds.HardcodedTenantWorkerPrevention;

        /// <summary>
        /// Gets the collection of diagnostics that this analyzer can report.
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptorFactory.HardcodedTenantWorkerPrevention);

        /// <summary>
        /// Initializes the analyzer and registers analysis actions.
        /// </summary>
        /// <param name="context">The analysis context for registration</param>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            // Register for method invocations to catch hardcoded tenant/worker usage
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

            // Check provider method calls that take tenant/worker parameters
            if (IsProviderMethodWithTenantWorker(methodName))
            {
                CheckForHardcodedArguments(context, invocation, methodName);
            }
        }

        private static bool IsProviderMethodWithTenantWorker(string methodName)
        {
            return methodName == AnalyzerConstants.MethodNames.GetChatClient ||
                   methodName == AnalyzerConstants.MethodNames.GetAvailableTools ||
                   methodName == AnalyzerConstants.MethodNames.ExecuteFunctionAsync;
        }

        private static void CheckForHardcodedArguments(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, string methodName)
        {
            var arguments = invocation.ArgumentList.Arguments;
            
            // Check first two arguments (tenantId, workerId) for string literals
            for (int i = 0; i < Math.Min(2, arguments.Count); i++)
            {
                var argument = arguments[i];
                if (argument.Expression is LiteralExpressionSyntax literal &&
                    literal.Token.IsKind(SyntaxKind.StringLiteralToken))
                {
                    var literalValue = literal.Token.ValueText;
                    
                    // Allow null or empty strings, but flag obvious hardcoded values
                    if (!string.IsNullOrEmpty(literalValue) && IsLikelyHardcodedValue(literalValue))
                    {
                        var parameterName = i == 0 ? "tenantId" : "workerId";
                        ReportHardcodedViolation(context, argument, methodName, parameterName, literalValue);
                    }
                }
            }
        }

        private static bool IsLikelyHardcodedValue(string value)
        {
            // Flag common hardcoded patterns
            var suspiciousValues = new[]
            {
                "tenant1", "tenant2", "default", "system", "admin", "test",
                "worker1", "worker2", "background", "service", "demo"
            };

            return suspiciousValues.Any(suspicious => 
                value.Equals(suspicious, StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith(suspicious, StringComparison.OrdinalIgnoreCase));
        }

        private static void ReportHardcodedViolation(SyntaxNodeAnalysisContext context, ArgumentSyntax argument, 
            string methodName, string parameterName, string literalValue)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptorFactory.HardcodedTenantWorkerPrevention,
                argument.GetLocation(),
                $"Hardcoded {parameterName} '{literalValue}' in {methodName}() call");

            context.ReportDiagnostic(diagnostic);
        }
    }
}