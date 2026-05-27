#!/usr/bin/env python3
"""
Mock customer webhook server.
Receives forwarded activities/notifications from the sidecar and prints them.
Run this FIRST, then start the sidecar pointing A365_CUSTOMER_WEBHOOK at this.
"""
import json
from http.server import HTTPServer, BaseHTTPRequestHandler


class WebhookHandler(BaseHTTPRequestHandler):
    def do_POST(self):
        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length)
        
        print(f"\n{'='*60}")
        print(f"📨 Received {self.command} {self.path}")
        print(f"   Headers: {dict(self.headers)}")
        try:
            payload = json.loads(body)
            print(f"   Body: {json.dumps(payload, indent=2)[:500]}")
        except Exception:
            print(f"   Body (raw): {body[:200]}")
        print(f"{'='*60}\n")
        
        # Return a simple reply (sidecar expects JSON response for turns)
        response = json.dumps({
            "activities": [
                {
                    "type": "message",
                    "text": "Got it! Echo from mock webhook."
                }
            ]
        })
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(response.encode())


if __name__ == "__main__":
    port = 8080
    server = HTTPServer(("127.0.0.1", port), WebhookHandler)
    print(f"🎯 Mock webhook server listening on http://127.0.0.1:{port}")
    print("   Waiting for sidecar to forward activities...\n")
    server.serve_forever()
