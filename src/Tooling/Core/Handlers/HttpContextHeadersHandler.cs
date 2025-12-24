// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Handlers
{
    using Microsoft.Agents.Builder;
    using Microsoft.Extensions.Logging;
    using Microsoft.Agents.A365.Runtime;
    using Microsoft.Agents.A365.Tooling.Models;
    using System;
    using System.Globalization;
    using System.Net.Http;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

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

        public HttpContextHeadersHandler(ITurnContext turnContext, ILogger logger, ToolOptions toolOptions)
        {
            this.turnContext = turnContext;
            this.logger = logger;
            this.toolOptions = toolOptions;
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

            return base.SendAsync(request, cancellationToken);
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