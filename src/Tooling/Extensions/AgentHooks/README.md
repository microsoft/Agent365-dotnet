# Microsoft.Agents.A365.Tooling.Extensions.AgentHooks

Microsoft Agent 365 real-time protection on the [agent-hooks](https://github.com/responsibleai/agent-hooks)
control contract (AGENT-HOOKS-0.1), using the .NET package
[`ResponsibleAI.AgentHooks`](https://www.nuget.org/packages/ResponsibleAI.AgentHooks).

`A365DefenderInterceptor` is an agent-hooks interceptor for Microsoft Defender for AI. Each context the host
emits at the four points Defender evaluates is sent to the prevention endpoint
(`POST .../v1/protection/evaluate`) as-is, keeping its session, sequence and tool call ids. Defender's verdict
decides:

| agent-hooks point | When | On `deny` |
|---|---|---|
| `input` | the user's message, before the agent runs | the agent does not run |
| `pre_tool_call` | a tool call, before it runs | the tool does not run |
| `post_tool_call` | a tool result, before the agent uses it | the result is withheld |
| `output` | the reply, before it is sent | the reply is replaced |

Other points (`agent_startup`, model calls, `agent_shutdown`) are allowed without a call.

## Authentication

Calls carry the **agent identity's own app-only token** in the agent's tenant, for the Defender API
(`api://86a21212-634e-4553-b3d6-e477e4c9d9ec`, role `RealtimeProtection.Evaluate.All`). This is the same
authority as Observability S2S export: `DefenderRtpTokenResolvers.FromAgenticConnection` asks the agent's
connection (`IAgenticTokenProvider`) for the agent identity's assertion and exchanges it. `a365 setup all` grants
the role to the blueprint as an inheritable permission, so every agent identity under it inherits it
([microsoft/Agent365-devTools#485](https://github.com/microsoft/Agent365-devTools/pull/485)). Defender also
requires the agent's tenant to be onboarded to Microsoft Defender for AI; otherwise it answers `403`, which
follows the fail mode.

## Usage

```csharp
using AgentHooks;
using Microsoft.Agents.A365.Tooling.Extensions.AgentHooks;
using Microsoft.Agents.A365.Tooling.Protection.Defender;

var defender = new DefenderRtpClient(DefenderRtpOptions.FromEnvironment(), httpClient);
var tokens = DefenderRtpTokenResolvers.FromAgenticConnection(
    (IAgenticTokenProvider)connections.GetDefaultConnection(), httpClient);

var emitter = A365AgentHooks.CreateProtectionEmitter(defender: defender.Options)
    .AddA365Defender(new A365DefenderInterceptor(
        defender,
        context => new A365DefenderCall(
            new DefenderRtpAgentContext
            {
                AgentId = turnContext.Activity.Recipient.AgenticAppId, // the agent identity
                TenantId = turnContext.Activity.Recipient.TenantId,     // the agent's tenant
                RequestId = turnContext.Activity.Id,
                UserId = turnContext.Activity.From.AadObjectId,
            },
            tokens),
        result => logger.LogInformation(
            "Defender {Point} allowed={Allowed} evaluated={Evaluated} cid={CorrelationId}",
            result.InterceptionPoint, result.Allowed, result.Evaluated, result.CorrelationId)));

var builder = new AgentContextBuilder(agentId, "agent-framework", sessionId, agentName);
var record = await emitter.EmitUncheckedAsync(builder.Input(userMessage), cancellationToken);
if (!record.Proceeds) { /* blocked: record.Verdict.Message */ }
```

Agents built on Microsoft Agent Framework can register the same interceptor with
`Microsoft.Agents.AI.AgentHooks`, which mediates model and tool calls.

## Configuration

| Variable | Meaning |
|---|---|
| `ENABLE_A365_DEFENDER_RTP` | `true` to call Defender |
| `A365_DEFENDER_RTP_ENDPOINT` | the prevention endpoint, `https://<host>/v1/protection/evaluate` |
| `A365_DEFENDER_RTP_FAIL_MODE` | `closed` blocks when no verdict is obtained; default is open |
| `A365_DEFENDER_RTP_TIMEOUT_MILLISECONDS` | per-call timeout (default 10000) |
| `A365_DEFENDER_RTP_AUTHENTICATION_SCOPE` | overrides the Defender API scope |
| `A365_DEFENDER_RTP_MAX_CONTENT_CHARACTERS` | clamps each string sent (default 20000) |

Every call sends a unique `x-ms-correlation-id`, returned as `DefenderRtpEvaluationResult.CorrelationId`;
Defender logs each evaluation under it. A `400` reports the failed validation rule in `Error`.

`ResponsibleAI.AgentHooks` is a prerelease package with a native core (`agent_hooks_ffi`) for linux-x64,
win-x64, osx-x64 and osx-arm64.
