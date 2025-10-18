using Microsoft.SemanticKernel;
using AnalyzerDemoApp;

var builder = WebApplication.CreateBuilder(args);

// ? Realistic startup configuration with violations
builder.Services.AddControllers();

// This would be a realistic mistake - configuring services incorrectly
ServiceConfiguration.ConfigureAgentServices(builder.Services);

// ? More startup violations that developers might actually make
builder.Services.AddSingleton<Kernel>(sp => 
{
    var kernel = new Kernel();
    // Direct plugin imports during startup
    kernel.ImportPluginFromObject(new WeatherPlugin(), "Weather"); // A365SK0004
    return kernel;
});

var app = builder.Build();

// ? Realistic app configuration violations
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.MapControllers();

// ? Direct agent creation in startup - a common mistake
var serviceProvider = app.Services;
var kernel = serviceProvider.GetRequiredService<Kernel>(); // A365SK0001
var logger = serviceProvider.GetRequiredService<ILogger<WeatherAgent>>();
var weatherAgent = new WeatherAgent(kernel, logger); // A365SK0005

app.MapGet("/test", async (HttpContext context) =>
{
    // ? Inline violations in endpoints
    var tenantId = context.User.FindFirst("tenant_id")?.Value; // A365SK0002
    var result = await weatherAgent.GetWeatherAsync("Seattle", context);
    return Results.Ok($"Weather: {result} for tenant: {tenantId}");
});

Console.WriteLine("?? Analyzer Demo Application with REALISTIC Analyzer Violations");
Console.WriteLine("?? This app demonstrates real-world patterns that violate governance rules");
Console.WriteLine("?? Build this project to see analyzer errors in Visual Studio");

app.Run();