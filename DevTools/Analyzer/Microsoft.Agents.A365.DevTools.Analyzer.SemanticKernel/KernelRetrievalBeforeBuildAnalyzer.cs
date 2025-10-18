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
    /// Analyzer for detecting Kernel retrieval before builder.Build() and AgentApplication registration violations.
    /// Enforces proper AgentApplication instantiation patterns and prevents premature Kernel access.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class KernelRetrievalBeforeBuildAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = AnalyzerConstants.DiagnosticIds.KernelRetrievalBeforeBuild;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            ImmutableArray.Create(DiagnosticDescriptorFactory.KernelRetrievalBeforeBuild);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeServiceRegistration, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
        }

        private void AnalyzeServiceRegistration(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            
            // Check for AddSingleton<TAgent> with lambda where TAgent implements AgentApplication
            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess) ||
                memberAccess.Name.Identifier.Text != "AddSingleton")
                return;

            var lambdaArg = invocation.ArgumentList.Arguments.FirstOrDefault();
            if (!(lambdaArg?.Expression is SimpleLambdaExpressionSyntax lambda))
                return;

            // Scan lambda body for problematic patterns
            AnalyzeLambdaForViolations(context, lambda);
        }

        private void AnalyzeLambdaForViolations(SyntaxNodeAnalysisContext context, SimpleLambdaExpressionSyntax lambda)
        {
            // Check for GetRequiredService<Kernel>() calls
            var kernelServiceCalls = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(SyntaxAnalysisHelpers.IsGetRequiredServiceForKernel);
            
            foreach (var violation in kernelServiceCalls)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptorFactory.KernelRetrievalBeforeBuild, 
                    violation.GetLocation()));
            }

            // Check for new AgentApplication-derived class calls using semantic analysis
            var agentCreations = lambda.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
                .Where(creation => IsAgentCreation(creation, context.SemanticModel));
            
            foreach (var violation in agentCreations)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptorFactory.KernelRetrievalBeforeBuild, 
                    violation.GetLocation()));
            }
        }

        private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var creation = (ObjectCreationExpressionSyntax)context.Node;
            
            // Skip object creation that's inside an AddSingleton lambda
            // to avoid duplicate reporting with AnalyzeLambdaForViolations
            if (IsInsideAddSingletonLambda(creation))
                return;
            
            // Check if it's an AgentApplication-derived type
            if (IsAgentCreation(creation, context.SemanticModel))
            {
                // Report direct instantiation of agent classes as a violation
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptorFactory.KernelRetrievalBeforeBuild, 
                    creation.GetLocation()));
            }
        }

        private void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
        {
            var ctorDecl = (ConstructorDeclarationSyntax)context.Node;
            
            if (!IsAgentConstructor(ctorDecl, context.SemanticModel))
                return;

            foreach (var param in ctorDecl.ParameterList.Parameters)
            {
                var paramTypeString = param.Type?.ToString() ?? "";
                if (SyntaxAnalysisHelpers.IsDirectKernelType(paramTypeString))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptorFactory.KernelRetrievalBeforeBuild, 
                        param.GetLocation()));
                }
            }
        }

        /// <summary>
        /// Determines if an object creation is for an agent class.
        /// Uses semantic analysis first, with fallback to pattern matching.
        /// </summary>
        private static bool IsAgentCreation(ObjectCreationExpressionSyntax creation, SemanticModel semanticModel)
        {
            try
            {
                // Try semantic analysis first - if it works, use it exclusively
                var typeInfo = semanticModel.GetTypeInfo(creation.Type);
                if (typeInfo.Type != null)
                {
                    // If semantic analysis is available, use it and don't fall back
                    return SyntaxAnalysisHelpers.IsAgentApplicationCreation(creation, semanticModel);
                }
            }
            catch
            {
                // Semantic analysis failed, fall back to pattern matching
            }
            
            // Fallback: Simple pattern matching for common agent types
            // Only use this when semantic analysis is not available
            // Also check if the constructor parameters suggest this is a violation
            var typeName = creation.Type.ToString();
            if (!(typeName.EndsWith("Agent") || 
                  typeName.Contains("MyAgent") || 
                  typeName.Contains("ChatAgent") || 
                  typeName.Contains("ComplianceAgent")))
            {
                return false; // Not an agent type
            }
            
            // It's an agent type, but check if it's being used correctly
            // If it's using IKernelProvider constructor, it's probably correct
            if (creation.ArgumentList?.Arguments.Count > 0)
            {
                foreach (var arg in creation.ArgumentList.Arguments)
                {
                    // Check if any argument suggests IKernelProvider usage
                    var argText = arg.ToString();
                    if (argText.Contains("IKernelProvider") || 
                        argText.Contains("GetRequiredService<IKernelProvider>"))
                    {
                        return false; // Using correct pattern
                    }
                }
            }
            
            return true; // Agent type but not using correct pattern
        }

        /// <summary>
        /// Determines if a constructor is for an agent class.
        /// Uses semantic analysis to determine inheritance from AgentApplication.
        /// </summary>
        private static bool IsAgentConstructor(ConstructorDeclarationSyntax constructor, SemanticModel semanticModel)
        {
            try
            {
                return SyntaxAnalysisHelpers.IsAgentApplicationConstructor(constructor, semanticModel);
            }
            catch
            {
                // If semantic analysis fails, don't report violations
                // This ensures we only report true positives
                return false;
            }
        }

        /// <summary>
        /// Checks if an object creation is inside an AddSingleton lambda to avoid duplicate reporting.
        /// </summary>
        private static bool IsInsideAddSingletonLambda(ObjectCreationExpressionSyntax creation)
        {
            var lambda = creation.Ancestors().OfType<SimpleLambdaExpressionSyntax>().FirstOrDefault();
            if (lambda == null) return false;

            var invocation = lambda.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault();
            if (invocation?.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                return memberAccess.Name.Identifier.Text == "AddSingleton";
            }

            return false;
        }
    }
}
