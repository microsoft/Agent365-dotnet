// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    /// <summary>
    /// Base class for OpenTelemetry tracing scopes in the AI SDK, providing common telemetry functionality.
    /// </summary>
    public abstract class OpenTelemetryScope : IDisposable
    {
        private static readonly ActivitySource ActivitySource = new ActivitySource(SourceName);
        private static readonly Meter Meter = new Meter(SourceName);

        private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
            GenAiClientOperationDurationMetricName, "s", "Measures GenAI operation duration.");

        private readonly Activity? activity;
        private readonly Stopwatch? duration;
        private readonly TagList commonTags;
        private DateTimeOffset? customStartTime;
        private DateTimeOffset? customEndTime;

        private string? errorType;
        private Exception? exception;
        private int hasEnded = 0;

        /// <summary>
        /// Initializes a new instance of the OpenTelemetryScope class.
        /// </summary>
        /// <param name="kind">The kind of activity (Client, Server, Internal, etc.).</param>
        /// <param name="agentDetails">Agent details</param>
        /// <param name="tenantDetails"></param>
        /// <param name="operationName">The name of the operation being traced.</param>
        /// <param name="activityName">The name of the activity for display purposes.</param>
        /// <param name="startTime">Optional custom start time for the scope. If not provided, the current time is used.</param>
        /// <param name="parentId">Optional parent ID for the activity.</param>
        protected OpenTelemetryScope(ActivityKind kind, AgentDetails agentDetails, TenantDetails tenantDetails, string operationName, string activityName, DateTimeOffset? startTime = null, string? parentId = null)
        {
            customStartTime = startTime;
            activity = ActivitySource.CreateActivity(activityName, kind, default(ActivityContext));
            if (!string.IsNullOrEmpty(parentId))
            {
                activity?.SetParentId(parentId!);
            }

            if (startTime != null) 
            {
                activity?.SetStartTime(startTime.Value.UtcDateTime);
            }

            commonTags = new TagList
                {
                    { GenAiOperationNameKey, operationName },
                };

            foreach (var kv in commonTags)
            {
                activity?.SetTag(kv.Key, kv.Value);
            }

            if (agentDetails != null)
            {
                SetTagMaybe(GenAiAgentIdKey, agentDetails.AgentId);
                SetTagMaybe(GenAiAgentNameKey, agentDetails.AgentName);
                SetTagMaybe(GenAiAgentDescriptionKey, agentDetails.AgentDescription);
                SetTagMaybe(GenAiAgentAUIDKey, agentDetails.AgentAUID);
                SetTagMaybe(GenAiAgentUPNKey, agentDetails.AgentUPN);
                SetTagMaybe(GenAiAgentBlueprintIdKey, agentDetails.AgentBlueprintId);
            }

            if (tenantDetails != null)
            {
                SetTagMaybe(TenantIdKey, tenantDetails.TenantId);
            }

            // Only start the stopwatch if no custom start time is provided
            if (!customStartTime.HasValue)
            {
                duration = Stopwatch.StartNew();
            }

            activity?.Start();
        }

        /// <summary>
        /// Log the error.
        /// </summary>
        /// <param name="e">Exception thrown by completion call.</param>
        public void RecordError(Exception e)
        {
            if (e is RequestFailedException requestFailed && requestFailed.Status != 0)
            {
                errorType = requestFailed.Status.ToString();
            }
            else
            {
                errorType = e.GetType().FullName ?? "error";
            }

            exception = e;
        }

        /// <summary>
        /// Record the task cancellation event.
        /// </summary>
        public void RecordCancellation()
        {
            errorType = typeof(TaskCanceledException).FullName;
            exception = null;
        }

        /// <summary>
        /// Sets a custom start time for the scope. This allows for manual control of the scope start time.
        /// Can be used in addition to or instead of setting start time via constructor.
        /// </summary>
        /// <param name="startTime">The start time to set for this scope.</param>
        public void SetStartTime(DateTimeOffset startTime)
        {
            customStartTime = startTime;
            activity?.SetStartTime(startTime.UtcDateTime);
        }

        /// <summary>
        /// Sets a custom end time for the scope. This allows for manual control of the scope duration.
        /// </summary>
        /// <param name="endTime">The end time to set for this scope.</param>
        public void SetEndTime(DateTimeOffset endTime)
        {
            customEndTime = endTime;
        }

        /// <summary>
        /// Record the events and metrics associated with the response.
        /// </summary>
        private void End()
        {
            var finalTags = commonTags;
            if (errorType != null)
            {
                finalTags.Add(ErrorTypeKey, errorType);
                activity?.SetTag(ErrorTypeKey, errorType);
                activity?.SetStatus(ActivityStatusCode.Error, exception?.Message);
            }

            // Calculate duration based on custom times if provided, otherwise use stopwatch
            double durationSeconds;
            if (customStartTime.HasValue && customEndTime.HasValue)
            {
                durationSeconds = (customEndTime.Value - customStartTime.Value).TotalSeconds;
                // Set the end time on the activity if we have custom times
                activity?.SetEndTime(customEndTime.Value.UtcDateTime);
            }
            else if (customStartTime.HasValue && !customEndTime.HasValue)
            {
                // Start time was custom but end time is now, calculate from custom start to now
                var endTime = DateTimeOffset.UtcNow;
                durationSeconds = (endTime - customStartTime.Value).TotalSeconds;
                activity?.SetEndTime(endTime.UtcDateTime);
            }
            else
            {
                // Use stopwatch for normal operation
                durationSeconds = duration?.Elapsed.TotalSeconds ?? 0;
            }

            Duration.Record(durationSeconds, finalTags);
        }

        /// <summary>
        /// Disposes the scope and finalizes telemetry data collection.
        /// </summary>
        public void Dispose()
        {
            // check if the scope has already ended
            if (Interlocked.Exchange(ref hasEnded, 1) == 0)
            {
                End();
                activity?.Dispose();
            }
        }

        /// <summary>
        /// Set the tag on the activity if the tag is present.
        /// </summary>
        /// <param name="name">The name of tag to set.</param>
        /// <param name="value">Nullable value to be set.</param>
        public void SetTagMaybe(string name, object? value)
        {
            if (value != null)
            {
                activity?.SetTag(name, value);
            }
        }

        /// <summary>
        /// Records multiple attribute key/value pairs for telemetry tracking.
        /// </summary>
        /// <param name="attributes">Collection of attribute key/value pairs.</param>
        public void RecordAttributes(IEnumerable<KeyValuePair<string, object?>> attributes)
        {
            if (attributes is null) return;
            foreach (var kv in attributes)
            {
                if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                activity?.SetTag(kv.Key, kv.Value);
            }
        }

        /// <summary>
        /// Adds baggage to the current activity for distributed tracing context propagation.
        /// </summary>
        /// <param name="key">The baggage key.</param>
        /// <param name="value">The baggage value.</param>
        protected void AddBaggage(string key, string value)
        {
            activity?.AddBaggage(key, value);
        }

        /// <summary>
        /// Gets the span ID for the current activity.
        /// </summary>
        public string Id => activity?.Id ?? string.Empty;
    }
}