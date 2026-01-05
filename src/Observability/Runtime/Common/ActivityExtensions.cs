// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Microsoft.Agents.A365.Observability.Runtime.Common
{
    /// <summary>
    /// Extension methods for Activity.
    /// </summary>
    public static class ActivityExtensions
    {
        /// <summary>
        /// Gets the value of a tag (takes precedence) or baggage item from the activity.
        /// </summary>
        public static string? GetAttributeOrBaggage(this Activity activity, string key)
        {
            var tagValue = activity.GetTagItem(key);
            if (tagValue != null)
            {
                return tagValue.ToString();
            }

            var baggageValue = activity.GetBaggageItem(key);
            return string.IsNullOrEmpty(baggageValue) ? null : baggageValue;
        }

        /// <summary>
        /// Sets a tag on the activity only if it does not already exist.
        /// </summary>
        public static void CoalesceTag(this Activity activity, string key, params string?[] values)
        {
            var tagValue = activity.GetTagItem(key);
            if (tagValue == null)
            {
                foreach (var value in values)
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        activity.SetTag(key, value);
                        break;
                    }
                }
            }
        }
    }
}
