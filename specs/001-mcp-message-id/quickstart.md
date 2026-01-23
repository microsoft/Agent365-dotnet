# Quickstart: MCP Request Message ID

**Feature**: 001-mcp-message-id
**Date**: 2026-01-23

## Overview

This feature automatically propagates the message ID from incoming user requests to all outbound MCP tool server requests. No code changes are required by SDK consumers - the feature is enabled automatically.

## How It Works

### Automatic Header Propagation

When your agent makes MCP tool calls, the SDK automatically includes the `x-ms-message-id` header:

```http
POST /tools/execute HTTP/1.1
Host: mcp-server.example.com
x-ms-message-id: abc-123-def-456
x-ms-conversation-id: conv-789
x-ms-channel-id: teams
Authorization: Bearer <token>
```

### Source of Message ID

The message ID is extracted from the incoming Activity:

```csharp
// The SDK does this automatically:
// turnContext.Activity.Id -> x-ms-message-id header
```

## Usage Scenarios

### Scenario 1: Debugging MCP Calls

When troubleshooting issues, use the message ID to find all related MCP calls:

```bash
# Search logs by message ID
grep "x-ms-message-id: abc-123-def" /var/log/mcp-server/*.log
```

### Scenario 2: Correlating Multiple Tool Calls

If your agent makes multiple tool calls for one user message, all calls share the same message ID:

```
User Message (Activity.Id: "msg-001")
├── MCP Call 1 (x-ms-message-id: msg-001)
├── MCP Call 2 (x-ms-message-id: msg-001)
└── MCP Call 3 (x-ms-message-id: msg-001)
```

### Scenario 3: Handling Missing Message ID

If the incoming Activity lacks a message ID (rare edge case), the SDK logs a warning and proceeds:

```
[Warning] Activity does not contain a message ID. MCP request will be sent without x-ms-message-id header.
```

The MCP request still executes successfully; it just won't have correlation information.

## Verification

### Check Headers in Logs

Enable HTTP logging to verify headers:

```csharp
var options = new ToolOptions
{
    EnableHttpLogging = true
};
```

### Expected Log Output

```
[Debug] Sending MCP request to https://mcp-server/tools
[Debug] Headers:
  x-ms-message-id: abc-123-def-456
  x-ms-conversation-id: conv-789
  x-ms-channel-id: teams
```

## No Configuration Required

This feature requires no configuration. It automatically:
- Extracts message ID from `turnContext.Activity.Id`
- Adds `x-ms-message-id` header to all MCP requests
- Logs a warning if message ID is missing
- Continues processing even without message ID

## Related Headers

The message ID is propagated alongside existing headers:

| Header | Purpose |
|--------|---------|
| `x-ms-message-id` | Correlate MCP calls to originating user message |
| `x-ms-conversation-id` | Correlate to conversation |
| `x-ms-channel-id` | Identify source channel (teams, email, etc.) |
| `x-ms-trace-id` | Distributed tracing correlation |
| `x-ms-span-id` | Distributed tracing span |
