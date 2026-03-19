// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Agents.A365.Observability.Runtime.Etw
{
    /// <summary>
    /// ETW Event Source for Observability
    /// </summary>
    [EventSource(Name = "A365-O11y-EventSource")]
    public class EtwEventSource : EventSource
    {
        private static volatile EtwEventSource _log = new EtwEventSource();
        private static readonly object _lock = new object();
        private static bool _initialized = false;

        /// <summary>
        /// Singleton instance of the EtwEventSource.
        /// </summary>
        public static EtwEventSource Log => _log;

        private EtwEventSource() : base() { }

        private EtwEventSource(EventSourceSettings settings) : base(settings) { }

        /// <summary>
        /// Configures the singleton to throw on event write errors.
        /// Must be called once, before any events are written.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if called more than once.
        /// </exception>
        public static void Initialize(bool throwOnEventWriteErrors)
        {
            if (!throwOnEventWriteErrors) return;

            lock (_lock)
            {
                if (_initialized)
                {
                    throw new InvalidOperationException(
                        "EtwEventSource has already been initialized.");
                }

                var oldLog = _log;
                _log = new EtwEventSource(EventSourceSettings.ThrowOnEventWriteErrors);
                _initialized = true;
                oldLog.Dispose();
            }
        }

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
