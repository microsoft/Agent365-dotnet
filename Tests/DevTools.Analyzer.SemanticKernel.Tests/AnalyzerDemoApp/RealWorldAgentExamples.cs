using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.Agents.A365.Observability.SemanticKernel;
using Microsoft.Agents.A365.Runtime.Common.AspNetCore;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Storage;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;
using System.Text.RegularExpressions;

namespace AnalyzerDemoApp
{
    /// <summary>
    /// A realistic agent implementation showing common patterns developers might write.
    /// This demonstrates typical agent code that would trigger analyzer warnings.
    /// </summary>
    public class WeatherAgent : AgentApplication
    {
        // Common pattern: Direct Kernel field storage
        private readonly Kernel _kernel;
        private readonly ILogger<WeatherAgent> _logger;

        // Common pattern: Constructor with Kernel parameter
        public WeatherAgent(Kernel kernel, ILogger<WeatherAgent> logger) : base(new AgentApplicationOptions((IStorage)null!))
        {
            _kernel = kernel;
            _logger = logger;
        }

        public async Task<string> GetWeatherAsync(string city, HttpContext context)
        {
            // Common pattern: Direct tenant access in methods
            var tenantId = context.User.FindFirst("tenant_id")?.Value;
            var workerId = context.Request.Headers["X-Worker-Id"].FirstOrDefault();

            // Common pattern: Direct kernel service access
            var kernel = context.RequestServices.GetRequiredService<Kernel>();

            // Common pattern: Direct plugin import
            kernel.ImportPluginFromObject(new WeatherPlugin(), "Weather");

            var prompt = $"Get the current weather for {city}";
            var result = await kernel.InvokePromptAsync(prompt);
            
            return result.ToString();
        }
    }

    /// <summary>
    /// Another realistic agent showing different common patterns
    /// </summary>
    public class ChatAgent : AgentApplication
    {
        // Simple pattern: Constructor with Kernel parameter
        public ChatAgent(Kernel kernel) : base(new AgentApplicationOptions((IStorage)null!)) { }

        public async Task ProcessMessageAsync(string message, HttpContext context)
        {
            // Typical flow with multiple common patterns
            var kernel = context.RequestServices.GetRequiredService<Kernel>(); // Direct access
            var tenantId = context.User.FindFirst("tenant_id")?.Value; // Direct tenant access
            var workerId = context.Items["worker_id"] as string; // Direct worker access

            // Import plugins the straightforward way
            kernel.ImportPluginFromObject(new ChatPlugin(), "Chat");

            await kernel.InvokePromptAsync($"Process this message: {message}");
        }
    }

    /// <summary>
    /// ComplianceAgent - demonstrates inheritance pattern
    /// </summary>
    public class ComplianceAgent : AgentApplication
    {
        // Simple constructor pattern
        public ComplianceAgent(Kernel kernel) : base(new AgentApplicationOptions((IStorage)null!)) { }
    }

    /// <summary>
    /// CustomAgent - demonstrates custom agent pattern
    /// </summary>
    public class CustomAgent : AgentApplication
    {
        // Simple constructor pattern
        public CustomAgent(Kernel kernel) : base(new AgentApplicationOptions((IStorage)null!)) { }
    }

    /// <summary>
    /// Realistic service registration patterns developers typically write
    /// </summary>
    public static class ServiceConfiguration
    {
        public static void ConfigureAgentServices(IServiceCollection services)
        {
            // Straightforward service registration patterns
            services.AddSingleton<WeatherAgent>(sp =>
                new WeatherAgent(
                    sp.GetRequiredService<Kernel>(), // Direct kernel access
                    sp.GetRequiredService<ILogger<WeatherAgent>>()));

            services.AddSingleton<ChatAgent>(sp =>
                new ChatAgent(sp.GetRequiredService<Kernel>())); // Simple registration

            // More service registrations
            services.AddSingleton<ComplianceAgent>(sp =>
                new ComplianceAgent(sp.GetRequiredService<Kernel>()));

            services.AddSingleton<CustomAgent>(sp =>
                new CustomAgent(sp.GetRequiredService<Kernel>()));

            // Factory method pattern
            services.AddScoped<WeatherAgent>(sp =>
            {
                var kernel = sp.GetRequiredService<Kernel>(); // Direct access
                var logger = sp.GetRequiredService<ILogger<WeatherAgent>>();
                // Create a different instance for scoped scenarios
                return new WeatherAgent(kernel, logger);
            });

            // Standard service patterns (these are correct)
            services.AddScoped<WeatherService>();
            services.AddHttpClient<WeatherService>();
        }
    }

    /// <summary>
    /// Realistic controller showing typical MVC patterns
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        // Common pattern: Direct Kernel injection in controller
        private readonly Kernel _kernel;
        private readonly WeatherAgent _agent;

        public WeatherController(Kernel kernel, WeatherAgent agent)
        {
            _kernel = kernel;
            _agent = agent;
        }

