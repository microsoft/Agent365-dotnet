using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Contracts
{
    /// <summary>
    /// Provides methods to serialize log data.
    /// </summary>
    public class LogSerializer
    {
        /// <summary>
        /// Formats the log data for the OTLP payload.
        /// </summary>
        /// <param name="data">The operation data containing the log information.</param>
        /// <returns>A JSON string representing the OTLP payload for the log data.</returns>
        public static string Serialize(IDictionary<string, object?> data)
        {
            var payload = new
            {
                Name = data["Name"],
                Attributes = data["Attributes"],
                StartTimeUnixNano = data.TryGetValue("StartTime", out var startTimeObj) && startTimeObj != null ? DatetimeHelper.ToUnixNanos(((DateTimeOffset)startTimeObj).UtcDateTime) : 0,
                EndTimeUnixNano = data.TryGetValue("EndTime", out var endTimeObj) && endTimeObj != null ? DatetimeHelper.ToUnixNanos(((DateTimeOffset)endTimeObj).UtcDateTime) : 0,
                SpanId = data["SpanId"],
                ParentSpanId = data["ParentSpanId"]
            };

            return SerializePayload(payload);
        }

        private static string SerializePayload<T>(T payload)
        {
            return JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = null,
                WriteIndented = false
            });
        }
    }
}
