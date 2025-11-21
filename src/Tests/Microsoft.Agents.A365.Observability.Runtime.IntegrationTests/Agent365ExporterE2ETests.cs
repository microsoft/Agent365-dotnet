// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.IntegrationTests
{
    [TestClass]
    public class Agent365ExporterE2ETests
    {
        private TestHttpMessageHandler? _handler;
        private ServiceProvider? _provider;
        private bool _receivedRequest;
        private string? _receivedContent;

        public Agent365ExporterE2ETests()
        {
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", "true");
        }

        [TestMethod]
        public async Task AddTracing_And_InvokeAgentScope_ExporterMakesExpectedRequest()
        {
            // Arrange
            this.SetupExporterTest();
            this._receivedRequest = false;
            this._receivedContent = null;
            var expectedAgentDetails = new AgentDetails(
                agentId: Guid.NewGuid().ToString(),
                agentName: "Test Agent",
                agentDescription: "Agent for testing.",
                agentAUID: Guid.NewGuid().ToString(),
                agentUPN: "testagent@ztaitest12.onmicrosoft.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString());
            var endpoint = new Uri("https://test-agent-endpoint");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint: endpoint, details: expectedAgentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            var expectedRequest = new Request(
                content: "Test request content",
                executionType: ExecutionType.HumanToAgent,
                sourceMetadata: new SourceMetadata(
                    name: "msteams",
                    description: "https://testchannel.link"));

            var expectedCallerDetails = new CallerDetails(
                callerId: "caller-123",
                callerName: "Test Caller",
                callerUpn: "caller-123@ztaitest12.onmicrosoft.com",
                tenantId: expectedAgentDetails.TenantId);

            // Act
            using (var scope = InvokeAgentScope.Start(
                invokeAgentDetails: invokeAgentDetails,
                tenantDetails: tenantDetails,
                request: expectedRequest,
                callerDetails: expectedCallerDetails))
            {
                scope.RecordInputMessages(new[] { "Input message 1", "Input message 2" });
                scope.RecordOutputMessages(new[] { "Output message 1" });
            }

            var timeout = TimeSpan.FromSeconds(30);
            var start = DateTime.UtcNow;
            while (!this._receivedRequest && DateTime.UtcNow - start < timeout)
            {
                await Task.Delay(1000).ConfigureAwait(false);
            }

            this._receivedRequest.Should().BeTrue("Exporter should make the expected HTTP request.");
            this._receivedContent.Should().NotBeNull("Exporter should send a request body.");

            using var doc = JsonDocument.Parse(this._receivedContent!);
            var root = doc.RootElement;



            var attributes = root
                .GetProperty("resourceSpans")[0]
                .GetProperty("scopeSpans")[0]
                .GetProperty("spans")[0]
                .GetProperty("attributes");
            this.GetAttribute(attributes, "server.address").Should().Be(invokeAgentDetails.Endpoint?.Host);
            this.GetAttribute(attributes, "gen_ai.channel.name").Should().Be(expectedRequest.SourceMetadata?.Name);
            this.GetAttribute(attributes, "gen_ai.channel.link").Should().Be(expectedRequest.SourceMetadata?.Description);
            this.GetAttribute(attributes, "gen_ai.execution.type").Should().Be(expectedRequest.ExecutionType.ToString());
            this.GetAttribute(attributes, "tenant.id").Should().Be(tenantDetails.TenantId.ToString());
            this.GetAttribute(attributes, "gen_ai.caller.id").Should().Be(expectedCallerDetails.CallerId);
            this.GetAttribute(attributes, "gen_ai.caller.upn").Should().Be(expectedCallerDetails.CallerUpn);
            this.GetAttribute(attributes, "gen_ai.caller.name").Should().Be(expectedCallerDetails.CallerName);
            this.GetAttribute(attributes, "gen_ai.caller.tenantid").Should().Be(expectedCallerDetails.TenantId);
            this.GetAttribute(attributes, "gen_ai.input.messages").Should().Be("Input message 1,Input message 2");
            this.GetAttribute(attributes, "gen_ai.output.messages").Should().Be("Output message 1");
            this.GetAttribute(attributes, "gen_ai.agent.id").Should().Be(expectedAgentDetails.AgentId);
            this.GetAttribute(attributes, "gen_ai.agent.name").Should().Be(expectedAgentDetails.AgentName);
            this.GetAttribute(attributes, "gen_ai.agent.description").Should().Be(expectedAgentDetails.AgentDescription);
            this.GetAttribute(attributes, "gen_ai.agent.userid").Should().Be(expectedAgentDetails.AgentAUID);
            this.GetAttribute(attributes, "gen_ai.agent.upn").Should().Be(expectedAgentDetails.AgentUPN);
            this.GetAttribute(attributes, "gen_ai.agent.applicationid").Should().Be(expectedAgentDetails.AgentBlueprintId);
            this.GetAttribute(attributes, "tenant.id").Should().Be(tenantDetails.TenantId.ToString());
            this.GetAttribute(attributes, "gen_ai.operation.name").Should().Be("invoke_agent");
        }

        [TestMethod]
        public async Task AddTracing_And_ExecuteToolScope_ExporterMakesExpectedRequest()
        {
            // Arrange
            this.SetupExporterTest();
            this._receivedRequest = false;
            this._receivedContent = null;
            var expectedAgentDetails = new AgentDetails(
                agentId: Guid.NewGuid().ToString(),
                agentName: "Tool Agent",
                agentDescription: "Agent for tool execution.",
                agentAUID: Guid.NewGuid().ToString(),
                agentUPN: "toolagent@ztaitest12.onmicrosoft.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
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

            // Act
            using (var scope = ExecuteToolScope.Start(toolCallDetails, expectedAgentDetails, tenantDetails))
            {
                scope.RecordResponse("Tool response content");
            }

            var timeout = TimeSpan.FromSeconds(30);
            var start = DateTime.UtcNow;
            while (!this._receivedRequest && DateTime.UtcNow - start < timeout)
            {
                await Task.Delay(1000).ConfigureAwait(false);
            }

            this._receivedRequest.Should().BeTrue("Exporter should make the expected HTTP request.");
            this._receivedContent.Should().NotBeNull("Exporter should send a request body.");

            using var doc = JsonDocument.Parse(this._receivedContent!);
            var root = doc.RootElement;

            var attributes = root
                .GetProperty("resourceSpans")[0]
                .GetProperty("scopeSpans")[0]
                .GetProperty("spans")[0]
                .GetProperty("attributes");

            this.GetAttribute(attributes, "gen_ai.operation.name").Should().Be("execute_tool");
            this.GetAttribute(attributes, "gen_ai.agent.id").Should().Be(expectedAgentDetails.AgentId);
            this.GetAttribute(attributes, "gen_ai.agent.name").Should().Be(expectedAgentDetails.AgentName);
            this.GetAttribute(attributes, "gen_ai.agent.description").Should().Be(expectedAgentDetails.AgentDescription);
            this.GetAttribute(attributes, "gen_ai.agent.userid").Should().Be(expectedAgentDetails.AgentAUID);
            this.GetAttribute(attributes, "gen_ai.agent.upn").Should().Be(expectedAgentDetails.AgentUPN);
            this.GetAttribute(attributes, "gen_ai.agent.applicationid").Should().Be(expectedAgentDetails.AgentBlueprintId);
            this.GetAttribute(attributes, "tenant.id").Should().Be(tenantDetails.TenantId.ToString());
            this.GetAttribute(attributes, "gen_ai.tool.name").Should().Be(toolCallDetails.ToolName);
            this.GetAttribute(attributes, "gen_ai.tool.arguments").Should().Be(toolCallDetails.Arguments);
            this.GetAttribute(attributes, "gen_ai.tool.call.id").Should().Be(toolCallDetails.ToolCallId);
            this.GetAttribute(attributes, "gen_ai.tool.description").Should().Be(toolCallDetails.Description);
            this.GetAttribute(attributes, "gen_ai.tool.type").Should().Be(toolCallDetails.ToolType);
            this.GetAttribute(attributes, "server.address").Should().Be(endpoint.Host);
            this.GetAttribute(attributes, "server.port").Should().Be(endpoint.Port.ToString());
            this.GetAttribute(attributes, "gen_ai.event.content").Should().Be("Tool response content");
        }

        [TestMethod]
        public async Task AddTracing_And_InferenceScope_ExporterMakesExpectedRequest()
        {
            // Arrange
            this.SetupExporterTest();
            this._receivedRequest = false;
            this._receivedContent = null;
            var expectedAgentDetails = new AgentDetails(
                agentId: Guid.NewGuid().ToString(),
                agentName: "Inference Agent",
                agentDescription: "Agent for inference testing.",
                agentAUID: Guid.NewGuid().ToString(),
                agentUPN: "inferenceagent@ztaitest12.onmicrosoft.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString());
            var tenantDetails = new TenantDetails(Guid.NewGuid());

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
            }

            var timeout = TimeSpan.FromSeconds(30);
            var start = DateTime.UtcNow;
            while (!this._receivedRequest && DateTime.UtcNow - start < timeout)
            {
                await Task.Delay(1000).ConfigureAwait(false);
            }

            this._receivedRequest.Should().BeTrue("Exporter should make the expected HTTP request.");
            this._receivedContent.Should().NotBeNull("Exporter should send a request body.");

            using var doc = JsonDocument.Parse(this._receivedContent!);
            var root = doc.RootElement;
            var attributes = root
                .GetProperty("resourceSpans")[0]
                .GetProperty("scopeSpans")[0]
                .GetProperty("spans")[0]
                .GetProperty("attributes");

            this.GetAttribute(attributes, "gen_ai.operation.name").Should().Be(inferenceDetails.OperationName.ToString());
            this.GetAttribute(attributes, "gen_ai.agent.id").Should().Be(expectedAgentDetails.AgentId);
            this.GetAttribute(attributes, "gen_ai.agent.name").Should().Be(expectedAgentDetails.AgentName);
            this.GetAttribute(attributes, "gen_ai.agent.description").Should().Be(expectedAgentDetails.AgentDescription);
            this.GetAttribute(attributes, "gen_ai.agent.userid").Should().Be(expectedAgentDetails.AgentAUID);
            this.GetAttribute(attributes, "gen_ai.agent.upn").Should().Be(expectedAgentDetails.AgentUPN);
            this.GetAttribute(attributes, "gen_ai.agent.applicationid").Should().Be(expectedAgentDetails.AgentBlueprintId);
            this.GetAttribute(attributes, "tenant.id").Should().Be(tenantDetails.TenantId.ToString());
            this.GetAttribute(attributes, "gen_ai.request.model").Should().Be(inferenceDetails.Model);
            this.GetAttribute(attributes, "gen_ai.provider.name").Should().Be(inferenceDetails.ProviderName);
            this.GetAttribute(attributes, "gen_ai.usage.input_tokens").Should().Be("42");
            this.GetAttribute(attributes, "gen_ai.usage.output_tokens").Should().Be("84");
            this.GetAttribute(attributes, "gen_ai.response.finish_reasons").Should().Be("stop,length");
            this.GetAttribute(attributes, "gen_ai.response.id").Should().Be("response-xyz");
            this.GetAttribute(attributes, "gen_ai.input.messages").Should().Be("Hello,World");
            this.GetAttribute(attributes, "gen_ai.output.messages").Should().Be("Hi there!");
        }

        [TestMethod]
        public async Task AddTracing_NestedScopes_AllExporterRequestsReceived()
        {
            // Arrange
            List<string> receivedContents = new();

            var agentDetails = new AgentDetails(
                agentId: Guid.NewGuid().ToString(),
                agentName: "Nested Agent",
                agentDescription: "Agent for nested scope testing.",
                agentAUID: Guid.NewGuid().ToString(),
                agentUPN: "nestedagent@ztaitest12.onmicrosoft.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString());

            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var endpoint = new Uri("https://nested-endpoint");

            var handler = new TestHttpMessageHandler(req =>
            {
                receivedContents.Add(req.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "");
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            });
            var httpClient = new HttpClient(handler);

            var provider = this.CreateTestServiceProvider(httpClient);

            var invokeAgentDetails = new InvokeAgentDetails(endpoint: endpoint, details: agentDetails);
            var request = new Request(
                content: "Nested request",
                executionType: ExecutionType.HumanToAgent,
                sourceMetadata: new SourceMetadata(name: "nested", description: "https://nestedchannel.link"));

            var toolCallDetails = new ToolCallDetails(
                toolName: "NestedTool",
                arguments: "{\"param\":\"nested\"}",
                toolCallId: "call-nested",
                description: "Nested tool call",
                toolType: "nested-type",
                endpoint: endpoint);

            var inferenceDetails = new InferenceCallDetails(
                operationName: InferenceOperationType.Chat,
                model: "gpt-nested",
                providerName: "OpenAI",
                inputTokens: 10,
                outputTokens: 20,
                finishReasons: new[] { "stop" },
                responseId: "response-nested");

            // Act
            using (var agentScope = InvokeAgentScope.Start(invokeAgentDetails, tenantDetails, request))
            {
                agentScope.RecordInputMessages(new[] { "Agent input" });
                agentScope.RecordOutputMessages(new[] { "Agent output" });

                using (var toolScope = ExecuteToolScope.Start(toolCallDetails, agentDetails, tenantDetails))
                {
                    toolScope.RecordResponse("Tool response");

                    using (var inferenceScope = InferenceScope.Start(inferenceDetails, agentDetails, tenantDetails))
                    {
                        inferenceScope.RecordInputMessages(new[] { "Inference input" });
                        inferenceScope.RecordOutputMessages(new[] { "Inference output" });
                        inferenceScope.RecordInputTokens(10);
                        inferenceScope.RecordOutputTokens(20);
                        inferenceScope.RecordResponseId("response-nested");
                        inferenceScope.RecordFinishReasons(new[] { "stop" });
                    }
                }
            }

            // Wait for up to 10 seconds for all spans to be exported
            await Task.Delay(10000).ConfigureAwait(false);

            // Assert
            var allOperationNames = new List<string>();
            foreach (var content in receivedContents)
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                var spans = root
                    .GetProperty("resourceSpans")[0]
                    .GetProperty("scopeSpans")[0]
                    .GetProperty("spans")
                    .EnumerateArray();

                foreach (var span in spans)
                {
                    var opName = this.GetAttribute(span.GetProperty("attributes"), "gen_ai.operation.name");
                    if (opName != null)
                        allOperationNames.Add(opName);
                }
            }
            allOperationNames.Should().Contain(new[] { "invoke_agent", "execute_tool", InferenceOperationType.Chat.ToString() }, "All three nested scopes should be exported, even if batched in fewer requests.");
        }

        private class TestHttpMessageHandler : HttpMessageHandler
        {
            private Func<HttpRequestMessage, HttpResponseMessage> _handler;
            public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                this._handler = handler;
            }
            public void SetHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                this._handler = handler;
            }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(this._handler(request));
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
                    return value.GetRawText();
                }
                if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("stringValue", out var sv))
                {
                    return sv.GetString();
                }
            }
            return null;
        }

        private ServiceProvider CreateTestServiceProvider(HttpClient httpClient)
        {
            HostApplicationBuilder builder = new HostApplicationBuilder();

            builder.Services.AddSingleton<HttpClient>(httpClient);
            builder.Services.AddSingleton<Agent365ExporterOptions>(sp =>
            {
                return new Agent365ExporterOptions
                {
                    UseS2SEndpoint = false,
                    TokenResolver = (_, _) => Task.FromResult<string?>("test-token")
                };
            });
            builder.AddA365Tracing(useOpenTelemetryBuilder: false, agent365ExporterType: Agent365ExporterType.Agent365Exporter);
            return builder.Services.BuildServiceProvider();
        }
        private void SetupExporterTest()
        {
            this._handler = new TestHttpMessageHandler(req =>
            {
                this._receivedRequest = true;
                this._receivedContent = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                req.RequestUri.Should().NotBeNull();
                req.Headers.Authorization.Should().NotBeNull();
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            });
            var httpClient = new HttpClient(this._handler);
            this._provider = this.CreateTestServiceProvider(httpClient);
        }
    }
}
