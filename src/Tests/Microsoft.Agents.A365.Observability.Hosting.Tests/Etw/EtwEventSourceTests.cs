using Microsoft.Agents.A365.Observability.Hosting.Etw;
using System.Diagnostics.Tracing;

namespace Microsoft.Agents.A365.Observability.Hosting.Tests.Etw
{
    [TestClass]
    public class EtwEventSourceTests
    {
        private class TestEventListener : EventListener
        {
            public List<EventWrittenEventArgs> Events { get; } = new List<EventWrittenEventArgs>();

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                Events.Add(eventData);
            }
        }

        [TestMethod]
        public void SpanStop_WritesExpectedEventData()
        {
            using var listener = new TestEventListener();
            listener.EnableEvents(EtwEventSource.Log, EventLevel.Informational);

            string name = "TestSpan";
            string spanId = "span123";
            string traceId = "trace456";
            string parentSpanId = "parent789";
            string content = $@"{{
                ""traceId"": ""{traceId}"",
                ""spanId"": ""{spanId}"",
                ""parentSpanId"": ""{parentSpanId}"",
                ""name"": ""{name}"",
                ""kind"": 1,
                ""startTimeUnixNano"": 1710000000000000000,
                ""endTimeUnixNano"": 1710000000000000123,
                ""attributes"": {{ ""http.method"": ""GET"", ""http.url"": ""https://example.com"" }},
                ""status"": {{ ""code"": 1, ""message"": ""OK"" }}
            }}";

            EtwEventSource.Log.SpanStop(name, spanId, traceId, parentSpanId, content);

            // Find the event with the expected event ID
            var evt = listener.Events.Find(e => e.EventId == 1000);

            Assert.IsNotNull(evt);
            Assert.AreEqual(EventLevel.Informational, evt.Level);
            Assert.AreEqual(EventOpcode.Stop, evt.Opcode);
            Assert.AreEqual("A365 Otel span: Name={0} Id={1} Body={4}", evt.Message);

            Assert.IsNotNull(evt.Payload);
            Assert.AreEqual(5, evt.Payload.Count);
            Assert.AreEqual(name, evt.Payload[0]);
            Assert.AreEqual(spanId, evt.Payload[1]);
            Assert.AreEqual(traceId, evt.Payload[2]);
            Assert.AreEqual(parentSpanId, evt.Payload[3]);
            Assert.AreEqual(content, evt.Payload[4]);
        }
    }
}
