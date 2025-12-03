// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Notifications
{
    /// <summary>
    /// Contains constants.
    /// </summary>
    public class SubChannels
    {
        /// <summary>
        /// The sub-channel name for Federated Knowledge Service notifications.
        /// </summary>
        public const string FederatedKnowledgeServiceSubChannel = "FederatedKnowledgeService";

        /// <summary>
        /// The sub-channel name for email notifications.
        /// </summary>
        public const string AgentsEmailSubChannel = "email";

        /// <summary>
        /// The sub-channel name for Excel notifications.
        /// </summary>
        public const string AgentsExcelSubChannel = "excel";

        /// <summary>
        /// The sub-channel name for Word notifications.
        /// </summary>
        public const string AgentsWordSubChannel = "word";

        /// <summary>
        /// The sub-channel name for PowerPoint notifications.
        /// </summary>
        public const string AgentsPowerPointSubChannel = "powerpoint";

        /// <summary>
        /// The sub-channel name for Teams notifications.
        /// </summary>
        public const string AgentsTeamsSubChannel = "teams";
    }

    /// <summary>
    /// Contains constants.
    /// </summary>
    public class Events
    {
        /// <summary>
        /// The event name for agent lifecycle events.
        /// </summary>
        public const string AgentLifecycleEvent = "agentLifecycle";

        /// <summary>
        /// The event name for agentic user creation
        /// </summary>
        public const string AgenticUserIdentityCreated = "agenticUserIdentityCreated";

        /// <summary>
        /// The event name for agentic user onboarding
        /// </summary>
        public const string AgenticUserWorkloadOnboardingUpdated = "agenticUserWorkloadOnboardingUpdated";

        /// <summary>
        /// The event name for agentic user deletion
        /// </summary>
        public const string AgenticUserDeleted = "agenticUserDeleted";
    }
}
