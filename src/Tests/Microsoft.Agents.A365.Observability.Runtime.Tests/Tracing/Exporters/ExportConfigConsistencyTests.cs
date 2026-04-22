// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Tracing.Exporters;

/// <summary>
/// Cross-constant consistency test for observability export configuration.
///
/// Three production constants must stay in sync: the auth scope
/// (<c>ProdObservabilityScope</c>), the default endpoint host
/// (<c>DefaultEndpointHost</c>), and the export URL path pattern
/// (<c>BuildEndpointPath</c>). This test pins all three together so that
/// changing any one forces a review of the others. Individual value tests
/// already exist in <c>EnvironmentUtilsTests</c> and <c>Agent365ExporterTests</c>;
/// this test adds the cross-cutting invariant they don't cover.
/// </summary>
[TestClass]
public sealed class ExportConfigConsistencyTests
{
    private const string ScopeOverrideEnvVar = "A365_OBSERVABILITY_SCOPE_OVERRIDE";

    // Pinned production values — update ALL of these together when any one changes.
    private const string ExpectedScope = "api://9b975845-388f-4429-889e-eab1ef63949c/Agent365.Observability.OtelWrite";
    private const string ExpectedStandardUri = "https://agent365.svc.cloud.microsoft/observability/tenants/t1/otlp/agents/a1/traces?api-version=1";
    private const string ExpectedS2SUr = "https://agent365.svc.cloud.microsoft/observabilityService/tenants/t1/otlp/agents/a1/traces?api-version=1";

    [TestInitialize]
    public void TestInitialize() => Environment.SetEnvironmentVariable(ScopeOverrideEnvVar, null);

    [TestCleanup]
    public void TestCleanup() => Environment.SetEnvironmentVariable(ScopeOverrideEnvVar, null);

    [TestMethod]
    public void ExportConfig_Scope_Endpoint_And_Paths_AreConsistent()
    {
        // Auth scope
        var scopes = EnvironmentUtils.GetObservabilityAuthenticationScope();
        scopes.Should().ContainSingle()
            .Which.Should().Be(ExpectedScope,
                "ProdObservabilityScope changed — also review DefaultEndpointHost and BuildEndpointPath.");

        // Full URIs (standard + S2S) combining DefaultEndpointHost + BuildEndpointPath + BuildRequestUri
        var core = new Agent365ExporterCore(
            new ExportFormatter(NullLogger<ExportFormatter>.Instance),
            NullLogger<Agent365ExporterCore>.Instance);

        var standardUri = core.BuildRequestUri(
            Agent365ExporterOptions.DefaultEndpointHost,
            core.BuildEndpointPath("t1", "a1", useS2SEndpoint: false));

        var s2sUri = core.BuildRequestUri(
            Agent365ExporterOptions.DefaultEndpointHost,
            core.BuildEndpointPath("t1", "a1", useS2SEndpoint: true));

        standardUri.Should().Be(ExpectedStandardUri,
            "Standard export URI changed — also review ProdObservabilityScope and DefaultEndpointHost.");

        s2sUri.Should().Be(ExpectedS2SUr,
            "S2S export URI changed — also review ProdObservabilityScope and DefaultEndpointHost.");

        // Coarse sanity: scope targets Agent365 Observability, endpoint targets agent365 service
        scopes[0].Should().Contain("Agent365.Observability");
        Agent365ExporterOptions.DefaultEndpointHost.Should().Contain("agent365");
    }
}
