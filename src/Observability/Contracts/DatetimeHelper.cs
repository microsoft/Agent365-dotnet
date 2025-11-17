using System;

namespace Microsoft.Agents.A365.Observability.Contracts
{
    /// <summary>
    /// Provides helper methods for working with date and time values in contracts and serialization.
    /// </summary>
    public class DatetimeHelper
    {
        /// <summary>
        /// Converts a UTC DateTime to Unix time in nanoseconds.
        /// </summary>
        /// <param name="datetime">The datetime to convert.</param>
        /// <returns>The Unix time in nanoseconds.</returns>
        public static ulong ToUnixNanos(DateTime datetime)
        {
            var dt = datetime.Kind == DateTimeKind.Utc ? datetime : datetime.ToUniversalTime();
            var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var ns = (dt - unixEpoch).Ticks * 100;
            return (ulong)ns;
        }
    }
}
