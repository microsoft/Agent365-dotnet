import "@typespec/http";
import "@typespec/rest";
import "@typespec/openapi3";
import "@typespec/versioning";

import "./models/messaging.tsp";
import "./models/tooling.tsp";
import "./models/notifications.tsp";
import "./models/observability.tsp";

using Http;
using Rest;
using Versioning;

@service(#{ title: "Agent365 Sidecar API" })
@versioned(Versions)
@server("http://localhost:5365", "Agent365 Sidecar")
namespace A365Sidecar;

enum Versions {
  v1: "v1",
}

// ─── Health Endpoints ────────────────────────────────────────────────

@route("/healthz")
@tag("Health")
interface Liveness {
  @get op check(): HealthResponse;
}

@route("/readyz")
@tag("Health")
interface Readiness {
  @get op check(): ReadinessResponse | NotReadyResponse;
}

@route("/api/v1/status")
@tag("Health")
interface Status {
  @get op get(): StatusResponse;
}

model HealthResponse {
  status: "healthy";
}

model ReadinessResponse {
  status: "ready";
}

@error
model NotReadyResponse {
  @statusCode statusCode: 503;
  status: "not_ready";
  issues: string[];
}

model StatusResponse {
  status: "running";
  agent: AgentInfo;
  modules: ModulesInfo;
  server: ServerInfo;
}

model AgentInfo {
  id: string;
  name: string;
  blueprintId: string;
}

model ModulesInfo {
  messaging: ModuleStatus;
  observability: ModuleStatus;
  tooling: ModuleStatus;
  notifications: ModuleStatus;
}

model ModuleStatus {
  enabled: boolean;
}

model ServerInfo {
  port: int32;
  bindAddress: string;
}
