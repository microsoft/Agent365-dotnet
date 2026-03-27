// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Agents.A365.Observability.Runtime.Etw
{
    /// <summary>
    /// ETW Event Source for Observability.
    /// Call <see cref="Initialize"/> once at startup to configure the singleton,
    /// then access it via <see cref="Log"/>. If <see cref="Log"/> is accessed
    /// before <see cref="Initialize"/>, a default instance (with throw on errors) is created.
    /// </summary>
    [EventSource(Name = "A365-O11y-EventSource")]
    public class EtwEventSource : EventSource
    {
        private static EtwEventSource? _instance;

        /// <summary>
        /// Gets the singleton instance. Creates a default instance (with
        /// <see cref="EventSourceSettings.ThrowOnEventWriteErrors"/>) if
        /// <see cref="Initialize"/> has not been called.
        /// </summary>
        public static EtwEventSource Log => _instance ??= new EtwEventSource(EventSourceSettings.ThrowOnEventWriteErrors);

        private EtwEventSource() : base() { }

        private EtwEventSource(EventSourceSettings settings) : base(settings) { }

        /// <summary>
        /// Initializes the singleton with the specified settings.
        /// Must be called before the first access of <see cref="Log"/>.
        /// </summary>
        /// <param name="suppressThrowOnEventWriteErrors">
        /// When <see langword="true"/>, the underlying <see cref="EventSource"/> will be created without
        /// <see cref="EventSourceSettings.ThrowOnEventWriteErrors"/>. By default, throw on errors is enabled.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the singleton has already been created.
        /// </exception>
        public static void Initialize(bool suppressThrowOnEventWriteErrors = false)
        {
            if (_instance != null)
            {
                throw new InvalidOperationException(
                    "EtwEventSource has already been initialized. Initialize() must be called before the first access of Log.");
            }

            _instance = suppressThrowOnEventWriteErrors
                ? new EtwEventSource()
                : new EtwEventSource(EventSourceSettings.ThrowOnEventWriteErrors);
        }

        /// <summary>
        /// Resets the singleton so tests can exercise <see cref="Initialize"/> on a fresh instance.
        /// </summary>
        internal static void ResetForTesting()
        {
            _instance?.Dispose();
            _instance = null;
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
