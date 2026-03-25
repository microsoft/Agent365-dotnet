# A365 Observability Input/Output Message Schema Versions

## Current Version

| Field | Value |
|---|---|
| **A365 Schema Version** | 1.0.0 |
| **OTel Semconv Baseline** | [v1.40.0](https://github.com/open-telemetry/semantic-conventions/tree/v1.40.0) |
| **OTel Semconv Commit** | [`30003a993`](https://github.com/open-telemetry/semantic-conventions/commit/30003a9937eb7bcde6984877149055278405e03c) |

## Schema Files

| File | Span Attribute | Description |
|---|---|---|
| [a365-input-messages.json](a365-input-messages.json) | `gen_ai.input.messages` | Chat history sent to the model |
| [a365-output-messages.json](a365-output-messages.json) | `gen_ai.output.messages` | Model response (choices/candidates) |

## OTel Semconv Compatibility Matrix

| A365 Schema Version | Based on OTel Semconv | Part Types | Notes |
|---|---|---|---|
| 1.0.0 | v1.40.0 | 10 (all OTel types) | Initial release. Includes ServerToolCall parts added in v1.40.0 |

### OTel Input/Output Schema Change History (for reference)

| OTel Semconv | File SHA (input) | Changes from Previous |
|---|---|---|
| v1.36.0 | — | Schema files did not exist |
| v1.37.0 | `22910f30` | **Schema introduced.** 4 part types: Text, ToolCallRequest, ToolCallResponse, Generic. No `name` field. |
| v1.38.0 | `5fbd431f` | **Major expansion.** Added Blob, File, Uri, Reasoning parts. Added Modality enum. Added `name` field to ChatMessage. |
| v1.39.0 | `5fbd431f` | No changes (identical to v1.38.0) |
| v1.40.0 | `5585531c` | Added ServerToolCallPart, ServerToolCallResponsePart. Minor wording fix. |

## Applicable Scopes

| A365 Scope | Uses This Schema? | Notes |
|---|---|---|
| `InvokeAgentScope` | ✅ | Full conversation context |
| `InferenceScope` | ✅ | Chat history + model response |
| `OutputScope` | ✅ | Outgoing messages to user |
| `ExecuteToolScope` | ❌ | Uses `gen_ai.tool.*` attributes instead |

## Changelog

### 1.0.0

- Initial A365 input/output message schema
- Profiles OTel GenAI semconv v1.40.0 input/output messages format
- Supports all OTel part types: TextPart, ToolCallRequestPart, ToolCallResponsePart, ServerToolCallPart, ServerToolCallResponsePart, BlobPart, FilePart, UriPart, ReasoningPart, GenericPart
- All objects allow `additionalProperties` for future A365-specific extensions
- Custom A365 part types can be added via `GenericPart` using `microsoft.a365.*` type prefix
