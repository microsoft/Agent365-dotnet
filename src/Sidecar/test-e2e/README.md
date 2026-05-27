# Agent365 Sidecar — End-to-End Testing

## Quick Start (3 terminals)

### Terminal 1: Start mock customer webhook
```bash
python src/Sidecar/test-e2e/mock_webhook.py
```
This listens on `http://127.0.0.1:8080` and prints anything the sidecar forwards.

### Terminal 2: Start the sidecar

#### With Real Auth (Blueprint credentials)
```powershell
cd src/Sidecar/Microsoft.Agents.A365.Sidecar

# Agent instance ID (assigned when blueprint is provisioned)
$env:A365_AGENT_ID = "<your-agent-instance-id>"

# Blueprint Entra app credentials (client-credentials mode)
# Auth.ClientId doubles as the blueprint ID; Auth.TenantId is the tenant ID
$env:A365_AUTH_MODE = "client-credentials"
$env:A365_AUTH__ClientId = "<blueprint-entra-app-client-id>"
$env:A365_AUTH__ClientSecret = "<blueprint-entra-app-client-secret>"
$env:A365_AUTH__TenantId = "<entra-tenant-id>"

# Tooling gateway (V2)
$env:A365_TOOLING_GATEWAY_ENDPOINT = "https://<tooling-gateway-host>"
$env:A365_TOOLING__GatewayScope = "<tooling-gateway-scope>"  # e.g., api://<gateway-app-id>/.default

# Customer webhook
$env:A365_CUSTOMER_WEBHOOK = "http://127.0.0.1:8080/agent/turn"

# Bot Framework auth (for Activity Protocol messaging)
# Remove the skip flag to enable real bot registration
# $env:A365_SKIP_AGENT_REGISTRATION = "true"

$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

#### With Managed Identity (Azure hosted)
```powershell
$env:A365_AUTH_MODE = "managed-identity"
$env:A365_AUTH__ClientId = "<user-assigned-MI-client-id>"  # omit for system-assigned
# ... rest same as above
```

#### Without Real Auth (local dev only)
```powershell
$env:A365_SKIP_AGENT_REGISTRATION = "true"
$env:A365_AGENT_ID = "test-agent-123"
$env:A365_AUTH__TenantId = "test-tenant-456"
$env:A365_AUTH__ClientId = "test-blueprint-789"
$env:A365_CUSTOMER_WEBHOOK = "http://127.0.0.1:8080/agent/turn"
$env:A365_TOOLING_GATEWAY_ENDPOINT = "http://localhost:9999/tooling"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

### Terminal 3: Run the test client
```bash
pip install requests  # if not already installed
python src/Sidecar/test-e2e/test_sidecar.py
```

## Auth Architecture

The sidecar acquires tokens using the **blueprint's Entra app registration**:

```
┌─────────────────────┐
│   SidecarTokenProvider  │──── Observability API token (scope: Agent365.Observability.OtelWrite)
│   (Azure.Identity)      │──── Tooling Gateway token (scope: gateway/.default)
└─────────────────────┘
          │
          ▼
┌─────────────────────┐
│ SidecarMcpTokenProvider │──── Per-server tokens (V2: server.audience/.default)
│   (IMcpTokenProvider)   │     Each MCP tool server has its own audience
└─────────────────────┘
```

**Auth modes:**
| Mode | Credential | Use case |
|------|-----------|----------|
| `client-credentials` | ClientSecretCredential | Blueprint app + secret |
| `managed-identity` | ManagedIdentityCredential | Azure-hosted with MI |
| `default` | DefaultAzureCredential | Developer machine (az login) |

## Tooling V2

The sidecar uses **V2 tooling** which means:
- Gateway endpoint: `/agents/v2/{agentInstanceId}/mcpServers`
- Each tool server has its own `audience` and `scope`
- Tokens are acquired per-server via `SidecarMcpTokenProvider`

```
Customer App ──POST──▶ Sidecar /api/v1/tools/enumerate
                              │
                              ▼
                     Tooling Gateway (V2)
                     /agents/v2/{id}/mcpServers
                              │
                              ▼
                    ┌──────────────────┐
                    │ MCP Server A     │◀── Token for audience A
                    │ MCP Server B     │◀── Token for audience B
                    └──────────────────┘
```

## What Gets Tested

| Endpoint | What it proves |
|----------|----------------|
| `GET /healthz` | Sidecar is alive |
| `GET /readyz` | Config is valid |
| `GET /api/v1/status` | All modules registered |
| `GET /api/v1/observability/config` | OTLP config returned correctly |
| `POST /api/v1/observability/v1/traces` | OTLP receiver accepts/rejects payloads |
| `GET /api/v1/tools` | V2 API documentation |
| `GET /api/v1/tools/servers` | V2 tool server discovery (needs real gateway) |
| `POST /api/v1/tools/enumerate` | Full V2 tool enumeration with per-audience tokens |
| `POST /api/v1/tools/servers/{id}/tools/{name}/invoke` | Direct tool invocation via MCP |
| `GET /api/v1/notifications/channels` | Channel listing |
| `GET /api/v1/notifications/status` | Notification status |

## Notes

- `A365_SKIP_AGENT_REGISTRATION=true` skips Bot Framework auth (for local testing without Azure creds)
- Trace forwarding to real A365 Observability API requires valid blueprint credentials
- Tool enumeration/invocation requires a reachable Tooling Gateway
- The sidecar auto-discovers blueprint ID from JWT claims (`xms_par_app_azp` → `appid` → `azp`) when processing incoming Activities
