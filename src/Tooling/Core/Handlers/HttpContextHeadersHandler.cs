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
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (turnContext?.Activity != null)
            {
                if (!string.IsNullOrEmpty(turnContext.Activity.Conversation.Id))
                {
                    request.Headers.Add("x-ms-conversation-id", turnContext.Activity.Conversation.Id);
                }

                if (!string.IsNullOrEmpty(turnContext.Activity.Text))
                {
                    request.Headers.Add("x-ms-usermessage", turnContext.Activity.Text);
                }
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}