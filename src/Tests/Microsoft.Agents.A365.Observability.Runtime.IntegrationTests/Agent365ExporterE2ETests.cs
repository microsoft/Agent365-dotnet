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
    public class Agent365ExporterE2ETests
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
                agentAUID: Guid.NewGuid().ToString(),
                agentUPN: "testagent@ztaittest12.onmicrosoft.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString(),
                agentType: expectedAgentType);
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
                callerClientIP: IPAddress.Parse("203.0.113.42"),
                tenantId: expectedAgentDetails.TenantId);

            var expectedThreatDiagnosticsSummary = new ThreatDiagnosticsSummary(
                blockAction: true,
                reasonCode: 112,
                reason: "The action was blocked due to security policy.",
                diagnostics: "{\"flaggedField\":\"bcc\"}");

            // Act
            using (var scope = InvokeAgentScope.Start(
                invokeAgentDetails: invokeAgentDetails,
                tenantDetails: tenantDetails,
                request: expectedRequest,
                callerDetails: expectedCallerDetails,
                threatDiagnosticsSummary: expectedThreatDiagnosticsSummary))
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
            this.GetAttribute(attributes, "microsoft.channel.name").Should().Be(expectedRequest.SourceMetadata?.Name);
            this.GetAttribute(attributes, "microsoft.channel.link").Should().Be(expectedRequest.SourceMetadata?.Description);
            this.GetAttribute(attributes, "microsoft.tenant.id").Should().Be(tenantDetails.TenantId.ToString());
            this.GetAttribute(attributes, "microsoft.caller.id").Should().Be(expectedCallerDetails.CallerId);
            this.GetAttribute(attributes, "microsoft.caller.upn").Should().Be(expectedCallerDetails.CallerUpn);
            this.GetAttribute(attributes, "microsoft.caller.name").Should().Be(expectedCallerDetails.CallerName);
            this.GetAttribute(attributes, "client.address").Should().Be(expectedCallerDetails.CallerClientIP?.ToString());
            this.GetAttribute(attributes, "gen_ai.input.messages").Should().Be("Input message 1,Input message 2");
            this.GetAttribute(attributes, "gen_ai.output.messages").Should().Be("Output message 1");
            this.GetAttribute(attributes, "gen_ai.agent.id").Should().Be(expectedAgentDetails.AgentId);
            this.GetAttribute(attributes, "gen_ai.agent.name").Should().Be(expectedAgentDetails.AgentName);
            this.GetAttribute(attributes, "gen_ai.agent.description").Should().Be(expectedAgentDetails.AgentDescription);
            this.GetAttribute(attributes, "microsoft.agent.user.id").Should().Be(expectedAgentDetails.AgentAUID);
            this.GetAttribute(attributes, "microsoft.agent.user.upn").Should().Be(expectedAgentDetails.AgentUPN);
            this.GetAttribute(attributes, "microsoft.a365.agent.blueprint.id").Should().Be(expectedAgentDetails.AgentBlueprintId);
            this.GetAttribute(attributes, "microsoft.tenant.id").Should().Be(tenantDetails.TenantId.ToString());
            this.GetAttribute(attributes, "gen_ai.operation.name").Should().Be("invoke_agent");
            var threatSummaryJson = this.GetAttribute(attributes, "threat.diagnostics.summary");
            threatSummaryJson.Should().Contain("\"blockAction\":true");
            threatSummaryJson.Should().Contain("\"reasonCode\":112");
            threatSummaryJson.Should().Contain("\"reason\":\"The action was blocked due to security policy.\"");
            threatSummaryJson.Should().Contain("flaggedField");
            threatSummaryJson.Should().Contain("bcc");
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
                agentAUID: Guid.NewGuid().ToString(),
                agentUPN: "toolagent@ztaittest12.onmicrosoft.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString(),
                agentType: expectedAgentType);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var endpoint = new Uri("https://tool-endpoint:8443");
            var toolCallDetails = new ToolCallDetails(
                toolName: "TestTool",
                arguments: "{\"param\":\"value\"}",
                toolCallId: "call-456",
                description: "Test tool call description",
                toolType: "custom-type",
                endpoint: endpoint,
                toolServerName: "test-tool-server");

            var expectedThreatDiagnosticsSummary = new ThreatDiagnosticsSummary(
                blockAction: false,
                reasonCode: 200,
                reason: "No threats detected during tool execution.",
                diagnostics: null);

            var expectedToolCallerDetails = new CallerDetails(
                callerId: "tool-caller-456",
                callerName: "Tool Caller",
                callerUpn: "tool-caller@ztaitest12.onmicrosoft.com",
                callerClientIP: IPAddress.Parse("10.0.0.42"),
                tenantId: expectedAgentDetails.TenantId);

            // Act
            using (var scope = ExecuteToolScope.Start(toolCallDetails, expectedAgentDetails, tenantDetails, threatDiagnosticsSummary: expectedThreatDiagnosticsSummary, callerDetails: expectedToolCallerDetails))
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
            this.GetAttribute(attributes, "microsoft.agent.user.id").Should().Be(expectedAgentDetails.AgentAUID);
            this.GetAttribute(attributes, "microsoft.agent.user.upn").Should().Be(expectedAgentDetails.AgentUPN);
            this.GetAttribute(attributes, "microsoft.a365.agent.blueprint.id").Should().Be(expectedAgentDetails.AgentBlueprintId);
            this.GetAttribute(attributes, "microsoft.tenant.id").Should().Be(tenantDetails.TenantId.ToString());
            this.GetAttribute(attributes, "gen_ai.tool.name").Should().Be(toolCallDetails.ToolName);
            this.GetAttribute(attributes, "gen_ai.tool.arguments").Should().Be(toolCallDetails.Arguments);
            this.GetAttribute(attributes, "gen_ai.tool.call.id").Should().Be(toolCallDetails.ToolCallId);
            this.GetAttribute(attributes, "gen_ai.tool.description").Should().Be(toolCallDetails.Description);
            this.GetAttribute(attributes, "gen_ai.tool.type").Should().Be(toolCallDetails.ToolType);
            this.GetAttribute(attributes, "gen_ai.tool.server.name").Should().Be(toolCallDetails.ToolServerName);
            this.GetAttribute(attributes, "server.address").Should().Be(endpoint.Host);
            this.GetAttribute(attributes, "server.port").Should().Be(endpoint.Port.ToString());
            this.GetAttribute(attributes, "gen_ai.tool.call.result").Should().Be("Tool response content");
            this.GetAttribute(attributes, "microsoft.caller.id").Should().Be(expectedToolCallerDetails.CallerId);
            this.GetAttribute(attributes, "microsoft.caller.upn").Should().Be(expectedToolCallerDetails.CallerUpn);
            this.GetAttribute(attributes, "microsoft.caller.name").Should().Be(expectedToolCallerDetails.CallerName);
            this.GetAttribute(attributes, "client.address").Should().Be(expectedToolCallerDetails.CallerClientIP?.ToString());
            var toolThreatSummaryJson = this.GetAttribute(attributes, "threat.diagnostics.summary");
            toolThreatSummaryJson.Should().Contain("\"blockAction\":false");
            toolThreatSummaryJson.Should().Contain("\"reasonCode\":200");
            toolThreatSummaryJson.Should().Contain("\"reason\":\"No threats detected during tool execution.\"");
            toolThreatSummaryJson.Should().Contain("\"diagnostics\":null");
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
                agentAUID: Guid.NewGuid().ToString(),
                agentUPN: "inferenceagent@ztaittest12.onmicrosoft.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString(),
                agentType: expectedAgentType);
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            var inferenceDetails = new InferenceCallDetails(
                operationName: InferenceOperationType.Chat,
                model: "gpt-4",
                providerName: "OpenAI",
                inputTokens: 42,
                outputTokens: 84,
                finishReasons: new[] { "stop", "length" },
                responseId: "response-xyz");

            var expectedInferenceCallerDetails = new CallerDetails(
                callerId: "inference-caller-789",
                callerName: "Inference Caller",
                callerUpn: "inference-caller@ztaitest12.onmicrosoft.com",
                callerClientIP: IPAddress.Parse("172.16.0.42"),
                tenantId: expectedAgentDetails.TenantId);

            // Act
            using (var scope = InferenceScope.Start(inferenceDetails, expectedAgentDetails, tenantDetails, callerDetails: expectedInferenceCallerDetails))
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
            this.GetAttribute(attributes, "microsoft.agent.user.id").Should().Be(expectedAgentDetails.AgentAUID);
            this.GetAttribute(attributes, "microsoft.agent.user.upn").Should().Be(expectedAgentDetails.AgentUPN);
            this.GetAttribute(attributes, "microsoft.a365.agent.blueprint.id").Should().Be(expectedAgentDetails.AgentBlueprintId);
            this.GetAttribute(attributes, "microsoft.tenant.id").Should().Be(tenantDetails.TenantId.ToString());
            this.GetAttribute(attributes, "gen_ai.request.model").Should().Be(inferenceDetails.Model);
            this.GetAttribute(attributes, "gen_ai.provider.name").Should().Be(inferenceDetails.ProviderName);
            this.GetAttribute(attributes, "gen_ai.usage.input_tokens").Should().Be("42");
            this.GetAttribute(attributes, "gen_ai.usage.output_tokens").Should().Be("84");
            this.GetAttribute(attributes, "gen_ai.response.finish_reasons").Should().Be("stop,length");
            this.GetAttribute(attributes, "gen_ai.input.messages").Should().Be("Hello,World");
            this.GetAttribute(attributes, "gen_ai.output.messages").Should().Be("Hi there!");
            this.GetAttribute(attributes, "microsoft.caller.id").Should().Be(expectedInferenceCallerDetails.CallerId);
            this.GetAttribute(attributes, "microsoft.caller.upn").Should().Be(expectedInferenceCallerDetails.CallerUpn);
            this.GetAttribute(attributes, "microsoft.caller.name").Should().Be(expectedInferenceCallerDetails.CallerName);
            this.GetAttribute(attributes, "client.address").Should().Be(expectedInferenceCallerDetails.CallerClientIP?.ToString());
        }

        [TestMethod]
        public async Task AddTracing_NestedScopes_AllExporterRequestsReceived_UsesAgentId()
        {
            await this.RunNestedScopes_AllExporterRequestsReceived(useAgentId: true);
        }

        [TestMethod]
        public async Task AddTracing_NestedScopes_AllExporterRequestsReceived_UsesAgentPlatformId()
        {
            await this.RunNestedScopes_AllExporterRequestsReceived(useAgentId: false);
        }

        private async Task RunNestedScopes_AllExporterRequestsReceived(bool useAgentId)
        {
            // Arrange
            List<string> receivedContents = new();

            var agentType = AgentType.EntraEmbodied;
            var agentDetails = useAgentId
                ? new AgentDetails(
                    agentId: Guid.NewGuid().ToString(),
                    agentName: "Nested Agent",
                    agentDescription: "Agent for nested scope testing.",
                    agentAUID: Guid.NewGuid().ToString(),
                    agentUPN: "nestedagent@ztaittest12.onmicrosoft.com",
                    agentBlueprintId: Guid.NewGuid().ToString(),
                    tenantId: Guid.NewGuid().ToString(),
                    agentType: agentType)
                : new AgentDetails(
                    agentId: null,
                    agentName: "Nested Agent",
                    agentDescription: "Agent for nested scope testing.",
                    agentAUID: Guid.NewGuid().ToString(),
                    agentUPN: "nestedagent@ztaittest12.onmicrosoft.com",
                    agentBlueprintId: Guid.NewGuid().ToString(),
                    tenantId: Guid.NewGuid().ToString(),
                    agentType: agentType,
                    agentClientIP: null,
                    agentPlatformId: Guid.NewGuid().ToString());

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

        [TestMethod]
        public async Task Exporter_Truncates_Scope()
        {
            // Arrange
            this.SetupExporterTest();
            this._receivedRequest = false;
            this._receivedContent = null;

            // Create a sample text file >250KB and base64 encode it
            var tempFile = Path.GetTempFileName();
            var fileBytes = new byte[300 * 1024]; // 300KB
            new Random(42).NextBytes(fileBytes);
            await File.WriteAllBytesAsync(tempFile, fileBytes);
            var base64 = Convert.ToBase64String(await File.ReadAllBytesAsync(tempFile));
            File.Delete(tempFile);

            var agentDetails = new AgentDetails(
                agentId: Guid.NewGuid().ToString(),
                agentName: "Test Agent",
                agentDescription: "Agent for truncation test.",
                agentAUID: Guid.NewGuid().ToString(),
                agentUPN: "testagent@contoso.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString());
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var endpoint = new Uri("https://test-endpoint");
            var invokeAgentDetails = new InvokeAgentDetails(details: agentDetails, endpoint: endpoint);
            var request = new Request(
                content: "Test request content",
                executionType: ExecutionType.HumanToAgent,
                sourceMetadata: new SourceMetadata(name: "test", id: "test-id"));

            var toolCallDetails = new ToolCallDetails(
                toolName: "LargeFileTool",
                arguments: base64,
                toolCallId: "call-123",
                description: "Test tool with large file content",
                toolType: "file-upload",
                endpoint: endpoint);

            // Act: Start nested scopes
            using (var agentScope = InvokeAgentScope.Start(invokeAgentDetails, tenantDetails, request))
            {
                agentScope.RecordInputMessages(new[] { "Agent input" });
                agentScope.RecordOutputMessages(new[] { "Agent output" });
                using (var toolScope = ExecuteToolScope.Start(toolCallDetails, agentDetails, tenantDetails))
                {
                    toolScope.RecordResponse("Tool response");
                }
            }

            // Wait for export
            var timeout = TimeSpan.FromSeconds(10);
            var start = DateTime.UtcNow;
            while (!this._receivedRequest && DateTime.UtcNow - start < timeout)
            {
                await Task.Delay(500).ConfigureAwait(false);
            }

            this._receivedRequest.Should().BeTrue("Exporter should make the expected HTTP request.");
            this._receivedContent.Should().NotBeNull("Exporter should send a request body.");

            // Assert: Find both activities in the exported payload
            using var doc = JsonDocument.Parse(this._receivedContent!);
            var root = doc.RootElement;
            var spans = root
                .GetProperty("resourceSpans")[0]
                .GetProperty("scopeSpans")[0]
                .GetProperty("spans")
                .EnumerateArray();

            bool foundInvokeAgent = false;
            bool foundExecuteTool = false;
            foreach (var span in spans)
            {
                var attrs = span.GetProperty("attributes");
                var opName = this.GetAttribute(attrs, "gen_ai.operation.name");
                if (opName == "invoke_agent")
                {
                    foundInvokeAgent = true;
                    // Should NOT be truncated
                    var input = this.GetAttribute(attrs, "gen_ai.input.messages");
                    input.Should().Be("Agent input");
                }
                if (opName == "execute_tool")
                {
                    foundExecuteTool = true;
                    // Should be truncated
                    var args = this.GetAttribute(attrs, "gen_ai.tool.arguments");
                    args.Should().Be("TRUNCATED");
                }
            }
            foundInvokeAgent.Should().BeTrue();
            foundExecuteTool.Should().BeTrue();
        }

        /// <summary>
        /// Verifies that multiple registrations of the Agent365 exporter through different APIs 
        /// (direct OpenTelemetry configuration and AddA365Tracing) result in only a single 
        /// exporter instance being registered, preventing duplicate telemetry exports.
        /// This test ensures the exporter registry correctly implements singleton behavior
        /// to avoid data duplication when the same exporter type is configured multiple times.
        /// </summary>
        [TestMethod]
        public async Task AddTracing_MultipleInvocations_NoDuplicateExports()
        {
            List<string> receivedContents = new();
            var requestCount = 0;

            var handler = new TestHttpMessageHandler(req =>
            {
                Interlocked.Increment(ref requestCount);
                var content = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
                lock (receivedContents)
                {
                    receivedContents.Add(content);
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            });
            var httpClient = new HttpClient(handler);

            // Create builder and simulate mixed API usage that could cause duplicates
            HostApplicationBuilder builder = new HostApplicationBuilder();
            builder.Configuration["EnableAgent365Exporter"] = "true";
            builder.Services.AddSingleton<HttpClient>(httpClient);
            builder.Services.AddSingleton<Agent365ExporterOptions>(_ => new Agent365ExporterOptions
            {
                UseS2SEndpoint = false,
                TokenResolver = (_, _) => Task.FromResult<string?>("test-token")
            });

            // AddA365Tracing call
            builder.AddA365Tracing(useOpenTelemetryBuilder: true, agent365ExporterType: Agent365ExporterType.Agent365Exporter);

            // Duplicate AddA365Tracing call
            builder.AddA365Tracing(useOpenTelemetryBuilder: true, agent365ExporterType: Agent365ExporterType.Agent365Exporter);

            var serviceProvider = builder.Services.BuildServiceProvider();

            // Initialize the OpenTelemetry infrastructure by accessing the TracerProvider
            // This ensures the tracing pipeline is built and exporters are initialized
            _ = serviceProvider.GetService<OpenTelemetry.Trace.TracerProvider>();

            var agentDetails = new AgentDetails(
                agentId: Guid.NewGuid().ToString(),
                agentName: "Singleton Test Agent",
                agentDescription: "Testing singleton exporter behavior.",
                agentAUID: Guid.NewGuid().ToString(),
                agentUPN: "singleton@test.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString());

            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var endpoint = new Uri("https://singleton-test-endpoint");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint: endpoint, details: agentDetails);
            var request = new Request(
                content: "Singleton test request",
                executionType: ExecutionType.HumanToAgent,
                sourceMetadata: new SourceMetadata(name: "test", description: "singleton test"));
            using (var scope = InvokeAgentScope.Start(invokeAgentDetails, tenantDetails, request))
            {
                scope.RecordInputMessages(new[] { "Test input" });
                scope.RecordOutputMessages(new[] { "Test output" });
            }

            // Wait for export(s) to complete
            await Task.Delay(10000).ConfigureAwait(false);

            // Assert - Should have exactly one export request, not two
            requestCount.Should().Be(1, "Exporter registry should prevent duplicate exporter registration, resulting in only one export request");
            receivedContents.Should().HaveCount(1, "Should receive exactly one export payload");
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
