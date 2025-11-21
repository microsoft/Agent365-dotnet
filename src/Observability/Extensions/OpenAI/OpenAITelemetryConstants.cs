// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Extensions.OpenAI;

/// <summary>
/// Contains constants for operation names, tag names, and activity source names used in OpenAI tracing.
/// </summary>
internal static class OpenAITelemetryConstants
{
    // Operation Names
    public const string ChatOperation = "chat";

    // Activity Source Names
    public const string OpenAISource = "OpenAI";
    public const string OpenAISourceWildcard = "OpenAI.*";
}
