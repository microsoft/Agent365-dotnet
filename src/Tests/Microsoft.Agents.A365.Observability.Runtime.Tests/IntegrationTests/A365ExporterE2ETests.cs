// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;

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
            HttpContent? receivedContent = null;
            var expectedAgentId = "agent-abc";
            var agentDetails = new AgentDetails(expectedAgentId, agentName: "TestAgent");
            var endpoint = new Uri("https://test-agent-endpoint");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            var handler = new TestHttpMessageHandler(req =>
            {
                receivedRequest = true;
                receivedContent = req.Content;
                req.RequestUri.Should().NotBeNull();
                req.RequestUri!.ToString().Should().Contain($"/maven/agent365/agents/{expectedAgentId}/traces");
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
            using (var scope = InvokeAgentScope.Start(invokeAgentDetails, tenantDetails))
            {
                // Simulate work
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
    }
}
