// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Runtime.Common;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="EtwLogProcessor"/> class.
        /// </summary>
        /// <param name="formatter">The formatter used to format log data.</param>
        public EtwLogProcessor(ExportFormatter formatter)
        {
            _formatter = formatter;
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

            EtwEventSource.Log.LogJson(jsonContent);
        }
    }
}
