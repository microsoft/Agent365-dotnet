// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionInvocationFilter"/> class.
    /// </summary>
    public FunctionInvocationFilter()
        : this(NullLogger<FunctionInvocationFilter>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionInvocationFilter"/> class with a logger.
    /// </summary>
    /// <param name="logger">The logger to use for logging function invocations.</param>
    public FunctionInvocationFilter(ILogger<FunctionInvocationFilter> logger)
    {
        this._logger = logger ?? NullLogger<FunctionInvocationFilter>.Instance;
    }

    /// <inheritdoc />
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var functionName = $"{context.Function.PluginName}-{context.Function.Name}";
        var arguments = JsonSerializer.Serialize(context.Arguments, SerializerOptions);

        // Log MCP tool invocations (plugins like CalendarTools, MailTools, etc.)
        if (context.Function.PluginName?.Contains("Tools", StringComparison.OrdinalIgnoreCase) == true)
        {
            this._logger.LogInformation("[MCP-TOOL] Invoking {FunctionName} with arguments: {Arguments}", functionName, arguments);
        }

        if (Activity.Current?.OperationName.StartsWith(ExecuteToolScope.OperationName) ?? false)
        {
            // If we are already in a tool execution scope, we do not need to create a new one
            Activity.Current.AddTag(OpenTelemetryConstants.GenAiToolArgumentsKey, arguments);
            Activity.Current.AddTag(OpenTelemetryConstants.GenAiToolTypeKey, ToolType.Function);
            await InvokeWithErrorHandlingAsync(next, context);

            var result = GetResult(context);
            Activity.Current.AddTag(OpenTelemetryConstants.GenAiEventContent, result);
            Activity.Current.AddTag(OpenTelemetryConstants.GenAiToolCallIdKey, context.Function.PluginName);

            // Log MCP tool results
            if (context.Function.PluginName?.Contains("Tools", StringComparison.OrdinalIgnoreCase) == true)
            {
                // Truncate very long results
                var truncatedResult = result.Length > 5000 ? result.Substring(0, 5000) + "... [TRUNCATED]" : result;
                this._logger.LogInformation("[MCP-TOOL] {FunctionName} result: {Result}", functionName, truncatedResult);
            }

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