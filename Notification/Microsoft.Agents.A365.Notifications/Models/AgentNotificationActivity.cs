using Microsoft.Agents.A365.Notifications.Models;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Text.Json.Nodes;

namespace Microsoft.Agents.A365.Notifications.Models
{
    /// <summary>
    /// Wraps an activity with strongly-typed agent notification data.
    /// </summary>
    public class AgentNotificationActivity
    {
        /// <summary>
        /// Gets or sets the Word/PowerPoint/Excel comment notification data.
        /// </summary>
        public WpxComment? WpxCommentNotification { get; set; }
        
        /// <summary>
        /// Gets or sets the email notification data.
        /// </summary>
        public EmailReference? EmailNotification { get; set; }
        
        /// <summary>
        /// Gets or sets the type of notification.
        /// </summary>
        public NotificationTypeEnum NotificationType { get; set; } = NotificationTypeEnum.Unknown;
        
        /// <summary>
        /// Gets or sets the conversation account information.
        /// </summary>
        public ConversationAccount? Conversation { get; set; }
        
        /// <summary>
        /// Gets or sets the sender's channel account.
        /// </summary>
        public ChannelAccount From { get; set; }
        
        /// <summary>
        /// Gets or sets the recipient's channel account.
        /// </summary>
        public ChannelAccount Recipient { get; set; }
        
        /// <summary>
        /// Gets or sets channel-specific data.
        /// </summary>
        public object ChannelData { get; set; }
        
        /// <summary>
        /// Gets or sets the text content of the activity.
        /// </summary>
        public string Text { get; set; }
        
        /// <summary>
        /// Gets or sets the type of value contained in the Value property.
        /// </summary>
        public string ValueType { get; set; }
        
        /// <summary>
        /// Gets or sets the value content of the activity.
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentNotificationActivity"/> class.
        /// </summary>
        /// <param name="activity">The activity to wrap.</param>
        public AgentNotificationActivity(IActivity activity)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));
            if ( activity.Entities != null && activity.Entities.Count > 0)
            {
                var wpxCommentEntity = activity.Entities.FirstOrDefault(e => e.Type.Equals(nameof(WpxComment),StringComparison.OrdinalIgnoreCase));
                if (wpxCommentEntity != null)
                {
                    WpxCommentNotification = ProtocolJsonSerializer.ToObject<WpxComment>(wpxCommentEntity) ?? new();
                    NotificationType = NotificationTypeEnum.WpxComment;
                }
                var emailEntity = activity.Entities.FirstOrDefault(e => e.Type.Equals(EmailReference.EntityTypeName,StringComparison.OrdinalIgnoreCase));
                if (emailEntity != null)
                {
                    EmailNotification = ProtocolJsonSerializer.ToObject<EmailReference>(emailEntity) ?? new();
                    NotificationType = NotificationTypeEnum.EmailNotification;
                }
            }

            // If NotificationType is still Unknown, we try to infer it from the Activity Sub channel name.
            if (NotificationType == NotificationTypeEnum.Unknown && !string.IsNullOrEmpty(activity.ChannelId?.SubChannel) )
            {
                if (activity.ChannelId.SubChannel.Equals(SubChannels.FederatedKnowledgeServiceSubChannel, StringComparison.OrdinalIgnoreCase))
                {
                    NotificationType = NotificationTypeEnum.FederatedKnowledgeServiceNotification;
                }
            }

            Conversation = activity.Conversation;
            From = activity.From ?? new ChannelAccount();
            Recipient = activity.Recipient ?? new ChannelAccount();
            ChannelData = activity.ChannelData ?? new JsonObject();
            Text = activity.Text ?? string.Empty;
            ValueType = activity.ValueType ?? string.Empty;
            Value = activity.Value ?? new JsonObject();
        }
    }
}
