# PRD: x-ms-agentId Header for MCP Platform Calls

## Overview

Add an `x-ms-agentId` header to all outbound HTTP requests from the tooling package to the MCP platform. This header identifies the calling agent using the best available identifier.

## Problem Statement

The MCP platform needs to identify which agent is making tooling requests for:
- Logging and diagnostics
- Usage analytics

Currently, no consistent agent identifier is sent with MCP platform requests.

## Requirements

### Functional Requirements

1. All HTTP requests to the MCP platform SHALL include the `x-ms-agentId` header
2. The header value SHALL be determined using the following priority:
   1. **Agent Blueprint ID from TurnContext** (highest priority) - from `TurnContext.Activity.From.AgenticAppBlueprintId`
   2. **Agent Blueprint ID from token** (`xms_par_app_azp` claim)
   3. **Entra Application ID from token** (`appid` or `azp` claim)
   4. **Application name** (lowest priority fallback) - from assembly name
3. If no identifier is available, the header SHOULD be omitted (not sent with empty value)

### Non-Functional Requirements

1. No additional network calls to retrieve identifiers
2. Minimal performance impact on existing flows
3. Backward compatible - existing integrations continue to work

## Technical Design

### Affected Components

| Package | File | Change |
|---------|------|--------|
| `Microsoft.Agents.A365.Runtime` | `Utility.cs` | Add `GetAgentIdFromToken()` method (checks `xms_par_app_azp` → `appid` → `azp`) |
| `Microsoft.Agents.A365.Runtime` | `Utility.cs` | Add `GetApplicationName()` method (returns assembly name) |
| `Microsoft.Agents.A365.Tooling` | `Constants.cs` | Add `AgentIdHeader` constant |
| `Microsoft.Agents.A365.Tooling` | `HttpContextHeadersHandler.cs` | Add `x-ms-agentid` header with priority fallback |

### Identifier Retrieval Strategy

#### 1. Agent Blueprint ID from TurnContext (Highest Priority)

**Source**: `TurnContext.Activity.From.Properties["agenticAppBlueprintId"]`

**Availability**: Only available in agentic request scenarios where a `TurnContext` is present and the request originates from another agent.

**Format**: GUID (e.g., `12345678-1234-1234-1234-123456789abc`)

---

#### 2 & 3. Agent ID from Token (Second/Third Priority)

