// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Tooling.Models;

/// <summary>
/// Represents the agent identity context that is passed to MCP servers in the
/// <c>_meta</c> field of JSON-RPC requests. This allows MCP servers to identify
/// the calling agent and its context (tenant, user, conversation, etc.).
/// </summary>
public class AgentIdentityContext
{
    /// <summary>
    /// Gets or sets the agent instance ID (the agentic identity for agentic requests,
    /// or the app ID from the auth token for non-agentic requests).
    /// </summary>
    [JsonPropertyName("agentInstanceId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AgentInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the agent blueprint application ID (the Azure AD Client ID of the agent app).
    /// This is the <c>Recipient.Properties["agenticAppId"]</c> or the app registered for the bot.
    /// </summary>
    [JsonPropertyName("agentAppId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AgentAppId { get; set; }

    /// <summary>
    /// Gets or sets the tenant ID of the organization where the agent operates.
    /// </summary>
    [JsonPropertyName("tenantId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the AAD object ID of the calling user.
    /// </summary>
    [JsonPropertyName("userAadObjectId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserAadObjectId { get; set; }

    /// <summary>
    /// Gets or sets the display name or email of the calling user.
    /// </summary>
    [JsonPropertyName("userName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the conversation ID.
    /// </summary>
    [JsonPropertyName("conversationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the channel ID (e.g., "msteams", "webchat").
    /// </summary>
    [JsonPropertyName("channelId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChannelId { get; set; }
}
