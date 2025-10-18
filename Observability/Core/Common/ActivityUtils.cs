using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.Builder;

namespace Microsoft.Agents.A365.Observability.Common;

/// <summary>
/// Utility class for Activity-related operations.
/// </summary>
public class ActivityUtils
{
    private static readonly string ChannelIdAgents = "agents";
    private static readonly string EntityTypeWpxComment = "wpxcomment";
    private static readonly string EntityTypeEmailNotification = "emailNotification";
    private static readonly string WpxCommentConversationIdFormat = "{0}_{1}";

    /// <summary>Sets the conversation ID baggage value from TurnContext.</summary>
    public static BaggageBuilder ConversationId(BaggageBuilder baggageBuilder, ITurnContext turnContext)
    {
        if (turnContext is null)
        {
            throw new ArgumentNullException(nameof(turnContext));
        }

        if (turnContext.Activity.ChannelId == ChannelIdAgents)
        {
            var wpxCommentEntity = turnContext.Activity.Entities?.FirstOrDefault(e => (e as dynamic)?.type == EntityTypeWpxComment);
            if (wpxCommentEntity != null)
            {
                dynamic entity = wpxCommentEntity;
                string documentId = entity.documentId;
                string parentCommentId = entity.parentCommentId;
                if (!string.IsNullOrWhiteSpace(documentId) && !string.IsNullOrWhiteSpace(parentCommentId))
                {
                    string conversationId = string.Format(WpxCommentConversationIdFormat, documentId, parentCommentId);
                    baggageBuilder.Set(OpenTelemetryConstants.GenAiConversationIdKey, conversationId);
                }
            }
            else
            {
                var emailNotificationEntity = turnContext.Activity.Entities?.FirstOrDefault(e => (e as dynamic)?.type == EntityTypeEmailNotification);
                if (emailNotificationEntity != null)
                {
                    dynamic entity = emailNotificationEntity;
                    string conversationId = entity.conversationId;
                    if (!string.IsNullOrWhiteSpace(conversationId))
                    {
                        baggageBuilder.Set(OpenTelemetryConstants.GenAiConversationIdKey, conversationId);
                    }
                }
            }
        }
        else
        {
            baggageBuilder.Set(OpenTelemetryConstants.GenAiConversationIdKey, turnContext?.Activity?.Conversation?.Id);
        }

        return baggageBuilder;
    }
}