**Sources** (checked in order):
1. `xms_par_app_azp` claim - Agent Blueprint ID (parent application's Azure app ID)
2. `appid` or `azp` claim - Entra Application ID

**Availability**: Available when an `authToken` is provided to the tooling methods.

**Retrieval**: New utility function that decodes token once and checks claims in priority order:

```csharp
// Microsoft.Agents.A365.Runtime.Utils.Utility
public static string GetAgentIdFromToken(string token)
{
    if (string.IsNullOrWhiteSpace(token))
    {
        return string.Empty;
    }

    try
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        
        // Priority: xms_par_app_azp (blueprint ID) > appid > azp
        var blueprintClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "xms_par_app_azp");
        if (!string.IsNullOrEmpty(blueprintClaim?.Value))
        {
            return blueprintClaim.Value;
        }

        var appIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "appid");
        if (!string.IsNullOrEmpty(appIdClaim?.Value))
        {
            return appIdClaim.Value;
        }

        var azpClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "azp");
        return azpClaim?.Value ?? string.Empty;
    }
    catch
    {
        return string.Empty;
    }
}
```

**Format**: GUID (e.g., `12345678-1234-1234-1234-123456789abc`)

---

#### 4. Application Name (Lowest Priority Fallback)

**Source**: Entry assembly name

**Strategy**:
1. Get the entry assembly name via `Assembly.GetEntryAssembly()?.GetName()?.Name`
2. If not available, omit the header

**Implementation**:
```csharp
// Microsoft.Agents.A365.Runtime.Utils.Utility
public static string? GetApplicationName()
{
    return Assembly.GetEntryAssembly()?.GetName()?.Name;
}
```

---

### Implementation

#### Updated HttpContextHeadersHandler

The `x-ms-agentid` header will be added in the `HttpContextHeadersHandler` which already handles other context headers. The header is only added when the auth token is available (via the `BearerTokenHandler` in the chain).

```csharp
// Microsoft.Agents.A365.Tooling.Handlers.HttpContextHeadersHandler
internal class HttpContextHeadersHandler : DelegatingHandler
{
    private const string AgentIdHeader = "x-ms-agentid";
    // ... existing headers ...

    private readonly string? authToken;

    public HttpContextHeadersHandler(ITurnContext turnContext, ILogger logger, ToolOptions toolOptions, string? authToken = null)
    {
        this.turnContext = turnContext;
        this.logger = logger;
        this.toolOptions = toolOptions;
        this.authToken = authToken;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // ... existing header logic ...

        // Add x-ms-agentid header if auth token is available
        if (!string.IsNullOrEmpty(authToken))
        {
            var agentId = ResolveAgentIdForHeader();
            if (!string.IsNullOrEmpty(agentId))
            {
                request.Headers.Add(AgentIdHeader, agentId);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }

    private string? ResolveAgentIdForHeader()
    {
        // Priority 1: Agent Blueprint ID from TurnContext
        var blueprintId = GetAgenticAppBlueprintIdFromContext();
        if (!string.IsNullOrEmpty(blueprintId))
        {
            return blueprintId;
        }

        // Priority 2 & 3: Agent ID from token (xms_par_app_azp > appid > azp)
        if (!string.IsNullOrEmpty(authToken))
        {
            var agentId = RuntimeUtility.GetAgentIdFromToken(authToken);
            if (!string.IsNullOrEmpty(agentId))
            {
                return agentId;
            }
        }

        // Priority 4: Application name from assembly
        return RuntimeUtility.GetApplicationName();
    }
}
```

### Call Sites Summary

| Call Site | authToken | turnContext | Gets `x-ms-agentid`? |
|-----------|-----------|-------------|----------------------|
| `GetMCPServerFromToolingGatewayAsync()` | ✅ | ❌ (not passed) | ✅ Yes (via new overload) |
| `GetMcpClientToolsAsync()` | ✅ | ✅ | ✅ Yes |
| `SendChatHistoryAsync()` | ❌ | ✅ | ❌ No (authToken required) |

**Note**: The `x-ms-agentid` header is only added when `authToken` is present. `SendChatHistoryAsync()` does not use authentication, so it won't include this header.

---

## Open Questions

### Q1: Application Name Strategy ✅ RESOLVED

**Decision**: Use `Assembly.GetEntryAssembly()?.GetName()?.Name` as the .NET equivalent of the Node.js npm_package_name.

### Q2: Header Name Casing ✅ RESOLVED

**Decision**: Use `x-ms-agentid` (all lowercase, case insensitive).

HTTP headers are case-insensitive per RFC 7230, so the server will accept any casing. Using lowercase is the conventional choice.

### Q3: Missing Identifier Behavior ✅ RESOLVED

**Decision**: Omit the header entirely if no identifier is available. Do not send empty or "unknown" values.

---

## Testing Strategy

### Unit Tests

1. Test `GetAgentIdFromToken()` checks claims in correct priority order (`xms_par_app_azp` > `appid` > `azp`)
2. Test `GetAgentIdFromToken()` returns empty string for empty/invalid tokens
3. Test `GetApplicationName()` returns assembly name
4. Test `HttpContextHeadersHandler` includes `x-ms-agentid` when identifier available
5. Test `HttpContextHeadersHandler` omits header when no identifier available
6. Test priority order: TurnContext > token claims > application name

### Integration Tests

1. Verify header is sent in `GetMcpClientToolsAsync()` requests
2. Verify header is NOT sent in `SendChatHistoryAsync()` requests (no authToken)

---

## Breaking Changes

**None** - This implementation is fully backward compatible.

### Migration Guide

**For existing consumers:**
- No changes required - existing code continues to work
- The `x-ms-agentid` header will automatically be included in requests where authentication is used

---

## Rollout Plan

1. **Phase 1**: Add utility methods to Runtime package
2. **Phase 2**: Update `HttpContextHeadersHandler` to add `x-ms-agentid` header
3. **Phase 3**: Update documentation

---

## Dependencies

- Runtime package for `GetAppIdFromToken()` utility (already exists)
- `System.IdentityModel.Tokens.Jwt` for JWT decoding (already referenced)
- No new external dependencies required

---

## Success Metrics

1. 100% of MCP platform requests include `x-ms-agentId` header (when identifier available)
2. No increase in request latency
3. No breaking changes for existing consumers
