# Microsoft.Agents.A365.Tooling.Extensions.AgentHooks

Microsoft Agent 365 real-time protection on the [agent-hooks](https://github.com/responsibleai/agent-hooks)
control contract (AGENT-HOOKS-0.1), using the .NET package
[`ResponsibleAI.AgentHooks`](https://www.nuget.org/packages/ResponsibleAI.AgentHooks).

`A365DefenderInterceptor` is an agent-hooks interceptor for Microsoft Defender for AI. Each context the host
emits at the four points Defender evaluates is sent to the prevention endpoint
(`POST .../v1/protection/evaluate`) as a fitted copy: the client builds a copy of the context that meets
Defender's request validation (spec version, UTC timestamp, `target`, envelope and tool objects with only their
spec members, repaired optional fields and clamped content), keeping its session, sequence and tool call ids. The
host's context is not modified.
Defender's verdict decides:

| agent-hooks point | When | On `deny` |
|---|---|---|
| `input` | the user's message, before the agent runs | the agent does not run |
| `pre_tool_call` | a tool call, before it runs | the tool does not run |
| `post_tool_call` | a tool result, before the agent uses it | the result is withheld |
| `output` | the reply, before it is sent | the reply is replaced |

Other points (`agent_startup`, model calls, `agent_shutdown`) are allowed without a call. Defender's warnings and
result labels are passed through to the agent-hooks verdict; a warning reason in the `host_error:` namespace, which
agent-hooks reserves for host failures, is reported as `defender:warning`.

## Authentication

Calls carry the **agent identity's own app-only token** in the agent's tenant, for the Defender API
(`api://86a21212-634e-4553-b3d6-e477e4c9d9ec`, role `RealtimeProtection.Evaluate.All`). This is the same
authority as Observability S2S export: `DefenderRtpTokenResolvers.FromAgenticConnection` asks the agent's
connection (`IAgenticTokenProvider`) for the agent identity's assertion and exchanges it.

Defender accepts only callers whose app-only token carries the application permission
`RealtimeProtection.Evaluate.All` on the Defender API (`86a21212-634e-4553-b3d6-e477e4c9d9ec`).
[microsoft/Agent365-devTools#485](https://github.com/microsoft/Agent365-devTools/pull/485) adds this to
`a365 setup`. Until it ships, a tenant administrator grants it once per agent blueprint, and every agent identity
created from the blueprint inherits it:

1. If the tenant has no service principal for the Defender API yet, create one:
   `az ad sp create --id 86a21212-634e-4553-b3d6-e477e4c9d9ec`.
2. Assign the app role to the blueprint's service principal:
   `POST https://graph.microsoft.com/v1.0/servicePrincipals/{blueprint-sp-object-id}/appRoleAssignments` with
   `principalId` (the blueprint service principal), `resourceId` (the Defender API service principal) and
   `appRoleId` (the id of `RealtimeProtection.Evaluate.All` in that service principal's `appRoles`). Requires
   Global Administrator or Privileged Role Administrator.
3. Make it inheritable:
   `POST https://graph.microsoft.com/beta/applications/microsoft.graph.agentIdentityBlueprint/{blueprint-object-id}/inheritablePermissions`
   with
   `{"resourceAppId":"86a21212-634e-4553-b3d6-e477e4c9d9ec","inheritableScopes":{"@odata.type":"#microsoft.graph.allAllowedScopes","kind":"allAllowed"},"inheritableRoles":{"@odata.type":"#microsoft.graph.allAllowedRoles","kind":"allAllowed"}}`.
   Requires Agent ID Administrator or Global Administrator.

The tenant must also be onboarded to Microsoft Defender for AI; otherwise Defender returns `403`, which follows
the fail mode.

## Usage

```csharp
using AgentHooks;
using Microsoft.Agents.A365.Tooling.Extensions.AgentHooks;
using Microsoft.Agents.A365.Tooling.Protection.Defender;
using Microsoft.Agents.Authentication;

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

`httpClient` is optional for both the client and the token resolver; without it they share a client that does not
follow redirects. A client you pass must not follow them either (for example `AllowAutoRedirect = false`): a 307 or 308
would replay the context, or the client assertion, to another host, so a redirected request is treated as a failure.

The evaluation callback is for logging and telemetry. It runs on the thread pool once the verdict is decided, outside
the emitter's interceptor timeout, so neither what it does nor how long it takes changes the verdict; it may run
after the interceptor returns and alongside later evaluations. An exception it throws is logged (to the optional
`ILogger` passed to `A365DefenderInterceptor`) and ignored. When resolving the identity or evaluating throws, the
result and verdict record only the exception's type; the exception itself goes to the same logger.

Agents built on Microsoft Agent Framework can register the same interceptor with
`Microsoft.Agents.AI.AgentHooks`, which mediates model and tool calls.

**Interceptor timeout.** The client evaluates within one deadline, `DefenderRtpOptions.Timeout` (default 10 s),
that covers token acquisition and the request, and applies the fail mode when it passes. The emitter's
per-interceptor timeout must be longer: when it fires first, agent-hooks records a fail-closed host error even
if Defender is configured to fail open. `A365AgentHooks.CreateProtectionEmitter` sets it to
`DefenderRtpOptions.Timeout` plus two seconds. If you register the interceptor on another emitter (for example
`new InterceptionEmitter()`, whose default is 5 s, or through Agent Framework), set its interceptor timeout above
`DefenderRtpOptions.Timeout`, or lower `A365_DEFENDER_RTP_TIMEOUT_MILLISECONDS`.

## Configuration

| Variable | Meaning |
|---|---|
| `ENABLE_A365_DEFENDER_RTP` | `true` to call Defender |
| `A365_DEFENDER_RTP_ENDPOINT` | the prevention endpoint, an absolute HTTPS URL: `https://<host>/v1/protection/evaluate` |
| `A365_DEFENDER_RTP_FAIL_MODE` | `open` (default) or `closed`, which blocks when no verdict is obtained; any other value is rejected |
| `A365_DEFENDER_RTP_TIMEOUT_MILLISECONDS` | the deadline for one evaluation, including token acquisition (default 10000) |
| `A365_DEFENDER_RTP_AUTHENTICATION_SCOPE` | overrides the Defender API scope |
| `A365_DEFENDER_RTP_MAX_CONTENT_CHARACTERS` | clamps each content string sent; ids, names and roles are not truncated (default 20000). See **Content size** |

**Content size.** Each content string longer than `A365_DEFENDER_RTP_MAX_CONTENT_CHARACTERS` (default 20000) is
truncated before it is sent, and the request as a whole carries at most four times that much content: the content
under decision at the point (the input, the tool call's arguments, the tool result or the reply) first, then at a
tool point the called tool's declaration, then the other tool declarations, the newest message history, extensions
and other members, with the oldest messages dropped first. Names, keys and nulls count toward that too, so a context
padded with many empty or null items cannot inflate the request, and only as many tool declarations as the budget
can hold are read. The called tool's declaration is found with a name-only scan of the first 10,000 declarations; a
tool missing from a list scanned to the end is declared from its name. Content nested more than 32 levels deep is
cut the same way.
The agent's own context is not modified. When the content under decision, or the called tool's declaration, was cut
(or the scan stopped before finding the declaration), Defender has not seen all of it: its deny still blocks, but its
allow does not cover the rest, so the result follows the fail mode. Fail open
allows with a `defender:unverified` warning; fail closed denies with `runtime_error:defender_unverified`. Raise the
limit for agents that handle long content. Evaluating long content in chunks is a planned follow-up. Lone UTF-16
surrogates in any string are replaced with U+FFFD, since Defender cannot parse them.

Every call sends a unique `x-ms-correlation-id`, returned as `DefenderRtpEvaluationResult.CorrelationId`;
Defender logs each evaluation under it. A `400` reports the failed validation rule in `Error`.

`ResponsibleAI.AgentHooks` is a prerelease package with a native core (`agent_hooks_ffi`) for linux-x64,
win-x64, osx-x64 and osx-arm64.
