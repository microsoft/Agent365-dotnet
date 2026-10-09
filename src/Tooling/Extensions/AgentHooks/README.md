# Microsoft.Agents.A365.Tooling.Extensions.AgentHooks

Microsoft Agent 365 real-time protection on the [agent-hooks](https://github.com/responsibleai/agent-hooks)
control contract (AGENT-HOOKS-0.1), using the .NET package
[`ResponsibleAI.AgentHooks`](https://www.nuget.org/packages/ResponsibleAI.AgentHooks).

`A365DefenderInterceptor` is an agent-hooks interceptor for Microsoft Defender for AI, and `A365PurviewInterceptor`
one for Microsoft Purview data loss prevention (DLP) and audit (see [Microsoft Purview DLP](#microsoft-purview-dlp)).
Both register on one emitter, where a deny from either wins.

Each context the host emits at the four points Defender evaluates is sent to the prevention endpoint
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
if Defender is configured to fail open. `A365AgentHooks.CreateProtectionEmitter` sets it to the longest timeout of the
options it is given (`DefenderRtpOptions.Timeout`, `PurviewDlpOptions.Timeout`) plus two seconds. If you register the
interceptor on another emitter (for example
`new InterceptionEmitter()`, whose default is 5 s, or through Agent Framework), set its interceptor timeout above
`DefenderRtpOptions.Timeout`, or lower `A365_DEFENDER_RTP_TIMEOUT_MILLISECONDS`.

## Configuration

| Variable | Meaning |
|---|---|
| `ENABLE_A365_DEFENDER_RTP` | `true` to call Defender; `false` or unset leaves it off (`1`/`0`, `yes`/`no` and `on`/`off` also work); any other value is rejected |
| `A365_DEFENDER_RTP_ENDPOINT` | the prevention endpoint, an absolute HTTPS URL: `https://<host>/v1/protection/evaluate` |
| `A365_DEFENDER_RTP_FAIL_MODE` | `open` (default) or `closed`, which blocks when no verdict is obtained; any other value is rejected |
| `A365_DEFENDER_RTP_TIMEOUT_MILLISECONDS` | the deadline for one evaluation, including token acquisition (default 10000) |
| `A365_DEFENDER_RTP_AUTHENTICATION_SCOPE` | overrides the Defender API scope |
| `A365_DEFENDER_RTP_MAX_CONTENT_CHARACTERS` | clamps each content string sent; ids, names and roles are not truncated (default 20000). See **Content size** |

**Content size.** Each content string longer than `A365_DEFENDER_RTP_MAX_CONTENT_CHARACTERS` (default 20000) is
truncated before it is sent, and the request as a whole carries at most four times that much content, in this order:
the content under decision at the point (the input, the tool call's arguments, the tool result or the reply); at a
tool point the called tool's declaration; the tool call's arguments at `post_tool_call`; the other tool declarations
in the agent's order; the message history, newest first; then extensions and other members. Names, keys and nulls
count toward that too, so a context padded with many empty or null items cannot inflate the request, and only as
many tool declarations as the budget can hold are read. Content nested more than 32 levels deep is cut the same way.

The called tool's declaration is found with a name-only scan of the first 10,000 declarations; its name is sent whole,
and its description and schema count toward the limit. Without any declarations, the `a365` extension's description
of the tool is used. A tool missing from a list scanned to the end is declared from its name.

The agent's own context is not modified. When the content under decision or the called tool's declaration was cut,
or the list is longer than the scan and the called tool was not among the declarations scanned, Defender has not
seen all of it: its deny still blocks, but its allow does not cover the rest, so the result follows the fail mode and
`Error` says which (`TruncatedContentError`, `TruncatedToolDeclarationError` or `UnseenToolDeclarationError`). Fail open
allows with a `defender:unverified` warning; fail closed denies with `runtime_error:defender_unverified`. Raise the
limit for agents that handle long content. Evaluating long content in chunks is a planned follow-up. Lone UTF-16
surrogates in any string are replaced with U+FFFD, since Defender cannot parse them.

Every call sends a unique `x-ms-correlation-id`, returned as `DefenderRtpEvaluationResult.CorrelationId`;
Defender logs each evaluation under it. A `400` reports the failed validation rule in `Error`.

## Microsoft Purview DLP

`A365PurviewInterceptor` sends the agent's prompts and replies to Microsoft Purview through the Microsoft Graph
`processContent` API (`POST https://graph.microsoft.com/v1.0/me/dataSecurityAndGovernance/processContent`). Purview
applies the tenant's DLP policies scoped to the agent's application and writes the audit record that Purview Audit
and Data Security Posture Management (DSPM) for AI show.

| agent-hooks point | Purview activity | By default | On a block |
|---|---|---|---|
| `input` | `uploadText`: the user's message | waits for the verdict | the agent does not run |
| `output` | `downloadText`: the reply | audit: sent without waiting, never blocked | with `enforce`, the reply is replaced |

Other points are allowed without a call; tool calls and tool results are not sent to Purview. The text sent is the
content's string, or every string and number in structured content, one per line; content without text is allowed
without a call. Each call is one conversation entry: the context's `session.id` is its `correlationId`, its
`sequence` the entry's `sequenceNumber`, and the entry names the agent (`agents`: the blueprint id, the agent
identity id and the agent's name). Policies match on the entry's application
(`protectedAppMetadata.applicationLocation`), `PurviewDlpAgentContext.ApplicationId`, which defaults to the blueprint
id, then the agent identity id.

A policy action whose `restrictionAction` is `block` blocks: the verdict denies with reason `purview:block`, the
message "The request was blocked by a Microsoft Purview data loss prevention policy." and the evidence
`urn:a365:purview:<client-request-id>`. Other actions (for example audit) allow and are counted in
`PurviewDlpEvaluationResult.Decision`. Every call sends a new `client-request-id`, returned as
`PurviewDlpEvaluationResult.CorrelationId`; Graph logs the call under it.

**Replies.** Purview DLP policies for custom AI apps restrict prompts (`uploadText`) and cannot block replies. By
default (`A365_PURVIEW_DLP_RESPONSE_MODE=audit`) the interceptor allows the reply at once, whatever the fail mode, and
sends it to Purview in the background, so it is audited without delaying the turn. The background evaluation is
bounded by the client's own timeout rather than the emitter's cancellation, contains its failures, and reports to the
evaluation callback. `enforce` waits for the verdict on replies and maps it as at `input`.

**Fail mode.** When no verdict is obtained (no agent identity, a token, transport, HTTP or timeout failure, an
unexpected response, or `processingErrors` that Graph reports inline: an invalid request comes back as HTTP 200 with a
processing error and no policy actions), the result follows `A365_PURVIEW_DLP_FAIL_MODE`. `open` (the default) allows
with a `purview:unverified` warning; `closed` denies with `runtime_error:purview_unverified`, which is never reported as
a detection. A block stands even when Graph reports processing errors, or actions of another shape, with it. `Error`
records only the status, the identifier-like codes of processing errors, or an exception's type; never a response body
or a token.

**Content size.** Text longer than `A365_PURVIEW_DLP_MAX_CONTENT_CHARACTERS` (default 100000) is cut and sent with
`isTruncated` set. Purview's block of the cut text stands; its allow does not cover what Purview did not see, so the
result follows the fail mode (`PurviewDlpEvaluationResult.Truncated`, with `Error` set to `TruncatedContentError`).
Lone UTF-16 surrogates are replaced with U+FFFD.

### Purview authentication

`PurviewDlpTokenResolvers.FromAgenticUser` asks the agent's connection
(`IAgenticTokenProvider.GetAgenticUserTokenAsync`) for the agentic user's delegated Microsoft Graph token, which
carries `Content.Process.User`, and evaluates as `/me`: the agent's agentic user. It needs the turn's tenant, agent
identity and agentic user, from the incoming activity: `GetAgenticTenantId()`, `GetAgenticInstanceId()` and
`GetAgenticUser()`. MSAL connections cache each token of the agentic chain, so the client does not cache tokens itself.

`PurviewDlpTokenResolvers.FromAccessTokenProvider` uses a Microsoft Graph token from the host instead, for example an
on-behalf-of token for the signed-in user (`/me`), or an app-only token with the application permission
`Content.Process.All` for a given user (`/users/{userId}`). The app-only path has not been validated end to end yet.
The provider should cache its tokens, since the client asks for one on every evaluation.

`protectionScopes/compute` is not called: `processContent` applies the policies itself, and computing scopes needs
another permission (`ProtectionScopes.Compute.User`).

### Purview tenant setup

1. **Licensing and billing.** Microsoft 365 E5 or E5 Compliance (or equivalent) for the users and agents, and
   Microsoft Purview pay-as-you-go billing linked to an Azure subscription: DLP for AI apps is metered. Without them,
   `processContent` returns no policy actions and nothing is blocked.
2. **DSPM for AI** is onboarded in the Microsoft Purview portal.
3. **A DLP policy on the AI app location.** In the Microsoft Purview portal, create a custom DLP policy whose only
   location is **Managed cloud apps** (the Applications workload), scoped to the agent blueprint's application id,
   with a rule whose condition matches the sensitive information to protect (for example credit card numbers) and
   whose action restricts access with **Block** for prompts (`uploadText`). Keep it a dedicated policy: the AI app
   location cannot be combined with Exchange, SharePoint, OneDrive or Teams locations. A new policy can take up to an
   hour to apply.
4. **`Content.Process.User` for agentic users.** Add the delegated Microsoft Graph permission `Content.Process.User`
   to the blueprint's tenant-wide (`AllPrincipals`) delegated grant for Microsoft Graph, appending it to the grant's
   existing scopes rather than replacing them. Re-consenting the blueprint's API permissions instead can drop the
   scopes its agents rely on. The blueprint's inheritable permissions for Microsoft Graph, which `a365 setup`
   configures as `allAllowed`, pass it on to every agentic user. With Microsoft Graph (as a Cloud Application
   Administrator, Privileged Role Administrator or Global Administrator):
   - find the grant: `GET https://graph.microsoft.com/v1.0/oauth2PermissionGrants?$filter=clientId eq '{blueprint-sp-object-id}' and consentType eq 'AllPrincipals' and resourceId eq '{microsoft-graph-sp-object-id}'`;
   - append the scope: `PATCH https://graph.microsoft.com/v1.0/oauth2PermissionGrants/{grant-id}` with
     `{"scope":"{existing scopes} Content.Process.User"}`.

### Purview usage

Defender and Purview on one emitter:

```csharp
using AgentHooks;
using Microsoft.Agents.A365.Tooling.Extensions.AgentHooks;
using Microsoft.Agents.A365.Tooling.Protection.Defender;
using Microsoft.Agents.A365.Tooling.Protection.Purview;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;

var connection = (IAgenticTokenProvider)connections.GetDefaultConnection();
var defender = new DefenderRtpClient(DefenderRtpOptions.FromEnvironment(), httpClient);
var purview = new PurviewDlpClient(PurviewDlpOptions.FromEnvironment(), httpClient);
var defenderTokens = DefenderRtpTokenResolvers.FromAgenticConnection(connection, httpClient);
var purviewTokens = PurviewDlpTokenResolvers.FromAgenticUser(connection);
var activity = turnContext.Activity;

var emitter = A365AgentHooks.CreateProtectionEmitter(defender: defender.Options, purview: purview.Options)
    .AddA365Defender(new A365DefenderInterceptor(
        defender,
        context => new A365DefenderCall(
            new DefenderRtpAgentContext { AgentId = activity.GetAgenticInstanceId(), TenantId = activity.GetAgenticTenantId() },
            defenderTokens)))
    .AddA365Purview(new A365PurviewInterceptor(
        purview,
        context => new A365PurviewCall(
            new PurviewDlpAgentContext
            {
                AgentId = activity.GetAgenticInstanceId(),  // the agent identity
                TenantId = activity.GetAgenticTenantId(),    // the agent's tenant
                AgenticUserId = activity.GetAgenticUser(),   // the agentic user the token is for
                BlueprintId = blueprintId,                   // the application DLP policies are scoped to
                AgentName = "SampleAgent",
            },
            purviewTokens),
        result => logger.LogInformation(
            "Purview {Activity} allowed={Allowed} evaluated={Evaluated} actions={Actions} cid={CorrelationId}",
            result.Activity, result.Allowed, result.Evaluated, result.Decision.ActionCount, result.CorrelationId)));
```

The factory, like Defender's, is invoked only when Purview DLP is enabled, at `input` and `output`, for content with
text. When it returns null or throws, or resolving the token fails, the verdict follows the fail mode (except for a
reply audited in the background). `A365AgentHooks.CreateProtectionEmitter` sizes the emitter's per-interceptor timeout
to the longer of the two clients' timeouts plus two seconds.

### Purview configuration

| Variable | Meaning |
|---|---|
| `ENABLE_A365_PURVIEW_DLP` | `true` to call Purview; `false` or unset leaves it off (`1`/`0`, `yes`/`no` and `on`/`off` also work); any other value is rejected |
| `A365_PURVIEW_DLP_GRAPH_BASE_URL` | the Microsoft Graph base URL, an absolute HTTPS URL without a query or fragment (default `https://graph.microsoft.com/v1.0`) |
| `A365_PURVIEW_DLP_AUTHENTICATION_SCOPE` | the token scope (default `https://graph.microsoft.com/.default`) |
| `A365_PURVIEW_DLP_FAIL_MODE` | `open` (default) or `closed`, which blocks when no verdict is obtained; any other value is rejected |
| `A365_PURVIEW_DLP_TIMEOUT_MILLISECONDS` | the deadline for one evaluation, including token acquisition (default 10000) |
| `A365_PURVIEW_DLP_MAX_CONTENT_CHARACTERS` | the most text sent in one evaluation (default 100000). See **Content size** |
| `A365_PURVIEW_DLP_RESPONSE_MODE` | `audit` (default) or `enforce`, which waits for Purview's verdict on replies; any other value is rejected |

### Purview limitations

- Tool calls and tool results are not sent to Purview.
- `protectionScopes/compute`, and caching what it returns, are not used: every evaluation calls `processContent`.
- Purview DLP cannot block the replies of custom AI apps; replies are audited by default.
- The app-only path (`/users/{userId}` with `Content.Process.All`) has not been validated end to end.

`ResponsibleAI.AgentHooks` is a prerelease package with a native core (`agent_hooks_ffi`) for linux-x64,
win-x64, osx-x64 and osx-arm64.
