// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
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

            // Cleanup
            var tp = provider.GetRequiredService<TracerProvider>();
            tp?.ForceFlush();
            tp?.Dispose();
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", "false");
        }

        [TestMethod]
        public async Task AddTracing_And_ExecuteToolScope_ExporterMakesExpectedRequest()
        {
            // Arrange
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", "true");

            var receivedRequest = false;
            string? receivedContent = null;
            var expectedAgentDetails = new AgentDetails(
                agentId: Guid.NewGuid().ToString(),
                agentName: "Tool Agent",
                agentDescription: "Agent for tool execution.",
                agentAUID: Guid.NewGuid().ToString(),
                agentUPN: "toolagent@ztaitest12.onmicrosoft.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                agentType: AgentType.Foundry,
                tenantId: Guid.NewGuid().ToString());
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var endpoint = new Uri("https://tool-endpoint:8443");
            var toolCallDetails = new ToolCallDetails(
                toolName: "TestTool",
                arguments: "{\"param\":\"value\"}",
                toolCallId: "call-456",
                description: "Test tool call description",
                toolType: "custom-type",
                endpoint: endpoint);

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

            // Act
            using (var scope = ExecuteToolScope.Start(toolCallDetails, expectedAgentDetails, tenantDetails))
            {
                scope.RecordResponse("Tool response content");
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

            GetAttribute(attributes, "gen_ai.operation.name").Should().Be("execute_tool");
            GetAttribute(attributes, "gen_ai.agent.id").Should().Be(expectedAgentDetails.AgentId);
            GetAttribute(attributes, "gen_ai.agent.name").Should().Be(expectedAgentDetails.AgentName);
            GetAttribute(attributes, "gen_ai.agent.description").Should().Be(expectedAgentDetails.AgentDescription);
            GetAttribute(attributes, "gen_ai.agent.userid").Should().Be(expectedAgentDetails.AgentAUID);
            GetAttribute(attributes, "gen_ai.agent.upn").Should().Be(expectedAgentDetails.AgentUPN);
            GetAttribute(attributes, "gen_ai.agent.applicationid").Should().Be(expectedAgentDetails.AgentBlueprintId);
            GetAttribute(attributes, "gen_ai.agent.type").Should().Be(expectedAgentDetails.AgentType.ToString());
            GetAttribute(attributes, "tenant.id").Should().Be(tenantDetails.TenantId.ToString());
            GetAttribute(attributes, "gen_ai.tool.name").Should().Be(toolCallDetails.ToolName);
            GetAttribute(attributes, "gen_ai.tool.arguments").Should().Be(toolCallDetails.Arguments);
            GetAttribute(attributes, "gen_ai.tool.call.id").Should().Be(toolCallDetails.ToolCallId);
            GetAttribute(attributes, "gen_ai.tool.description").Should().Be(toolCallDetails.Description);
            GetAttribute(attributes, "gen_ai.tool.type").Should().Be(toolCallDetails.ToolType);
            GetAttribute(attributes, "server.address").Should().Be(endpoint.Host);
            GetAttribute(attributes, "server.port").Should().Be(endpoint.Port.ToString());
            GetAttribute(attributes, "gen_ai.event.content").Should().Be("Tool response content");

            // Cleanup
            var tp = provider.GetRequiredService<TracerProvider>();
            tp?.ForceFlush();
            tp?.Dispose();
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", "false");
        }

        [TestMethod]
        public async Task AddTracing_And_InferenceScope_ExporterMakesExpectedRequest()
        {
            // Arrange
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", "true");

            var receivedRequest = false;
            string? receivedContent = null;
            var expectedAgentDetails = new AgentDetails(
                agentId: Guid.NewGuid().ToString(),
                agentName: "Inference Agent",
                agentDescription: "Agent for inference testing.",
                agentAUID: Guid.NewGuid().ToString(),
                agentUPN: "inferenceagent@ztaitest12.onmicrosoft.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                agentType: AgentType.MicrosoftCopilot,
                tenantId: Guid.NewGuid().ToString());
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

            var inferenceDetails = new InferenceCallDetails(
                operationName: InferenceOperationType.Chat,
                model: "gpt-4",
                providerName: "OpenAI",
                inputTokens: 42,
                outputTokens: 84,
                finishReasons: new[] { "stop", "length" },
                responseId: "response-xyz");

            // Act
            using (var scope = InferenceScope.Start(inferenceDetails, expectedAgentDetails, tenantDetails))
            {
                scope.RecordInputMessages(new[] { "Hello", "World" });
                scope.RecordOutputMessages(new[] { "Hi there!" });
                scope.RecordInputTokens(42);
                scope.RecordOutputTokens(84);
                scope.RecordResponseId("response-xyz");
                scope.RecordFinishReasons(new[] { "stop", "length" });
                scope.RecordThoughtProcess("Reasoning step 1; step 2");
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

            GetAttribute(attributes, "gen_ai.operation.name").Should().Be(inferenceDetails.OperationName.ToString());
            GetAttribute(attributes, "gen_ai.agent.id").Should().Be(expectedAgentDetails.AgentId);
            GetAttribute(attributes, "gen_ai.agent.name").Should().Be(expectedAgentDetails.AgentName);
            GetAttribute(attributes, "gen_ai.agent.description").Should().Be(expectedAgentDetails.AgentDescription);
            GetAttribute(attributes, "gen_ai.agent.userid").Should().Be(expectedAgentDetails.AgentAUID);
            GetAttribute(attributes, "gen_ai.agent.upn").Should().Be(expectedAgentDetails.AgentUPN);
            GetAttribute(attributes, "gen_ai.agent.applicationid").Should().Be(expectedAgentDetails.AgentBlueprintId);
            GetAttribute(attributes, "gen_ai.agent.type").Should().Be(expectedAgentDetails.AgentType.ToString());
            GetAttribute(attributes, "tenant.id").Should().Be(tenantDetails.TenantId.ToString());
            GetAttribute(attributes, "gen_ai.request.model").Should().Be(inferenceDetails.Model);
            GetAttribute(attributes, "gen_ai.provider.name").Should().Be(inferenceDetails.ProviderName);
            GetAttribute(attributes, "gen_ai.usage.input_tokens").Should().Be("42");
            GetAttribute(attributes, "gen_ai.usage.output_tokens").Should().Be("84");
            GetAttribute(attributes, "gen_ai.response.finish_reasons").Should().Be("stop,length");
            GetAttribute(attributes, "gen_ai.response.id").Should().Be("response-xyz");
            GetAttribute(attributes, "gen_ai.input.messages").Should().Be("Hello,World");
            GetAttribute(attributes, "gen_ai.output.messages").Should().Be("Hi there!");
            GetAttribute(attributes, "gen_ai.agent.thought.process").Should().Be("Reasoning step 1; step 2");

            // Cleanup
            var tp = provider.GetRequiredService<TracerProvider>();
            tp?.ForceFlush();
            tp?.Dispose();
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", "false");
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
            if (attributes.TryGetProperty(key, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
                if (value.ValueKind == JsonValueKind.Number)
                {
                    return value.GetRawText(); // Converts number to string
                }
                // If value is an object with a "stringValue" property
                if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("stringValue", out var sv))
                {
                    return sv.GetString();
                }
            }
            return null;
        }
    }
}
