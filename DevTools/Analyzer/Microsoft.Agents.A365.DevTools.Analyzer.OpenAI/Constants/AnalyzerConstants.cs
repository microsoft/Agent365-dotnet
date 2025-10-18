using System.Collections.Immutable;

namespace Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Constants
{
    /// <summary>
    /// Centralized constants for all OpenAI analyzers to eliminate hardcoded strings.
    /// </summary>
    public static class AnalyzerConstants
    {
        /// <summary>
        /// Diagnostic IDs for all OpenAI analyzers.
        /// </summary>
        public static class DiagnosticIds
        {
            // Pattern: A365 + 2-letter orchestrator code + 4-digit sequence
            // Orchestrator code: OAI = OpenAI

            // Sequence assigned for OpenAI (OAI)
            public const string ChatClientDirectAccess = "A365OAI0001";
            public const string OpenAIClientDirectAccess = "A365OAI0002";
            // A365OAI0003 - Removed (deprecated function manager)
            public const string TenantWorkerIdAccess = "A365OAI0004";
            public const string ChatClientProviderUsage = "A365OAI0005";
            public const string FunctionProviderEnforcement = "A365OAI0006";
            // A365OAI0007 - Merged into A365OAI0009
            public const string ProviderRegistrationValidation = "A365OAI0008";
            public const string HardcodedTenantWorkerPrevention = "A365OAI0009";
            public const string CrossTenantDataAccessPrevention = "A365OAI0010";
            public const string AgentConstructionValidation = "A365OAI0011";
            // A365OAI0012 - Not needed (covered by A365OAI0009)
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
            public const string ChatClient = "ChatClient";
            public const string OpenAIClient = "OpenAIClient";
            public const string IChatClientProvider = "IChatClientProvider";
            public const string ChatClientProvider = "ChatClientProvider";
            public const string IOpenAIFunctionProvider = "IOpenAIFunctionProvider";
            public const string OpenAIFunctionProvider = "OpenAIFunctionProvider";
            public const string AgentApplication = "AgentApplication";
            public const string HttpContext = "HttpContext";
            public const string TenantContextHelper = "TenantContextHelper";
            public const string ChatTool = "ChatTool";
            public const string WebApplication = "WebApplication";
            public const string IServiceCollection = "IServiceCollection";
            public const string BackgroundService = "BackgroundService";
        }

        /// <summary>
        /// Method names used in analysis.
        /// </summary>
        public static class MethodNames
        {
            public const string FindFirst = "FindFirst";
            public const string GetRequiredService = "GetRequiredService";
            public const string AddSingleton = "AddSingleton";
            public const string AddScoped = "AddScoped";
            public const string AddTransient = "AddTransient";
            public const string GetChatClient = "GetChatClient";
            public const string GetAvailableTools = "GetAvailableTools";
            public const string ExecuteFunctionAsync = "ExecuteFunctionAsync";
            public const string GetTenantId = "GetTenantId";
            public const string GetWorkerId = "GetWorkerId";
            public const string CreateFunctionTool = "CreateFunctionTool";
            public const string MapPost = "MapPost";
            public const string MapGet = "MapGet";
            public const string MapPut = "MapPut";
            public const string MapDelete = "MapDelete";
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
            /// <summary>Common ChatClient field naming pattern.</summary>
            public const string ChatClientField = "_chatClient";
            /// <summary>Common OpenAI client field naming pattern.</summary>
            public const string OpenAIClientField = "_openAIClient";
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
            public const string OpenAIRuntime = "Microsoft.Kairo.Sdk.Runtime.OpenAI";
            public const string OpenAI = "OpenAI";
            public const string OpenAIChat = "OpenAI.Chat";
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