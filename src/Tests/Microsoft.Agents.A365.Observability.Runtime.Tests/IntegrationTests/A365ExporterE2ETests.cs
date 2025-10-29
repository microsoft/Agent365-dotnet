// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.IntegrationTests
{
    [TestClass]
    public class A365ExporterE2ETests
    {
        [TestMethod]
        public async Task AddTracing_And_InvokeAgentScope_ExporterMakesExpectedRequest()
        {
            // Arrange
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", "true");

            var receivedRequest = false;
            string? receivedContent = null;
            var expectedAgentDetails = new AgentDetails(
                agentId: Guid.NewGuid().ToString(),
                agentName: "Test Agent", 
                agentDescription: "Agent for testing.", 
                agentAUID: Guid.NewGuid().ToString(),
                agentUPN: "testagent@ztaitest12.onmicrosoft.com", 
                agentBlueprintId: Guid.NewGuid().ToString(),
                agentType: AgentType.EntraEmbodied,
                tenantId: Guid.NewGuid().ToString());
            var endpoint = new Uri("https://test-agent-endpoint");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, expectedAgentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            var handler = new TestHttpMessageHandler(req =>
            {
                receivedRequest = true;
                receivedContent = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                req.RequestUri.Should().NotBeNull();
                req.RequestUri!.ToString().Should().Contain($"/maven/agent365/agents/{expectedAgentDetails.AgentId}/traces");
                req.Headers.Authorization.Should().NotBeNull();
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            });
            var httpClient = new HttpClient(handler);

            var services = new ServiceCollection();
            services.AddSingleton<HttpClient>(httpClient);
            services.AddSingleton<Agent365ExporterOptions>(sp =>
            {
                return new Agent365ExporterOptions
                {
                    UseS2SEndpoint = false,
                    ClusterCategory = "test",
                    TokenResolver = (_, _) => Task.FromResult<string?>("test-token")
                };
            });
            services.AddTracing(useOpenTelemetryBuilder: false);
            var provider = services.BuildServiceProvider();

            var expectedRequest = new Request(
                content: "Test request content",
                executionType: ExecutionType.HumanToAgent,
                channelMetadata: new ChannelMetadata(
                    name: "msteams",
                    link: "https://testchannel.link"));

            var expectedCallerDetails = new CallerDetails(
                callerId: "caller-123",
                callerName: "Test Caller",
                callerUpn: "caller-123@ztaitest12.onmicrosoft.com",
                callerUserId: Guid.NewGuid().ToString(),
                tenantId: expectedAgentDetails.TenantId);

            // Act
            using (var scope = InvokeAgentScope.Start(
                invokeAgentDetails: invokeAgentDetails, 
                tenantDetails: tenantDetails, 
                request: expectedRequest, 
                callerDetails: expectedCallerDetails))
            {
                scope.RecordInputMessages(new[] { "Input message 1", "Input message 2" });
                // Simulate work
                scope.RecordOutputMessages(new[] { "Output message 1" });
            } // Dispose triggers activity export

            // Wait for up to 30 seconds or until receivedRequest is true, whichever happens first
            var timeout = TimeSpan.FromSeconds(30);
            var start = DateTime.UtcNow;
            while (!receivedRequest && DateTime.UtcNow - start < timeout)
            {
                await Task.Delay(1).ConfigureAwait(false);
            }

            // Assert
            receivedRequest.Should().BeTrue("Exporter should make the expected HTTP request.");
            receivedContent.Should().NotBeNull("Exporter should send a request body.");

            using var doc = JsonDocument.Parse(receivedContent!);
            var root = doc.RootElement;

            var attributes = root
                .GetProperty("resourceSpans")[0]
                .GetProperty("scopeSpans")[0]
                .GetProperty("spans")[0]
                .GetProperty("attributes");
            GetAttribute(attributes, "server.address").Should().Be(invokeAgentDetails.Endpoint.Host);
            GetAttribute(attributes, "gen_ai.channel.name").Should().Be(expectedRequest.ChannelMetadata?.Name);
            GetAttribute(attributes, "gen_ai.channel.link").Should().Be(expectedRequest.ChannelMetadata?.Link);
            GetAttribute(attributes, "gen_ai.execution.type").Should().Be(expectedRequest.ExecutionType.ToString());
            GetAttribute(attributes, "tenant.id").Should().Be(tenantDetails.TenantId.ToString());
            GetAttribute(attributes, "gen_ai.caller.id").Should().Be(expectedCallerDetails.CallerId);
            GetAttribute(attributes, "gen_ai.caller.upn").Should().Be(expectedCallerDetails.CallerUpn);
            GetAttribute(attributes, "gen_ai.caller.name").Should().Be(expectedCallerDetails.CallerName);
            GetAttribute(attributes, "gen_ai.caller.userid").Should().Be(expectedCallerDetails.CallerUserId);
            GetAttribute(attributes, "gen_ai.caller.tenantid").Should().Be(expectedCallerDetails.TenantId);
            GetAttribute(attributes, "gen_ai.input.messages").Should().Be("Input message 1,Input message 2");
            GetAttribute(attributes, "gen_ai.output.messages").Should().Be("Output message 1");
            GetAttribute(attributes, "gen_ai.agent.id").Should().Be(expectedAgentDetails.AgentId);
            GetAttribute(attributes, "gen_ai.agent.name").Should().Be(expectedAgentDetails.AgentName);
            GetAttribute(attributes, "gen_ai.agent.description").Should().Be(expectedAgentDetails.AgentDescription);
            GetAttribute(attributes, "gen_ai.agent.userid").Should().Be(expectedAgentDetails.AgentAUID);
            GetAttribute(attributes, "gen_ai.agent.upn").Should().Be(expectedAgentDetails.AgentUPN);
            GetAttribute(attributes, "gen_ai.agent.applicationid").Should().Be(expectedAgentDetails.AgentBlueprintId);
            GetAttribute(attributes, "gen_ai.agent.type").Should().Be(expectedAgentDetails.AgentType.ToString());
            GetAttribute(attributes, "tenant.id").Should().Be(tenantDetails.TenantId.ToString());
            GetAttribute(attributes, "gen_ai.operation.name").Should().Be("invoke_agent");
        }

        private class TestHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
            public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_handler(request));
            }
        }

        private string? GetAttribute(JsonElement attributes, string key)
        {
            if (attributes.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
            // If value is an object with a "stringValue" property
            if (attributes.TryGetProperty(key, out var objValue) && objValue.ValueKind == JsonValueKind.Object)
            {
                if (objValue.TryGetProperty("stringValue", out var sv))
                    return sv.GetString();
            }
            return null;
        }
    }
}
