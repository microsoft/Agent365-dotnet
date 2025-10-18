using System.Collections.Immutable;

namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Constants
{
    /// <summary>
    /// Centralized constants for all analyzers to eliminate hardcoded strings.
    /// </summary>
    public static class AnalyzerConstants
    {
        /// <summary>
        /// Diagnostic IDs for all analyzers.
        /// </summary>
        public static class DiagnosticIds
        {
            // Pattern: A365 + 2-letter orchestrator code + 4-digit sequence
            // Orchestrator codes: SK = Semantic Kernel; future: OI = OpenAI, CL = Claude, etc.

            // Sequence assigned for Semantic Kernel (SK)
            public const string KernelRetrievalBeforeBuild = "A365SK0001";
            public const string KernelDirectAccess = "A365SK0002";
            public const string UnsafePluginImport = "A365SK0003";
            public const string TenantWorkerIdAccess = "A365SK0004";
            public const string ChatCompletionServiceRegistration = "A365SK0005";
        }

        /// <summary>
        /// Categories for diagnostic rules.
        /// </summary>
        public static class Categories
        {
            public const string Governance = "Governance";
            public const string Usage = "Usage";
        }

        /// <summary>
        /// Type names used across analyzers.
        /// </summary>
        public static class TypeNames
        {
            public const string Kernel = "Kernel";
            public const string IKernelProvider = "IKernelProvider";
            public const string KernelProvider = "KernelProvider";
            public const string AgentApplication = "AgentApplication";
            public const string HttpContext = "HttpContext";
        }

        /// <summary>
        /// Method names used in analysis.
        /// </summary>
        public static class MethodNames
        {
            public const string FindFirst = "FindFirst";
            public const string GetRequiredService = "GetRequiredService";
            public const string ImportPluginFromObject = "ImportPluginFromObject";
            public const string TryImportPluginFromObject = "TryImportPluginFromObject";
            public const string MapPost = "MapPost";
            public const string MapGet = "MapGet";
            public const string Build = "Build";
        }

        /// <summary>
        /// Property and field names used in semantic analysis.
        /// </summary>
        public static class MemberNames
        {
            /// <summary>Request headers collection name.</summary>
            public const string Headers = "Headers";
            /// <summary>HttpContext items collection name.</summary>
            public const string Items = "Items";
            /// <summary>Services property name.</summary>
            public const string Services = "Services";
            /// <summary>RequestServices property name.</summary>
            public const string RequestServices = "RequestServices";
            /// <summary>Common kernel field naming pattern.</summary>
            public const string KernelField = "_kernel";
            /// <summary>KeyValuePair Value property name for compile-time safety.</summary>
            public static readonly string Value = nameof(System.Collections.Generic.KeyValuePair<string, object>.Value);
        }

        /// <summary>
        /// Tenant/Worker ID related strings.
        /// </summary>
        public static class TenantWorkerIds
        {
            public static readonly ImmutableArray<string> ClaimNames = ImmutableArray.Create(
                "tenant_id",
                "worker_id"
            );

            public static readonly ImmutableArray<string> HeaderNames = ImmutableArray.Create(
                "X-Tenant-Id",
                "X-Worker-Id"
            );

            public static readonly ImmutableArray<string> AllIdentifiers = ClaimNames.AddRange(HeaderNames);
        }

        /// <summary>
        /// Namespace names used in governance.
        /// </summary>
        public static class Namespaces
        {
            public const string SemanticKernelExtensions = "Microsoft.Agents.A365.Tools.SemanticKernel.Extensions";
            public const string SemanticKernelTools = "Microsoft.Agents.A365.Tools.SemanticKernel";
        }

        /// <summary>
        /// Single source-of-truth for help link base used by diagnostic descriptors.
        /// Help link URIs will be built as {HelpLinkBase}/{DiagnosticId}.md
        /// </summary>
        public const string HelpLinkBase = "https://github.com/microsoft/Kairo/tree/main/docs/analyzers";

        /// <summary>
        /// Default severity used for these analyzers. Kept here so tests and descriptors stay consistent.
        /// </summary>
        public const Microsoft.CodeAnalysis.DiagnosticSeverity DefaultSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity.Error;

        /// <summary>
        /// Common guidance suffix appended to user-facing messages to encourage visiting the help link.
        /// </summary>
        public const string GuidanceSuffix = "See the analyzer help link for remediation steps.";
    }
}
