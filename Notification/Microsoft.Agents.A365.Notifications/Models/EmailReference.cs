using Microsoft.Agents.Core.Models;


namespace Microsoft.Agents.A365.Notifications.Models
{
    /// <summary>
    /// Represents an email reference entity containing email notification details.
    /// </summary>
    public class EmailReference : Entity
    {
        // Need this because the Entity Type name is different then the class name.
        /// <summary>
        /// The entity type name used for serialization ("emailNotification").
        /// </summary>
        public static readonly string EntityTypeName = "emailNotification";

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailReference"/> class.
        /// </summary>
        public EmailReference() : base(EntityTypeName)
        {

        }

        /// <summary>
        /// Gets or sets the email identifier.
        /// </summary>
        public string? Id { get; set; }
        
        /// <summary>
        /// Gets or sets the conversation identifier for the email thread.
        /// </summary>
        public string? ConversationId { get; set; }
        
        /// <summary>
        /// Gets or sets the HTML body content of the email.
        /// </summary>
        public string? HtmlBody { get; set; }
    }
}
