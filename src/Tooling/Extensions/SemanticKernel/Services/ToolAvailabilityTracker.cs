// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services
{
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Extensions.Logging;
    using Microsoft.SemanticKernel;
    using Microsoft.SemanticKernel.ChatCompletion;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Tracks tool availability across conversation turns and annotates stale tool results
    /// in the chat history when the tool set changes.
    /// <para>
    /// This solves the problem where local desktop tools become available on a new turn
    /// (after being unavailable on the previous turn due to e.g. Intune timeout), but the LLM
    /// sees stale file/tool results from cloud-only execution in the conversation history and
    /// decides not to re-execute with the now-available local tools.
    /// </para>
    /// <para>
    /// This class is designed to be stored per-conversation in turn state. It is NOT thread-safe;
    /// each conversation should have its own instance.
    /// </para>
    /// </summary>
    public class ToolAvailabilityTracker
    {
        /// <summary>
        /// Whether local desktop tools (WNS transport) were available on the previous turn.
        /// </summary>
        public bool HadLocalTools { get; set; }

        /// <summary>
        /// Sorted comma-separated list of registered plugin names from the previous turn.
        /// Used for logging and detecting which tools changed.
        /// </summary>
        public string PreviousPlugins { get; set; } = "";

        /// <summary>
        /// The turn state key used to store this tracker across conversation turns.
        /// </summary>
        public const string TurnStateKey = "conversation.toolAvailabilityTracker";

        /// <summary>
        /// Checks whether the tool set has changed between the previous turn and the current turn,
        /// and if local tools have become newly available, injects a system message into the chat
        /// history so the LLM knows to re-execute file operations using local tools instead of
        /// relying on stale cloud-only results.
        /// </summary>
        /// <param name="chatHistory">The conversation's chat history (will be mutated if annotation is needed).</param>
        /// <param name="discoveryResult">The discovery result from the current turn's tool registration.</param>
        /// <param name="kernel">The Semantic Kernel instance with registered plugins for this turn.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        /// <returns>True if an annotation was injected, false otherwise.</returns>
        public bool AnnotateIfToolSetChanged(
            ChatHistory chatHistory,
            LocalDiscoveryResult discoveryResult,
            Kernel kernel,
            ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(chatHistory);
            ArgumentNullException.ThrowIfNull(discoveryResult);
            ArgumentNullException.ThrowIfNull(kernel);

            bool currentHasLocal = discoveryResult.HasLocalTools;
            var currentPlugins = string.Join(",", kernel.Plugins.Select(p => p.Name).OrderBy(n => n));

            bool annotated = false;

            // Only annotate if:
            // 1. There IS chat history (not the first turn)
            // 2. Local tools are now available but were NOT available on the previous turn
            // 3. The plugin set actually changed (prevents re-annotation on stable turns)
            if (chatHistory.Count > 0
                && currentHasLocal
                && !HadLocalTools
                && currentPlugins != PreviousPlugins)
            {
                var newPlugins = GetNewPluginNames(currentPlugins);

                logger?.LogInformation(
                    "[ToolAvailability] Local tools became available. Previous plugins: [{Previous}], Current plugins: [{Current}], New: [{New}]",
                    PreviousPlugins, currentPlugins, string.Join(", ", newPlugins));

                var annotation = BuildAnnotationMessage(newPlugins, kernel);
                chatHistory.AddSystemMessage(annotation);
                annotated = true;

                logger?.LogInformation("[ToolAvailability] Injected stale-result annotation into chat history ({Length} chars)", annotation.Length);
            }
            else
            {
                logger?.LogDebug(
                    "[ToolAvailability] No annotation needed. HistoryCount={HistoryCount}, CurrentHasLocal={CurrentHasLocal}, HadLocalTools={HadLocalTools}, PluginsChanged={PluginsChanged}",
                    chatHistory.Count, currentHasLocal, HadLocalTools, currentPlugins != PreviousPlugins);
            }

            // Update state for the next turn
            HadLocalTools = currentHasLocal;
            PreviousPlugins = currentPlugins;

            return annotated;
        }

        /// <summary>
        /// Determines which plugin names are new compared to the previous turn.
        /// </summary>
        private List<string> GetNewPluginNames(string currentPluginsCsv)
        {
            var previousSet = new HashSet<string>(
                PreviousPlugins.Split(',', StringSplitOptions.RemoveEmptyEntries));
            var currentList = currentPluginsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
            return currentList.Where(p => !previousSet.Contains(p)).ToList();
        }

        /// <summary>
        /// Builds the system message that tells the LLM about newly available local tools
        /// and instructs it to re-execute file operations instead of reusing stale results.
        /// </summary>
        private static string BuildAnnotationMessage(List<string> newPluginNames, Kernel kernel)
        {
            var newToolDetails = new List<string>();
            foreach (var pluginName in newPluginNames)
            {
                var plugin = kernel.Plugins.FirstOrDefault(p => p.Name == pluginName);
                if (plugin != null)
                {
                    var toolNames = string.Join(", ", plugin.Select(f => f.Name));
                    newToolDetails.Add($"  - {pluginName}: {toolNames}");
                }
            }

            var toolSection = newToolDetails.Count > 0
                ? "New local tools now available:\n" + string.Join("\n", newToolDetails)
                : "New local desktop tools are now available.";

            return $@"[TOOL SET CHANGE NOTICE]
Your available tools have changed since the previous message. Local desktop tools are now connected that were NOT available during previous responses in this conversation.

{toolSection}

IMPORTANT: Any file search, file read, or file access results from earlier in this conversation were obtained using cloud-only (remote) tools because local tools were unavailable at that time. Those results may be incomplete or different from what the local desktop tools would return.

ACTION REQUIRED: If the user's current request involves file operations, you MUST re-execute those operations using the newly available local tools. Do NOT reuse or reference file results from earlier messages — they are stale.";
        }
    }
}
