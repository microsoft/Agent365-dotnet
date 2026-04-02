// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        /// <param name="operationName">The name of the operation being traced.</param>
        /// <param name="activityName">The name of the activity for display purposes.</param>
        /// <param name="agentDetails">Optional agent details. Tenant ID is read from <see cref="AgentDetails.TenantId"/>.</param>
        /// <param name="spanDetails">Optional span configuration including parent context, start/end times,
        /// span kind, and span links. Subclasses may override <see cref="SpanDetails.SpanKind"/> before calling this constructor;
        /// defaults to <see cref="ActivityKind.Client"/>.</param>
        /// <param name="userDetails">Optional human caller identity details (id, email, name, client IP).</param>
        protected OpenTelemetryScope(string operationName, string activityName, AgentDetails agentDetails, SpanDetails? spanDetails = null, UserDetails? userDetails = null)
        {
            var kind = spanDetails?.SpanKind ?? ActivityKind.Client;
            var parentContext = spanDetails?.ParentContext;
            var startTime = spanDetails?.StartTime;
            var endTime = spanDetails?.EndTime;
            var spanLinks = spanDetails?.SpanLinks;

            customStartTime = startTime;
            customEndTime = endTime;
            activity = parentContext.HasValue && parentContext.Value.TraceId != default
                ? ActivitySource.CreateActivity(activityName, kind, parentContext.Value, links: spanLinks)
                : ActivitySource.CreateActivity(activityName, kind, default(ActivityContext), links: spanLinks);

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
                SetTagMaybe(GenAiAgentVersionKey, agentDetails.AgentVersion);
                SetTagMaybe(AgentAUIDKey, agentDetails.AgenticUserId);
                SetTagMaybe(AgentEmailKey, agentDetails.AgenticUserEmail);
                SetTagMaybe(AgentBlueprintIdKey, agentDetails.AgentBlueprintId);
                SetTagMaybe(AgentPlatformIdKey, agentDetails.AgentPlatformId);
                SetTagMaybe(TenantIdKey, agentDetails.TenantId);
            }

            if (userDetails != null)
            {
                SetTagMaybe(UserIdKey, userDetails.UserId);
                SetTagMaybe(UserEmailKey, userDetails.UserEmail);
                SetTagMaybe(UserNameKey, userDetails.UserName);
                SetTagMaybe(CallerClientIpKey, userDetails.UserClientIP?.ToString());
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
        /// Gets the <see cref="ActivityContext"/> for this scope's span.
        /// </summary>
        /// <remarks>
        /// The returned context can be passed as <c>parentContext</c> to child scope
        /// <c>Start</c> methods to establish a parent-child relationship within the
        /// same process.
        /// </remarks>
        /// <returns>
        /// An <see cref="ActivityContext"/> for this scope's span, or <c>null</c> if
        /// no activity exists.
        /// </returns>
        public ActivityContext? GetActivityContext()
        {
            return activity?.Context;
        }

        /// <summary>
        /// Injects this span's trace context into W3C HTTP headers.
        /// </summary>
        /// <remarks>
        /// Returns a dictionary containing <c>traceparent</c> and optionally
        /// <c>tracestate</c> headers that can be forwarded to downstream services
        /// for distributed trace propagation.
        /// </remarks>
        /// <returns>
        /// A dictionary containing W3C trace context headers. Returns an empty
        /// dictionary if no activity exists.
        /// </returns>
        public Dictionary<string, string> InjectTraceContext()
        {
            var headers = new Dictionary<string, string>();
            if (activity != null && activity.Id != null)
            {
                headers["traceparent"] = activity.Id;
                if (!string.IsNullOrEmpty(activity.TraceStateString))
                {
                    headers["tracestate"] = activity.TraceStateString!;
                }
            }

            return headers;
        }

        /// <summary>
        /// Gets the span ID for the current activity.
        /// </summary>
        public string Id => activity?.Id ?? string.Empty;

        /// <summary>
        /// Gets the trace ID for the current activity.
        /// </summary>
        public string TraceId => activity?.TraceId.ToHexString().ToLowerInvariant() ?? string.Empty;
    }
}