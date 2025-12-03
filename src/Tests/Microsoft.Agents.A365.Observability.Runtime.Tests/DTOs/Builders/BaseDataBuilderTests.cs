using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs;
using Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs.Builders
{
    [TestClass]
    public class BaseDataBuilderTests
    {
        private sealed class TestBuilder : BaseDataBuilder<BaseData>
        {
            public static IDictionary<string, object?> BuildAll(
                AgentDetails? agent = null,
                TenantDetails? tenant = null,
                Uri? endpoint = null,
                Request? request = null,
                CallerDetails? caller = null,
                AgentDetails? callerAgent = null,
                string[]? input = null,
                string[]? output = null)
            {
                var dict = new Dictionary<string, object?>();
                if (agent != null) AddAgentDetails(dict, agent);
                if (tenant != null) AddTenantDetails(dict, tenant);
                if (endpoint != null) AddEndpointDetails(dict, endpoint);
                if (request != null) AddRequestDetails(dict, request);
                if (caller != null) AddCallerDetails(dict, caller);
                if (callerAgent != null) AddCallerAgentDetails(dict, callerAgent);
                AddInputMessagesAttributes(dict, input);
                AddOutputMessagesAttributes(dict, output);
                return dict;
            }
        }

        [TestMethod]
        public void AddAgentDetails_PopulatesExpectedKeys()
        {
            var agent = new AgentDetails("agent-1", "AgentName", "Desc", agentAUID: "auid", agentUPN: "upn", agentBlueprintId: "bp", tenantId: "tenant-x", agentPlatformId: "platform-123");
            var dict = TestBuilder.BuildAll(agent: agent);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiAgentIdKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiAgentNameKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiAgentDescriptionKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiAgentAUIDKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiAgentUPNKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiAgentBlueprintIdKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiAgentPlatformIdKey);
        }

        [TestMethod]
        public void AddTenantDetails_AddsTenantId()
        {
            var tenant = new TenantDetails(Guid.NewGuid());
            var dict = TestBuilder.BuildAll(tenant: tenant);
            dict.Should().ContainKey(OpenTelemetryConstants.TenantIdKey);
        }

        [TestMethod]
        public void AddEndpointDetails_AddsHostAndPort()
        {
            var endpoint = new Uri("https://example.com:8080");
            var dict = TestBuilder.BuildAll(endpoint: endpoint);
            dict.Should().ContainKey(OpenTelemetryConstants.ServerAddressKey);
            dict.Should().ContainKey(OpenTelemetryConstants.ServerPortKey);
        }

        [TestMethod]
        public void AddEndpointDetails_StandardPort_OmitsPort()
        {
            var endpoint = new Uri("https://example.com:443");
            var dict = TestBuilder.BuildAll(endpoint: endpoint);
            dict.Should().ContainKey(OpenTelemetryConstants.ServerAddressKey);
            dict.Should().NotContainKey(OpenTelemetryConstants.ServerPortKey);
        }

        [TestMethod]
        public void AddRequestDetails_PopulatesRequestKeys()
        {
            var request = new Request("content", ExecutionType.HumanToAgent, "session", new SourceMetadata(id: "src-id", name: "src-name", role: Role.Human, description: "src-desc"));
            var dict = TestBuilder.BuildAll(request: request);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiChannelLinkKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiChannelNameKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiExecutionTypeKey);
        }

        [TestMethod]
        public void AddCallerDetails_PopulatesCallerKeys()
        {
            var caller = new CallerDetails("caller-1", "Caller Name", "caller@upn", tenantId: "tenant-y");
            var dict = TestBuilder.BuildAll(caller: caller);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiCallerIdKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiCallerUpnKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiCallerNameKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiCallerTenantIdKey);
        }

        [TestMethod]
        public void AddCallerAgentDetails_PopulatesCallerAgentKeys()
        {
            var callerAgent = new AgentDetails("c-agent", "CallerAgent", agentAUID: "ca-uid", agentUPN: "ca-upn", agentBlueprintId: "ca-bp", tenantId: "ca-tenant", agentPlatformId: "ca-platform");
            var dict = TestBuilder.BuildAll(callerAgent: callerAgent);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiCallerAgentIdKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiCallerAgentNameKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiCallerAgentApplicationIdKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiCallerAgentAUIDKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiCallerAgentUPNKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiCallerAgentTenantKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiCallerAgentPlatformIdKey);
        }

        [TestMethod]
        public void AddInputMessagesAttributes_JoinsMessages()
        {
            var dict = TestBuilder.BuildAll(input: new[] { "one", "two" });
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            dict[OpenTelemetryConstants.GenAiInputMessagesKey].Should().Be("one,two");
        }

        [TestMethod]
        public void AddOutputMessagesAttributes_JoinsMessages()
        {
            var dict = TestBuilder.BuildAll(output: new[] { "out1", "out2" });
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
            dict[OpenTelemetryConstants.GenAiOutputMessagesKey].Should().Be("out1,out2");
        }

        [TestMethod]
        public void AddInputMessagesAttributes_EmptyArray_OmitsKey()
        {
            var dict = TestBuilder.BuildAll(input: Array.Empty<string>());
            dict.Should().NotContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
        }

        [TestMethod]
        public void AddOutputMessagesAttributes_EmptyArray_OmitsKey()
        {
            var dict = TestBuilder.BuildAll(output: Array.Empty<string>());
            dict.Should().NotContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
        }

        [TestMethod]
        public void AddIfNotNull_DoesNotAddNullValues()
        {
            var dict = TestBuilder.BuildAll();
            dict.Should().BeEmpty();
        }
    }
}
