// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs;
using Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Common;

public partial class ExportFormatterTests
{
    [TestMethod]
    public void FormatLogData_NoEvents_OmitsEventsField()
    {
        // Arrange
        var data = new InvokeAgentData(
            new Dictionary<string, object?> { { "key", "val" } },
            spanId: "span-1");
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatLogData(data.ToDictionary());

        // Assert
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("Events", out _).Should().BeFalse();
    }

    [TestMethod]
    public void FormatLogData_WithFindings_EmitsEventsArray()
    {
        // Arrange
        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.LlmInput,
            decisionType: GuardrailDecisionType.Deny);
        var findings = new[]
        {
            new GuardrailFinding("prompt_injection", GuardrailRiskSeverity.High, policyDecisionType: "deny")
        };
        var data = ApplyGuardrailDataBuilder.Build(details, TestAgentDetails, "conv-1", "parent-1", findings: findings);
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatLogData(data.ToDictionary());

        // Assert
        using var doc = JsonDocument.Parse(json);
        var events = doc.RootElement.GetProperty("Events");
        events.GetArrayLength().Should().Be(1);
        var evt = events[0];
        evt.GetProperty("name").GetString().Should().Be(OpenTelemetryConstants.GenAiSecurityFindingEventName);
        evt.GetProperty("timeUnixNano").GetUInt64().Should().BeGreaterThan(0);
        var attrs = evt.GetProperty("attributes");
        attrs.GetProperty(OpenTelemetryConstants.GenAiSecurityRiskCategoryKey).GetString().Should().Be("prompt_injection");
        attrs.GetProperty(OpenTelemetryConstants.GenAiSecurityRiskSeverityKey).GetString().Should().Be("high");
        attrs.GetProperty(OpenTelemetryConstants.GenAiSecurityPolicyDecisionTypeKey).GetString().Should().Be("deny");
    }

    [TestMethod]
    public void FindingEvent_DtoPath_StructurallyMatches_ActivityPath()
    {
        // Arrange — one finding with all fields populated.
        var finding = new GuardrailFinding(
            riskCategory: "sensitive_info_disclosure",
            riskSeverity: GuardrailRiskSeverity.High,
            policyDecisionType: "deny",
            policyId: "policy_pii_v2",
            policyName: "PII Policy",
            policyVersion: "2.0",
            riskScore: 0.92,
            riskMetadata: new[] { "pattern:ssn", "count:2" });

        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.LlmInput,
            decisionType: GuardrailDecisionType.Deny);

        var resource = CreateResource();
        var formatter = CreateFormatter();

        // Activity path: RecordFinding -> FormatSingle
        var activity = ListenForActivity(() =>
        {
            using var scope = ApplyGuardrailScope.Start(details, TestAgentDetails);
            scope.RecordFinding(finding);
        });
        using var activityDoc = JsonDocument.Parse(formatter.FormatSingle(activity, resource));
        var activityEvent = activityDoc.RootElement
            .GetProperty("resourceSpan")
            .GetProperty("scopeSpan")
            .GetProperty("span")
            .GetProperty("events")[0];

        // DTO path: ApplyGuardrailDataBuilder -> FormatLogData
        var data = ApplyGuardrailDataBuilder.Build(details, TestAgentDetails, "conv-1", "parent-1", findings: new[] { finding });
        using var dtoDoc = JsonDocument.Parse(formatter.FormatLogData(data.ToDictionary()));
        var dtoEvent = dtoDoc.RootElement.GetProperty("Events")[0];

        // Assert — same event name and same attributes (timeUnixNano intentionally ignored).
        dtoEvent.GetProperty("name").GetString()
            .Should().Be(activityEvent.GetProperty("name").GetString());

        AttributeMap(dtoEvent.GetProperty("attributes"))
            .Should().BeEquivalentTo(AttributeMap(activityEvent.GetProperty("attributes")));
    }

    private static Dictionary<string, string> AttributeMap(JsonElement attributes) =>
        attributes.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetRawText());
}
