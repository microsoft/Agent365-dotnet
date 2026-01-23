# Tasks: MCP Request Message ID

**Input**: Design documents from `/specs/001-mcp-message-id/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md

**Tests**: Included per Constitution Principle VI (Test Coverage Required).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **SDK Package**: `src/Tooling/Core/`
- **Tests**: `src/Tests/Microsoft.Agents.A365.Tooling.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: No setup tasks needed - existing project infrastructure

> This feature modifies an existing package. Project structure already exists.

**Checkpoint**: Proceed directly to Foundational phase.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add the header constant used by all user stories

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T001 Add MessageIdHeader constant (`x-ms-message-id`) in src/Tooling/Core/Handlers/HttpContextHeadersHandler.cs

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Correlate MCP Calls to Original Message (Priority: P1) 🎯 MVP

**Goal**: Add message ID header to all MCP requests, enabling correlation of tool calls back to the originating user message.

**Independent Test**: Send a user message that triggers MCP tool calls, verify `x-ms-message-id` header is present and matches `Activity.Id`.

### Tests for User Story 1

- [X] T002 [P] [US1] Add unit test for message ID header added when Activity.Id is present in src/Tests/Microsoft.Agents.A365.Tooling.Tests/HttpContextHeadersHandlerTests.cs
- [X] T003 [P] [US1] Add unit test for warning logged when Activity.Id is missing in src/Tests/Microsoft.Agents.A365.Tooling.Tests/HttpContextHeadersHandlerTests.cs
- [X] T004 [P] [US1] Add unit test for request proceeding without header when Activity.Id is missing in src/Tests/Microsoft.Agents.A365.Tooling.Tests/HttpContextHeadersHandlerTests.cs

### Implementation for User Story 1

- [X] T005 [US1] Extract Activity.Id and add x-ms-message-id header in SendAsync method in src/Tooling/Core/Handlers/HttpContextHeadersHandler.cs
- [X] T006 [US1] Add warning log when Activity.Id is null or empty in src/Tooling/Core/Handlers/HttpContextHeadersHandler.cs
- [X] T007 [US1] Verify existing tests still pass after modification by running `dotnet test src/Tests/Microsoft.Agents.A365.Tooling.Tests/`

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently. All MCP requests include the message ID header when Activity.Id is present.

---

## Phase 4: User Story 2 - Debug Failed Tool Invocations (Priority: P2)

**Goal**: Enable support engineers to find all MCP platform interactions for a specific message.

**Independent Test**: Verify message ID in logs allows locating all related MCP calls.

> **Note**: This user story is fully satisfied by US1 implementation. The message ID header enables log correlation. No additional code changes needed.

### Implementation for User Story 2

- [X] T008 [US2] Verify warning log message includes actionable information for debugging in src/Tooling/Core/Handlers/HttpContextHeadersHandler.cs (review only)

**Checkpoint**: User Story 2 is satisfied by US1's implementation. Message ID enables debugging.

---

## Phase 5: User Story 3 - Analyze Tool Usage Per Message (Priority: P3)

**Goal**: Enable platform operators to track MCP call patterns per user message.

**Independent Test**: Verify message IDs in MCP request headers can be aggregated for metrics.

> **Note**: This user story is fully satisfied by US1 implementation. The message ID header enables aggregation and analytics on the receiving MCP servers. No additional SDK code changes needed.

### Implementation for User Story 3

- [X] T009 [US3] Document message ID header in quickstart.md for MCP server implementers (review specs/001-mcp-message-id/quickstart.md)

**Checkpoint**: User Story 3 is satisfied by US1's implementation. Message ID enables analytics.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and cleanup

- [X] T010 Run full solution build: `dotnet build src/Microsoft.Agents.A365.Sdk.sln`
- [X] T011 Run all tests: `dotnet test src/Microsoft.Agents.A365.Sdk.sln`
- [X] T012 [P] Verify copyright header is present in modified file
- [X] T013 [P] Verify XML documentation is present for any new public members
- [X] T014 Validate quickstart.md scenarios work as documented

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Skipped - existing project
- **Foundational (Phase 2)**: No dependencies - can start immediately
- **User Story 1 (Phase 3)**: Depends on Phase 2 (T001) completion
- **User Story 2 (Phase 4)**: Depends on Phase 3 completion (satisfied by US1)
- **User Story 3 (Phase 5)**: Depends on Phase 3 completion (satisfied by US1)
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Depends on T001 (constant) - CORE IMPLEMENTATION
- **User Story 2 (P2)**: Satisfied by US1 - review task only
- **User Story 3 (P3)**: Satisfied by US1 - review task only

### Within Each User Story

- Tests (T002-T004) can run in parallel
- Tests should be written first, verified to fail, then implementation (T005-T006)
- T007 validates all tests pass

### Parallel Opportunities

- T002, T003, T004 can run in parallel (different test methods, same file)
- T012, T013 can run in parallel (independent validation checks)
- US2 and US3 phases can run in parallel (both are review-only)

---

## Parallel Example: User Story 1 Tests

```bash
# Launch all tests for User Story 1 together:
Task: "Add unit test for message ID header added when Activity.Id is present"
Task: "Add unit test for warning logged when Activity.Id is missing"
Task: "Add unit test for request proceeding without header when Activity.Id is missing"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 2: Foundational (T001)
2. Complete Phase 3: User Story 1 (T002-T007)
3. **STOP and VALIDATE**: Test User Story 1 independently
4. All three user stories are now satisfied

### Incremental Delivery

1. T001 (constant) → Foundation ready
2. T002-T004 (tests) → Red tests exist
3. T005-T006 (implementation) → Tests go green
4. T007 (validation) → All tests pass
5. T010-T014 (polish) → Ready for merge

### Single Developer Strategy

All tasks are designed to be completed sequentially by a single developer:

1. T001 → T002 → T003 → T004 → T005 → T006 → T007
2. T008 → T009 (review tasks)
3. T010 → T011 → T012 → T013 → T014

---

## Notes

- [P] tasks = different files or methods, no dependencies
- [Story] label maps task to specific user story for traceability
- US2 and US3 are satisfied by US1's implementation (header enables both debugging and analytics)
- Total implementation touches only ONE source file (HttpContextHeadersHandler.cs)
- Constitution compliance: Tests included per Principle VI
