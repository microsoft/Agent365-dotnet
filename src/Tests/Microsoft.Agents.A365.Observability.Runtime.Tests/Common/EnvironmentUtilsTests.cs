// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Common;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Common;

[TestClass]
public class EnvironmentUtilsTests
{
    private const string OverrideEnvVarName = "A365_OBSERVABILITY_SCOPE_OVERRIDE";

    [TestInitialize]
    public void TestInitialize()
    {
        // Ensure clean environment before each test
        Environment.SetEnvironmentVariable(OverrideEnvVarName, null);
    }

    [TestMethod]
    public void GetObservabilityAuthenticationScope_ReturnsOverride_WhenEnvVarIsSet()
    {
        // Arrange
        var expectedOverride = "https://override.example.com/.default";
        Environment.SetEnvironmentVariable(OverrideEnvVarName, expectedOverride);

        // Act
        var scopes = EnvironmentUtils.GetObservabilityAuthenticationScope();

        // Assert
        Assert.IsNotNull(scopes);
        Assert.AreEqual(1, scopes.Length);
        Assert.AreEqual(expectedOverride, scopes[0]);
    }

    [TestMethod]
    public void GetObservabilityAuthenticationScope_ReturnsDefault_WhenEnvVarIsNotSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable(OverrideEnvVarName, null);

        // Act
        var scopes = EnvironmentUtils.GetObservabilityAuthenticationScope();

        // Assert
        Assert.IsNotNull(scopes);
        Assert.AreEqual(1, scopes.Length);
        Assert.AreEqual("api://9b975845-388f-4429-889e-eab1ef63949c/Agent365.Observability.OtelWrite", scopes[0]);
    }
}
