// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
// ❌ VIOLATION: Removed Microsoft.Kairo.Sdk.AgentsSdkExtensions reference to show violations

using OpenAI;                // ❌ VIOLATION: Direct OpenAI access
using OpenAI.Chat;           // ❌ VIOLATION: Direct ChatClient access
using OpenAIMultiturn.Agents;

namespace OpenAIMultiturn;

public class MyAgent : AgentApplication
{
    // ❌ VIOLATION A365OAI0011: Agent class with direct client fields
    private readonly OpenAIClient _openAIClient;
    private readonly ChatClient _chatClient;
    private Agent365Agent? _agent365Agent;

    // ❌ VIOLATION A365OAI0011: Agent constructor should use providers, not direct clients
    public MyAgent(AgentApplicationOptions options, IServiceProvider serviceProvider)
        : base(options)
    {
        Console.WriteLine("🚀 MyAgent constructor called - AnalyzerDemoApp starting...");
        
        // ❌ VIOLATIONS A365OAI0001/0002: Get direct clients from DI container instead of using IOpenAIClientProvider
        _openAIClient = serviceProvider.GetRequiredService<OpenAIClient>(); 
        _chatClient = serviceProvider.GetRequiredService<ChatClient>();
        
        Console.WriteLine("✅ Direct OpenAI clients retrieved (governance violations for demo)");
        
        // ❌ VIOLATION: Commented out due to missing extension - shows incomplete governance integration
        // this.OnAgentNotification("*", AgentNotificationActivityAsync);

        OnActivity(ActivityTypes.InstallationUpdate, OnHireMessageAsync);
        OnActivity(ActivityTypes.Message, MessageActivityAsync, rank: RouteRank.Last);
        
        Console.WriteLine("📝 Activity handlers registered for demo");
    }

    internal static bool IsApplicationInstalled { get; set; } = false;
    internal static bool TermsAndConditionsAccepted { get; set; } = false;

