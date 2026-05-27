import "@typespec/http";

using Http;

namespace A365Sidecar;

// ─── Notification Models ─────────────────────────────────────────────

model NotificationSubscription {
  id: string;

  /** Notification subchannels to subscribe to. */
  subChannels?: ("email" | "word" | "excel" | "powerpoint" | "FederatedKnowledgeService")[];

  /** Lifecycle events to subscribe to. */
  lifecycleEvents?: string[];

  /** Customer's local webhook URL to POST notifications to. */
  webhookUrl: string;

  /** HMAC signing secret for webhook payload validation. */
  secret?: string;
}

model NotificationWebhookPayload {
  id: string;
  timestamp: string;
  type: "subchannel" | "lifecycle";
  subChannel?: string;
  lifecycleEvent?: string;
  data: Record<unknown>;
}

model EmailNotification {
  id: string;
  conversationId: string;
  htmlBody: string;
}

model WpxCommentNotification {
  odataId: string;
  documentId: string;
  parentCommentId?: string;
  commentId: string;
}

model CreateSubscriptionRequest {
  subChannels?: ("email" | "word" | "excel" | "powerpoint" | "FederatedKnowledgeService")[];
  lifecycleEvents?: string[];
  webhookUrl: string;
  secret?: string;
}

// ─── Notification Endpoints ──────────────────────────────────────────

@route("/api/v1/notifications")
@tag("Notifications")
interface NotificationManagement {
  /** Register a webhook for notification types. */
  @route("subscribe")
  @post
  subscribe(@body body: CreateSubscriptionRequest): NotificationSubscription;

  /** List active subscriptions. */
  @route("subscriptions")
  @get
  listSubscriptions(): NotificationSubscription[];

  /** Remove a subscription. */
  @route("subscriptions/{id}")
  @delete
  removeSubscription(@path id: string): void;

  /** Get notification payload schemas. */
  @route("schemas")
  @get
  getSchemas(): Record<unknown>;

  /** Acknowledge a notification. */
  @route("{id}/ack")
  @post
  acknowledge(@path id: string): void;
}
