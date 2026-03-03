// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.A365.Observability.Hosting.Extensions;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.A365.Observability.Hosting.Middleware
{
    /// <summary>
    /// Bot Framework middleware that propagates OpenTelemetry baggage context
    /// derived from <see cref="ITurnContext"/>.
    /// </summary>
    /// <remarks>
    /// Async replies (ContinueConversation events) are passed through without
    /// baggage setup because their context is established by the originating turn.
    /// </remarks>
    public sealed class BaggageTurnMiddleware : IMiddleware
    {
        /// <inheritdoc/>
        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
        {
            var activity = turnContext.Activity;
            bool isAsyncReply = activity != null
                && activity.Type == ActivityTypes.Event
                && activity.Name == ActivityEventNames.ContinueConversation;

            if (isAsyncReply)
            {
                await next(cancellationToken).ConfigureAwait(false);
                return;
            }

            var builder = new BaggageBuilder();
            builder.FromTurnContext(turnContext);

            using (builder.Build())
            {
                await next(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
