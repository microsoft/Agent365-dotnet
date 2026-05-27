#!/usr/bin/env python3
"""
E2E test client for the Agent365 Sidecar.
Exercises: health, observability (OTLP traces), tooling, and notifications APIs.

Usage:
    python test_sidecar.py [sidecar_url]
    
Default sidecar_url: http://127.0.0.1:5365
"""
import json
import sys
import requests

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5365"


def test_health():
    print("\n🏥 Testing Health Endpoints...")
    
    r = requests.get(f"{BASE}/healthz")
    assert r.status_code == 200, f"healthz failed: {r.status_code}"
    print(f"   ✅ /healthz → {r.json()}")
    
    r = requests.get(f"{BASE}/readyz")
    print(f"   {'✅' if r.status_code == 200 else '⚠️'} /readyz → {r.status_code} {r.json()}")
    
    r = requests.get(f"{BASE}/api/v1/status")
    assert r.status_code == 200, f"status failed: {r.status_code}"
    print(f"   ✅ /api/v1/status → modules: {list(r.json().get('modules', {}).keys())}")


def test_observability_config():
    print("\n📡 Testing Observability Config...")
    
    r = requests.get(f"{BASE}/api/v1/observability/config")
    assert r.status_code == 200, f"config failed: {r.status_code}"
    config = r.json()
    print(f"   ✅ OTLP endpoint: {config['endpoint']}")
    print(f"   ✅ Protocol: {config['protocol']}")
    print(f"   ✅ Headers: {config['headers']}")


def test_send_traces():
    print("\n🔭 Testing OTLP Trace Ingestion...")
    
    # Empty spans — should return 200 with 0 accepted
    payload = {"resourceSpans": []}
    r = requests.post(
        f"{BASE}/api/v1/observability/v1/traces",
        json=payload,
        headers={"Content-Type": "application/json"}
    )
    assert r.status_code == 200, f"empty traces failed: {r.status_code} {r.text}"
    print(f"   ✅ Empty payload → acceptedSpans: {r.json()['acceptedSpans']}")
    
    # Payload with a real span
    payload = {
        "resourceSpans": [{
            "resource": {
                "attributes": [
                    {"key": "service.name", "value": {"stringValue": "my-python-agent"}}
                ]
            },
            "scopeSpans": [{
                "scope": {"name": "my-agent"},
                "spans": [{
                    "traceId": "0af7651916cd43dd8448eb211c80319c",
                    "spanId": "b7ad6b7169203331",
                    "name": "invoke_agent",
                    "kind": 1,
                    "startTimeUnixNano": "1700000000000000000",
                    "endTimeUnixNano": "1700000001000000000",
                    "attributes": [
                        {"key": "gen_ai.operation.name", "value": {"stringValue": "invoke_agent"}}
                    ]
                }]
            }]
        }]
    }
    r = requests.post(
        f"{BASE}/api/v1/observability/v1/traces",
        json=payload,
        headers={"Content-Type": "application/json"}
    )
    # This might return 502 if the exporter can't reach A365 (expected in local dev)
    print(f"   {'✅' if r.status_code == 200 else '⚠️'} Span payload → {r.status_code} {r.text[:100]}")


def test_tooling():
    print("\n🔧 Testing Tooling API...")
    
    r = requests.get(f"{BASE}/api/v1/tools/servers")
    # May return 500 if gateway isn't reachable — that's OK for local testing
    print(f"   {'✅' if r.status_code == 200 else '⚠️'} /api/v1/tools/servers → {r.status_code}")
    if r.status_code == 200:
        print(f"      Servers: {r.json()}")


def test_notifications():
    print("\n🔔 Testing Notifications API...")
    
    r = requests.get(f"{BASE}/api/v1/notifications/channels")
    assert r.status_code == 200, f"channels failed: {r.status_code}"
    channels = r.json()
    print(f"   ✅ Channels: {[c['name'] for c in channels['channels']]}")
    
    r = requests.get(f"{BASE}/api/v1/notifications/status")
    assert r.status_code == 200, f"status failed: {r.status_code}"
    print(f"   ✅ Status: {r.json()}")


def test_unsupported_content_type():
    print("\n🚫 Testing Error Handling...")
    
    r = requests.post(
        f"{BASE}/api/v1/observability/v1/traces",
        data=b"\x01\x02\x03",
        headers={"Content-Type": "application/x-protobuf"}
    )
    assert r.status_code != 200, "Expected rejection for protobuf"
    print(f"   ✅ Protobuf rejected → {r.status_code}")
    
    r = requests.post(
        f"{BASE}/api/v1/observability/v1/traces",
        data="",
        headers={"Content-Type": "application/json"}
    )
    assert r.status_code == 400, f"Expected 400 for empty body, got {r.status_code}"
    print(f"   ✅ Empty body → 400")


if __name__ == "__main__":
    print(f"🚀 Agent365 Sidecar E2E Test — targeting {BASE}")
    print("=" * 60)
    
    try:
        test_health()
        test_observability_config()
        test_send_traces()
        test_tooling()
        test_notifications()
        test_unsupported_content_type()
        
        print("\n" + "=" * 60)
        print("🎉 All E2E tests complete!")
    except requests.ConnectionError:
        print(f"\n❌ Cannot connect to sidecar at {BASE}")
        print("   Make sure the sidecar is running first.")
        sys.exit(1)
    except AssertionError as e:
        print(f"\n❌ Test failed: {e}")
        sys.exit(1)
