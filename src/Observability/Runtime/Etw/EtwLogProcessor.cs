// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.Etw
{
    /// <summary>
    /// Processes logs by emitting ETW events.
    /// </summary>
    public class EtwLogProcessor : BaseProcessor<LogRecord>
    {
        private readonly ExportFormatter _formatter;
        private readonly ILogger<EtwLogProcessor>? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EtwLogProcessor"/> class.
        /// </summary>
        /// <param name="formatter">The formatter used to format log data.</param>
        /// <param name="logger">The logger used to log messages.</param>
        public EtwLogProcessor(ExportFormatter formatter, ILogger<EtwLogProcessor>? logger = null)
        {
            _formatter = formatter;
            _logger = logger;
        }
        /// <summary>
        /// Emits an ETW event with log details.
        /// </summary>
        public override void OnEnd(LogRecord data)
        {
            var attributes = new Dictionary<string, object?>();
            if (data.Attributes != null) {
                foreach (var kvp in data.Attributes)
                {
                    attributes[kvp.Key] = kvp.Value;
                }
            }

            var jsonContent = _formatter.FormatLogData(attributes);

            _logger?.LogInformation($"EtwLogProcessor: Emitting ETW log event. `Name`: {attributes["Name"]} `SpanId`: {attributes["SpanId"]}");

            EtwEventSource.Log.LogJson(jsonContent);
        }
    }
}
