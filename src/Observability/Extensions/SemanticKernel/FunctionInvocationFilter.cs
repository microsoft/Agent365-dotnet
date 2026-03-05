// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.SemanticKernel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Function invocation filter that adds tracing capabilities to SemanticKernel function calls.
/// </summary>
public sealed class FunctionInvocationFilter : IFunctionInvocationFilter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <inheritdoc />
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var arguments = JsonSerializer.Serialize(context.Arguments, SerializerOptions);

        if (Activity.Current?.OperationName.StartsWith(ExecuteToolScope.OperationName) ?? false)
        {
            // If we are already in a tool execution scope, we do not need to create a new one
            Activity.Current.AddTag(OpenTelemetryConstants.GenAiToolArgumentsKey, arguments);
            Activity.Current.AddTag(OpenTelemetryConstants.GenAiToolTypeKey, ToolType.Function);
            await InvokeWithErrorHandlingAsync(next, context);
            Activity.Current.AddTag(OpenTelemetryConstants.GenAiToolCallResultKey, GetResult(context));
            Activity.Current.AddTag(OpenTelemetryConstants.GenAiToolCallIdKey, context.Function.PluginName);
            return;
        }
    }

    private async Task InvokeWithErrorHandlingAsync(Func<FunctionInvocationContext, Task> next, FunctionInvocationContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            Activity.Current?.AddTag(OpenTelemetryConstants.ErrorTypeKey, ex.GetType().Name);
            Activity.Current?.AddTag(OpenTelemetryConstants.ErrorMessageKey, ex.Message);
            throw;
        }
    }

    private static string GetResult(FunctionInvocationContext context)
    {
        return JsonSerializer.Serialize(context.Result.GetValue<object>(), SerializerOptions);
    }
}