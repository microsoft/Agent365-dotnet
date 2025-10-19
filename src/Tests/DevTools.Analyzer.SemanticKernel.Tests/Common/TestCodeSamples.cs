namespace Microsoft.Agents.A365.DevTools.Analyzer.SemanticKernel.Tests.Common
{
    /// <summary>
    /// Centralized test code samples to eliminate duplication across test files.
    /// All test code is kept in one place for consistency and maintainability.
    /// </summary>
    public static class TestCodeSamples
    {
        /// <summary>
        /// Common using statements for analyzer tests.
        /// </summary>
        public const string CommonUsings = @"
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Agents.A365.Tools.SemanticKernel;
using Microsoft.Agents.A365.Tools.SemanticKernel.Extensions;
using System.Security.Claims;";

        /// <summary>
        /// Mock type definitions needed for tests.
        /// </summary>
        public const string MockTypes = @"
namespace Microsoft.Agents.A365.Tools.SemanticKernel
{
    public interface IKernelProvider
    {
        Kernel GetKernel(string tenantId, string workerId);
    }
}

namespace Microsoft.Agents.A365.Tools.SemanticKernel.Extensions
{
    public static class KernelExtensions
    {
        public static bool TryImportPluginFromObject(this Kernel kernel, object plugin, string pluginName) => true;
        public static bool TryAddFromFunctions(this Kernel kernel, string name, object functions) => true;
    }
}

public static class TenantContextHelper
{
    public static string GetTenantId(HttpContext context) => ""tenant"";
    public static string GetWorkerId(HttpContext context) => ""worker"";
}

// Base class for all agents
public abstract class AgentApplication
{
    protected AgentApplication() { }
}";

        /// <summary>
        /// Kernel direct access violation patterns.
        /// </summary>
        public static class KernelDirectAccess
        {
            public const string FieldDeclaration = CommonUsings + @"
public class TestClass
{
    {|#0:private Kernel _kernel;|}
}";

            public const string ConstructorParameter = CommonUsings + @"
public class TestClass
{
    public TestClass({|#0:Kernel kernel|})
    {
    }
}";

            public const string ServiceRetrieval = CommonUsings + @"
public class TestClass
{
    public void TestMethod(HttpContext context)
    {
        var kernel = {|#0:context.RequestServices.GetRequiredService<Kernel>()|};
    }
}";

            public const string UnsafePluginImport = CommonUsings + @"
public class TestClass
{
    public void TestMethod(Kernel kernel, object plugin)
    {
        {|#0:kernel.ImportPluginFromObject(plugin, ""TestPlugin"")|};
    }
}";

            public const string CorrectPattern = CommonUsings + MockTypes + @"
public class TestClass
{
    private readonly IKernelProvider _kernelProvider;
    
    public TestClass(IKernelProvider kernelProvider)
    {
        _kernelProvider = kernelProvider;
    }
    
    public void ProcessRequest(HttpContext context)
    {
        var tenantId = TenantContextHelper.GetTenantId(context);
        var workerId = TenantContextHelper.GetWorkerId(context);
        var kernel = _kernelProvider.GetKernel(tenantId, workerId);
        kernel.TryImportPluginFromObject(new object(), ""test"");
    }
}";
        }

        /// <summary>
        /// Tenant/Worker ID access violation patterns.
        /// </summary>
        public static class TenantWorkerAccess
        {
            public const string FindFirstTenantId = CommonUsings + @"
public class TestClass
{
    public void TestMethod(HttpContext context)
    {
        var tenantId = {|#0:context.User.FindFirst(""tenant_id"")|};
    }
}";

            public const string FindFirstWorkerId = CommonUsings + @"
public class TestClass
{
    public void TestMethod(HttpContext context)
    {
        var workerId = {|#0:context.User.FindFirst(""worker_id"")|};
    }
}";

            public const string HeadersAccess = CommonUsings + @"
public class TestClass
{
    public void TestMethod(HttpContext context)
    {
        var tenantId = {|#0:context.Request.Headers[""X-Tenant-Id""]|};
    }
}";

            public const string ItemsAccess = CommonUsings + @"
public class TestClass
{
    public void TestMethod(HttpContext context)
    {
        var tenantId = {|#0:context.Items[""tenant_id""]|};
    }
}";

            public const string CorrectPattern = CommonUsings + MockTypes + @"
public class TestClass
{
    public void TestMethod(HttpContext context)
    {
        var tenantId = TenantContextHelper.GetTenantId(context);
        var workerId = TenantContextHelper.GetWorkerId(context);
    }
}";
        }

        /// <summary>
        /// AgentApplication registration violation patterns - supports multiple agent types.
        /// </summary>
        public static class AgentApplicationPatterns
        {
            public const string MyAgentDirectInstantiation = MockTypes + @"
public class TestClass
{
    public void TestMethod()
    {
        var agent = {|#0:new MyAgent()|};
    }
}

public class MyAgent : AgentApplication
{
    public MyAgent() { }
}";

            public const string ChatAgentDirectInstantiation = MockTypes + @"
public class TestClass
{
    public void TestMethod()
    {
        var agent = {|#0:new ChatAgent()|};
    }
}

public class ChatAgent : AgentApplication
{
    public ChatAgent() { }
}";

            public const string CustomAgentDirectInstantiation = MockTypes + @"
public class TestClass
{
    public void TestMethod()
    {
        var agent = {|#0:new ComplianceAgent()|};
    }
}

public class ComplianceAgent : AgentApplication
{
    public ComplianceAgent() { }
}";

            public const string MyAgentKernelConstructor = CommonUsings + MockTypes + @"
public class MyAgent : AgentApplication
{
    public MyAgent({|#0:Kernel kernel|})
    {
    }
}";

            public const string ChatAgentKernelConstructor = CommonUsings + MockTypes + @"
public class ChatAgent : AgentApplication
{
    public ChatAgent({|#0:Kernel kernel|})
    {
    }
}";

            public const string MyAgentServiceRegistrationWithKernel = CommonUsings + MockTypes + @"
public class Program
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<MyAgent>(sp =>
            {|#0:new MyAgent({|#1:sp.GetRequiredService<Kernel>()|})|})
        );
    }
}

public class MyAgent : AgentApplication
{
    public MyAgent({|#2:Kernel kernel|}) { }
}";

            public const string ChatAgentServiceRegistrationWithKernel = CommonUsings + MockTypes + @"
public class Program
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ChatAgent>(sp =>
            {|#0:new ChatAgent({|#1:sp.GetRequiredService<Kernel>()|})|})
        );
    }
}

public class ChatAgent : AgentApplication
{
    public ChatAgent({|#2:Kernel kernel|}) { }
}";

            public const string CorrectPatternMyAgent = CommonUsings + MockTypes + @"
public class Program
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IKernelProvider, MyKernelProvider>();
        services.AddSingleton<MyAgent>();
    }
}

public class MyAgent : AgentApplication
{
    public MyAgent(IKernelProvider kernelProvider) { }
}

public class MyKernelProvider : IKernelProvider
{
    public Kernel GetKernel(string tenantId, string workerId) => null;
}";

            public const string CorrectPatternChatAgent = CommonUsings + MockTypes + @"
public class Program
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IKernelProvider, MyKernelProvider>();
        services.AddSingleton<ChatAgent>();
    }
}

public class ChatAgent : AgentApplication
{
    public ChatAgent(IKernelProvider kernelProvider) { }
}

public class MyKernelProvider : IKernelProvider
{
    public Kernel GetKernel(string tenantId, string workerId) => null;
}";

            public const string CorrectPatternMultipleAgents = CommonUsings + MockTypes + @"
public class Program
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IKernelProvider, MyKernelProvider>();
        services.AddSingleton<MyAgent>();
        services.AddSingleton<ChatAgent>();
        services.AddSingleton<ComplianceAgent>();
    }
}

public class MyAgent : AgentApplication
{
    public MyAgent(IKernelProvider kernelProvider) { }
}

public class ChatAgent : AgentApplication
{
    public ChatAgent(IKernelProvider kernelProvider) { }
}

public class ComplianceAgent : AgentApplication
{
    public ComplianceAgent(IKernelProvider kernelProvider) { }
}

public class MyKernelProvider : IKernelProvider
{
    public Kernel GetKernel(string tenantId, string workerId) => null;
}";
        }

        // Backward compatibility - keep MyAgentPatterns for existing tests
        /// <summary>
        /// [Deprecated] Use AgentApplicationPatterns instead for better flexibility.
        /// MyAgent registration violation patterns.
        /// </summary>
        [System.Obsolete("Use AgentApplicationPatterns instead to support multiple agent types")]
        public static class MyAgentPatterns
        {
            public const string DirectInstantiation = AgentApplicationPatterns.MyAgentDirectInstantiation;
            public const string KernelConstructor = AgentApplicationPatterns.MyAgentKernelConstructor;
            public const string ServiceRegistrationWithKernel = AgentApplicationPatterns.MyAgentServiceRegistrationWithKernel;
            public const string CorrectPattern = AgentApplicationPatterns.CorrectPatternMyAgent;
        }
    }
}
