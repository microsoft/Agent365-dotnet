# Implementation Plan: MCP Request Message ID

**Branch**: `001-mcp-message-id` | **Date**: 2026-01-23 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-mcp-message-id/spec.md`

## Summary

Add message ID propagation to all MCP platform requests. The message ID is extracted from the incoming Activity (`turnContext.Activity.Id`) and sent as the `x-ms-message-id` HTTP header on all outbound MCP requests. This enables correlation of multiple MCP tool invocations back to the original user request for debugging and monitoring.

## Technical Context

**Language/Version**: C# / .NET 8.0 (net8.0)
**Primary Dependencies**: Microsoft.Agents.Builder (ITurnContext, Activity), Microsoft.Extensions.Logging
**Storage**: N/A
**Testing**: xUnit, Moq, FluentAssertions
**Target Platform**: .NET 8.0 class library (NuGet package)
**Project Type**: SDK library (Core + Extensions pattern)
**Performance Goals**: <1ms latency overhead for header propagation
**Constraints**: Must not break existing functionality; must handle missing message ID gracefully
**Scale/Scope**: Affects all MCP requests made through the Tooling package

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Core + Extensions Architecture | ✅ PASS | Change is in Core Tooling package (framework-agnostic) |
| II. Multi-Tenant First | ✅ PASS | Message ID is per-request, not tenant-specific; no hard-coded IDs |
| III. Strict Code Quality | ✅ PASS | Will include XML docs, copyright header, nullable types |
| IV. Standardized Error Handling | ✅ PASS | Uses logging for warning on missing ID; no exceptions thrown |
| V. Disposable Scope Pattern | N/A | No new scopes introduced |
| VI. Test Coverage Required | ✅ PASS | Will add unit tests for new functionality |

**Gate Result**: PASS - No violations requiring justification.

## Project Structure

### Documentation (this feature)

```text
specs/001-mcp-message-id/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (N/A - internal change)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── Tooling/
│   └── Core/
│       ├── Handlers/
│       │   └── HttpContextHeadersHandler.cs  # MODIFY: Add message ID header
│       └── Utils/
│           └── Constants.cs                   # MODIFY: Add header constant
└── Tests/
    └── Microsoft.Agents.A365.Tooling.Tests/
        └── HttpContextHeadersHandlerTests.cs  # ADD: Tests for message ID propagation
```

**Structure Decision**: Minimal change to existing Tooling Core package. Follows established handler pattern already used for conversation ID, channel ID, and observability headers.

## Complexity Tracking

> No constitution violations to justify.

| Item | Complexity | Justification |
|------|------------|---------------|
| Single file modification | Low | Follows existing pattern in HttpContextHeadersHandler |
| New constant | Trivial | Standard header name constant |
| Test coverage | Low | Mirror existing test patterns |
