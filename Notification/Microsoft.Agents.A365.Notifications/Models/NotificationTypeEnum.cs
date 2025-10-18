namespace Microsoft.Agents.A365.Notifications.Models
{
    /// <summary>
    /// Enumeration of notification types supported by the agent notification system.
    /// </summary>
    public enum NotificationTypeEnum
    {
        /// <summary>
        /// Unknown or unrecognized notification type.
        /// </summary>
        Unknown = 0,
        
        /// <summary>
        /// Notification for Word/PowerPoint/Excel comment.
        /// </summary>
        WpxComment = 1,
        
        /// <summary>
        /// Email notification.
        /// </summary>
        EmailNotification = 2,
        
        /// <summary>
        /// Federated Knowledge Service notification.
        /// </summary>
        FederatedKnowledgeServiceNotification = 3
    }
}
