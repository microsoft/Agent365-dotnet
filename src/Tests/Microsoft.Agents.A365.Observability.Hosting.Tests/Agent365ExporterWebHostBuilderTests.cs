using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.A365.Observability.Hosting.Tests
{
    [TestClass]
    public class Agent365ExporterWebHostBuilderTests
    {
        private TestHttpMessageHandler? _handler;
        private bool _receivedRequest;
        private string? _receivedContent;

        public Agent365ExporterWebHostBuilderTests()
        {
            Environment.SetEnvironmentVariable("EnableAgent365Exporter", "true");
        }

        [TestMethod]
        public async Task AddTracing_And_InvokeAgentScope_ExporterMakesExpectedRequest_UsingWebHostBuilder()
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
                callerUpn: "caller-123@ztaittest12.onmicrosoft.com",
                callerClientIP: IPAddress.Parse("203.0.113.42"),
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
            this.GetAttribute(attributes, "gen_ai.caller.client.ip").Should().Be(expectedCallerDetails.CallerClientIP?.ToString());
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
            this.GetAttribute(attributes, "gen_ai.agent.type").Should().Be(expectedAgentType.ToString());
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

        private ServiceProvider CreateTestServiceProviderUsingWebHostBuilder(HttpClient httpClient)
        {
            var webHostBuilder = new WebHostBuilder();

            // Ensure environment variables are part of configuration
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            webHostBuilder.UseConfiguration(configuration);

            // Configure services and tracing via the IWebHostBuilder extension
            webHostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<HttpClient>(httpClient);
                services.AddSingleton<Agent365ExporterOptions>(sp =>
                {
                    return new Agent365ExporterOptions
                    {
                        UseS2SEndpoint = false,
                        TokenResolver = (_, _) => Task.FromResult<string?>("test-token")
                    };
                });
            });
            webHostBuilder.AddA365Tracing(useOpenTelemetryBuilder: false, agent365ExporterType: Agent365ExporterType.Agent365Exporter);

            // Minimal startup configuration to satisfy WebHost startup requirements
            webHostBuilder.UseStartup<MinimalStartup>();

            var host = webHostBuilder.Build();
            return host.Services.CreateScope().ServiceProvider as ServiceProvider ?? (ServiceProvider)host.Services;
        }

        // Minimal Startup class for WebHostBuilder
        private class MinimalStartup
        {
            public void Configure(IApplicationBuilder app)
            {
                // no-op
            }
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
            this.CreateTestServiceProviderUsingWebHostBuilder(httpClient);
        }
    }
}
