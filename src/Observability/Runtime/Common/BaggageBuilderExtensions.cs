using Microsoft.Agents.Builder;
using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Common
{
    /// <summary>
    /// Utility class for BaggageBuilder extensions.
    /// </summary>
    public static class BaggageBuilderExtensions
    {
        /// <summary>
        /// Sets the baggage values from TurnContext.
        /// </summary>
        public static BaggageBuilder FromTurnContext(this BaggageBuilder baggageBuilder, ITurnContext turnContext)
        {
            if (turnContext is null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            baggageBuilder
                .SetCallerBaggage(turnContext)
                .SetExecutionTypeBaggage(turnContext)
                .SetTargetAgentBaggage(turnContext)
                .SetTenantIdBaggage(turnContext)
                .SetSourceMetadataBaggage(turnContext)
                .SetConversationIdBaggage(turnContext);

            return baggageBuilder;
        }

        /// <summary>
        /// Sets the caller-related baggage values from the TurnContext.
        /// </summary>
        /// <param name="baggageBuilder">The BaggageBuilder instance.</param>
        /// <param name="turnContext">The turn context containing activity information.</param>
        /// <returns>The updated BaggageBuilder instance.</returns>
        public static BaggageBuilder SetCallerBaggage(this BaggageBuilder baggageBuilder, ITurnContext turnContext)
        {
            baggageBuilder.SetRange(turnContext.GetCallerBaggagePairs());
            return baggageBuilder;
        }

        /// <summary>
        /// Sets the execution type baggage value based on caller and recipient agentic status.
        /// </summary>
        /// <param name="baggageBuilder">The BaggageBuilder instance.</param>
        /// <param name="turnContext">The turn context containing activity information.</param>
        /// <returns>The updated BaggageBuilder instance.</returns>
        public static BaggageBuilder SetExecutionTypeBaggage(this BaggageBuilder baggageBuilder, ITurnContext turnContext)
        {
            baggageBuilder.SetRange(turnContext.GetExecutionTypePair());
            return baggageBuilder;
        }

        /// <summary>
        /// Sets the target agent-related baggage values from the TurnContext.
        /// </summary>
        /// <param name="baggageBuilder">The BaggageBuilder instance.</param>
        /// <param name="turnContext">The turn context containing activity information.</param>
        /// <returns>The updated BaggageBuilder instance.</returns>
        public static BaggageBuilder SetTargetAgentBaggage(this BaggageBuilder baggageBuilder, ITurnContext turnContext)
        {
            baggageBuilder.SetRange(turnContext.GetTargetAgentBaggagePairs());
            return baggageBuilder;
        }

        /// <summary>
        /// Sets the tenant ID baggage value, extracting from ChannelData if necessary.
        /// </summary>
        /// <param name="baggageBuilder">The BaggageBuilder instance.</param>
        /// <param name="turnContext">The turn context containing activity information.</param>
        /// <returns>The updated BaggageBuilder instance.</returns>
        public static BaggageBuilder SetTenantIdBaggage(this BaggageBuilder baggageBuilder, ITurnContext turnContext)
        {
            baggageBuilder.SetRange(turnContext.GetTenantIdPair());
            return baggageBuilder;
        }

        /// <summary>
        /// Sets the source metadata baggage values from the TurnContext.
        /// </summary>
        /// <param name="baggageBuilder">The BaggageBuilder instance.</param>
        /// <param name="turnContext">The turn context containing activity information.</param>
        /// <returns>The updated BaggageBuilder instance.</returns>
        public static BaggageBuilder SetSourceMetadataBaggage(this BaggageBuilder baggageBuilder, ITurnContext turnContext)
        {
            baggageBuilder.SetRange(turnContext.GetSourceMetadataBaggagePairs());
            return baggageBuilder;
        }

        /// <summary>
        /// Sets the conversation ID and item link baggage values from the TurnContext.
        /// </summary>
        /// <param name="baggageBuilder">The BaggageBuilder instance.</param>
        /// <param name="turnContext">The turn context containing activity information.</param>
        /// <returns>The updated BaggageBuilder instance.</returns>
        public static BaggageBuilder SetConversationIdBaggage(this BaggageBuilder baggageBuilder, ITurnContext turnContext)
        {
            baggageBuilder.SetRange(turnContext.GetConversationIdAndItemLinkPairs());
            return baggageBuilder;
        }
    }
}