# Test Plan for Agent365-dotnet SDK

> **Note:** This plan is under active development. Keep updating as testing progresses.

**Version:** 1.0  
**Date:** December 4, 2025  
**Status:** Draft

---

## Overview

### Current State
- ✅ Unit tests exist for `Runtime` core module (48 tests passing)
- ⏳ Partial tests for `Observability.Runtime` module
- ❌ Missing tests for `Tooling`, `Notifications`, and extension modules
- ❌ No integration tests or complete CI/CD automation

### Goals
- Achieve **80%+ code coverage** across all modules
- Implement integration tests for cross-module functionality
- Integrate testing into CI/CD pipeline with coverage enforcement

---

## Testing Strategy

**Framework:** xUnit 2.8.2  
**Mocking:** Moq 4.20.72  
**Target Framework:** .NET 8

**Test Pattern:** AAA (Arrange → Act → Assert)  
**Naming Convention:** `test_<method>_<condition>_<expected_result>`

---

## Implementation Roadmap

| Phase | Deliverables | Priority |
|-------|-------------|----------|
| 1.1 | Runtime Core unit tests | ✅ Complete |
| 1.2 | Observability Runtime unit tests | HIGH |
| 1.3 | Observability Hosting unit tests | MEDIUM |
| 1.4 | Observability Extensions tests | MEDIUM |
| 1.5 | Tooling Core unit tests | HIGH |
| 1.6 | Tooling Extensions tests | LOW |
| 1.7 | Notifications unit tests | HIGH |
| 2 | Integration tests | MEDIUM |
| 3 | CI/CD automation | HIGH |

---

## Phase 1: Unit Tests

### 1.1 Runtime Core Module
**Priority:** HIGH

| Module | Test File | Status |
|--------|-----------|--------|
| `Utility.cs` | `UtilityTests.cs` | ✅ Complete |
| `AgenticAuthenticationService.cs` | `AgenticAuthenticationServiceTests.cs` | ✅ Complete |
| `TenantContextHelper.cs` | `TenantContextHelperTests.cs` | ✅ Complete |

---

### 1.2 Observability Runtime Module
**Priority:** HIGH

| Module | Test File | Status |
|--------|-----------|--------|
| `Agent365Exporter.cs` | `Agent365ExporterTests.cs` | ⏳ Partial |
| `Agent365ExporterCore.cs` | `Agent365ExporterCoreTests.cs` | ❌ Missing |
| `AgenticTokenCache.cs` | `AgenticTokenCacheTests.cs` | ❌ Missing |
| `ExportFormatter.cs` | `ExportFormatterTests.cs` | ❌ Missing |
| `OpenTelemetryConstants.cs` | `OpenTelemetryConstantsTests.cs` | ❌ Missing |

---

### 1.3 Observability Hosting Module
**Priority:** MEDIUM

| Module | Test File | Status |
|--------|-----------|--------|
| Service registration | `ServiceRegistrationTests.cs` | ❌ Missing |
| Configuration validation | `ConfigurationTests.cs` | ❌ Missing |
| Middleware integration | `MiddlewareTests.cs` | ❌ Missing |

---

### 1.4 Observability Extensions
**Priority:** MEDIUM

| Extension | Status |
|-----------|--------|
| `OpenAI` | ❌ Missing |
| `AgentFramework` | ❌ Missing |
| `SemanticKernel` | ❌ Missing |

---

### 1.5 Tooling Core Module
**Priority:** HIGH

| Module | Test File | Status |
|--------|-----------|--------|
| `Utility.cs` | `UtilityTests.cs` | ❌ Missing |
| `McpServerConfig.cs` | `McpServerConfigTests.cs` | ❌ Missing |
| `McpToolServerConfigurationService.cs` | `McpToolServerConfigurationServiceTests.cs` | ❌ Missing |

---

### 1.6 Tooling Extensions
**Priority:** LOW

| Extension | Status |
|-----------|--------|
| `AgentFramework` | ❌ Missing |
| `AzureAIFoundry` | ❌ Missing |
| `SemanticKernel` | ❌ Missing |

---

### 1.7 Notifications Module
**Priority:** HIGH

| Module | Test File | Status |
|--------|-----------|--------|
| `AgentLifecycleEvent.cs` | `AgentLifecycleEventTests.cs` | ❌ Missing |
| `AgentNotificationActivity.cs` | `AgentNotificationActivityTests.cs` | ❌ Missing |
| `EmailReference.cs` | `EmailReferenceTests.cs` | ❌ Missing |
| `AgentNotification.cs` | `AgentNotificationTests.cs` | ❌ Missing |

---

## Phase 2: Integration Tests

**Priority:** MEDIUM

| Integration | Status |
|-------------|--------|
| Runtime + Observability | ❌ Missing |
| Tooling + Runtime | ❌ Missing |
| Notifications + Runtime | ❌ Missing |
| OpenAI end-to-end tracing | ❌ Missing |
| SemanticKernel end-to-end | ❌ Missing |

---

## Phase 3: CI/CD Integration

**Priority:** HIGH

| Component | Status |
|-----------|--------|
| GitHub Actions workflow | ⏳ Partial |
| .NET matrix (8.0) | ✅ Complete |
| Coverage enforcement (80%+) | ❌ Missing |
| Codecov integration | ❌ Missing |
| PR blocking on failures | ⏳ Partial |

---

## Success Criteria

- ✅ 80%+ code coverage for all modules
- ✅ All tests pass independently
- ✅ Full suite completes in < 30 seconds (unit) / < 5 minutes (full)
- ✅ Automated test execution on all PRs
- ✅ Coverage reports visible and enforced
