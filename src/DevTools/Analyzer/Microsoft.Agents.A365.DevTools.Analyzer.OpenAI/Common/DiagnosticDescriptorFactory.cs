using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Constants;

namespace Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Common
{
    /// <summary>
    /// Factory for creating consistent diagnostic descriptors across all OpenAI analyzers.
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
        /// Creates a diagnostic descriptor for ChatClient direct access violations.
        /// </summary>
        public static DiagnosticDescriptor ChatClientDirectAccess => CreateDescriptor(
            AnalyzerConstants.DiagnosticIds.ChatClientDirectAccess,
            "Direct ChatClient access or storage is not allowed",
            "Use IChatClientProvider.GetChatClient(tenantId, workerId) instead of direct ChatClient access. " +
            "Fix: 1) Inject IChatClientProvider instead of ChatClient, " +
            "2) Call GetChatClient() when needed, " +
            "3) Remove ChatClient fields/properties. " + 
            AnalyzerConstants.GuidanceSuffix,
            AnalyzerConstants.Categories.Governance,
            AnalyzerConstants.DefaultSeverity,
            isEnabledByDefault: true,
            description: "ChatClient must be accessed via ChatClientProvider for tenant/worker isolation. " +
                        "This ensures proper multi-tenant governance and prevents cross-tenant data leakage. " +
                        "Do not store ChatClient as a field or inject via constructor.");

        /// <summary>
        /// Creates a diagnostic descriptor for OpenAIClient direct access violations.
        /// </summary>
        public static DiagnosticDescriptor OpenAIClientDirectAccess => CreateDescriptor(
            AnalyzerConstants.DiagnosticIds.OpenAIClientDirectAccess,
            "Direct OpenAIClient access or storage is not allowed",
            "Use IChatClientProvider.GetChatClient(tenantId, workerId) instead of direct OpenAIClient access. " +
            "Fix: 1) Inject IChatClientProvider instead of OpenAIClient, " +
            "2) Call GetChatClient() when needed, " +
            "3) Remove OpenAIClient fields/properties. " + 
            AnalyzerConstants.GuidanceSuffix,
            AnalyzerConstants.Categories.Governance,
            AnalyzerConstants.DefaultSeverity,
            isEnabledByDefault: true,
            description: "OpenAIClient must be accessed via ChatClientProvider for tenant/worker isolation. " +
                        "This ensures proper multi-tenant governance and prevents cross-tenant data leakage. " +
                        "Do not store OpenAIClient as a field or inject via constructor.");

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
        /// Creates a diagnostic descriptor for ChatClient provider usage violations.
        /// </summary>
        public static DiagnosticDescriptor ChatClientProviderUsage => CreateDescriptor(
            AnalyzerConstants.DiagnosticIds.ChatClientProviderUsage,
            "ChatClient provider must be configured properly for multi-tenant scenarios",
            "Ensure IChatClientProvider is registered in DI and configured with proper tenant isolation. " +
            "Fix: 1) Register IChatClientProvider in dependency injection, " +
            "2) Configure provider with appropriate caching and security settings, " +
            "3) Use GetChatClient(tenantId, workerId) pattern consistently. " + 
            AnalyzerConstants.GuidanceSuffix,
            AnalyzerConstants.Categories.Usage,
            AnalyzerConstants.DefaultSeverity,
            isEnabledByDefault: true,
            description: "Proper ChatClient provider setup is essential for multi-tenant governance. " +
                        "Ensure providers are registered and configured before usage.");

        /// <summary>
        /// Creates a diagnostic descriptor for Function Provider enforcement violations.
        /// </summary>
        public static DiagnosticDescriptor FunctionProviderEnforcement => CreateDescriptor(
            AnalyzerConstants.DiagnosticIds.FunctionProviderEnforcement,
            "Functions must be accessed via IOpenAIFunctionProvider for tenant isolation",
            "Use IOpenAIFunctionProvider.GetAvailableTools() and ExecuteFunctionAsync() instead of direct function access. " +
            "Fix: 1) Inject IOpenAIFunctionProvider instead of creating functions directly, " +
            "2) Call GetAvailableTools(tenantId, workerId) to get tenant-specific tools, " +
            "3) Use ExecuteFunctionAsync(tenantId, workerId, functionName, args) for execution. " + 
            AnalyzerConstants.GuidanceSuffix,
            AnalyzerConstants.Categories.Governance,
            AnalyzerConstants.DefaultSeverity,
            isEnabledByDefault: true,
            description: "Function execution must be tenant-isolated via IOpenAIFunctionProvider. " +
                        "Direct function creation or execution bypasses multi-tenant governance.");

