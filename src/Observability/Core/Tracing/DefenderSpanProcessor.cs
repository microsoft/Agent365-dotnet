// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Tracing;

using Azure.Monitor.Ingestion;
using OpenTelemetry;
using System.Diagnostics;
using static Microsoft.Agents.A365.Observability.Contracts.OpenTelemetryConstants;

/// <summary>
/// Processor for sending activity data to Azure Analytics Workspace, inheriting from ActivityProcessor.
/// </summary>
internal sealed class AzureAnalyticsWorkspaceProcessor(
    LogsIngestionClient logsIngestionClient,
    string ruleId,
    string streamName)
    : BaseProcessor<Activity>
{
    public override void OnEnd(Activity data)
    {
        // Call the base class first to maintain existing functionality
        base.OnEnd(data);

        try
        {
            // Extract agent information from the activity
            var agentId = data.GetTagItem(GenAiAgentIdKey) ?? 
                         data.GetBaggageItem(GenAiAgentIdKey);
            
            var agentName = data.GetTagItem(GenAiAgentNameKey) ?? 
                           data.GetBaggageItem(GenAiAgentNameKey) ?? 
                           "Unknown Agent Name";

            // If agentId is null or empty, we're not in an agent context
            if (string.IsNullOrEmpty(agentId?.ToString()))
            {
                return;
            }
            
            using var scope = SuppressInstrumentationScope.Begin();

            // Convert Activity to Azure Monitor log record matching Python structure
            var record = new
            {
                @object = "trace.span",
                id = data.Id,
                trace_id = data.TraceId.ToString(),
                parent_id = data.ParentId,
                started_at = data.StartTimeUtc.ToString("O"), // ISO 8601 format
                ended_at = (data.StartTimeUtc + data.Duration).ToString("O"),
                span_data = new
                {
                    // Map Activity properties to span_data structure
                    type = data.OperationName,
                    name = data.DisplayName,
                    kind = data.Kind.ToString(),
                    status = data.Status.ToString(),
                    source = data.Source?.Name,
                    // Add all tags as attributes
                    attributes = data.Tags.ToDictionary(tag => tag.Key, tag => tag.Value),
                    // Add baggage if any, handle duplicate keys from A2A
                    baggage = data.Baggage.GroupBy(tag => tag.Key, tag => tag.Value)
                        .ToDictionary(b => b.Key, b => b.ToArray()),
                    // Add agent-specific details extracted from activity
                    agent_id = agentId,
                    agent_name = agentName,
                    // Add events
                    events = data.Events.Select(e => new
                    {
                        name = e.Name,
                        timestamp = e.Timestamp.ToString("O"),
                        attributes = e.Tags?.ToDictionary(tag => tag.Key, tag => tag.Value?.ToString()) ?? new Dictionary<string, string?>()
                    }).ToArray(),
                    // Add links if any
                    links = data.Links.Select(link => new
                    {
                        trace_id = link.Context.TraceId.ToString(),
                        span_id = link.Context.SpanId.ToString(),
                        attributes = link.Tags?.ToDictionary(tag => tag.Key, tag => tag.Value?.ToString()) ?? new Dictionary<string, string?>()
                    }).ToArray()
                },
                error = GetErrorInfo(data),
                TimeGenerated = DateTime.UtcNow.ToString("O")
            };

            var logs = new[] { record };

            // Send to Analytics Workspace asynchronously
         _ = logsIngestionClient.UploadAsync(ruleId, streamName, logs)
                .ContinueWith(task =>
                {
                    if (task.Exception != null)
                    {
                        Console.WriteLine($"❌ Failed to send data to Analytics Workspace: {task.Exception.GetBaseException().Message}");
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex)
        {
            // Log the error but don't throw to avoid breaking the telemetry pipeline
            Console.WriteLine($"❌ Error in AzureAnalyticsWorkspaceProcessor: {ex.Message}");
        }
    }

    private static object? GetErrorInfo(Activity data)
    {
        // Check if the activity has error information
        var errorMessage = data.Tags.FirstOrDefault(tag => tag.Key == "error.message").Value;
        var errorType = data.Tags.FirstOrDefault(tag => tag.Key == "error.type").Value;

        // Also check status for errors
        if (data.Status == ActivityStatusCode.Error || !string.IsNullOrEmpty(errorMessage) || !string.IsNullOrEmpty(errorType))
        {
            return new
            {
                message = errorMessage ?? data.StatusDescription,
                type = errorType ?? "ActivityError"
            };
        }

        return null;
    }
}