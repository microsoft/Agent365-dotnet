# Data Model: MCP Request Message ID

**Feature**: 001-mcp-message-id
**Date**: 2026-01-23

## Overview

This feature does not introduce new entities or data models. It propagates an existing identifier (Activity.Id) through HTTP headers to MCP tool servers.

## Existing Entities (No Changes)

### Activity

**Source**: Microsoft.Agents.Builder SDK

The Activity is the incoming request to the agent application. No modifications needed.

| Property | Type | Description | Used By This Feature |
|----------|------|-------------|---------------------|
| Id | string | Unique identifier for this message/activity | ✅ YES - Propagated as `x-ms-message-id` header |
| Conversation.Id | string | Conversation identifier | Existing header propagation |
| ChannelId | ChannelId | Channel information | Existing header propagation |
| Text | string | User message text | Existing header propagation |

### HTTP Headers (Extension)

**Affected File**: `HttpContextHeadersHandler.cs`

New constant added to existing header constants:

| Constant | Header Name | Value Source | Required |
|----------|-------------|--------------|----------|
| MessageIdHeader | `x-ms-message-id` | `turnContext.Activity.Id` | No (optional - logs warning if missing) |

## Data Flow

```
┌─────────────────────────────────────┐
│ Incoming User Request               │
│ Activity { Id: "abc-123-def" }      │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│ Agent Application                   │
│ ITurnContext with Activity          │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│ HttpContextHeadersHandler           │
│ Extracts Activity.Id                │
│ Adds: x-ms-message-id: abc-123-def  │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│ MCP Tool Server Request             │
│ Headers:                            │
│   x-ms-message-id: abc-123-def      │
│   x-ms-conversation-id: conv-456    │
│   x-ms-channel-id: teams            │
│   ...                               │
└─────────────────────────────────────┘
```

## Validation Rules

| Rule | Description |
|------|-------------|
| Nullable | Activity.Id may be null or empty; this is handled gracefully |
| Format | No format validation performed; passed through as-is |
| Length | No length restrictions; standard HTTP header length limits apply |

## State Transitions

N/A - No stateful entities introduced. Header is added per-request based on current Activity.
