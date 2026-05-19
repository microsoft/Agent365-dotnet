// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
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
                agenticUserId: Guid.NewGuid().ToString(),
                agenticUserEmail: "testagent@ztaittest12.onmicrosoft.com",
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

            var expectedThreatDiagnosticsSummary = new ThreatDiagnosticsSummary(
                blockAction: true,
                reasonCode: 112,
                reason: "The action was blocked due to security policy.",
                diagnostics: "{\"flaggedField\":\"bcc\"}");

            // Act
            using (var scope = InvokeAgentScope.Start(
                request: expectedRequest,
                scopeDetails: invokeAgentScopeDetails,
                agentDetails: expectedAgentDetails,
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
            this.GetAttribute(attributes, "server.address").Should().Be(invokeAgentScopeDetails.Endpoint?.Host);
            this.GetAttribute(attributes, "microsoft.channel.name").Should().Be(expectedRequest.Channel?.Name);
            this.GetAttribute(attributes, "microsoft.channel.link").Should().Be(expectedRequest.Channel?.Link);
            this.GetAttribute(attributes, "microsoft.tenant.id").Should().Be(expectedAgentDetails.TenantId);
            this.GetAttribute(attributes, "user.id").Should().Be(expectedCallerDetails.UserDetails?.UserId);
            this.GetAttribute(attributes, "user.email").Should().Be(expectedCallerDetails.UserDetails?.UserEmail);
            this.GetAttribute(attributes, "user.name").Should().Be(expectedCallerDetails.UserDetails?.UserName);
            this.GetAttribute(attributes, "client.address").Should().Be(expectedCallerDetails.UserDetails?.UserClientIP?.ToString());
            this.GetAttribute(attributes, "gen_ai.input.messages").Should().Contain("Input message 1").And.Contain("Input message 2").And.Contain("\"version\":\"0.1.0\"");
            this.GetAttribute(attributes, "gen_ai.output.messages").Should().Contain("Output message 1").And.Contain("\"version\":\"0.1.0\"");
            this.GetAttribute(attributes, "gen_ai.agent.id").Should().Be(expectedAgentDetails.AgentId);
            this.GetAttribute(attributes, "gen_ai.agent.name").Should().Be(expectedAgentDetails.AgentName);
            this.GetAttribute(attributes, "gen_ai.agent.description").Should().Be(expectedAgentDetails.AgentDescription);
            this.GetAttribute(attributes, "microsoft.agent.user.id").Should().Be(expectedAgentDetails.AgenticUserId);
            this.GetAttribute(attributes, "microsoft.agent.user.email").Should().Be(expectedAgentDetails.AgenticUserEmail);
            this.GetAttribute(attributes, "microsoft.a365.agent.blueprint.id").Should().Be(expectedAgentDetails.AgentBlueprintId);
            this.GetAttribute(attributes, "microsoft.tenant.id").Should().Be(expectedAgentDetails.TenantId);
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
                agenticUserId: Guid.NewGuid().ToString(),
                agenticUserEmail: "toolagent@ztaittest12.onmicrosoft.com",
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
                endpoint: endpoint,
                toolServerName: "test-tool-server");

            var expectedThreatDiagnosticsSummary = new ThreatDiagnosticsSummary(
                blockAction: false,
                reasonCode: 200,
                reason: "No threats detected during tool execution.",
                diagnostics: null);

            var expectedToolUserDetails = new UserDetails(
                userId: "tool-caller-456",
                userName: "Tool Caller",
                userEmail: "tool-caller@ztaitest12.onmicrosoft.com",
                userClientIP: IPAddress.Parse("10.0.0.42"));

            var expectedToolRequest = new Request(
                content: "Tool execution request",
                channel: new Channel(name: "tool-channel"));

            // Act
            using (var scope = ExecuteToolScope.Start(expectedToolRequest, toolCallDetails, expectedAgentDetails, userDetails: expectedToolUserDetails, threatDiagnosticsSummary: expectedThreatDiagnosticsSummary))
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
            this.GetAttribute(attributes, "gen_ai.tool.call.arguments").Should().Be(toolCallDetails.Arguments);
            this.GetAttribute(attributes, "gen_ai.tool.call.id").Should().Be(toolCallDetails.ToolCallId);
            this.GetAttribute(attributes, "gen_ai.tool.description").Should().Be(toolCallDetails.Description);
            this.GetAttribute(attributes, "gen_ai.tool.type").Should().Be(toolCallDetails.ToolType);
            this.GetAttribute(attributes, "gen_ai.tool.server.name").Should().Be(toolCallDetails.ToolServerName);
            this.GetAttribute(attributes, "server.address").Should().Be(endpoint.Host);
            this.GetAttribute(attributes, "server.port").Should().Be(endpoint.Port.ToString());
            this.GetAttribute(attributes, "gen_ai.tool.call.result").Should().Contain("Tool response content");
            this.GetAttribute(attributes, "user.id").Should().Be(expectedToolUserDetails.UserId);
            this.GetAttribute(attributes, "user.email").Should().Be(expectedToolUserDetails.UserEmail);
            this.GetAttribute(attributes, "user.name").Should().Be(expectedToolUserDetails.UserName);
            this.GetAttribute(attributes, "client.address").Should().Be(expectedToolUserDetails.UserClientIP?.ToString());
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
                agenticUserId: Guid.NewGuid().ToString(),
                agenticUserEmail: "inferenceagent@ztaittest12.onmicrosoft.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString(),
                agentType: expectedAgentType);

            var inferenceDetails= new InferenceCallDetails(
                operationName: InferenceOperationType.Chat,
                model: "gpt-4",
                providerName: "OpenAI",
                inputTokens: 42,
                outputTokens: 84,
                finishReasons: new[] { "stop", "length" },
                responseId: "response-xyz");

            var expectedInferenceUserDetails = new UserDetails(
                userId: "inference-caller-789",
                userName: "Inference Caller",
                userEmail: "inference-caller@ztaitest12.onmicrosoft.com",
                userClientIP: IPAddress.Parse("172.16.0.42"));

            var expectedInferenceRequest = new Request(
                content: "Inference request",
                channel: new Channel(name: "inference-channel"));

            // Act
            using (var scope = InferenceScope.Start(expectedInferenceRequest, inferenceDetails, expectedAgentDetails, userDetails: expectedInferenceUserDetails))
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
            this.GetAttribute(attributes, "gen_ai.input.messages").Should().Contain("Hello").And.Contain("World").And.Contain("\"version\":\"0.1.0\"");
            this.GetAttribute(attributes, "gen_ai.output.messages").Should().Contain("Hi there!").And.Contain("\"version\":\"0.1.0\"");
            this.GetAttribute(attributes, "user.id").Should().Be(expectedInferenceUserDetails.UserId);
            this.GetAttribute(attributes, "user.email").Should().Be(expectedInferenceUserDetails.UserEmail);
            this.GetAttribute(attributes, "user.name").Should().Be(expectedInferenceUserDetails.UserName);
            this.GetAttribute(attributes, "client.address").Should().Be(expectedInferenceUserDetails.UserClientIP?.ToString());
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
                    agenticUserId: Guid.NewGuid().ToString(),
                    agenticUserEmail: "nestedagent@ztaittest12.onmicrosoft.com",
                    agentBlueprintId: Guid.NewGuid().ToString(),
                    tenantId: Guid.NewGuid().ToString(),
                    agentType: agentType)
                : new AgentDetails(
                    agentId: null,
                    agentName: "Nested Agent",
                    agentDescription: "Agent for nested scope testing.",
                    agenticUserId: Guid.NewGuid().ToString(),
                    agenticUserEmail: "nestedagent@ztaittest12.onmicrosoft.com",
                    agentBlueprintId: Guid.NewGuid().ToString(),
                    tenantId: Guid.NewGuid().ToString(),
                    agentType: agentType,
                    agentClientIP: null,
                    agentPlatformId: Guid.NewGuid().ToString());

            var endpoint = new Uri("https://nested-endpoint");

            var handler = new TestHttpMessageHandler(req =>
            {
                receivedContents.Add(req.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "");
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            });
            var httpClient = new HttpClient(handler);

            var provider = this.CreateTestServiceProvider(httpClient);

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
                agenticUserId: Guid.NewGuid().ToString(),
                agenticUserEmail: "testagent@contoso.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString());
            var endpoint = new Uri("https://test-endpoint");
            var invokeAgentScopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var request = new Request(
                content: "Test request content",
                channel: new Channel(name: "test"));

            var toolCallDetails = new ToolCallDetails(
                toolName: "LargeFileTool",
                arguments: base64,
                toolCallId: "call-123",
                description: "Test tool with large file content",
                toolType: "file-upload",
                endpoint: endpoint);

            // Act: Start nested scopes
            using (var agentScope = InvokeAgentScope.Start(request, invokeAgentScopeDetails, agentDetails))
            {
                agentScope.RecordInputMessages(new[] { "Agent input" });
                agentScope.RecordOutputMessages(new[] { "Agent output" });
                using (var toolScope = ExecuteToolScope.Start(request, toolCallDetails, agentDetails))
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
                    input.Should().Contain("Agent input").And.Contain("\"version\":\"0.1.0\"");
                }
                if (opName == "execute_tool")
                {
                    foundExecuteTool = true;
                    // Should be truncated
                    var args = this.GetAttribute(attrs, "gen_ai.tool.call.arguments");
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
        public void AddTracing_And_ApplyGuardrailScope_ExporterMakesExpectedRequest()
        {
            // Arrange
            var expectedAgentDetails = new AgentDetails(
                agentId: Guid.NewGuid().ToString(),
                agentName: "Guardrail Agent",
                agentDescription: "Agent for guardrail testing.",
                agenticUserId: Guid.NewGuid().ToString(),
                agenticUserEmail: "guardrailagent@ztaittest12.onmicrosoft.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString(),
                agentType: AgentType.EntraEmbodied);

            var guardrailDetails = new GuardrailDetails(
                targetType: GuardrailTargetType.LlmInput,
                decisionType: GuardrailDecisionType.Deny,
                guardianId: "azure-content-safety-001",
                guardianName: "Azure Content Safety",
                guardianProviderName: "Azure",
                guardianVersion: "2.0.0",
                targetId: "msg-12345",
                decisionReason: "Content violates hate speech policy",
                decisionCode: "HATE_SPEECH_001",
                policyId: "policy-abc",
                policyName: "Content Safety Policy",
                policyVersion: "1.2.0",
                contentInputHash: "sha256:abc123def456",
                contentModified: false,
                externalEventId: "ext-event-789");

            var expectedUserDetails = new UserDetails(
                userId: "guardrail-caller-123",
                userName: "Guardrail Caller",
                userEmail: "guardrail-caller@ztaitest12.onmicrosoft.com",
                userClientIP: IPAddress.Parse("192.168.1.42"));

            var expectedRequest = new Request(
                content: "Check this content for safety",
                channel: new Channel(
                    name: "msteams",
                    link: "https://guardrail-channel.link"));

            // Use ActivityListener to capture the activity directly
            System.Diagnostics.Activity? capturedActivity = null;
            using var listener = new System.Diagnostics.ActivityListener();
            listener.ShouldListenTo = source => source.Name == OpenTelemetryConstants.SourceName;
            listener.Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) =>
                System.Diagnostics.ActivitySamplingResult.AllDataAndRecorded;
            listener.ActivityStarted = a => capturedActivity = a;
            System.Diagnostics.ActivitySource.AddActivityListener(listener);

            // Act
            using (var scope = ApplyGuardrailScope.Start(
                details: guardrailDetails,
                agentDetails: expectedAgentDetails,
                request: expectedRequest,
                userDetails: expectedUserDetails))
            {
                scope.RecordDecision(GuardrailDecisionType.Deny);
                scope.RecordContentOutput("sanitized-hash-output");
                scope.RecordFinding(new GuardrailFinding(
                    riskCategory: "hate_speech",
                    riskSeverity: GuardrailRiskSeverity.High,
                    policyDecisionType: "deny",
                    policyId: "policy-abc",
                    riskScore: 0.95,
                    riskMetadata: new[] { "{\"category\":\"hate\",\"confidence\":0.95}" }));
            }

            // Assert activity was captured
            capturedActivity.Should().NotBeNull("ApplyGuardrailScope should create an activity");

            // Format activity as OTLP-like JSON for inspection
            var spanJson = new
            {
                name = capturedActivity!.DisplayName,
                traceId = capturedActivity.TraceId.ToHexString(),
                spanId = capturedActivity.SpanId.ToHexString(),
                parentSpanId = capturedActivity.ParentSpanId.ToHexString(),
                kind = capturedActivity.Kind.ToString(),
                startTimeUnixNano = capturedActivity.StartTimeUtc.Ticks,
                endTimeUnixNano = (capturedActivity.StartTimeUtc + capturedActivity.Duration).Ticks,
                attributes = capturedActivity.TagObjects.ToDictionary(t => t.Key, t => t.Value),
                events = capturedActivity.Events.Select(e => new
                {
                    name = e.Name,
                    timeUnixNano = e.Timestamp.Ticks,
                    attributes = e.Tags.ToDictionary(t => t.Key, t => t.Value)
                }).ToArray()
            };

            var prettyJson = JsonSerializer.Serialize(spanJson, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine("=== ApplyGuardrail Span JSON ===");
            Console.WriteLine(prettyJson);
            Console.WriteLine("=== End ApplyGuardrail Span JSON ===");

            // Verify span name
            capturedActivity.DisplayName.Should().Be("apply_guardrail Azure Content Safety llm_input");

            // Verify operation name
            capturedActivity.GetTagItem("gen_ai.operation.name").Should().Be("apply_guardrail");

            // Verify agent details
            capturedActivity.GetTagItem("gen_ai.agent.id").Should().Be(expectedAgentDetails.AgentId);
            capturedActivity.GetTagItem("gen_ai.agent.name").Should().Be(expectedAgentDetails.AgentName);
            capturedActivity.GetTagItem("gen_ai.agent.description").Should().Be(expectedAgentDetails.AgentDescription);
            capturedActivity.GetTagItem("microsoft.agent.user.id").Should().Be(expectedAgentDetails.AgenticUserId);
            capturedActivity.GetTagItem("microsoft.agent.user.email").Should().Be(expectedAgentDetails.AgenticUserEmail);
            capturedActivity.GetTagItem("microsoft.a365.agent.blueprint.id").Should().Be(expectedAgentDetails.AgentBlueprintId);
            capturedActivity.GetTagItem("microsoft.tenant.id").Should().Be(expectedAgentDetails.TenantId);

            // Verify guardrail-specific attributes
            capturedActivity.GetTagItem("microsoft.security.decision.type").Should().Be("deny");
            capturedActivity.GetTagItem("microsoft.security.target.type").Should().Be("llm_input");
            capturedActivity.GetTagItem("microsoft.guardian.id").Should().Be("azure-content-safety-001");
            capturedActivity.GetTagItem("microsoft.guardian.name").Should().Be("Azure Content Safety");
            capturedActivity.GetTagItem("microsoft.guardian.provider.name").Should().Be("Azure");
            capturedActivity.GetTagItem("microsoft.guardian.version").Should().Be("2.0.0");
            capturedActivity.GetTagItem("microsoft.security.target.id").Should().Be("msg-12345");
            capturedActivity.GetTagItem("microsoft.security.decision.reason").Should().Be("Content violates hate speech policy");
            capturedActivity.GetTagItem("microsoft.security.decision.code").Should().Be("HATE_SPEECH_001");
            capturedActivity.GetTagItem("microsoft.security.policy.id").Should().Be("policy-abc");
            capturedActivity.GetTagItem("microsoft.security.policy.name").Should().Be("Content Safety Policy");
            capturedActivity.GetTagItem("microsoft.security.policy.version").Should().Be("1.2.0");
            capturedActivity.GetTagItem("microsoft.security.content.input.hash").Should().Be("sha256:abc123def456");
            capturedActivity.GetTagItem("microsoft.security.content.output.value").Should().Be("sanitized-hash-output");
            capturedActivity.GetTagItem("microsoft.security.content.modified").Should().Be(false);
            capturedActivity.GetTagItem("microsoft.security.external_event_id").Should().Be("ext-event-789");

            // Verify caller details
            capturedActivity.GetTagItem("user.id").Should().Be(expectedUserDetails.UserId);
            capturedActivity.GetTagItem("user.email").Should().Be(expectedUserDetails.UserEmail);
            capturedActivity.GetTagItem("user.name").Should().Be(expectedUserDetails.UserName);
            capturedActivity.GetTagItem("client.address").Should().Be(expectedUserDetails.UserClientIP?.ToString());

            // Verify channel
            capturedActivity.GetTagItem("microsoft.channel.name").Should().Be(expectedRequest.Channel?.Name);
            capturedActivity.GetTagItem("microsoft.channel.link").Should().Be(expectedRequest.Channel?.Link);

            // Verify events (finding)
            capturedActivity.Events.Should().HaveCount(1);
            var findingEvent = capturedActivity.Events.First();
            findingEvent.Name.Should().Be("microsoft.security.finding");
            var findingTags = findingEvent.Tags.ToDictionary(t => t.Key, t => t.Value);
            findingTags["microsoft.security.risk.category"].Should().Be("hate_speech");
            findingTags["microsoft.security.risk.severity"].Should().Be(GuardrailRiskSeverity.High);
            findingTags["microsoft.security.policy.decision.type"].Should().Be("deny");
            findingTags["microsoft.security.policy.id"].Should().Be("policy-abc");
            findingTags["microsoft.security.risk.score"].Should().Be(0.95);
        }

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
                agenticUserId: Guid.NewGuid().ToString(),
                agenticUserEmail: "singleton@test.com",
                agentBlueprintId: Guid.NewGuid().ToString(),
                tenantId: Guid.NewGuid().ToString());

            var endpoint = new Uri("https://singleton-test-endpoint");
            var invokeAgentScopeDetails = new InvokeAgentScopeDetails(endpoint: endpoint);
            var request = new Request(
                content: "Singleton test request",
                channel: new Channel(name: "test", link: "singleton test"));
            using (var scope = InvokeAgentScope.Start(request, invokeAgentScopeDetails, agentDetails))
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
