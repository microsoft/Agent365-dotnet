// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Common;

[TestClass]
public class EnvironmentUtilsTests
{
    [TestInitialize]
    public void TestInit()
    {
        // Reset initialization before each test by forcing re-initialize with empty configuration
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        EnvironmentUtils.Initialize(cfg, force: true);
    }

    [TestMethod]
    public void GetObservabilityAuthenticationScope_DefaultsToProd_WhenNoOverride()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        EnvironmentUtils.Initialize(cfg, force: true);

        var scopes = EnvironmentUtils.GetObservabilityAuthenticationScope();
        Assert.AreEqual(1, scopes.Length);
        Assert.AreEqual("https://api.powerplatform.com/.default", scopes[0]);
    }

    [TestMethod]
    public void GetObservabilityAuthenticationScope_UsesOverride_WhenProvided()
    {
        var expected = "https://api.preprod.powerplatform.com/.default";
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["A365_OBSERVABILITY_SCOPE_OVERRIDE"] = expected,
            })
            .Build();

        EnvironmentUtils.Initialize(cfg, force: true);

        var scopes = EnvironmentUtils.GetObservabilityAuthenticationScope();
        Assert.AreEqual(1, scopes.Length);
        Assert.AreEqual(expected, scopes[0]);
    }

    [TestMethod]
    public void Initialize_WithForce_ReplacesCachedOverride()
    {
        var first = "https://first.scope/.default";
        var second = "https://second.scope/.default";

        var cfg1 = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["A365_OBSERVABILITY_SCOPES_OVERRIDE"] = first,
            })
            .Build();
        EnvironmentUtils.Initialize(cfg1, force: true);
        var scopes1 = EnvironmentUtils.GetObservabilityAuthenticationScope();
        Assert.AreEqual(first, scopes1[0]);

        // Without force, override should remain unchanged
        var cfg2 = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["A365_OBSERVABILITY_SCOPES_OVERRIDE"] = second,
            })
            .Build();
        EnvironmentUtils.Initialize(cfg2, force: false);
        var scopesNoForce = EnvironmentUtils.GetObservabilityAuthenticationScope();
        Assert.AreEqual(first, scopesNoForce[0]);

        // With force, value should update
        EnvironmentUtils.Initialize(cfg2, force: true);
        var scopes2 = EnvironmentUtils.GetObservabilityAuthenticationScope();
        Assert.AreEqual(second, scopes2[0]);
    }
}
