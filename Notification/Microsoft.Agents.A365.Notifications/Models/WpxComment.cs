using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.A365.Notifications.Models
{
    /// <summary>
    /// Represents a Word/PowerPoint/Excel comment notification entity.
    /// </summary>
    public class WpxComment : Entity
    {
        /// <summary>
        /// Gets or sets the OData identifier for the comment.
        /// </summary>
        public string? OdataId { get; set; }
        
        /// <summary>
        /// Gets or sets the document identifier where the comment was made.
        /// </summary>
        public string? DocumentId { get; set; }
        
        /// <summary>
        /// Gets or sets the identifier of the parent comment if this is a reply.
        /// </summary>
        public string? ParentCommentId { get; set; }
        
        /// <summary>
        /// Gets or sets the unique identifier of this comment.
        /// </summary>
        public string? CommentId { get; set; }
    }
}
