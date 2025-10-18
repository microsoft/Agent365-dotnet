using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.A365.Notifications.Models
{
    /// <summary>
    /// Represents an email response entity containing HTML body content.
    /// </summary>
    public class EmailResponse : Entity
    {
        /// <summary>
        /// HTML Body of the email Response.
        /// </summary>
        public string? HtmlBody { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailResponse"/> class.
        /// </summary>
        /// <param name="emailHtmlBody">The HTML body content of the email response.</param>
        public EmailResponse(string? emailHtmlBody = default) : base("emailResponse")
        {
            HtmlBody = emailHtmlBody;
        }
    }

}
