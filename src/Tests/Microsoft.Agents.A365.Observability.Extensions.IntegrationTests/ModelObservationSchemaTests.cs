// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Extensions.AgentFramework;
using Microsoft.Agents.A365.Observability.Extensions.OpenAI;
using Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Extensions.IntegrationTests;

[TestClass]
public class ModelObservationSchemaTests
{
    private static readonly ActivityListener Listener = CreateListener();

    [TestMethod]
    public void OpenAIProcessor_NormalizesProviderAndServerPort()
    {
        using var activity = CreateActivity(
            "OpenAI.Chat",
            ("gen_ai.operation.name", "chat"),
            ("gen_ai.system", "openai"),
            ("gen_ai.request.model", "gpt-4o"),
            ("gen_ai.response.model", "gpt-4o-2024-08-06"),
            ("server.address", "models.example.com"),
            ("server.port", "8443"));

        new OpenAISpanProcessor().OnEnd(activity);

        activity.GetTagItem("gen_ai.operation.name").Should().Be("chat");
        activity.GetTagItem("gen_ai.provider.name").Should().Be("openai");
        activity.GetTagItem("gen_ai.request.model").Should().Be("gpt-4o");
        activity.GetTagItem("gen_ai.response.model").Should().Be("gpt-4o-2024-08-06");
        activity.GetTagItem("server.address").Should().Be("models.example.com");
        activity.GetTagItem("server.port").Should().Be(8443);
    }

    [TestMethod]
    public void SemanticKernelProcessor_NormalizesChatOperationAndProvider()
    {
        using var activity = CreateActivity(
            "Microsoft.SemanticKernel",
            ("gen_ai.operation.name", "chat.completions"),
            ("gen_ai.system", "azure.ai.openai"),
            ("gen_ai.request.model", "gpt-4o"));

        new SemanticKernelSpanProcessor().OnEnd(activity);

        activity.GetTagItem("gen_ai.operation.name").Should().Be("chat");
        activity.GetTagItem("gen_ai.provider.name").Should().Be("azure.ai.openai");
    }

    [TestMethod]
    [DataRow("chat.completions", "chat.completions MyChatDeployment", "chat MyChatDeployment")]
    [DataRow("chat.completions", "chat.completions ChatGPT-4o", "chat ChatGPT-4o")]
    [DataRow("chat.completions", "chat.completions mychat.completionsDeployment", "chat mychat.completionsDeployment")]
    [DataRow("chat.completions", "chat.completions", "chat")]
    [DataRow("chat", "chat MyChatDeployment", "chat MyChatDeployment")]
    [DataRow("chat", "chat mychat.completionsDeployment", "chat mychat.completionsDeployment")]
    public void SemanticKernelProcessor_NormalizesOnlyTheOperationPrefix(
        string operationName,
        string displayName,
        string expectedDisplayName)
    {
        using var activity = CreateActivity(
            "Microsoft.SemanticKernel",
            ("gen_ai.operation.name", operationName));
        activity.DisplayName = displayName;

        new SemanticKernelSpanProcessor().OnEnd(activity);

        activity.GetTagItem("gen_ai.operation.name").Should().Be("chat");
        activity.DisplayName.Should().Be(expectedDisplayName);
    }

    [TestMethod]
    public void AgentFrameworkProcessor_PreservesCanonicalCacheUsage()
    {
        using var activity = CreateActivity(
            Microsoft.Agents.A365.Observability.Extensions.AgentFramework.BuilderExtensions.AgentFrameworkChatClientSource,
            ("gen_ai.operation.name", "chat"),
            ("gen_ai.provider.name", "openai"),
            ("gen_ai.request.model", "gpt-4o"),
            ("gen_ai.usage.input_tokens", 100),
            ("gen_ai.usage.output_tokens", 25),
            ("gen_ai.usage.cache_read.input_tokens", 40),
            ("gen_ai.usage.cache_write.input_tokens", 10));

        new AgentFrameworkSpanProcessor().OnEnd(activity);

        activity.GetTagItem("gen_ai.operation.name").Should().Be("chat");
        activity.GetTagItem("gen_ai.provider.name").Should().Be("openai");
        activity.GetTagItem("gen_ai.usage.input_tokens").Should().Be(100);
        activity.GetTagItem("gen_ai.usage.output_tokens").Should().Be(25);
        activity.GetTagItem("gen_ai.usage.cache_read.input_tokens").Should().Be(40);
        activity.GetTagItem("gen_ai.usage.cache_write.input_tokens").Should().Be(10);
    }

    private static Activity CreateActivity(string sourceName, params (string Key, object Value)[] tags)
    {
        _ = Listener;
        using var source = new ActivitySource(sourceName);
        var activity = source.StartActivity("chat", ActivityKind.Client)!;

        foreach (var (key, value) in tags)
        {
            activity.SetTag(key, value);
        }

        return activity;
    }

    private static ActivityListener CreateListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
