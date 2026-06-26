# Guardrail findings as span events on the ETW DTO logging path

**Date:** 2026-06-26
**Status:** Approved
**Scope:** `Microsoft.Agents.A365.Observability.Runtime` — ETW "Path B" (`A365EtwLogger.LogApplyGuardrail` → `ApplyGuardrailDataBuilder` → `BaseData` → `ExportFormatter.FormatLogData` → `EtwEventSource.LogJson`).

## Problem

The `apply_guardrail` span records security findings as **span events**. On the Activity-based
path this works: `ApplyGuardrailScope.RecordFinding(GuardrailFinding)` adds an
`ActivityEvent` named `microsoft.security.finding`, and the ETW Activity path
(`EtwScopeEventProcessor.OnEnd` → `ExportFormatter.FormatSingle` → `BuildOtlpSpan` →
`MapEvents`) copies those events into the emitted OTLP `events` array.

The ETW **DTO logging path** does not use the Activity flow — it builds telemetry from DTO
objects and emits them through its own formatter (`ExportFormatter.FormatLogData`). On this path,
guardrail findings are **dropped entirely**:

- `IA365EtwLogger.LogApplyGuardrail` / `A365EtwLogger.LogApplyGuardrail` have **no parameter** to
  accept findings.
- `BaseData` / `ApplyGuardrailData` carry **no events/findings collection**.
- `BaseData.ToDictionary()` emits no `Events`.
- `ExportFormatter.FormatLogData` emits **no `events` field at all**.

As a result, guardrail findings logged through the ETW DTO path are invisible to downstream
consumers. This work brings the DTO path to parity with the Activity path for span events — the
same way the `etw-span-status` work (2026-06-25) brought span `status` to parity.

## Goals

- Allow guardrail findings to be supplied to the ETW DTO path and emitted as span events.
- The emitted event JSON is **structurally identical** to the Activity path's finding events —
  one structure, not two.
- Backward compatible: new parameters are optional/defaulted.

## Non-goals

- No changes to the Activity path (`ApplyGuardrailScope.RecordFinding`, `FormatSingle`,
  `BuildOtlpSpan`, `MapEvents`); it already emits finding events correctly.
- No new types added to `Runtime/Common`. The change is confined to the ETW DTO path.
- No new ETW event IDs or event-source schema changes.
- No generalization of findings to other span types beyond the generic `Events` carrier on
  `BaseData` (only `ApplyGuardrail` populates it).

## Reference: Activity-path event structure (the target shape)

`ExportFormatter.MapEvents` + `OtlpEvent` (camelCase via `JsonPropertyName`) produce, per finding:

```json
{
  "timeUnixNano": 1750000000000000000,
  "name": "microsoft.security.finding",
  "attributes": {
    "microsoft.security.risk.category": "prompt_injection",
    "microsoft.security.risk.severity": "high",
    "microsoft.security.policy.decision.type": "block",
    "microsoft.security.policy.id": "p1",
    "microsoft.security.policy.name": "Prompt Injection Policy",
    "microsoft.security.policy.version": "1.0",
    "microsoft.security.risk.score": 0.92,
    "microsoft.security.risk.metadata": ["field:bcc"]
  }
}
```

Attribute keys are the `OpenTelemetryConstants.GenAiSecurity*Key` / `GenAiSecurityFindingEventName`
constants. Value types: `risk.score` is a `double`; `risk.metadata` is a `string[]`. Inclusion:
`risk.category` and `risk.severity` always present; the rest emitted only when non-null /
`HasValue` (matching `ApplyGuardrailScope.RecordFinding`).

## Design

### 1. `IA365EtwLogger<T>.LogApplyGuardrail` / `A365EtwLogger<T>.LogApplyGuardrail`

Add an optional final parameter:

```csharp
IEnumerable<GuardrailFinding>? findings = null
```

Optional/defaulted → backward-compatible. `A365EtwLogger.LogApplyGuardrail` forwards `findings`
to `ApplyGuardrailDataBuilder.Build`.

### 2. `ApplyGuardrailDataBuilder.Build`

Add `IEnumerable<GuardrailFinding>? findings = null`. After building attributes/status, map each
finding to a plain event dictionary and set it on the DTO:

```csharp
foreach (var finding in findings ?? Enumerable.Empty<GuardrailFinding>())
{
    var attrs = new Dictionary<string, object?>
    {
        { OpenTelemetryConstants.GenAiSecurityRiskCategoryKey, finding.RiskCategory },
        { OpenTelemetryConstants.GenAiSecurityRiskSeverityKey, finding.RiskSeverity },
    };
    if (finding.PolicyDecisionType != null)
        attrs[OpenTelemetryConstants.GenAiSecurityPolicyDecisionTypeKey] = finding.PolicyDecisionType;
    if (finding.PolicyId != null)
        attrs[OpenTelemetryConstants.GenAiSecurityPolicyIdKey] = finding.PolicyId;
    if (finding.PolicyName != null)
        attrs[OpenTelemetryConstants.GenAiSecurityPolicyNameKey] = finding.PolicyName;
    if (finding.PolicyVersion != null)
        attrs[OpenTelemetryConstants.GenAiSecurityPolicyVersionKey] = finding.PolicyVersion;
    if (finding.RiskScore.HasValue)
        attrs[OpenTelemetryConstants.GenAiSecurityRiskScoreKey] = finding.RiskScore.Value;   // double
    if (finding.RiskMetadata != null)
        attrs[OpenTelemetryConstants.GenAiSecurityRiskMetadataKey] = finding.RiskMetadata;    // string[]

    data.Events.Add(new Dictionary<string, object?>
    {
        { "timeUnixNano", ToUnixNanos(DateTimeOffset.UtcNow) },   // captured at log time, per finding
        { "name", OpenTelemetryConstants.GenAiSecurityFindingEventName },
        { "attributes", attrs },
    });
}
```

