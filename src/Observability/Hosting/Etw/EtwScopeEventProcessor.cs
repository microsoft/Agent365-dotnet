// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Runtime.Common;
using OpenTelemetry;
using OpenTelemetry.Resources;
using System.Diagnostics;

namespace Microsoft.Agents.A365.Observability.Hosting.Etw
{
    /// <summary>
    /// Processes spans by emitting ETW events.
    /// </summary>
    public class EtwScopeEventProcessor: BaseProcessor<Activity>
    {
        private readonly Resource _resource;

        /// <summary>
        /// Initializes a new instance of the <see cref="EtwScopeEventProcessor"/> class.
        /// </summary>
        /// <param name="resource">The OpenTelemetry resource to use for event formatting.</param>
        public EtwScopeEventProcessor(Resource? resource = null)
        {
            _resource = resource ?? ResourceBuilder.CreateEmpty().Build();
        }

        /// <summary>
        /// Called when an activity ends. Emits an ETW event with span details.
        /// </summary>
        public override void OnEnd(Activity data)
        {
            var activityContent = ExportFormatter.FormatSingle(data, _resource);

            EtwEventSource.Log.SpanStop(
                data.DisplayName,
                data.SpanId.ToHexString(),
                data.TraceId.ToHexString(),
                data.ParentSpanId.ToHexString(),
                activityContent);
        }
    }
}
