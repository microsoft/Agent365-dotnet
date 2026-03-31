// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text.Json;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.IntegrationTests
{
    [TestClass]
    public class Agent365ExporterAsyncE2ETests
    {
        private TestHttpMessageHandler? _handler;
        private ServiceProvider? _provider;
        private bool _receivedRequest;
        private string? _receivedContent;

        [TestMethod]
        public async Task AddTracing_And_InvokeAgentScope_ExporterMakesExpectedRequest()
        {
            // Arrange
            this.SetupExporterTest();
            this._receivedRequest = false;
            this._receivedContent = null;
            var expectedAgentType = AgentType.EntraEmbodied;
            var expectedAgentDetails = new AgentDetails(
                agentId: Guid.NewGuid().ToString(),
                agentName: "Test Agent",
                agentDescription: "Agent for testing.",
                agenticUserId: Guid.NewGuid().ToString(),
                agenticUserEmail: "testagent@ztaitest12.onmicrosoft.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString(),
                agentType: expectedAgentType);
            var endpoint = new Uri("https://test-agent-endpoint");
            var invokeAgentScopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);

            var expectedRequest = new Request(
                content: "Test request content",
                channel: new Channel(
                    name: "msteams",
                    link: "https://testchannel.link"));

            var expectedCallerDetails = new CallerDetails(
                userDetails: new UserDetails(
                    userId: "caller-123",
                    userName: "Test Caller",
                    userEmail: "caller-123@ztaitest12.onmicrosoft.com",
                    userClientIP: IPAddress.Parse("203.0.113.42")),
                callerAgentDetails: null);

            // Act
            using (var scope = InvokeAgentScope.Start(
                request: expectedRequest,
                scopeDetails: invokeAgentScopeDetails,
                agentDetails: expectedAgentDetails,
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
            this.GetAttribute(attributes, "server.address").Should().Be(invokeAgentScopeDetails.Endpoint?.Host);
            this.GetAttribute(attributes, "microsoft.channel.name").Should().Be(expectedRequest.Channel?.Name);
            this.GetAttribute(attributes, "microsoft.channel.link").Should().Be(expectedRequest.Channel?.Link);
            this.GetAttribute(attributes, "microsoft.tenant.id").Should().Be(expectedAgentDetails.TenantId);
            this.GetAttribute(attributes, "user.id").Should().Be(expectedCallerDetails.UserDetails?.UserId);
            this.GetAttribute(attributes, "user.email").Should().Be(expectedCallerDetails.UserDetails?.UserEmail);
            this.GetAttribute(attributes, "user.name").Should().Be(expectedCallerDetails.UserDetails?.UserName);
            this.GetAttribute(attributes, "gen_ai.input.messages").Should().Be("Input message 1,Input message 2");
            this.GetAttribute(attributes, "gen_ai.output.messages").Should().Be("Output message 1");
            this.GetAttribute(attributes, "gen_ai.agent.id").Should().Be(expectedAgentDetails.AgentId);
            this.GetAttribute(attributes, "gen_ai.agent.name").Should().Be(expectedAgentDetails.AgentName);
            this.GetAttribute(attributes, "gen_ai.agent.description").Should().Be(expectedAgentDetails.AgentDescription);
            this.GetAttribute(attributes, "microsoft.agent.user.id").Should().Be(expectedAgentDetails.AgenticUserId);
            this.GetAttribute(attributes, "microsoft.agent.user.email").Should().Be(expectedAgentDetails.AgenticUserEmail);
            this.GetAttribute(attributes, "microsoft.a365.agent.blueprint.id").Should().Be(expectedAgentDetails.AgentBlueprintId);
            this.GetAttribute(attributes, "microsoft.tenant.id").Should().Be(expectedAgentDetails.TenantId);
            this.GetAttribute(attributes, "gen_ai.operation.name").Should().Be("invoke_agent");
        }

        [TestMethod]
        public async Task AddTracing_And_ExecuteToolScope_ExporterMakesExpectedRequest()
        {
            // Arrange
            this.SetupExporterTest();
            this._receivedRequest = false;
            this._receivedContent = null;
            var expectedAgentType = AgentType.EntraEmbodied;
            var expectedAgentDetails = new AgentDetails(
                agentId: Guid.NewGuid().ToString(),
                agentName: "Tool Agent",
                agentDescription: "Agent for tool execution.",
                agenticUserId: Guid.NewGuid().ToString(),
                agenticUserEmail: "toolagent@ztaitest12.onmicrosoft.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString(),
                agentType: expectedAgentType);
            var endpoint = new Uri("https://tool-endpoint:8443");
            var toolCallDetails = new ToolCallDetails(
                toolName: "TestTool",
                arguments: "{\"param\":\"value\"}",
                toolCallId: "call-456",
                description: "Test tool call description",
                toolType: "custom-type",
                endpoint: endpoint);

            var expectedRequest = new Request();

            // Act
            using (var scope = ExecuteToolScope.Start(expectedRequest, toolCallDetails, expectedAgentDetails))
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
            this.GetAttribute(attributes, "microsoft.agent.user.id").Should().Be(expectedAgentDetails.AgenticUserId);
            this.GetAttribute(attributes, "microsoft.agent.user.email").Should().Be(expectedAgentDetails.AgenticUserEmail);
            this.GetAttribute(attributes, "microsoft.a365.agent.blueprint.id").Should().Be(expectedAgentDetails.AgentBlueprintId);
            this.GetAttribute(attributes, "microsoft.tenant.id").Should().Be(expectedAgentDetails.TenantId);
            this.GetAttribute(attributes, "gen_ai.tool.name").Should().Be(toolCallDetails.ToolName);
            this.GetAttribute(attributes, "gen_ai.tool.arguments").Should().Be(toolCallDetails.Arguments);
            this.GetAttribute(attributes, "gen_ai.tool.call.id").Should().Be(toolCallDetails.ToolCallId);
            this.GetAttribute(attributes, "gen_ai.tool.description").Should().Be(toolCallDetails.Description);
            this.GetAttribute(attributes, "gen_ai.tool.type").Should().Be(toolCallDetails.ToolType);
            this.GetAttribute(attributes, "server.address").Should().Be(endpoint.Host);
            this.GetAttribute(attributes, "server.port").Should().Be(endpoint.Port.ToString());
            this.GetAttribute(attributes, "gen_ai.tool.call.result").Should().Be("Tool response content");
        }

        [TestMethod]
        public async Task AddTracing_And_InferenceScope_ExporterMakesExpectedRequest()
        {
            // Arrange
            this.SetupExporterTest();
            this._receivedRequest = false;
            this._receivedContent = null;
            var expectedAgentType = AgentType.EntraEmbodied;
            var expectedAgentDetails = new AgentDetails(
                agentId: Guid.NewGuid().ToString(),
                agentName: "Inference Agent",
                agentDescription: "Agent for inference testing.",
                agenticUserId: Guid.NewGuid().ToString(),
                agenticUserEmail: "inferenceagent@ztaitest12.onmicrosoft.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString(),
                agentType: expectedAgentType);

            var inferenceDetails = new InferenceCallDetails(
                operationName: InferenceOperationType.Chat,
                model: "gpt-4",
                providerName: "OpenAI",
                inputTokens: 42,
                outputTokens: 84,
                finishReasons: new[] { "stop", "length" },
                responseId: "response-xyz");

            var expectedRequest = new Request();

            // Act
            using (var scope = InferenceScope.Start(expectedRequest, inferenceDetails, expectedAgentDetails))
            {
                scope.RecordInputMessages(new[] { "Hello", "World" });
                scope.RecordOutputMessages(new[] { "Hi there!" });
                scope.RecordInputTokens(42);
                scope.RecordOutputTokens(84);
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
            this.GetAttribute(attributes, "microsoft.agent.user.id").Should().Be(expectedAgentDetails.AgenticUserId);
            this.GetAttribute(attributes, "microsoft.agent.user.email").Should().Be(expectedAgentDetails.AgenticUserEmail);
            this.GetAttribute(attributes, "microsoft.a365.agent.blueprint.id").Should().Be(expectedAgentDetails.AgentBlueprintId);
            this.GetAttribute(attributes, "microsoft.tenant.id").Should().Be(expectedAgentDetails.TenantId);
            this.GetAttribute(attributes, "gen_ai.request.model").Should().Be(inferenceDetails.Model);
            this.GetAttribute(attributes, "gen_ai.provider.name").Should().Be(inferenceDetails.ProviderName);
            this.GetAttribute(attributes, "gen_ai.usage.input_tokens").Should().Be("42");
            this.GetAttribute(attributes, "gen_ai.usage.output_tokens").Should().Be("84");
            this.GetAttribute(attributes, "gen_ai.response.finish_reasons").Should().Be("stop,length");
            this.GetAttribute(attributes, "gen_ai.input.messages").Should().Be("Hello,World");
            this.GetAttribute(attributes, "gen_ai.output.messages").Should().Be("Hi there!");
        }

        [TestMethod]
        public async Task AddTracing_NestedScopes_AllExporterRequestsReceived()
        {
            // Arrange
            List<string> receivedContents = new();

            var agentType = AgentType.EntraEmbodied;
            var agentDetails = new AgentDetails(
                agentId: Guid.NewGuid().ToString(),
                agentName: "Nested Agent",
                agentDescription: "Agent for nested scope testing.",
                agenticUserId: Guid.NewGuid().ToString(),
                agenticUserEmail: "nestedagent@ztaitest12.onmicrosoft.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString(),
                agentType: agentType);

            var endpoint = new Uri("https://nested-endpoint");

            var handler = new TestHttpMessageHandler(req =>
            {
                receivedContents.Add(req.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "");
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            });
            var httpClient = new HttpClient(handler);

            this.CreateTestServiceProvider(httpClient);

            var invokeAgentScopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var request = new Request(
                content: "Nested request",
                channel: new Channel(name: "nested", link: "https://nestedchannel.link"));

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
            using (var agentScope = InvokeAgentScope.Start(request, invokeAgentScopeDetails, agentDetails))
            {
                agentScope.RecordInputMessages(new[] { "Agent input" });
                agentScope.RecordOutputMessages(new[] { "Agent output" });

                using (var toolScope = ExecuteToolScope.Start(request, toolCallDetails, agentDetails))
                {
                    toolScope.RecordResponse("Tool response");

                    using (var inferenceScope = InferenceScope.Start(request, inferenceDetails, agentDetails))
                    {
                        inferenceScope.RecordInputMessages(new[] { "Inference input" });
                        inferenceScope.RecordOutputMessages(new[] { "Inference output" });
                        inferenceScope.RecordInputTokens(10);
                        inferenceScope.RecordOutputTokens(20);
                        inferenceScope.RecordFinishReasons(new[] { "stop" });
                    }
                }
            }

            // Wait for up to 5 seconds for all spans to be exported
            await Task.Delay(5000).ConfigureAwait(false);

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

            builder.Configuration["EnableAgent365Exporter"] = "true";
            builder.Services.AddSingleton<HttpClient>(httpClient);
            builder.Services.AddSingleton<Agent365ExporterOptions>(sp =>
            {
                return new Agent365ExporterOptions
                {
                    UseS2SEndpoint = false,
                    TokenResolver = (_, _) => Task.FromResult<string?>("test-token")
                };
            });
            
            builder.AddA365Tracing(useOpenTelemetryBuilder: false, agent365ExporterType: Agent365ExporterType.Agent365ExporterAsync);
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
