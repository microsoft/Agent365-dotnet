// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Hosting.Etw;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Microsoft.Agents.A365.Observability.Hosting.Tests.Etw
{
    [TestClass]
    public class EtwLoggingBuilderTests
    {
        [TestMethod]
        public void Build_AddsEtwLogProcessor_AndFunnelsExpectedLogs()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
            });

            services.AddLoggingWithEtw();

            var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILogger<EtwLoggingBuilderTests>>();
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var agentDetails = new AgentDetails("agent-id", agentName: "agent-name");
            var invokeAgentDetails = new InvokeAgentDetails(new Uri("https://example.com/agent"), agentDetails, sessionId: "session-1");
            var inferenceDetails = new InferenceCallDetails(InferenceOperationType.Chat, "model-x", "provider-y");
            var toolDetails = new ToolCallDetails("tool-a", arguments: "{ 'arg': 1 }", toolCallId: "tool-call-1");
            string conversationId = "conv-123";

            var originalOut = Console.Out;
            var sb = new StringBuilder();
            using var writer = new StringWriter(sb);
            Console.SetOut(writer);

            // Act
            logger.LogInvokeAgent(invokeAgentDetails, tenantDetails, conversationId);
            logger.LogInferenceCall(inferenceDetails, agentDetails, tenantDetails, conversationId);
            logger.LogToolCall(toolDetails, agentDetails, tenantDetails, conversationId);
            Console.SetOut(originalOut);

            // Assert
            var output = sb.ToString();
            output.Should().Contain("Name: InvokeAgent");
            output.Should().Contain("Name: ExecuteInference");
            output.Should().Contain("Name: ExecuteTool");
        }
    }
}
