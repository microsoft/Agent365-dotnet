// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Runtime.Common;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Common;


[TestClass]
public class Agent365EndpointDiscoveryTests
{
    [TestMethod]
    public void GetHost_Mapping_IsCorrect()
    {
        var expected = new Dictionary<string, string>
        {
            ["firstrelease"] = "agent365.svc.cloud.microsoft",
            ["production"] = "agent365.svc.cloud.microsoft",
            ["prod"] = "agent365.svc.cloud.microsoft",
        };

        foreach (var kv in expected)
        {
            var disc = new Agent365EndpointDiscovery(kv.Key);
            Assert.AreEqual(kv.Value, disc.GetHost());
        }
    }

    [TestMethod]
    public void GetHost_IsCaseInsensitive()
    {
        var disc1 = new Agent365EndpointDiscovery("PRODUCTION");
        Assert.AreEqual("agent365.svc.cloud.microsoft", disc1.GetHost());

        var disc2 = new Agent365EndpointDiscovery("PROD");
        Assert.AreEqual("agent365.svc.cloud.microsoft", disc2.GetHost());
    }

    [TestMethod]
    public void GetHost_ThrowsForUnknown()
    {
        var disc = new Agent365EndpointDiscovery("unknown-category");
        Assert.ThrowsException<System.ArgumentException>(() => disc.GetHost());
    }
}
