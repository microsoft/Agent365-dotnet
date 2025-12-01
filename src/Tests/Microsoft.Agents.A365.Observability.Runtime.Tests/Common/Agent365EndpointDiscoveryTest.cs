// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Runtime.Common;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Common;


[TestClass]
public class Agent365EndpointDiscoveryTests
{
    [TestMethod]
    public void GetBaseHost_Mapping_IsCorrect()
    {
        var expected = new Dictionary<string, string>
        {
            ["preprod"] = "preprod.agent365.svc.cloud.dev.microsoft",
            ["firstrelease"] = "preprod.agent365.svc.cloud.dev.microsoft",
            ["production"] = "agent365.svc.cloud.microsoft",
            ["prod"] = "agent365.svc.cloud.microsoft",
        };

        foreach (var kv in expected)
        {
            var disc = new Agent365EndpointDiscovery(kv.Key);
            Assert.AreEqual(kv.Value, disc.GetBaseHost());
        }
    }

    [TestMethod]
    public void GetBaseHost_IsCaseInsensitive()
    {
        var disc1 = new Agent365EndpointDiscovery("PreProd");
        Assert.AreEqual("preprod.agent365.svc.cloud.dev.microsoft", disc1.GetBaseHost());

        var disc2 = new Agent365EndpointDiscovery("PROD");
        Assert.AreEqual("agent365.svc.cloud.microsoft", disc2.GetBaseHost());
    }

    [TestMethod]
    public void GetBaseHost_DefaultsToProductionForUnknown()
    {
        var disc = new Agent365EndpointDiscovery("unknown-category");
        Assert.AreEqual("agent365.svc.cloud.microsoft", disc.GetBaseHost());
    }
}
