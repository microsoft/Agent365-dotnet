// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs.Builders
{
    [TestClass]
    public class ApplyGuardrailDataBuilderTests
    {
        private static GuardrailDetails Details() => new GuardrailDetails(
            targetType: GuardrailTargetType.LlmInput,
            decisionType: GuardrailDecisionType.Deny);

        private static AgentDetails Agent() => new AgentDetails("agent-id");

        [TestMethod]
        public void Build_NoFindings_ProducesNoEvents()
        {
            var data = ApplyGuardrailDataBuilder.Build(Details(), Agent(), "conv-1", "parent-1");

            data.Events.Should().BeEmpty();
            data.ToDictionary().Should().NotContainKey("Events");
        }

        [TestMethod]
        public void Build_SingleFinding_ProducesOneEvent_WithRequiredShape()
        {
            var findings = new[]
            {
                new GuardrailFinding(
                    riskCategory: "prompt_injection",
                    riskSeverity: GuardrailRiskSeverity.High)
            };

            var data = ApplyGuardrailDataBuilder.Build(Details(), Agent(), "conv-1", "parent-1", findings: findings);

            data.Events.Should().HaveCount(1);
            var evt = data.Events[0];
            evt["name"].Should().Be(OpenTelemetryConstants.GenAiSecurityFindingEventName);
            ((ulong)evt["timeUnixNano"]!).Should().BeGreaterThan(0);

            var attrs = evt["attributes"].Should().BeAssignableTo<IDictionary<string, object?>>().Subject;
            attrs[OpenTelemetryConstants.GenAiSecurityRiskCategoryKey].Should().Be("prompt_injection");
            attrs[OpenTelemetryConstants.GenAiSecurityRiskSeverityKey].Should().Be("high");
        }

        [TestMethod]
        public void Build_OptionalFieldsOmitted_WhenNull()
        {
            var findings = new[]
            {
                new GuardrailFinding(
                    riskCategory: "toxicity",
                    riskSeverity: GuardrailRiskSeverity.Low)
            };

            var data = ApplyGuardrailDataBuilder.Build(Details(), Agent(), "conv-1", "parent-1", findings: findings);

            var attrs = (IDictionary<string, object?>)data.Events[0]["attributes"]!;
            attrs.Should().NotContainKey(OpenTelemetryConstants.GenAiSecurityPolicyDecisionTypeKey);
            attrs.Should().NotContainKey(OpenTelemetryConstants.GenAiSecurityPolicyIdKey);
            attrs.Should().NotContainKey(OpenTelemetryConstants.GenAiSecurityPolicyNameKey);
            attrs.Should().NotContainKey(OpenTelemetryConstants.GenAiSecurityPolicyVersionKey);
            attrs.Should().NotContainKey(OpenTelemetryConstants.GenAiSecurityRiskScoreKey);
            attrs.Should().NotContainKey(OpenTelemetryConstants.GenAiSecurityRiskMetadataKey);
        }

        [TestMethod]
        public void Build_AllFields_PreservesValueTypes()
        {
            var findings = new[]
            {
                new GuardrailFinding(
                    riskCategory: "sensitive_info_disclosure",
                    riskSeverity: GuardrailRiskSeverity.High,
                    policyDecisionType: "deny",
                    policyId: "policy_pii_v2",
                    policyName: "PII Policy",
                    policyVersion: "2.0",
                    riskScore: 0.92,
                    riskMetadata: new[] { "pattern:ssn", "count:2" })
            };

            var data = ApplyGuardrailDataBuilder.Build(Details(), Agent(), "conv-1", "parent-1", findings: findings);

            var attrs = (IDictionary<string, object?>)data.Events[0]["attributes"]!;
            attrs[OpenTelemetryConstants.GenAiSecurityPolicyDecisionTypeKey].Should().Be("deny");
            attrs[OpenTelemetryConstants.GenAiSecurityPolicyIdKey].Should().Be("policy_pii_v2");
            attrs[OpenTelemetryConstants.GenAiSecurityPolicyNameKey].Should().Be("PII Policy");
            attrs[OpenTelemetryConstants.GenAiSecurityPolicyVersionKey].Should().Be("2.0");
            attrs[OpenTelemetryConstants.GenAiSecurityRiskScoreKey].Should().BeOfType<double>().And.Be(0.92);
            attrs[OpenTelemetryConstants.GenAiSecurityRiskMetadataKey].Should().BeOfType<string[]>()
                .Which.Should().BeEquivalentTo(new[] { "pattern:ssn", "count:2" });
        }

        [TestMethod]
        public void Build_MultipleFindings_ProducesMultipleEvents()
        {
            var findings = new[]
            {
                new GuardrailFinding("pii", GuardrailRiskSeverity.Medium),
                new GuardrailFinding("toxicity", GuardrailRiskSeverity.Low)
            };

            var data = ApplyGuardrailDataBuilder.Build(Details(), Agent(), "conv-1", "parent-1", findings: findings);

            data.Events.Should().HaveCount(2);
        }
    }
}
