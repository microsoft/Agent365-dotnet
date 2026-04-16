// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry;

namespace Microsoft.Agents.A365.Observability.Extensions.IntegrationTests;

/// <summary>
/// Exporter that captures activities at export time — after all processors have run.
/// Use with <see cref="SimpleActivityExportProcessor"/> to capture spans at the final stage.
/// </summary>
internal sealed class ActivityCapturingExporter : BaseExporter<Activity>
{
    private readonly List<Activity> _activities;

    public ActivityCapturingExporter(List<Activity> activities) => _activities = activities;

    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch)
        {
            _activities.Add(activity);
        }
        return ExportResult.Success;
    }
}
