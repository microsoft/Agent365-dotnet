// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Microsoft.Agents.A365.Tooling.Handlers
{
    using Microsoft.Agents.Builder;
    using Microsoft.Extensions.Logging;
    using Microsoft.Agents.A365.Runtime;
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Agents.A365.Tooling.Utils;
    using System;
    using System.Globalization;
    using System.Net.Http;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using RuntimeUtility = Microsoft.Agents.A365.Runtime.Utils.Utility;

    internal class HttpContextHeadersHandler : DelegatingHandler
    {
        // Header names for passing context information in HTTP requests
        private const string ConversationIdHeader = "x-ms-conversation-id";
        private const string ChannelIdHeader = "x-ms-channel-id";
        private const string SubChannelIdHeader = "x-ms-subchannel-id";
        private const string UserMessageHeader = "x-ms-usermessage";
        private const string O11ySpanIdHeader = "x-ms-span-id";
        private const string O11yTraceIdHeader = "x-ms-trace-id";
        private const string UserAgentHeader = "User-Agent";

        // Keys set from Observability
        private const string O11ySpanIdKey = "O11ySpanId";
        private const string O11yTraceIdKey = "O11yTraceId";

        private readonly ITurnContext turnContext;
        private readonly ILogger logger;
        private readonly ToolOptions toolOptions;
        private readonly string? authToken;

        public HttpContextHeadersHandler(ITurnContext turnContext, ILogger logger, ToolOptions toolOptions, string? authToken = null)
        {
            this.turnContext = turnContext;
            this.logger = logger;
            this.toolOptions = toolOptions;
            this.authToken = authToken;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (turnContext?.Activity != null)
            {
                if (!string.IsNullOrEmpty(turnContext.Activity.Conversation.Id))
                {
                    request.Headers.Add(ConversationIdHeader, turnContext.Activity.Conversation.Id);
                }

                if (!string.IsNullOrEmpty(turnContext.Activity.ChannelId?.Channel))
                {
                    request.Headers.Add(ChannelIdHeader, turnContext.Activity.ChannelId.Channel);
                }

                if (!string.IsNullOrEmpty(turnContext.Activity.ChannelId?.SubChannel))
                {
                    request.Headers.Add(SubChannelIdHeader, turnContext.Activity.ChannelId.SubChannel);
                }

                if (!string.IsNullOrEmpty(turnContext.Activity.Text))
                {
                    var sanitizedMessage = SanitizeTextForHeader(turnContext.Activity.Text, logger);
                    request.Headers.Add(UserMessageHeader, sanitizedMessage);
                }
            }

            if (turnContext != null)
            {
                turnContext.StackState.TryGetValue(O11ySpanIdKey, out var spanIdObj);
                if (spanIdObj is string spanId && !string.IsNullOrEmpty(spanId))
                {
                    request.Headers.Add(O11ySpanIdHeader, spanId);
                }

                turnContext.StackState.TryGetValue(O11yTraceIdKey, out var traceIdObj);
                if (traceIdObj is string traceId && !string.IsNullOrEmpty(traceId))
                {
                    request.Headers.Add(O11yTraceIdHeader, traceId);
                }
            }

            if (this.toolOptions.UserAgentConfiguration != null)
            {
                request.Headers.Add(UserAgentHeader, UserAgentHelper.BuildUserAgent(this.toolOptions.UserAgentConfiguration));
            }

            // Add x-ms-agentid header if auth token is available
            if (!string.IsNullOrEmpty(authToken))
            {
                var agentId = ResolveAgentIdForHeader();
                if (!string.IsNullOrEmpty(agentId))
                {
                    request.Headers.Add(Constants.Headers.AgentIdHeader, agentId);
                }
            }

            return base.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Resolves the best available agent identifier for the x-ms-agentid header.
        /// Priority: TurnContext.agenticAppBlueprintId > token claims (xms_par_app_azp > appid > azp) > application name
        /// </summary>
        /// <returns>Agent ID string or null if not available.</returns>
        private string? ResolveAgentIdForHeader()
        {
            // Priority 1: Agent Blueprint ID from TurnContext
            // The 'From' property may include agenticAppBlueprintId when the request originates from an agentic app
            var blueprintId = GetAgenticAppBlueprintIdFromContext();
            if (!string.IsNullOrEmpty(blueprintId))
            {
                return blueprintId;
            }

            // Priority 2 & 3: Agent ID from token (xms_par_app_azp > appid > azp)
            // Single decode, checks claims in priority order
            if (!string.IsNullOrEmpty(authToken))
            {
                var agentId = RuntimeUtility.GetAgentIdFromToken(authToken);
                if (!string.IsNullOrEmpty(agentId))
                {
                    return agentId;
                }
            }

            // Priority 4: Application name from assembly
            return RuntimeUtility.GetApplicationName();
        }

        /// <summary>
        /// Gets the agentic app blueprint ID from the turn context if available.
        /// </summary>
        /// <returns>The blueprint ID or null if not available.</returns>
        private string? GetAgenticAppBlueprintIdFromContext()
        {
            if (turnContext?.Activity?.From?.Properties == null)
            {
                return null;
            }

            if (turnContext.Activity.From.Properties.TryGetValue("agenticAppBlueprintId", out var blueprintIdElement))
            {
                var blueprintId = blueprintIdElement.ToString();
                if (!string.IsNullOrEmpty(blueprintId))
                {
                    return blueprintId;
                }
            }

            return null;
        }

        public static string SanitizeTextForHeader(string input, ILogger logger)
        {
            try
            {
                if (string.IsNullOrEmpty(input))
                    return string.Empty;

                // Step 1: Normalize common non-breaking spaces and thin spaces
                input = input
                    .Replace('\u00A0', ' ')  // NBSP
                    .Replace('\u202F', ' ')  // NNBSP
                    .Trim();

                // Step 2: Normalize Unicode characters (é -> e)
                string normalized = input.Normalize(NormalizationForm.FormD);
                var sb = new StringBuilder(input.Length);

                foreach (char c in normalized)
                {
                    var category = CharUnicodeInfo.GetUnicodeCategory(c);
                    if (category == UnicodeCategory.NonSpacingMark)
                        continue;

                    // Convert common Unicode punctuation to ASCII equivalents
                    switch (c)
                    {
                        case '’':
                        case '‘':
                            sb.Append('\'');
                            break;
                        case '“':
                        case '”':
                            sb.Append('"');
                            break;
                        case '–':
                        case '—':
                            sb.Append('-');
                            break;
                        case '…':
                            sb.Append("...");
                            break;
                        default:
                            // Keep only printable ASCII (32–126)
                            if (c >= 32 && c <= 126)
                                sb.Append(c);
                            else
                                sb.Append(' '); // Replace non-ASCII with a space
                            break;
                    }
                }

                // Step 3: Collapse multiple spaces/tabs/newlines into one space
                string result = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();

                return result;
            }
            catch (Exception ex)
            {
                logger?.LogWarning("Sanitization failed for input text. Using original text. Exception: {ExceptionMessage}", ex.Message);
                return input;
            }
        }
    }
}