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
        private static readonly object _lock = new object();
        private static bool _throwOnEventWriteErrors = false;
        private static Lazy<EtwEventSource> _lazy =
            new Lazy<EtwEventSource>(CreateInstance);

        private static EtwEventSource CreateInstance() =>
            _throwOnEventWriteErrors
                ? new EtwEventSource(EventSourceSettings.ThrowOnEventWriteErrors)
                : new EtwEventSource();

        /// <summary>
        /// Singleton instance of the EtwEventSource.
        /// </summary>
        public static EtwEventSource Log
        {
            get
            {
                if (_lazy.IsValueCreated)
                {
                    return _lazy.Value;
                }

                lock (_lock)
                {
                    return _lazy.Value;
                }
            }
        }

        private EtwEventSource() : base() { }

        private EtwEventSource(EventSourceSettings settings) : base(settings) { }

        /// <summary>
        /// Configures the singleton before it is first used.
        /// Must be called before accessing <see cref="Log"/>.
        /// </summary>
        /// <param name="throwOnEventWriteErrors">
        /// When <see langword="true"/>, the underlying <see cref="EventSource"/> will be created with
        /// <see cref="EventSourceSettings.ThrowOnEventWriteErrors"/>.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the singleton has already been created (i.e. <see cref="Log"/> has been accessed).
        /// </exception>
        public static void Configure(bool throwOnEventWriteErrors)
        {
            lock (_lock)
            {
                if (_lazy.IsValueCreated)
                {
                    throw new InvalidOperationException(
                        "EtwEventSource has already been created. Configure() must be called before the first access of Log.");
                }

                _throwOnEventWriteErrors = throwOnEventWriteErrors;
            }
        }

        /// <summary>
        /// Resets the singleton so tests can exercise <see cref="Configure"/> on a fresh instance.
        /// </summary>
        internal static void ResetForTesting()
        {
            lock (_lock)
            {
                if (_lazy.IsValueCreated)
                {
                    _lazy.Value.Dispose();
                }

                _throwOnEventWriteErrors = false;
                _lazy = new Lazy<EtwEventSource>(CreateInstance);
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
