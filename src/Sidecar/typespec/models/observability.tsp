import "@typespec/http";

using Http;

namespace A365Sidecar;

// ─── Observability Span Attribute Schema ─────────────────────────────

/**
 * Documents the span attributes that the sidecar recognizes and enriches.
 * Customers emit standard OTLP traces with these attributes via their
 * language's OpenTelemetry SDK. The sidecar filters genAI spans and
 * enriches them before export to Agent365 Observability API.
 */

/** Attributes for invoke_agent spans. */
model InvokeAgentSpanAttributes {
  /** OTLP attribute: gen_ai.conversation.id */
  conversationId: string;

  /** OTLP attribute: microsoft.session.id */
  sessionId?: string;

  /** OTLP attribute: microsoft.a365.caller.agent.name */
  callerAgentName?: string;

  /** OTLP attribute: microsoft.a365.caller.agent.id */
  callerAgentId?: string;

  /** OTLP attribute: microsoft.a365.caller.agent.blueprint.id */
  callerAgentBlueprintId?: string;

  /** OTLP attribute: microsoft.a365.channel.name */
  channelName?: string;

  /** OTLP attribute: microsoft.a365.channel.link */
  channelLink?: string;

  /** OTLP attribute: microsoft.a365.threat.diagnostics.summary */
  threatDiagnosticsSummary?: string;
}

/** Attributes for chat/inference spans. */
model InferenceSpanAttributes {
  /** OTLP attribute: gen_ai.operation.name (e.g., "chat") */
  operationName: string;

  /** OTLP attribute: gen_ai.system (e.g., "openai", "azure_openai") */
  system: string;

  /** OTLP attribute: gen_ai.request.model */
  requestModel?: string;

  /** OTLP attribute: gen_ai.response.model */
  responseModel?: string;

  /** OTLP attribute: gen_ai.usage.input_tokens */
  inputTokens?: int32;

  /** OTLP attribute: gen_ai.usage.output_tokens */
  outputTokens?: int32;

  /** OTLP attribute: gen_ai.response.finish_reasons */
  finishReasons?: string;

  /** OTLP attribute: gen_ai.conversation.id */
  conversationId?: string;
}

/** Attributes for execute_tool spans. */
model ExecuteToolSpanAttributes {
  /** OTLP attribute: gen_ai.tool.name */
  toolName: string;

  /** OTLP attribute: gen_ai.tool.call.id */
  toolCallId?: string;

  /** OTLP attribute: gen_ai.tool.call.arguments (JSON string) */
  toolCallArguments?: string;

  /** OTLP attribute: gen_ai.tool.description */
  toolDescription?: string;

  /** OTLP attribute: microsoft.a365.tool.type */
  toolType?: string;

  /** OTLP attribute: microsoft.a365.tool.server.name */
  toolServerName?: string;

  /** OTLP attribute: microsoft.a365.threat.diagnostics.summary */
  threatDiagnosticsSummary?: string;
}
