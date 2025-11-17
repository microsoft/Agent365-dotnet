// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using OpenTelemetry;
using OpenTelemetry.Logs;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Hosting.Etw
{
    /// <summary>
    /// Processes logs by emitting ETW events.
    /// </summary>
    public class EtwLogProcessor : BaseProcessor<LogRecord>
    {
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

            var jsonContent = LogSerializer.Serialize(attributes);

            EtwEventSource.Log.LogJson(jsonContent);
        }
    }
}
