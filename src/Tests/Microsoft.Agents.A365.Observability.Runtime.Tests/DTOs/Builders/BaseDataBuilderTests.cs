// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
                Uri? endpoint = null,
                Request? request = null,
                CallerDetails? caller = null,
                AgentDetails? callerAgent = null,
                string[]? input = null,
                string[]? output = null)
            {
                var dict = new Dictionary<string, object?>();
                if (agent != null) AddAgentDetails(dict, agent);
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
            var agent = new AgentDetails("agent-1", "AgentName", "Desc", agenticUserId: "auid", agenticUserEmail: "upn", agentBlueprintId: "bp", tenantId: "tenant-x", agentPlatformId: "platform-123");
            var dict = TestBuilder.BuildAll(agent: agent);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiAgentIdKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiAgentNameKey);
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiAgentDescriptionKey);
            dict.Should().ContainKey(OpenTelemetryConstants.AgentAUIDKey);
            dict.Should().ContainKey(OpenTelemetryConstants.AgentEmailKey);
            dict.Should().ContainKey(OpenTelemetryConstants.AgentBlueprintIdKey);
            dict.Should().ContainKey(OpenTelemetryConstants.AgentPlatformIdKey);
            dict.Should().ContainKey(OpenTelemetryConstants.TenantIdKey);
        }

        [TestMethod]
        public void AddAgentDetails_AddsTenantId()
        {
            var agent = new AgentDetails("agent-1", tenantId: Guid.NewGuid().ToString());
            var dict = TestBuilder.BuildAll(agent: agent);
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
            var request = new Request("content", sessionId: "session", channel: new Channel(name: "src-name", link: "src-desc"));
            var dict = TestBuilder.BuildAll(request: request);
            dict.Should().ContainKey(OpenTelemetryConstants.ChannelLinkKey);
            dict.Should().ContainKey(OpenTelemetryConstants.ChannelNameKey);
        }

        [TestMethod]
        public void AddCallerDetails_PopulatesCallerKeys()
        {
            var caller = new CallerDetails(
                userDetails: new UserDetails(userId: "caller-1", userName: "Caller Name", userEmail: "caller@upn"));
            var dict = TestBuilder.BuildAll(caller: caller);
            dict.Should().ContainKey(OpenTelemetryConstants.UserIdKey);
            dict.Should().ContainKey(OpenTelemetryConstants.UserEmailKey);
            dict.Should().ContainKey(OpenTelemetryConstants.UserNameKey);
        }

        [TestMethod]
        public void AddCallerAgentDetails_PopulatesCallerAgentKeys()
        {
            var callerAgent = new AgentDetails("c-agent", "CallerAgent", agenticUserId: "ca-uid", agenticUserEmail: "ca-upn", agentBlueprintId: "ca-bp", tenantId: "ca-tenant", agentPlatformId: "ca-platform");
            var dict = TestBuilder.BuildAll(callerAgent: callerAgent);
            dict.Should().ContainKey(OpenTelemetryConstants.CallerAgentIdKey);
            dict.Should().ContainKey(OpenTelemetryConstants.CallerAgentNameKey);
            dict.Should().ContainKey(OpenTelemetryConstants.CallerAgentBlueprintIdKey);
            dict.Should().ContainKey(OpenTelemetryConstants.CallerAgentAUIDKey);
            dict.Should().ContainKey(OpenTelemetryConstants.CallerAgentEmailKey);
            dict.Should().ContainKey(OpenTelemetryConstants.CallerAgentPlatformIdKey);
        }

        [TestMethod]
        public void AddInputMessagesAttributes_JoinsMessages()
        {
            var dict = TestBuilder.BuildAll(input: new[] { "one", "two" });
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            dict[OpenTelemetryConstants.GenAiInputMessagesKey]!.ToString()!.Should().Contain("one").And.Contain("two").And.Contain("\"version\":\"0.1.0\"");
        }

        [TestMethod]
        public void AddOutputMessagesAttributes_JoinsMessages()
        {
            var dict = TestBuilder.BuildAll(output: new[] { "out1", "out2" });
            dict.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
            dict[OpenTelemetryConstants.GenAiOutputMessagesKey]!.ToString()!.Should().Contain("out1").And.Contain("out2").And.Contain("\"version\":\"0.1.0\"");
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