        /// <summary>
        /// Creates a diagnostic descriptor for Provider Registration validation violations.
        /// </summary>
        public static DiagnosticDescriptor ProviderRegistrationValidation => CreateDescriptor(
            AnalyzerConstants.DiagnosticIds.ProviderRegistrationValidation,
            "Providers must be registered with delegate-based configuration",
            "Register IChatClientProvider and IOpenAIFunctionProvider with proper delegates, not direct clients. " +
            "Fix: 1) Use AddSingleton<IChatClientProvider> with delegate factory, " +
            "2) Use AddSingleton<IOpenAIFunctionProvider> with delegate factory, " +
            "3) Remove direct AddSingleton<ChatClient> or AddSingleton<OpenAIClient> registrations. " + 
            AnalyzerConstants.GuidanceSuffix,
            AnalyzerConstants.Categories.Usage,
            AnalyzerConstants.DefaultSeverity,
            isEnabledByDefault: true,
            description: "Provider registration must use delegate-based factories for proper tenant isolation. " +
                        "Direct client registration bypasses governance controls.");

        /// <summary>
        /// Creates a diagnostic descriptor for Hardcoded Tenant/Worker prevention violations.
        /// </summary>
        public static DiagnosticDescriptor HardcodedTenantWorkerPrevention => CreateDescriptor(
            AnalyzerConstants.DiagnosticIds.HardcodedTenantWorkerPrevention,
            "Tenant and Worker IDs must not be hardcoded",
            "Use TenantContextHelper.GetTenantId() and GetWorkerId() instead of hardcoded values. " +
            "Fix: 1) Extract tenant/worker IDs from HttpContext using TenantContextHelper, " +
            "2) Pass extracted IDs to provider methods, " +
            "3) Remove hardcoded string literals for tenant/worker identification. " + 
            AnalyzerConstants.GuidanceSuffix,
            AnalyzerConstants.Categories.Governance,
            AnalyzerConstants.DefaultSeverity,
            isEnabledByDefault: true,
            description: "Hardcoded tenant/worker IDs bypass multi-tenant isolation and create security risks. " +
                        "Always extract these values from request context.");

        /// <summary>
        /// Creates a diagnostic descriptor for Cross-Tenant Data Access prevention violations.
        /// </summary>
        public static DiagnosticDescriptor CrossTenantDataAccessPrevention => CreateDescriptor(
            AnalyzerConstants.DiagnosticIds.CrossTenantDataAccessPrevention,
            "Data storage must be tenant-isolated to prevent cross-tenant access",
            "Use tenant-scoped storage patterns instead of shared static or singleton storage. " +
            "Fix: 1) Include tenantId/workerId in storage keys, " +
            "2) Use tenant-scoped DI containers where applicable, " +
            "3) Remove shared static collections that could leak data across tenants. " + 
            AnalyzerConstants.GuidanceSuffix,
            AnalyzerConstants.Categories.Governance,
            AnalyzerConstants.DefaultSeverity,
            isEnabledByDefault: true,
            description: "Shared storage without tenant isolation creates data leakage risks. " +
                        "All persistent state must be properly scoped to prevent cross-tenant access.");

        /// <summary>
        /// Creates a diagnostic descriptor for Agent Construction validation violations.
        /// </summary>
        public static DiagnosticDescriptor AgentConstructionValidation => CreateDescriptor(
            AnalyzerConstants.DiagnosticIds.AgentConstructionValidation,
            "Agent classes must use providers instead of direct OpenAI clients",
            "Agent constructors should accept IChatClientProvider and IOpenAIFunctionProvider instead of direct clients. " +
            "Fix: 1) Replace ChatClient/OpenAIClient constructor parameters with providers, " +
            "2) Extract tenant/worker context in processing methods, " +
            "3) Use providers with extracted context to get clients. " + 
            AnalyzerConstants.GuidanceSuffix,
            AnalyzerConstants.Categories.Governance,
            AnalyzerConstants.DefaultSeverity,
            isEnabledByDefault: true,
            description: "Agent classes with direct client dependencies cannot support multi-tenancy. " +
                        "Use provider-based dependency injection for proper governance.");

        /// <summary>
        /// Gets all supported diagnostics for multi-rule analyzers.
        /// NOTE: FunctionManagerDirectAccess excluded as it's deprecated.
        /// </summary>
        public static ImmutableArray<DiagnosticDescriptor> GetOpenAIAccessDiagnostics()
        {
            return ImmutableArray.Create(ChatClientDirectAccess, OpenAIClientDirectAccess);
            // FunctionManagerDirectAccess intentionally excluded - deprecated abstraction
        }

        /// <summary>
        /// Gets all supported diagnostics including new governance rules.
        /// </summary>
        public static ImmutableArray<DiagnosticDescriptor> GetAllOpenAIDiagnostics()
        {
            return ImmutableArray.Create(
                ChatClientDirectAccess, 
                OpenAIClientDirectAccess, 
                TenantWorkerIdAccess,
                ChatClientProviderUsage,
                FunctionProviderEnforcement,
                ProviderRegistrationValidation,
                HardcodedTenantWorkerPrevention,
                CrossTenantDataAccessPrevention,
                AgentConstructionValidation
            );
        }
    }
}