using Microsoft.Agents.A365.Observability.Runtime.Common;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Common;


[TestClass]
public class PowerPlatformApiDiscoveryTests
{
    [TestMethod]
    public void GetTokenAudience_Mapping_IsCorrect()
    {
        var expected = new Dictionary<string, string>
        {
            ["firstrelease"] = "https://api.powerplatform.com",
            ["prod"] = "https://api.powerplatform.com",
            ["gov"] = "https://api.gov.powerplatform.microsoft.us",
            ["high"] = "https://api.high.powerplatform.microsoft.us",
            ["dod"] = "https://api.appsplatform.us",
            ["mooncake"] = "https://api.powerplatform.partner.microsoftonline.cn",
            ["ex"] = "https://api.powerplatform.eaglex.ic.gov",
            ["rx"] = "https://api.powerplatform.microsoft.scloud",
        };

        foreach (var kv in expected)
        {
            var disc = new PowerPlatformApiDiscovery(kv.Key);
            Assert.AreEqual(kv.Value, disc.GetTokenAudience());
        }
    }

    [TestMethod]
    public void GetTokenEndpointHost_Mapping_IsCorrect()
    {
        var expected = new Dictionary<string, string>
        {
            ["firstrelease"] = "api.powerplatform.com",
            ["prod"] = "api.powerplatform.com",
            ["gov"] = "api.gov.powerplatform.microsoft.us",
            ["high"] = "api.high.powerplatform.microsoft.us",
            ["dod"] = "api.appsplatform.us",
            ["mooncake"] = "api.powerplatform.partner.microsoftonline.cn",
            ["ex"] = "api.powerplatform.eaglex.ic.gov",
            ["rx"] = "api.powerplatform.microsoft.scloud",
        };

        foreach (var kv in expected)
        {
            var disc = new PowerPlatformApiDiscovery(kv.Key);
            Assert.AreEqual(kv.Value, disc.GetTokenEndpointHost());
        }
    }

    [TestMethod]
    public void GetTenantEndpoint_GeneratesExpectedValues()
    {
        var tenantId = "e3064512-cc6d-4703-be71-a2ecaecaa98a";
        var expected = new Dictionary<string, string>
        {
            ["firstrelease"] = "e3064512cc6d4703be71a2ecaecaa9.8a.tenant.api.powerplatform.com",
            ["prod"] = "e3064512cc6d4703be71a2ecaecaa9.8a.tenant.api.powerplatform.com",
            ["gov"] = "e3064512cc6d4703be71a2ecaecaa98.a.tenant.api.gov.powerplatform.microsoft.us",
            ["high"] = "e3064512cc6d4703be71a2ecaecaa98.a.tenant.api.high.powerplatform.microsoft.us",
            ["dod"] = "e3064512cc6d4703be71a2ecaecaa98.a.tenant.api.appsplatform.us",
            ["mooncake"] = "e3064512cc6d4703be71a2ecaecaa98.a.tenant.api.powerplatform.partner.microsoftonline.cn",
            ["ex"] = "e3064512cc6d4703be71a2ecaecaa98.a.tenant.api.powerplatform.eaglex.ic.gov",
            ["rx"] = "e3064512cc6d4703be71a2ecaecaa98.a.tenant.api.powerplatform.microsoft.scloud",
        };

        foreach (var kv in expected)
        {
            var disc = new PowerPlatformApiDiscovery(kv.Key);
            Assert.AreEqual(kv.Value, disc.GetTenantEndpoint(tenantId));
        }
    }

    [TestMethod]
    public void GetTenantIslandClusterEndpoint_GeneratesExpectedValues()
    {
        var tenantId = "e3064512-cc6d-4703-be71-a2ecaecaa98a";
        var expected = new Dictionary<string, string>
        {
            ["firstrelease"] = "il-e3064512cc6d4703be71a2ecaecaa9.8a.tenant.api.powerplatform.com",
            ["prod"] = "il-e3064512cc6d4703be71a2ecaecaa9.8a.tenant.api.powerplatform.com",
            ["gov"] = "il-e3064512cc6d4703be71a2ecaecaa98.a.tenant.api.gov.powerplatform.microsoft.us",
            ["high"] = "il-e3064512cc6d4703be71a2ecaecaa98.a.tenant.api.high.powerplatform.microsoft.us",
            ["dod"] = "il-e3064512cc6d4703be71a2ecaecaa98.a.tenant.api.appsplatform.us",
            ["mooncake"] = "il-e3064512cc6d4703be71a2ecaecaa98.a.tenant.api.powerplatform.partner.microsoftonline.cn",
            ["ex"] = "il-e3064512cc6d4703be71a2ecaecaa98.a.tenant.api.powerplatform.eaglex.ic.gov",
            ["rx"] = "il-e3064512cc6d4703be71a2ecaecaa98.a.tenant.api.powerplatform.microsoft.scloud",
        };

        foreach (var kv in expected)
        {
            var disc = new PowerPlatformApiDiscovery(kv.Key);
            Assert.AreEqual(kv.Value, disc.GetTenantIslandClusterEndpoint(tenantId));
        }
    }

    [TestMethod]
    public void GetTenantEndpoint_InvalidCharacters_ThrowsExactMessage()
    {
        var disc = new PowerPlatformApiDiscovery("prod");
        var ex = Assert.Throws<ArgumentException>(() => disc.GetTenantEndpoint("invalid?"));
        Assert.Contains("invalid host name characters", ex.Message);
    }

    [TestMethod]
    public void GetTenantEndpoint_InsufficientLength_ThrowsExactMessage()
    {
        var disc = new PowerPlatformApiDiscovery("prod");
        var ex1 = Assert.Throws<ArgumentException>(() => disc.GetTenantEndpoint("a"));
        Assert.Contains("must be at least 3 characters in length", ex1.Message);

        var ex2 = Assert.Throws<ArgumentException>(() => disc.GetTenantEndpoint("a-"));
        Assert.Contains("must be at least 3 characters in length", ex2.Message);

        var discProd = new PowerPlatformApiDiscovery("prod");
        var ex3 = Assert.Throws<ArgumentException>(() => discProd.GetTenantEndpoint("aa"));
        Assert.Contains("must be at least 3 characters in length", ex3.Message);

        var ex4 = Assert.Throws<ArgumentException>(() => discProd.GetTenantEndpoint("a-a"));
        Assert.Contains("must be at least 3 characters in length", ex4.Message);
    }
}

