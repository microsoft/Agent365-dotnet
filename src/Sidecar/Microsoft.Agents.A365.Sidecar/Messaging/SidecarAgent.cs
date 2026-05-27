// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.A365.Sidecar.Messaging;

/// <summary>
/// The sidecar's agent application. Receives all activities from M365 channels,
/// translates them to the simplified webhook payload, and delivers to the customer's endpoint.
/// </summary>
public class SidecarAgent : AgentApplication
{
    private readonly TurnManager _turnManager;
    private readonly StreamingHandler _streamingHandler;
    private readonly ILogger<SidecarAgent> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="SidecarAgent"/>.
    /// </summary>
    public SidecarAgent(
        AgentApplicationOptions options,
        TurnManager turnManager,
        StreamingHandler streamingHandler,
        ILogger<SidecarAgent> logger) : base(options)
    {
        _turnManager = turnManager;
        _streamingHandler = streamingHandler;
        _logger = logger;

        // Register catch-all handlers for all activity types
        OnActivity(ActivityTypes.Message, HandleActivityAsync, rank: RouteRank.Last);
        OnActivity(ActivityTypes.Event, HandleActivityAsync, rank: RouteRank.Last);
        OnActivity(ActivityTypes.Invoke, HandleActivityAsync, rank: RouteRank.Last);
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, HandleActivityAsync);
        OnConversationUpdate(ConversationUpdateEvents.MembersRemoved, HandleActivityAsync);
    }

    private async Task HandleActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var turnId = _turnManager.RegisterTurn(turnContext);

        try
        {
            // Translate the activity to our simplified payload
            var payload = _turnManager.TranslateActivity(turnId, turnContext.Activity);

            _logger.LogInformation("Processing turn {TurnId} type={Type} from={From} conversation={ConversationId}",
                turnId, payload.Type, payload.From.Id, payload.ConversationId);

            // Deliver to customer's webhook
            var result = await _turnManager.DeliverToCustomerAsync(payload, cancellationToken);

            switch (result.ResultType)
            {
                case WebhookResultType.JsonResponse:
                    if (result.Response != null)
                    {
                        await SendJsonResponseAsync(turnContext, result.Response, cancellationToken);
                    }
                    break;

                case WebhookResultType.StreamingResponse:
                    if (result.Stream != null)
                    {
                        await using (result.Stream)
                        {
                            await _streamingHandler.ProcessStreamAsync(result.Stream, turnContext, cancellationToken);
                        }
                    }
                    break;

                case WebhookResultType.NoReply:
                    // Customer will use the outbound turns API to reply
                    _logger.LogDebug("Turn {TurnId}: customer will reply via outbound API", turnId);
                    break;

                case WebhookResultType.Failed:
                    _logger.LogError("Turn {TurnId}: webhook delivery failed: {Error}", turnId, result.ErrorMessage);
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text("Sorry, I'm having trouble processing your request."),
                        cancellationToken);
                    break;
            }
        }
        finally
        {
            _turnManager.CompleteTurn(turnId);
        }
    }

    private static async Task SendJsonResponseAsync(
        ITurnContext turnContext,
        TurnResponse response,
        CancellationToken cancellationToken)
    {
        var activity = MessageFactory.Text(response.Text ?? string.Empty);

        if (!string.IsNullOrEmpty(response.TextFormat))
        {
            activity.TextFormat = response.TextFormat;
        }

        if (response.Attachments?.Count > 0)
        {
            activity.Attachments = response.Attachments.Select(a => new Attachment
            {
                ContentType = a.ContentType,
                ContentUrl = a.ContentUrl,
                Content = a.Content,
                Name = a.Name,
            }).ToList();
        }

        if (response.SuggestedActions?.Count > 0)
        {
            activity.SuggestedActions = new SuggestedActions
            {
                Actions = response.SuggestedActions.Select(text => new CardAction
                {
                    Type = ActionTypes.ImBack,
                    Title = text,
                    Value = text,
                }).ToList(),
            };
        }

        await turnContext.SendActivityAsync(activity, cancellationToken);
    }
}
