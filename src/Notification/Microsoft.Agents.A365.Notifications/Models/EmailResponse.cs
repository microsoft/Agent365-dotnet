using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Notifications.Models
{
    /// <summary>
    /// Represents an email response entity containing HTML body content.
    /// </summary>
    [EntityName(name:"emailNotification")]
    public class EmailResponse : Entity
    {
        /// <summary>
        /// HTML Body of the email Response.
        /// </summary>
        public string? HtmlBody { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailResponse"/> class.
        /// </summary>
        /// <param name="htmlBody">The HTML body content of the email response.</param>
        [JsonConstructor]
        public EmailResponse(string? htmlBody = default) : base("emailResponse")
        {
            HtmlBody = htmlBody;
        }
    }

}
