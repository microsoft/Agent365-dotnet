import "@typespec/http";

using Http;

namespace A365Sidecar;

// ─── Activity Protocol Proxy Models ──────────────────────────────────

/** Inbound turn payload delivered to customer's webhook. */
model TurnPayload {
  /** Unique ID for this turn. */
  turnId: string;

  /** Activity type: message, event, conversationUpdate, invoke. */
  type: "message" | "event" | "conversationUpdate" | "invoke";

  /** Conversation identifier. */
  conversationId: string;

  /** Source channel (e.g., "msteams", "webchat", "m365copilot"). */
  channelId: string;

  /** The sender. */
  from: ChannelAccount;

  /** The recipient (the agent). */
  recipient: ChannelAccount;

  /** Message text (for message activities). */
  text?: string;

  /** File/card attachments. */
  attachments?: Attachment[];

  /** ISO 8601 timestamp. */
  timestamp: string;

  // ConversationUpdate fields
  membersAdded?: ChannelAccount[];
  membersRemoved?: ChannelAccount[];

  // Event fields
  eventName?: string;
  eventValue?: Record<unknown>;

  // Invoke fields
  invokeName?: string;
  invokeValue?: Record<unknown>;

  /** Raw channel-specific data (pass-through). */
  channelData?: Record<unknown>;
}

model ChannelAccount {
  id: string;
  name?: string;
  aadObjectId?: string;
}

model Attachment {
  contentType: string;
  contentUrl?: string;
  content?: Record<unknown>;
  name?: string;
}

/** Non-streaming response from customer's webhook. */
model TurnResponse {
  text?: string;
  textFormat?: "plain" | "markdown" | "xml";
  attachments?: Attachment[];
  citations?: Citation[];
  suggestedActions?: string[];
}

model Citation {
  title: string;
  url?: string;
  content?: string;
}

/** SSE streaming event types. */
model StreamingEvent {
  type: "typing" | "chunk" | "done" | "error";
  text?: string;
  citations?: Citation[];
  message?: string;
  code?: string;
}

// ─── Outbound Turn API ───────────────────────────────────────────────

@route("/api/v1/turns/{turnId}")
@tag("Messaging")
interface Turns {
  /** Send a reply within an active turn. */
  @route("reply")
  @post
  reply(@path turnId: string, @body body: TurnResponse): void;

  /** Send a typing indicator. */
  @route("typing")
  @post
  typing(@path turnId: string): void;

  /** Delete a previously sent message. */
  @route("activities/{activityId}")
  @delete
  deleteActivity(@path turnId: string, @path activityId: string): void;

  /** Update a previously sent message. */
  @route("activities/{activityId}")
  @patch
  updateActivity(
    @path turnId: string,
    @path activityId: string,
    @body body: TurnResponse,
  ): void;
}

// ─── Proactive Messaging ─────────────────────────────────────────────

@route("/api/v1/conversations/{conversationId}")
@tag("Messaging")
interface ProactiveMessaging {
  /** Send a proactive message outside of a turn. */
  @route("message")
  @post
  sendMessage(@path conversationId: string, @body body: TurnResponse): void;

  /** Send a proactive typing indicator. */
  @route("typing")
  @post
  sendTyping(@path conversationId: string): void;
}
