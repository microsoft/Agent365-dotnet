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

        /// <summary>
        /// Creates a new message <see cref="IActivity"/> and attaches an <see cref="EmailResponse"/> entity
        /// containing the supplied HTML body.
        /// </summary>
        /// <param name="emailResponseHtmlBody">The HTML body to embed in the <see cref="EmailResponse"/> entity. May be null or empty.</param>
        /// <returns>
        /// A message activity whose <see cref="IActivity.Entities"/> collection includes a single <see cref="EmailResponse"/>
        /// entity populated with the specified HTML body.
        /// </returns>
        /// <remarks>
        /// The returned activity does not set <see cref="IActivity.Text"/>; consumers are expected to render or process
        /// the HTML via the attached <see cref="EmailResponse"/> entity. Additional activity properties (e.g. importance,
        /// locale, attachments) can be set by the caller after creation.
        /// </remarks>
        /// <example>
        /// <code>
        /// var activity = EmailResponse.CreateEmailResponseActivity("<p>Processed results attached.</p>");
        /// // Optionally set routing or other metadata:
        /// activity.Locale = "en-US";
        /// </code>
        /// </example>
        public static IActivity CreateEmailResponseActivity(string emailResponseHtmlBody)
        {
            var workingActivity = Activity.CreateMessageActivity();
            var emailResponse = new EmailResponse(emailResponseHtmlBody);
            workingActivity.Entities ??= new List<Entity>();
            workingActivity.Entities.Add(emailResponse);
            return workingActivity;
        }
    }

}
