# Microsoft.Agents.A365.Tooling.LocalMcp

This package provides WNS-based communication infrastructure for connecting cloud agents to local Windows desktop MCP servers.

## Overview

The Local MCP Proxy enables agents running in the cloud to invoke MCP tools on a user's Windows desktop. It uses Windows Push Notification Service (WNS) to wake up the desktop client and establish a WebSocket connection for MCP communication.

## Quick Start

### 1. Add the NuGet Package

```bash
dotnet add package Microsoft.Agents.A365.Tooling.LocalMcp
```

### 2. Configure Services (Program.cs)

```csharp
using Microsoft.Agents.A365.Tooling.LocalMcp.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add Local MCP Proxy services
builder.Services.AddLocalMcpProxy(builder.Configuration);

// ... other service registrations

var app = builder.Build();

// Use Local MCP Proxy (enables WebSockets and maps endpoints)
app.UseLocalMcpProxy();

// ... other middleware

app.Run();
```

### 3. Add Configuration (appsettings.json)

```json
{
  "WnsConfiguration": {
    "TenantId": "your-azure-ad-tenant-id",
    "ClientId": "your-wns-app-client-id",
    "ClientSecret": "your-wns-app-client-secret"
  },
  "LocalMcpProxy": {
    "IdleTimeoutSeconds": 120,
    "PendingSessionTimeoutMinutes": 5,
    "McpRequestTimeoutSeconds": 120
  }
}
```

## Endpoints Exposed

The following endpoints are automatically mapped when you call `UseLocalMcpProxy()`:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/channels/register` | POST | Desktop client registration |
| `/api/channels` | GET | List registered clients |
| `/api/notify/{clientName}` | POST | Send WNS notification |
| `/api/status/{sessionId}` | GET | Check session status |
| `/api/heartbeat/{sessionId}` | POST | Keep session alive |
| `/api/mcp/{sessionId}` | POST | HTTP proxy for MCP requests |
| `/ws/mcp/{sessionId}` | WS | WebSocket endpoint for desktop connection |
| `/api/discovery/{requestId}/servers` | POST | Receive discovery results |
| `/api/discovery/{requestId}/servers` | GET | Poll discovery results |

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Cloud Agent                                 │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │              Microsoft.Agents.A365.Tooling.LocalMcp         │   │
│  │  ┌───────────────┐  ┌──────────────────┐  ┌──────────────┐  │   │
│  │  │ WNS Service   │  │ Session Manager  │  │  Endpoints   │  │   │
│  │  │ (sends push)  │  │ (tracks clients) │  │  (7 routes)  │  │   │
│  │  └───────┬───────┘  └────────┬─────────┘  └──────┬───────┘  │   │
│  └──────────┼───────────────────┼───────────────────┼──────────┘   │
└─────────────┼───────────────────┼───────────────────┼──────────────┘
              │                   │                   │
              │ WNS Push          │ WebSocket         │ HTTP
              ▼                   ▼                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     Desktop Client (LocaProto)                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐  │
│  │ WNS Receiver │  │ WS Client    │  │ Local MCP Server (ODR)   │  │
│  └──────────────┘  └──────────────┘  └──────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

## Session Storage

By default, sessions are stored in memory using `InMemorySessionManager`. This is suitable for single-instance deployments.

For horizontal scaling, implement `ISessionManager` with a distributed store (Redis, Cosmos DB, etc.):

```csharp
services.AddSingleton<ISessionManager, RedisSessionManager>();
```

## Configuration Options

### WnsConfiguration

| Property | Description |
|----------|-------------|
| `TenantId` | Azure AD tenant ID |
| `ClientId` | Azure AD application (client) ID for WNS |
| `ClientSecret` | Azure AD client secret for WNS |

### LocalMcpProxyOptions

| Property | Default | Description |
|----------|---------|-------------|
| `IdleTimeoutSeconds` | 120 | Timeout for idle sessions |
| `PendingSessionTimeoutMinutes` | 5 | Timeout for pending (not connected) sessions |
| `CleanupIntervalSeconds` | 10 | How often to run cleanup task |
| `McpRequestTimeoutSeconds` | 120 | Timeout for MCP requests |
| `DefaultServerId` | file-mcp-server | Default MCP server ID |

## Requirements

- .NET 8.0+
- Azure AD App Registration configured for WNS
- WebSockets enabled on hosting environment