        [HttpGet("{city}")]
        public async Task<IActionResult> GetWeather(string city)
        {
            // Typical controller method patterns
            var tenantId = HttpContext.User.FindFirst("tenant_id")?.Value; // Direct tenant access
            var workerId = HttpContext.Request.Headers["X-Worker-Id"].FirstOrDefault(); // Direct header access

            // Direct kernel usage in controller
            var kernel = HttpContext.RequestServices.GetRequiredService<Kernel>();
            kernel.ImportPluginFromObject(new WeatherPlugin(), "Weather");

            var result = await _agent.GetWeatherAsync(city, HttpContext);
            return Ok(new { City = city, Weather = result, TenantId = tenantId });
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            // Straightforward agent instantiation
            var chatAgent = new ChatAgent(_kernel);
            await chatAgent.ProcessMessageAsync(request.Message, HttpContext);
            
            return Ok("Message processed");
        }
    }

    /// <summary>
    /// Additional controller showing more typical patterns
    /// </summary>
    [ApiController] 
    [Route("api/[controller]")]
    public class AgentController : ControllerBase
    {
        [HttpPost("create")]
        public IActionResult CreateAgents()
        {
            // Direct agent instantiation patterns
            var weatherAgent = new WeatherAgent(null!, null!);
            var chatAgent = new ChatAgent(null!);
            var complianceAgent = new ComplianceAgent(null!);
            var customAgent = new CustomAgent(null!);

            return Ok("Agents created");
        }

        [HttpGet("tenant-info")]
        public IActionResult GetTenantInfo()
        {
            // Common tenant/worker information access patterns
            var info = new
            {
                TenantFromClaim = HttpContext.User.FindFirst("tenant_id")?.Value,
                WorkerFromClaim = HttpContext.User.FindFirst("worker_id")?.Value,
                TenantFromHeader = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault(),
                WorkerFromHeader = HttpContext.Request.Headers["X-Worker-Id"].FirstOrDefault(),
                TenantFromItems = HttpContext.Items["tenant_id"] as string,
                WorkerFromItems = HttpContext.Items["worker_id"] as string
            };

            return Ok(info);
        }
    }

    /// <summary>
    /// Realistic startup class showing typical ASP.NET Core configuration
    /// </summary>
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // Standard service setup
            services.AddControllers();
            services.AddHttpClient();
            services.AddLogging();

            // Configure agent services
            ServiceConfiguration.ConfigureAgentServices(services);

            // Direct agent registration
            services.AddSingleton(sp => new WeatherAgent(
                sp.GetRequiredService<Kernel>(),
                sp.GetRequiredService<ILogger<WeatherAgent>>()));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            // Common startup patterns
            var serviceProvider = app.ApplicationServices;
            var kernel = serviceProvider.GetRequiredService<Kernel>();
            var agent = new WeatherAgent(kernel, null!);
        }
    }

    /// <summary>
    /// Class showing mixed patterns developers might write
    /// </summary>
    public class MixedPatternExamples
    {
        // Field declaration pattern
        private Kernel _kernel;

        // Constructor parameter pattern
        public MixedPatternExamples(Kernel kernel)
        {
            _kernel = kernel;
        }

        public async Task ProcessRequestWithCommonPatterns(HttpContext context)
        {
            // Common tenant/worker access patterns
            var tenantId = context.User.FindFirst("tenant_id")?.Value;
            var workerId = context.Request.Headers["X-Worker-Id"].FirstOrDefault();

            // Direct kernel access
            var kernel = context.RequestServices.GetRequiredService<Kernel>();

            // Plugin import patterns
            kernel.ImportPluginFromObject(new WeatherPlugin(), "Weather");
            kernel.ImportPluginFromObject(new TimePlugin(), "TestPlugin");

            // Direct agent instantiation
            var agent1 = new WeatherAgent(kernel, null!);
            var agent2 = new ChatAgent(kernel);
            var agent3 = new ComplianceAgent(kernel);

            // Use the kernel (this part is fine)
            await kernel.InvokePromptAsync("Hello");
        }
    }

    // Supporting classes for realistic scenarios
    public class WeatherPlugin
    {
        public string GetCurrentWeather(string city) => $"Sunny, 25�C in {city}";
        public string GetForecast(string city) => $"Partly cloudy tomorrow in {city}";
    }

    public class ChatPlugin
    {
        public string ProcessMessage(string message) => $"Processed: {message}";
    }

    public class WeatherService
    {
        private readonly HttpClient _httpClient;
        
        public WeatherService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetWeatherDataAsync(string city)
        {
            // Realistic HTTP call
            await Task.Delay(100); // Simulate API call
            return $"Weather data for {city}";
        }
    }

    public class TimePlugin
    {
        public string GetCurrentTime() => DateTime.Now.ToString();
        public string GetUtcTime() => DateTime.UtcNow.ToString();
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
    }
}