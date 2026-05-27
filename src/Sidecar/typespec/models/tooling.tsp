import "@typespec/http";

using Http;

namespace A365Sidecar;

// ─── Tooling Models ──────────────────────────────────────────────────

model McpToolServer {
  id: string;
  name: string;
  url: string;
  scope?: string;
  audience?: string;
  publisher?: string;
}

model McpTool {
  name: string;
  description?: string;

  /** JSON Schema for the tool's input parameters. */
  inputSchema: Record<unknown>;
  serverName: string;
  serverId: string;
}

model ToolInvocationRequest {
  arguments: Record<unknown>;
  conversationContext?: ConversationContext;
}

model ConversationContext {
  conversationId?: string;
  channelId?: string;
  subChannelId?: string;
  userMessage?: string;
}

model ToolInvocationResponse {
  result: Record<unknown>;
  isError: boolean;
  errorMessage?: string;
}

// ─── Tooling Endpoints ───────────────────────────────────────────────

@route("/api/v1/tools")
@tag("Tooling")
interface ToolDiscovery {
  /** List all tools across all servers (flattened). */
  @get op listAll(): McpTool[];

  /** List available MCP tool servers. */
  @route("servers")
  @get
  listServers(): McpToolServer[];

  /** List tools on a specific server. */
  @route("servers/{serverId}/tools")
  @get
  listServerTools(@path serverId: string): McpTool[];

  /** Invoke a tool on a specific server. */
  @route("servers/{serverId}/tools/{toolName}/invoke")
  @post
  invoke(
    @path serverId: string,
    @path toolName: string,
    @body body: ToolInvocationRequest,
  ): ToolInvocationResponse;

  /** Send conversation history / chat context to an MCP server. */
  @route("chat-history")
  @post
  sendChatHistory(@body body: ChatHistoryRequest): void;
}

model ChatHistoryRequest {
  serverId: string;
  messages: ChatMessage[];
}

model ChatMessage {
  role: "user" | "assistant" | "system";
  content: string;
}
