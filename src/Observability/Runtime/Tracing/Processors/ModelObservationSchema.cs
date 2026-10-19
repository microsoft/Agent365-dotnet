// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors
{
    internal static class ModelObservationSchema
    {
        private const string LegacyProviderNameKey = "gen_ai.system";

        public static void NormalizeChatSpan(Activity activity)
        {
            var operationName = activity.GetTagItem(OpenTelemetryConstants.GenAiOperationNameKey) as string;
            if (!string.Equals(operationName, "chat", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(operationName, "chat.completions", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            activity.SetTag(OpenTelemetryConstants.GenAiOperationNameKey, "chat");

            if (activity.GetTagItem(OpenTelemetryConstants.GenAiProviderNameKey) == null &&
                activity.GetTagItem(LegacyProviderNameKey) is string providerName &&
                !string.IsNullOrWhiteSpace(providerName))
            {
                activity.SetTag(OpenTelemetryConstants.GenAiProviderNameKey, providerName);
            }

            if (activity.GetTagItem(OpenTelemetryConstants.ServerPortKey) is string port &&
                int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPort))
            {
                activity.SetTag(OpenTelemetryConstants.ServerPortKey, parsedPort);
            }
        }
    }
}
