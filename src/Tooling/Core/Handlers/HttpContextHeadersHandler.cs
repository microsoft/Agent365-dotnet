// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Handlers
{
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Agents.Builder;

    internal class HttpContextHeadersHandler(ITurnContext turnContext) : DelegatingHandler
    {
        // Header names for passing context information in HTTP requests
        private const string ConversationIdHeader = "x-ms-conversation-id";
        private const string UserMessageHeader = "x-ms-usermessage";
        private const string O11ySpanIdHeader = "x-ms-span-id";
        private const string O11yTraceIdHeader = "x-ms-trace-id";

        // Keys set from Observability
        private const string O11ySpanIdKey = "O11ySpanId";
        private const string O11yTraceIdKey = "O11yTraceId";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (turnContext?.Activity != null)
            {
                if (!string.IsNullOrEmpty(turnContext.Activity.Conversation.Id))
                {
                    request.Headers.Add(ConversationIdHeader, turnContext.Activity.Conversation.Id);
                }

                if (!string.IsNullOrEmpty(turnContext.Activity.Text))
                {
                    request.Headers.Add(UserMessageHeader, turnContext.Activity.Text);
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

            return base.SendAsync(request, cancellationToken);
        }
    }
}