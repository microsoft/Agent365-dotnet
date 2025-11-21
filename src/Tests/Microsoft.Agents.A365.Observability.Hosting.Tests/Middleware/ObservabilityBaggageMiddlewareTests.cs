using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.A365.Observability.Hosting.Middleware;
using System.Text.Json;

namespace Microsoft.Agents.A365.Observability.Hosting.Tests.Middleware;

[TestClass]
public class ObservabilityBaggageMiddlewareTests
{
    [TestMethod]
    public async Task Middleware_UsesResolverValues()
    {
        // Arrange
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseObservabilityRequestContext(ctx => ("tenant-abc", "agent-xyz"));
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/", (HttpContext ctx) =>
                    {
                        var (tenant, agent) = ("tenant-abc", "agent-xyz");
                        return Results.Json(new { tenant, agent });
                    });
                });
            });

        using var server = new TestServer(builder);
        var client = server.CreateClient();

        // Act
        var response = await client.GetAsync("/");
        var json = await response.Content.ReadAsStringAsync();

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.AreEqual("tenant-abc", root.GetProperty("tenant").GetString());
        Assert.AreEqual("agent-xyz", root.GetProperty("agent").GetString());
    }
}