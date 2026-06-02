// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;

[TestClass]
public sealed class ApplyGuardrailScopeTest : ActivityTest
{
    [TestMethod]
    public void Start_SetsRequiredAttributes()
    {
        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.LlmInput,
            decisionType: GuardrailDecisionType.Deny);

        var activity = ListenForActivity(() =>
        {
            using var scope = ApplyGuardrailScope.Start(details, Util.GetAgentDetails());
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiOperationNameKey, OpenTelemetryConstants.ApplyGuardrailOperationName);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiSecurityDecisionTypeKey, "deny");
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiSecurityTargetTypeKey, GuardrailTargetType.LlmInput);
    }

    [TestMethod]
    public void Start_SetsGuardianAttributes_WhenProvided()
    {
        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.LlmOutput,
            decisionType: GuardrailDecisionType.Allow,
            guardianName: "PII Filter",
            guardianId: "guard_abc123",
            guardianProviderName: "azure.ai.content_safety",
            guardianVersion: "2.1.0");

        var activity = ListenForActivity(() =>
        {
            using var scope = ApplyGuardrailScope.Start(details, Util.GetAgentDetails());
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiGuardianNameKey, "PII Filter");
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiGuardianIdKey, "guard_abc123");
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiGuardianProviderNameKey, "azure.ai.content_safety");
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiGuardianVersionKey, "2.1.0");
    }

    [TestMethod]
    public void Start_SetsPolicyAttributes_WhenProvided()
    {
        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.ToolCall,
            decisionType: GuardrailDecisionType.Modify,
            policyId: "policy_pii_v2",
            policyName: "PII Protection Policy",
            policyVersion: "1.0");

        var activity = ListenForActivity(() =>
        {
            using var scope = ApplyGuardrailScope.Start(details, Util.GetAgentDetails());
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiSecurityPolicyIdKey, "policy_pii_v2");
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiSecurityPolicyNameKey, "PII Protection Policy");
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiSecurityPolicyVersionKey, "1.0");
    }

    [TestMethod]
    public void Start_SpanName_IncludesGuardianNameAndTargetType()
    {
        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.LlmInput,
            decisionType: GuardrailDecisionType.Allow,
            guardianName: "Content Safety");

        var activity = ListenForActivity(() =>
        {
            using var scope = ApplyGuardrailScope.Start(details, Util.GetAgentDetails());
        });

        activity.DisplayName.Should().Be("apply_guardrail Content Safety llm_input");
    }

    [TestMethod]
    public void Start_SpanName_OmitsGuardianName_WhenNull()
    {
        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.LlmOutput,
            decisionType: GuardrailDecisionType.Deny);

        var activity = ListenForActivity(() =>
        {
            using var scope = ApplyGuardrailScope.Start(details, Util.GetAgentDetails());
        });

        activity.DisplayName.Should().Be("apply_guardrail llm_output");
    }

    [TestMethod]
    public void Start_SetsContentAttributes_WhenProvided()
    {
        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.LlmOutput,
            decisionType: GuardrailDecisionType.Modify,
            contentInputHash: "sha256:a3f2b8c9",
            contentModified: true);

        var activity = ListenForActivity(() =>
        {
            using var scope = ApplyGuardrailScope.Start(details, Util.GetAgentDetails());
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiSecurityContentInputHashKey, "sha256:a3f2b8c9");
        var modifiedTag = activity.TagObjects.First(t => t.Key == OpenTelemetryConstants.GenAiSecurityContentModifiedKey);
        modifiedTag.Value.Should().Be(true);
    }

    [TestMethod]
    public void RecordDecision_UpdatesDecisionType()
    {
        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.LlmInput,
            decisionType: GuardrailDecisionType.Allow);

        var activity = ListenForActivity(() =>
        {
            using var scope = ApplyGuardrailScope.Start(details, Util.GetAgentDetails());
            scope.RecordDecision(GuardrailDecisionType.Deny, "Prompt injection detected");
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiSecurityDecisionTypeKey, "deny");
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiSecurityDecisionReasonKey, "Prompt injection detected");
    }

    [TestMethod]
    public void RecordContentOutput_SetsOutputValue()
    {
        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.LlmOutput,
            decisionType: GuardrailDecisionType.Modify);

        var activity = ListenForActivity(() =>
        {
            using var scope = ApplyGuardrailScope.Start(details, Util.GetAgentDetails());
            scope.RecordContentOutput("Redacted content here");
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiSecurityContentOutputValueKey, "Redacted content here");
    }

    [TestMethod]
    public void RecordFinding_EmitsSingleEvent()
    {
        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.LlmInput,
            decisionType: GuardrailDecisionType.Deny);

        var activity = ListenForActivity(() =>
        {
            using var scope = ApplyGuardrailScope.Start(details, Util.GetAgentDetails());
            scope.RecordFinding(new GuardrailFinding(
                riskCategory: "prompt_injection",
                riskSeverity: GuardrailRiskSeverity.High,
                policyDecisionType: "deny"));
        });

        activity.Events.Should().HaveCount(1);
        var evt = activity.Events.First();
        evt.Name.Should().Be(OpenTelemetryConstants.GenAiSecurityFindingEventName);
        evt.Tags.First(t => t.Key == OpenTelemetryConstants.GenAiSecurityRiskCategoryKey).Value.Should().Be("prompt_injection");
        evt.Tags.First(t => t.Key == OpenTelemetryConstants.GenAiSecurityRiskSeverityKey).Value.Should().Be("high");
        evt.Tags.First(t => t.Key == OpenTelemetryConstants.GenAiSecurityPolicyDecisionTypeKey).Value.Should().Be("deny");
    }

    [TestMethod]
    public void RecordFinding_EmitsMultipleEvents()
    {
        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.LlmOutput,
            decisionType: GuardrailDecisionType.Modify);

        var activity = ListenForActivity(() =>
        {
            using var scope = ApplyGuardrailScope.Start(details, Util.GetAgentDetails());
            scope.RecordFinding(new GuardrailFinding(
                riskCategory: "pii",
                riskSeverity: GuardrailRiskSeverity.Medium,
                policyDecisionType: "modify"));
            scope.RecordFinding(new GuardrailFinding(
                riskCategory: "toxicity",
                riskSeverity: GuardrailRiskSeverity.Low));
        });

        activity.Events.Should().HaveCount(2);
    }

    [TestMethod]
    public void RecordFinding_IncludesAllAttributes()
    {
        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.LlmInput,
            decisionType: GuardrailDecisionType.Deny);

        var activity = ListenForActivity(() =>
        {
            using var scope = ApplyGuardrailScope.Start(details, Util.GetAgentDetails());
            scope.RecordFinding(new GuardrailFinding(
                riskCategory: "sensitive_info_disclosure",
                riskSeverity: GuardrailRiskSeverity.High,
                policyDecisionType: "deny",
                policyId: "policy_pii_v2",
                policyName: "PII Policy",
                policyVersion: "2.0",
                riskScore: 0.92,
                riskMetadata: new[] { "pattern:ssn", "count:2" }));
        });

        var evt = activity.Events.First();
        evt.Tags.First(t => t.Key == OpenTelemetryConstants.GenAiSecurityRiskCategoryKey).Value.Should().Be("sensitive_info_disclosure");
        evt.Tags.First(t => t.Key == OpenTelemetryConstants.GenAiSecurityRiskSeverityKey).Value.Should().Be("high");
        evt.Tags.First(t => t.Key == OpenTelemetryConstants.GenAiSecurityPolicyDecisionTypeKey).Value.Should().Be("deny");
        evt.Tags.First(t => t.Key == OpenTelemetryConstants.GenAiSecurityPolicyIdKey).Value.Should().Be("policy_pii_v2");
        evt.Tags.First(t => t.Key == OpenTelemetryConstants.GenAiSecurityPolicyNameKey).Value.Should().Be("PII Policy");
        evt.Tags.First(t => t.Key == OpenTelemetryConstants.GenAiSecurityPolicyVersionKey).Value.Should().Be("2.0");
        evt.Tags.First(t => t.Key == OpenTelemetryConstants.GenAiSecurityRiskScoreKey).Value.Should().Be(0.92);
        evt.Tags.First(t => t.Key == OpenTelemetryConstants.GenAiSecurityRiskMetadataKey).Value.Should().BeEquivalentTo(new[] { "pattern:ssn", "count:2" });
    }

    [TestMethod]
    public void RecordError_SetsExpectedFields()
    {
        const string expected = "Guardian service unavailable";
        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.LlmInput,
            decisionType: GuardrailDecisionType.Allow);

        var activity = ListenForActivity(() =>
        {
            using var scope = ApplyGuardrailScope.Start(details, Util.GetAgentDetails());
            scope.RecordError(new Exception(expected));
        });

        activity.ShouldBeError(expected);
    }

    [TestMethod]
    public void Start_SetsConversationId_WhenProvided()
    {
        var conversationId = "conv-guardrail-123";
        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.LlmInput,
            decisionType: GuardrailDecisionType.Allow);

        var activity = ListenForActivity(() =>
        {
            using var scope = ApplyGuardrailScope.Start(
                details,
                Util.GetAgentDetails(),
                request: new Request(conversationId: conversationId));
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiConversationIdKey, conversationId);
    }

    [TestMethod]
    public void Start_SetsExternalEventId_WhenProvided()
    {
        var details = new GuardrailDetails(
            targetType: GuardrailTargetType.Message,
            decisionType: GuardrailDecisionType.Audit,
            externalEventId: "evt_abc123");

        var activity = ListenForActivity(() =>
        {
            using var scope = ApplyGuardrailScope.Start(details, Util.GetAgentDetails());
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiSecurityExternalEventIdKey, "evt_abc123");
    }
}
