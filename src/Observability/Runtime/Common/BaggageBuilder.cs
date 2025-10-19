using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using OpenTelemetry;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.Common
{
    /// <summary>Per request baggage builder</summary>
    public sealed class BaggageBuilder
    {
        private sealed class Scope : IDisposable
        {
            private readonly Baggage _previous;
            private bool _disposed;
            public Scope(Baggage prev) => _previous = prev;
            public void Dispose()
            {
                if (_disposed) return;
                Baggage.Current = _previous;
                _disposed = true;
            }
        }

        private readonly Dictionary<string, string?> _pairs = new Dictionary<string, string?>();

        /// <summary>Sets the operation source baggage value.</summary>
        public BaggageBuilder OperationSource(string? v) { Set(OpenTelemetryConstants.OperationSourceKey, v); return this; }

        /// <summary>Sets the tenant ID baggage value.</summary>
        public BaggageBuilder TenantId(string? v) { Set(OpenTelemetryConstants.TenantIdKey, v); return this; }

        /// <summary>Sets the agent ID baggage value.</summary>
        public BaggageBuilder AgentId(string? v) { Set(OpenTelemetryConstants.GenAiAgentIdKey, v); return this; }

        /// <summary>Sets the agent AUID baggage value.</summary>
        public BaggageBuilder AgentAuid(string? v) { Set(OpenTelemetryConstants.GenAiAgentAUIDKey, v); return this; }

        /// <summary>Sets the agent UPN baggage value.</summary>
        public BaggageBuilder AgentUpn(string? v) { Set(OpenTelemetryConstants.GenAiAgentUPNKey, v); return this; }

        /// <summary>Sets the agent blueprint ID baggage value.</summary>
        public BaggageBuilder AgentBlueprintId(string? v) { Set(OpenTelemetryConstants.GenAiAgentBlueprintIdKey, v); return this; }

        /// <summary>Sets the correlation ID baggage value.</summary>
        public BaggageBuilder CorrelationId(string? v) { Set(OpenTelemetryConstants.CorrelationIdKey, v); return this; }

        /// <summary>Sets the caller ID baggage value.</summary>
        public BaggageBuilder CallerId(string? v) { Set(OpenTelemetryConstants.GenAiCallerIdKey, v); return this; }

        /// <summary>Sets the hiring manager ID baggage value.</summary>
        public BaggageBuilder HiringManagerId(string? v) { Set(OpenTelemetryConstants.HiringManagerIdKey, v); return this; }

        /// <summary>Sets the conversation ID baggage value.</summary>
        public BaggageBuilder ConversationId(string? v) { Set(OpenTelemetryConstants.GenAiConversationIdKey, v); return this; }

        /// <summary>Applies the collected baggage to the current context.</summary>
        public IDisposable Build()
        {
            var previous = Baggage.Current;
            // Iterate through all key/value pairs and set them in _pairs
            foreach (var kvp in _pairs)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Key))
                {
                    Baggage.Current = Baggage.Current.SetBaggage(kvp.Key, kvp.Value);
                }
            }
            return new Scope(previous);
        }

        /// <summary>Convenience: begin a request baggage scope with common fields.</summary>
        public static IDisposable SetRequestContext(string? tenantId, string? agentId, string? correlationId = null)
            => new BaggageBuilder()
                .TenantId(tenantId)
                .AgentId(agentId)
                .CorrelationId(correlationId)
                .Build();


        /// <summary>Adds a baggage key/value if the value is not null or whitespace.</summary>
        public void Set(string k, string? v) { if (!string.IsNullOrWhiteSpace(v)) _pairs[k] = v; }
    }


}