Notes:

- The mapping (keys, value types, conditional inclusion) is an exact mirror of
  `ApplyGuardrailScope.RecordFinding`. This duplication is intentional given the ETW-only scope;
  a cross-path consistency test (below) enforces that the two stay aligned.
- Dictionary keys `timeUnixNano` / `name` / `attributes` match `OtlpEvent`'s camelCase
  serialized output. Because `FormatLogData` serializes with `PropertyNamingPolicy = null`,
  dictionary keys pass through verbatim.
- `ToUnixNanos` is a small private helper (epoch math `(utc - 1970).Ticks * 100`) added to the
  builder / `BaseDataBuilder`. It stays in the DTO path — nothing added to `Runtime/Common`.
- Per-finding timestamp captured from `DateTimeOffset.UtcNow` at log time.

### 3. `BaseData` (in `Runtime/DTOs`)

Add a generic events carrier (empty by default):

```csharp
public List<Dictionary<string, object?>> Events { get; } = new List<Dictionary<string, object?>>();
```

`ToDictionary()` adds the key **only when non-empty**, so existing operations (and guardrail spans
with no findings) emit no `Events` key:

```csharp
if (Events.Count > 0)
{
    dict["Events"] = Events;
}
```

No constructor changes; builders populate via `data.Events.Add(...)`. Only `ApplyGuardrail`
populates it today.

### 4. `ExportFormatter.FormatLogData` (edit existing method; no new type)

Add `Events` to the emitted anonymous payload:

```csharp
Events = data.TryGetValue("Events", out var eventsObj) ? eventsObj : null,
```

`DefaultIgnoreCondition.WhenWritingNull` omits the field when absent. Event dictionaries pass
through verbatim, so inner keys remain OTLP-style (`timeUnixNano` / `name` / `attributes`),
identical to the Activity path.

### Resulting emitted structure (Path B, `LogJson`), one finding

```json
{
  "Name": "ApplyGuardrail",
  "Attributes": { "...": "..." },
  "StartTimeUnixNano": 0,
  "EndTimeUnixNano": 0,
  "SpanId": "…",
  "ParentSpanId": "…",
  "TraceId": "…",
  "Kind": "Client",
  "Status": { "code": 0, "message": "" },
  "Events": [
    {
      "timeUnixNano": 1750000000000000000,
      "name": "microsoft.security.finding",
      "attributes": {
        "microsoft.security.risk.category": "prompt_injection",
        "microsoft.security.risk.severity": "high"
      }
    }
  ]
}
```

No findings → the `Events` key is omitted entirely.

## Testing

Added to `Microsoft.Agents.A365.Observability.Runtime.Tests`:

- **`BaseData.ToDictionary`**: no `Events` key when the events list is empty; present with the
  expected shape when populated.
- **`ApplyGuardrailDataBuilder.Build(..., findings)`**: maps findings → event dictionaries with the
  correct `name`, attribute keys, preserved value types (`double` score, `string[]` metadata),
  conditional omission of optional fields, and `timeUnixNano > 0`.
- **`ExportFormatter.FormatLogData`**: emits the `Events` array when present (correct camelCase
  inner keys); omits `Events` when absent.
- **`A365EtwLogger.LogApplyGuardrail(..., findings)`**: findings flow through to the emitted DTO.
- **Cross-path consistency test (anti-drift)**: feed a single `GuardrailFinding` through both
  paths — `ApplyGuardrailScope.RecordFinding` → `FormatSingle` (Activity path) and
  `ApplyGuardrailDataBuilder.Build` → `FormatLogData` (DTO path) — and assert the resulting event
  objects (`name` + `attributes`) are structurally equal, ignoring the `timeUnixNano` value. This
  guarantees the two paths emit one structure even though the mapping is duplicated.

## Files

Modified:
- `src/Observability/Runtime/DTOs/BaseData.cs` — add `Events` carrier; emit in `ToDictionary` when non-empty.
- `src/Observability/Runtime/DTOs/Builders/ApplyGuardrailDataBuilder.cs` — `findings` param; map findings → event dictionaries; `ToUnixNanos` helper (or on `BaseDataBuilder`).
- `src/Observability/Runtime/Etw/A365EtwLogger.cs` — `findings` param on `LogApplyGuardrail`; forward to builder.
- `src/Observability/Runtime/Etw/IA365EtwLogger.cs` — `findings` param on the interface.
- `src/Observability/Runtime/Common/ExportFormatter.cs` — `FormatLogData` emits `Events` (existing method edited; no new type).
- Tests under `src/Tests/Microsoft.Agents.A365.Observability.Runtime.Tests/`.
