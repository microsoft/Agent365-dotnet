# Research: MCP Request Message ID

**Feature**: 001-mcp-message-id
**Date**: 2026-01-23

## Research Questions

### 1. Where is the message ID located in the turn context?

**Decision**: Use `turnContext.Activity.Id`

**Rationale**:
- The Activity.Id property is the canonical identifier for the incoming message
- Already used in `McpToolServerConfigurationService.cs:117` for similar correlation purposes
- Part of the Microsoft.Agents.Builder SDK contract

**Alternatives Considered**:
- `turnContext.Activity.ReplyToId` - Rejected: This refers to the message being replied to, not the current message
- Custom property in `StackState` - Rejected: Activity.Id is the standard location

### 2. What HTTP header name should be used?

**Decision**: `x-ms-message-id`

**Rationale**:
- Specified by user input
- Follows Microsoft header naming convention (`x-ms-*`)
- Consistent with other headers in the SDK (`x-ms-conversation-id`, `x-ms-channel-id`, etc.)

**Alternatives Considered**:
- `X-Message-Id` - Rejected: Not Microsoft-branded
- `X-Request-Id` - Rejected: Could be confused with HTTP request correlation, not message correlation
- `X-Correlation-Id` - Rejected: Already exists for different purpose; message ID is complementary

### 3. What is the existing pattern for adding headers?

**Decision**: Extend `HttpContextHeadersHandler.SendAsync()` method

**Rationale**:
- Established pattern already used for 6+ headers (conversation ID, channel ID, subchannel ID, user message, span ID, trace ID)
- Handler has access to `ITurnContext` with the Activity
- Handler has access to `ILogger` for warning logging
- No architectural changes needed

**Code Pattern** (from existing implementation):
```csharp
if (!string.IsNullOrEmpty(turnContext.Activity.Conversation.Id))
{
    request.Headers.Add(ConversationIdHeader, turnContext.Activity.Conversation.Id);
}
```

### 4. How should missing message ID be handled?

**Decision**: Log warning and proceed without header

**Rationale**:
- Specified in clarification: "log a warning and then proceed without including a message id"
- Follows existing pattern - other headers are conditionally added only if value is present
- Non-blocking: MCP requests should still succeed even without correlation header

**Implementation**:
```csharp
if (!string.IsNullOrEmpty(turnContext.Activity?.Id))
{
    request.Headers.Add(MessageIdHeader, turnContext.Activity.Id);
}
else
{
    logger.LogWarning("Activity does not contain a message ID. MCP request will be sent without x-ms-message-id header.");
}
```

### 5. What about observability integration?

**Decision**: Message ID propagation through header is sufficient for initial implementation

**Rationale**:
- MCP requests already propagate `x-ms-span-id` and `x-ms-trace-id` for distributed tracing
- Message ID in header allows downstream services to correlate
- OpenTelemetry baggage propagation (if needed later) would be a separate enhancement

**Future Enhancement** (out of scope for this feature):
- Could add message ID to OpenTelemetry span tags for richer correlation

## Technical Findings

### Existing Header Constants (HttpContextHeadersHandler.cs)

| Constant | Header Name | Source |
|----------|-------------|--------|
| ConversationIdHeader | x-ms-conversation-id | Activity.Conversation.Id |
| ChannelIdHeader | x-ms-channel-id | Activity.ChannelId.Channel |
| SubChannelIdHeader | x-ms-subchannel-id | Activity.ChannelId.SubChannel |
| UserMessageHeader | x-ms-usermessage | Activity.Text (sanitized) |
| O11ySpanIdHeader | x-ms-span-id | StackState["O11ySpanId"] |
| O11yTraceIdHeader | x-ms-trace-id | StackState["O11yTraceId"] |
| **MessageIdHeader** (NEW) | **x-ms-message-id** | **Activity.Id** |

### Existing Test Patterns

The Tooling.Tests project contains tests for the handler. New tests should follow the same pattern:
- Mock `ITurnContext` with Activity
- Verify header is added when Activity.Id is present
- Verify warning is logged when Activity.Id is missing
- Verify request proceeds in both cases

## Resolution Summary

All technical questions resolved. No NEEDS CLARIFICATION items remain.

| Question | Resolution |
|----------|------------|
| Message ID source | `turnContext.Activity.Id` |
| Header name | `x-ms-message-id` |
| Implementation location | `HttpContextHeadersHandler.SendAsync()` |
| Missing ID handling | Log warning, proceed without header |
| Observability | Header propagation sufficient; span tags deferred |
