// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.Builder;

namespace Microsoft.Agents.A365.Observability.Hosting.Extensions
{
    /// <summary>
    /// Extension methods for InvokeAgentScope.
    /// </summary>
    public static class InvokeAgentScopeExtensions
    {
        /// <summary>
        /// Sets tag values from TurnContext.
        /// </summary>
        public static InvokeAgentScope FromTurnContext(this InvokeAgentScope invokeAgentScope, ITurnContext turnContext)
        {
            if (turnContext is null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            invokeAgentScope
                .SetCallerTags(turnContext)
                .SetTargetAgentTags(turnContext)
                .SetTenantIdTags(turnContext)
                .SetChannelTags(turnContext)
                .SetConversationIdTags(turnContext);

            return invokeAgentScope;
        }

        /// <summary>
        /// Sets the caller-related attribute values from the TurnContext.
        /// </summary>
        /// <param name="invokeAgentScope">The InvokeAgentScope instance.</param>
        /// <param name="turnContext">The turn context containing activity information.</param>
        /// <returns>The updated InvokeAgentScope instance.</returns>
        public static InvokeAgentScope SetCallerTags(this InvokeAgentScope invokeAgentScope, ITurnContext turnContext)
        {
            invokeAgentScope.RecordAttributes(turnContext.GetCallerBaggagePairs());
            return invokeAgentScope;
        }

        /// <summary>
        /// Sets the target agent-related tags from the TurnContext.
        /// </summary>
        public static InvokeAgentScope SetTargetAgentTags(this InvokeAgentScope invokeAgentScope, ITurnContext turnContext)
        {
            invokeAgentScope.RecordAttributes(turnContext.GetTargetAgentBaggagePairs());
            return invokeAgentScope;
        }

        /// <summary>
        /// Sets the tenant ID tag, extracting from ChannelData if necessary.
        /// </summary>
        public static InvokeAgentScope SetTenantIdTags(this InvokeAgentScope invokeAgentScope, ITurnContext turnContext)
        {
            invokeAgentScope.RecordAttributes(turnContext.GetTenantIdPair());
            return invokeAgentScope;
        }

        /// <summary>
        /// Sets the channel tags from the TurnContext.
        /// </summary>
        public static InvokeAgentScope SetChannelTags(this InvokeAgentScope invokeAgentScope, ITurnContext turnContext)
        {
            invokeAgentScope.RecordAttributes(turnContext.GetChannelBaggagePairs());
            return invokeAgentScope;
        }

        /// <summary>
        /// Sets the conversation ID and item link tags from the TurnContext.
        /// </summary>
        public static InvokeAgentScope SetConversationIdTags(this InvokeAgentScope invokeAgentScope, ITurnContext turnContext)
        {
            invokeAgentScope.RecordAttributes(turnContext.GetConversationIdAndItemLinkPairs());
            return invokeAgentScope;
        }

        /// <summary>
        /// Sets the input message tag from the TurnContext using the structured OTEL message format.
        /// </summary>
        public static InvokeAgentScope SetInputMessageTags(this InvokeAgentScope invokeAgentScope, ITurnContext turnContext)
        {
            var text = turnContext?.Activity?.Text;
            if (text != null)
            {
                invokeAgentScope.RecordInputMessages(new[] { text });
            }

            return invokeAgentScope;
        }
    }
}