    protected async Task MessageActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        Console.WriteLine("📨 MessageActivityAsync called!");
        try
        {
            Console.WriteLine($"🔍 Received message: {turnContext.Activity.Text}");
            
            // Test basic response first
            if (turnContext.Activity.Text?.ToLower().Contains("hello") == true)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("✅ AnalyzerDemoApp Working!"), cancellationToken);
                return;
            }

            // Check if user is accepting terms and conditions
            if (turnContext.Activity.Text?.ToLower().Contains("accept") == true && 
                turnContext.Activity.Text?.ToLower().Contains("terms") == true)
            {
                TermsAndConditionsAccepted = true;
                await turnContext.SendActivityAsync(MessageFactory.Text("✅ Terms and conditions accepted! I can now help you with your work tasks."), cancellationToken);
                return;
            }

            // Check terms and conditions state first
            if (!TermsAndConditionsAccepted)
            {
                // ❌ VIOLATION: Using Agent365Agent with direct OpenAI clients (demonstrates governance violations)
                Console.WriteLine($"🤖 Using Agent365Agent with direct OpenAI clients (governance violation)...");
                
                try
                {
                    // ❌ VIOLATIONS A365OAI0001/0002: Create Agent365Agent with direct clients
                    _agent365Agent = new Agent365Agent(_openAIClient, _chatClient);
                    
                    // Use Agent365Agent to process the message
                    var chatHistory = new List<ChatMessage>();
                    var agentResponse = await _agent365Agent.InvokeAgentAsync(turnContext.Activity.Text ?? "Hello", chatHistory);
                    
                    Console.WriteLine($"✅ Agent365Agent Response: {agentResponse.Content}");
                    await turnContext.SendActivityAsync(MessageFactory.Text(agentResponse.Content ?? "I'm sorry, I couldn't process your request at the moment."), cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Agent365Agent Error: {ex.Message}");
                    // Fallback response when Agent365Agent fails
                    await turnContext.SendActivityAsync(MessageFactory.Text("You must accept the terms and conditions before I can help you. Please say 'I accept the terms and conditions' to continue."), cancellationToken);
                }
            }
            else
            {
                // ✅ USING Agent365Agent with weather functions for accepted users
                Console.WriteLine($"🌤️ Using Agent365Agent with weather functions (terms accepted)...");
                
                try
                {
                    // ❌ VIOLATIONS A365OAI0001/0002: Create Agent365Agent with direct clients
                    _agent365Agent = new Agent365Agent(_openAIClient, _chatClient);
                    
                    // Use Agent365Agent to process the message with weather functions available
                    var chatHistory = new List<ChatMessage>();
                    var agentResponse = await _agent365Agent.InvokeAgentAsync(turnContext.Activity.Text ?? "Hello", chatHistory);
                    
                    Console.WriteLine($"✅ Agent365Agent Response: {agentResponse.Content}");
                    await turnContext.SendActivityAsync(MessageFactory.Text(agentResponse.Content ?? "I'm sorry, I couldn't process your request at the moment."), cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Agent365Agent Error: {ex.Message}");
                    // Fallback response when Agent365Agent fails
                    await turnContext.SendActivityAsync(MessageFactory.Text($"I encountered an error: {ex.Message}"), cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Agent Error: {ex.Message}");
            await turnContext.SendActivityAsync(MessageFactory.Text($"I encountered an error: {ex.Message}"), cancellationToken);
        }
    }

    private async Task AgentNotificationActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        if (!IsApplicationInstalled)
        {
            await turnContext.SendActivityAsync(MessageFactory.Text("Please install the application before sending notifications."), cancellationToken);
            return;
        }

        if (!TermsAndConditionsAccepted)
        {
            _agent365Agent = new Agent365Agent(_openAIClient, _chatClient); // ❌ VIOLATIONS A365OAI0001/0002: Direct client usage
            var response = await _agent365Agent.InvokeAgentAsync(turnContext.Activity.Text, new List<ChatMessage>());
            await OutputResponseAsync(turnContext, turnState, response, cancellationToken);
            return;
        }

        _agent365Agent = new Agent365Agent(_openAIClient, _chatClient); // ❌ VIOLATIONS A365OAI0001/0002: Direct client usage
        switch (turnContext.Activity.ChannelId?.SubChannel)
        {
            case "email":
                await turnContext.StreamingResponse.QueueInformativeUpdateAsync($"Thanks for the email notification! Working on a response...");
                var emailNotificationEntity = turnContext.Activity.Entities.FirstOrDefault(entity => entity.Type == "emailnotification");
                if (emailNotificationEntity == null)
                {
                    turnContext.StreamingResponse.QueueTextChunk("I could not find the email notification details.");
                    await turnContext.StreamingResponse.EndStreamAsync(cancellationToken);
                    return;
                }

                var emailNotificationId = emailNotificationEntity.Properties["Id"].ToString();
                var emailNotificationConversationId = emailNotificationEntity.Properties["conversationId"].ToString();
                var emailNotificationConversationIndex = emailNotificationEntity.Properties["conversationIndex"].ToString();
                var emailNotificationChangeKey = emailNotificationEntity.Properties["changeKey"].ToString();
                var chatHistory = new List<ChatMessage>();
                var emailContent = await _agent365Agent.InvokeAgentAsync($"You have a new email from {turnContext.Activity.From?.Name} with id '{emailNotificationId}', ConversationId '{emailNotificationConversationId}', ConversationIndex '{emailNotificationConversationIndex}', and ChangeKey '{emailNotificationChangeKey}'. Please retrieve this message and return it in text format.", chatHistory);
                var response = await _agent365Agent.InvokeAgentAsync($"You have received the following email. Please follow any instructions in it. {emailContent.Content}", chatHistory);
                await OutputResponseAsync(turnContext, turnState, response, cancellationToken);
                return;
            case "word":
                await turnContext.StreamingResponse.QueueInformativeUpdateAsync($"Thanks for the Word notification! Working on a response...", cancellationToken);
                var wpxCommentEntity = turnContext.Activity.Entities.FirstOrDefault(entity => entity.Type == "wpxcomment");
                if (wpxCommentEntity == null)
                {
                    turnContext.StreamingResponse.QueueTextChunk("I could not find the Word notification details.");
                    await turnContext.StreamingResponse.EndStreamAsync(cancellationToken);
                    return;
                }

                var documentId = wpxCommentEntity.Properties["documentId"].ToString();
                var commentId = wpxCommentEntity.Properties["initiatingCommentId"].ToString();
                var driveId = "default";
                chatHistory = new List<ChatMessage>();
                var wordContent = await _agent365Agent.InvokeAgentAsync($"You have a new comment on the Word document with id '{documentId}', comment id '{commentId}', drive id '{driveId}'. Please retrieve the Word document as well as the comments in the Word document and return it in text format.", chatHistory);
                var commentToAgent = turnContext.Activity.Text;
                response = await _agent365Agent.InvokeAgentAsync($"You have received the following Word document content and comments. Please follow refer to these when responding to comment '{commentToAgent}'. {wordContent.Content}", chatHistory);
                await OutputResponseAsync(turnContext, turnState, response, cancellationToken);
                return;
        }

        throw new NotImplementedException();
    }

    protected async Task TeamsMessageActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // Start a Streaming Process 
        if (turnContext.StreamingResponse != null)
        {
            await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Working on a response for you", cancellationToken);
        } 

        List<ChatMessage> chatHistory = turnState.GetValue("conversation.chatHistory", () => new List<ChatMessage>());

        if (_agent365Agent == null)
        {
            await turnContext.SendActivityAsync(MessageFactory.Text("Agent is not initialized."), cancellationToken);
            return;
        }
        
        // Invoke the Agent365Agent to process the message
        Agent365AgentResponse response = await _agent365Agent.InvokeAgentAsync(turnContext.Activity?.Text ?? "", chatHistory);
        await OutputResponseAsync(turnContext, turnState, response, cancellationToken);
    }

    protected async Task OutputResponseAsync(ITurnContext turnContext, ITurnState turnState, Agent365AgentResponse response, CancellationToken cancellationToken)
    {
        if (response == null)
        {
            if (turnContext.StreamingResponse != null)
            {
                turnContext.StreamingResponse.QueueTextChunk("Sorry, I couldn't get an answer at the moment.");
                await turnContext.StreamingResponse.EndStreamAsync(cancellationToken);
            }
            return;
        }

        // Create a response message based on the response content type from the Agent365Agent
        // Send the response message back to the user. 
        if (turnContext.StreamingResponse != null)
        {
            switch (response.ContentType)
            {
                case Agent365AgentResponseContentType.Text:
                    turnContext.StreamingResponse.QueueTextChunk(response.Content!);
                    break;
                default:
                    break;
            }
            await turnContext.StreamingResponse.EndStreamAsync(cancellationToken); // End the streaming response
        }
    }

    protected async Task OnHireMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        Console.WriteLine("👋 OnHireMessageAsync called!");
        if (turnContext.Activity.Action == InstallationUpdateActionTypes.Add)
        {
            IsApplicationInstalled = true;
            TermsAndConditionsAccepted = false;
            await turnContext.SendActivityAsync(MessageFactory.Text("Thank you for hiring me! Looking forward to assist you in your professional journey! Before I begin, could you please confirm that you accept the terms and conditions?"), cancellationToken);
        }
        else if (turnContext.Activity.Action == InstallationUpdateActionTypes.Remove)
        {
            IsApplicationInstalled = false;
            TermsAndConditionsAccepted = false;
            await turnContext.SendActivityAsync(MessageFactory.Text("Thank you for your time, I enjoyed working with you."), cancellationToken);
        }
    }
}