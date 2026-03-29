// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Diagnostics.Tracing;

namespace Microsoft.Agents.A365.Observability.Runtime.Etw
{
    /// <summary>
    /// ETW Event Source for Observability.
    /// </summary>
    [EventSource(Name = "A365-O11y-EventSource")]
    public class EtwEventSource : EventSource
    {
        /// <summary>
        /// Singleton instance of the EtwEventSource.
        /// </summary>
        public static readonly EtwEventSource Log = new EtwEventSource(EventSourceSettings.ThrowOnEventWriteErrors);

        private EtwEventSource(EventSourceSettings settings) : base(settings) { }

        /// <summary>
        /// Handler for stopping a span.
        /// Writes an ETW event with the necessary information from the span.
        /// </summary>
        [Event(1000,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Message = "A365 Otel span: Name={0} Id={1} Body={4}")]
        public void SpanStop(string name, string spanId, string traceId, string parentSpanId, string content) =>
            WriteEvent(1000, name, spanId, traceId, parentSpanId, content);

        /// <summary>
        /// Handler for logging JSON messages.
        /// Writes an ETW event with the provided JSON message.
        /// </summary>
        [Event(2000,
            Level = EventLevel.Informational,
            Message = "{0}")]
        public void LogJson(string message) =>
            WriteEvent(2000, message);
    }
}
