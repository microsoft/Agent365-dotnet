# Feature Specification: MCP Request Message ID

**Feature Branch**: `001-mcp-message-id`
**Created**: 2026-01-23
**Status**: Draft
**Input**: User description: "Requests sent to MCP platform should contain message id so that they can be uniquely identified"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Correlate MCP Calls to Original Message (Priority: P1)

As a developer troubleshooting an issue, I need to correlate all MCP platform calls made during the processing of a user message back to the original Activity so that I can trace the complete flow from user input through all tool invocations.

**Why this priority**: This is the core capability that enables all debugging and monitoring scenarios. Without the message ID, there is no way to connect MCP platform calls back to the user request that triggered them.

**Independent Test**: Can be fully tested by sending a user message that triggers multiple MCP tool calls, then verifying all MCP requests contain the same message ID matching the original Activity.

**Acceptance Scenarios**:

1. **Given** a user message arrives at the agent application with a message ID in the Activity, **When** the agent calls one or more MCP tool servers, **Then** all MCP requests contain the same message ID from the original Activity.
2. **Given** a message ID from a user request, **When** searching logs across MCP platform services, **Then** all MCP calls triggered by that user message can be found using the message ID.
3. **Given** an agent processes a user message and makes 5 different MCP tool calls, **When** inspecting the MCP requests, **Then** all 5 requests share the same message ID.

---

### User Story 2 - Debug Failed Tool Invocations (Priority: P2)

As a support engineer investigating a failed user request, I need to find all MCP platform interactions for that specific message so that I can identify which tool call failed and why.

**Why this priority**: Debugging depends on the correlation capability being in place, but is a critical use case for operational support.

**Independent Test**: Can be tested by simulating a failed MCP call and verifying the message ID allows locating the failure across all system logs.

**Acceptance Scenarios**:

1. **Given** a user reports an error with their request, **When** support provides the message ID, **Then** all MCP platform calls for that message can be retrieved from logs.
2. **Given** one of multiple MCP calls fails during message processing, **When** investigating using the message ID, **Then** the specific failed call can be identified among all calls made for that message.

---

### User Story 3 - Analyze Tool Usage Per Message (Priority: P3)

As a platform operator, I need to understand how many MCP platform calls are made per user message so that I can monitor system load, optimize performance, and plan capacity.

**Why this priority**: Analytics and monitoring are valuable but can be achieved after the core message ID propagation exists.

**Independent Test**: Can be tested by aggregating MCP calls by message ID and computing statistics on calls-per-message.

**Acceptance Scenarios**:

1. **Given** MCP requests include message IDs, **When** analyzing platform metrics, **Then** the average number of MCP calls per user message can be calculated.
2. **Given** message IDs are captured in telemetry, **When** reviewing dashboards, **Then** messages with unusually high MCP call counts can be identified for optimization.

---

### Edge Cases

- What happens when the Activity does not contain a message ID? System logs a warning and proceeds without including a message ID in MCP requests.
- How are message IDs handled when the agent makes nested or recursive tool calls? All calls within the same message processing share the same ID.
- What if an MCP call triggers a callback that processes a new message? The new message should have its own message ID (different Activity).
- How does this interact with existing correlation headers (e.g., X-Correlation-Id)? Message ID is complementary and should be propagated alongside existing correlation mechanisms.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST extract the message ID from the incoming Activity for each user request.
- **FR-002**: System MUST include the message ID in every request sent to MCP tool servers during processing of that message (when message ID is present).
- **FR-003**: All MCP requests made while processing a single user message MUST share the same message ID.
- **FR-004**: Message IDs MUST be transmitted in request headers to MCP tool servers.
- **FR-005**: System MUST propagate message IDs to telemetry/observability data for correlation.
- **FR-006**: When the Activity does not contain a message ID, the system MUST log a warning and proceed without including a message ID header in MCP requests.

### Key Entities

- **Message ID**: An identifier from the incoming Activity that uniquely identifies the user request. Propagated to all MCP platform calls made during processing of that message. Enables correlation of multiple MCP calls back to the originating user request.
- **Activity**: The incoming request to the agent application, which already contains the message ID.
- **MCP Request**: An outbound request to an MCP tool server, enhanced to include the message ID header from the originating Activity.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of MCP requests from Activities with message IDs contain that message ID in their headers.
- **SC-002**: Support teams can locate all MCP platform calls for a specific user message within 30 seconds using only the message ID.
- **SC-003**: Message ID extraction and propagation adds less than 1 millisecond latency to request processing.
- **SC-004**: All MCP calls made during processing of a single user message share the same message ID (verifiable via logs/telemetry).
- **SC-005**: Message IDs appear in distributed traces, enabling end-to-end correlation from user request through all MCP tool invocations.
- **SC-006**: When an Activity lacks a message ID, a warning is logged (verifiable in logs).

## Assumptions

- The message ID is already present in the Activity when it reaches the agent application (set upstream).
- The Activity's message ID format is already globally unique (no additional uniqueness guarantees needed in this feature).
- MCP tool servers are expected to log/trace the received message ID for correlation.
- The SDK already has HTTP handler infrastructure that can be extended to add headers.
- Integration with existing observability infrastructure is the expected approach for telemetry propagation.
- Header name convention will follow standard HTTP correlation patterns.

## Clarifications

### Session 2026-01-23

- Q: What happens when Activity does not contain a message ID? → A: Log a warning and proceed without including a message ID in MCP requests (no fallback ID generation).
- Q: Is message ID unique per MCP request? → A: No, it is inherited from the incoming Activity and shared across all MCP calls made while processing that single user message.
