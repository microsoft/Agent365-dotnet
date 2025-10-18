using Microsoft.Agents.Core.Models;
using Microsoft.Agents.A365.Notifications.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Notifications.Extensions
{
    /// <summary>
    /// Extension methods for IActivity to extract notification-specific data.
    /// </summary>
    public static class ActivityExtension
    {
        /// <summary>
        /// Extracts the EmailReference entity from the activity's entities collection.
        /// </summary>
        /// <param name="activity">The activity to extract from.</param>
        /// <returns>The EmailReference if found; otherwise, null.</returns>
        public static EmailReference? GetEmailReference(this IActivity activity)
        {
            if (activity.Entities == null || activity.Entities.Count == 0)
            {
                return null;
            }

            var entity = activity.Entities.FirstOrDefault(e => string.Equals(e.Type, "emailnotification", StringComparison.OrdinalIgnoreCase));
            return entity?.GetAs<EmailReference>();
        }
        
        /// <summary>
        /// Extracts the WpxComment entity from the activity's entities collection.
        /// </summary>
        /// <param name="activity">The activity to extract from.</param>
        /// <returns>The WpxComment if found; otherwise, null.</returns>
        public static WpxComment? GetWpxComment(this IActivity activity)
        {
            if (activity.Entities == null || activity.Entities.Count == 0)
            {
                return null;
            }
            var entity = activity.Entities.FirstOrDefault(e => string.Equals(e.Type, "wpxcomment", StringComparison.OrdinalIgnoreCase));
            return entity?.GetAs<WpxComment>();
        }

        /// <summary>
        /// Wraps the activity in an AgentNotificationActivity.
        /// </summary>
        /// <param name="activity">The activity to wrap.</param>
        /// <returns>A new AgentNotificationActivity wrapping the provided activity.</returns>
        public static AgentNotificationActivity GetAgentNotificationActivity(this IActivity activity)
        {
            return new AgentNotificationActivity(activity);
        }
    }
}
