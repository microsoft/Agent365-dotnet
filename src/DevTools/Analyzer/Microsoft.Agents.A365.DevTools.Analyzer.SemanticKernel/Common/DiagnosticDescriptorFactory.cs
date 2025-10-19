using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Constants;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Common
{
    /// <summary>
    /// Factory for creating consistent diagnostic descriptors across all analyzers.
    /// Eliminates duplication and ensures consistent messaging.
    /// </summary>
    public static class DiagnosticDescriptorFactory
    {
        private static DiagnosticDescriptor CreateDescriptor(
            string id,
            string title,
            string messageFormat,
            string category,
            Microsoft.CodeAnalysis.DiagnosticSeverity defaultSeverity,
            bool isEnabledByDefault,
            string? description = null)
        {
            var helpLink = AnalyzerConstants.HelpLinkBase.TrimEnd('/') + "/" + id + ".md";
            var desc = description ?? title;

            return new DiagnosticDescriptor(
                id,
                title,
                messageFormat,
                category,
                defaultSeverity,
                isEnabledByDefault: isEnabledByDefault,
                description: desc,
                helpLinkUri: helpLink);
        }

        /// <summary>
        /// Creates a diagnostic descriptor for Kernel direct access violations.
        /// </summary>
        public static DiagnosticDescriptor KernelDirectAccess => CreateDescriptor(
            AnalyzerConstants.DiagnosticIds.KernelDirectAccess,
            "Direct Kernel access or storage is not allowed",
            "Use IKernelProvider.GetKernel(tenantId, workerId) instead of direct Kernel access. " +
            "Fix: 1) Inject IKernelProvider instead of Kernel, " +
            "2) Call GetKernel() when needed, " +
            "3) Remove Kernel fields/properties. " + 
            AnalyzerConstants.GuidanceSuffix,
            AnalyzerConstants.Categories.Governance,
            AnalyzerConstants.DefaultSeverity,
            isEnabledByDefault: true,
            description: "Kernel must be accessed via KernelProvider for tenant/worker isolation. " +
                        "This ensures proper multi-tenant governance and prevents cross-tenant data leakage. " +
                        "Do not store Kernel as a field or inject via constructor.");

        /// <summary>
        /// Creates a diagnostic descriptor for unsafe plugin import violations.
        /// </summary>
        public static DiagnosticDescriptor UnsafePluginImport => CreateDescriptor(
            AnalyzerConstants.DiagnosticIds.UnsafePluginImport,
            "Use safe plugin import to prevent duplicate registration",
            "Use TryImportPluginFromObject instead of ImportPluginFromObject to prevent exceptions. " +
            "Fix: 1) Change ImportPluginFromObject() to TryImportPluginFromObject(), " +
            "2) Add 'using Microsoft.Agents.A365.Tools.SemanticKernel.Extensions;' if needed, " +
            "3) Handle the boolean return value appropriately. " + 
            AnalyzerConstants.GuidanceSuffix,
            AnalyzerConstants.Categories.Governance,
            AnalyzerConstants.DefaultSeverity,
            isEnabledByDefault: true,
            description: "ImportPluginFromObject can cause 'key already added' exceptions when governance " +
                        "automatically registers plugins. TryImportPluginFromObject provides safe, " +
                        "idempotent plugin registration that works with governance auto-registration.");

        /// <summary>
        /// Creates a diagnostic descriptor for tenant/worker ID access violations.
        /// </summary>
        public static DiagnosticDescriptor TenantWorkerIdAccess => CreateDescriptor(
            AnalyzerConstants.DiagnosticIds.TenantWorkerIdAccess,
            "Tenant/Worker ID must be accessed via TenantContextHelper",
            "Replace direct header/claim access with TenantContextHelper methods. " +
            "Fix: 1) Use TenantContextHelper.GetTenantId(HttpContext), " +
            "2) Use TenantContextHelper.GetWorkerId(HttpContext), " +
            "3) Add 'using Microsoft.Agents.A365.Runtime.Common.AspNetCore;' if needed. " + 
            AnalyzerConstants.GuidanceSuffix,
            AnalyzerConstants.Categories.Governance,
            AnalyzerConstants.DefaultSeverity,
            isEnabledByDefault: true,
            description: "Always use TenantContextHelper to extract tenant/worker id from HttpContext. " +
                        "This ensures consistent behavior and handles fallback scenarios properly.");

        /// <summary>
        /// Creates a diagnostic descriptor for kernel retrieval before build violations.
        /// </summary>
        public static DiagnosticDescriptor KernelRetrievalBeforeBuild => CreateDescriptor(
            AnalyzerConstants.DiagnosticIds.KernelRetrievalBeforeBuild,
            "Kernel retrieval before Build() causes initialization errors",
            "Do not retrieve Kernel from DI before calling builder.Build(). " +
            "Fix: 1) Move Kernel retrieval after builder.Build(), " +
            "2) Use parameterless AgentApplication constructor, " +
            "3) Register AgentApplication as singleton without Kernel parameter. " + 
            AnalyzerConstants.GuidanceSuffix,
            AnalyzerConstants.Categories.Usage,
            AnalyzerConstants.DefaultSeverity,
            isEnabledByDefault: true,
            description: "Retrieving Kernel before Build() causes dependency injection errors. " +
                        "The build process must complete before Kernel can be safely retrieved.");

        /// <summary>
        /// Gets all supported diagnostics for multi-rule analyzers.
        /// </summary>
        public static ImmutableArray<DiagnosticDescriptor> GetKernelAccessDiagnostics()
        {
            return ImmutableArray.Create(KernelDirectAccess, UnsafePluginImport);
        }
    }
}
