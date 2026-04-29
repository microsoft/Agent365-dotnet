// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using System.Diagnostics;

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Exporters;

[TestClass]
public sealed class PayloadChunkingTests
{
    private static Activity CreateActivity(string displayName, IDictionary<string, object?>? tags = null)
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Agent365Sdk.Test.Chunking",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _ => { },
            ActivityStopped = _ => { }
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("Agent365Sdk.Test.Chunking");
        var activity = source.StartActivity(displayName, ActivityKind.Client)
            ?? throw new InvalidOperationException("Failed to start activity.");
        if (tags != null)
        {
            foreach (var kvp in tags)
            {
                activity.SetTag(kvp.Key, kvp.Value);
            }
        }
        activity.Stop();
        return activity;
    }

    // -----------------------------------------------------------------------
    // EstimateValueBytes
    // -----------------------------------------------------------------------

    [TestMethod]
    public void EstimateValueBytes_String_ReturnsHeaderPlusUtf8ByteCount()
    {
        PayloadChunking.EstimateValueBytes("hello").Should().Be(40 + 5);
    }

    [TestMethod]
    public void EstimateValueBytes_EmptyArray_ReturnsBaseOverhead()
    {
        PayloadChunking.EstimateValueBytes(Array.Empty<string>()).Should().Be(60);
    }

    [TestMethod]
    public void EstimateValueBytes_StringArray_SumsPerElementOverhead()
    {
        PayloadChunking.EstimateValueBytes(new[] { "a", "bb" })
            .Should().Be(60 + (40 + 1) + (40 + 2));
    }

    [TestMethod]
    public void EstimateValueBytes_NumericArray_UsesFlatPerElementCost()
    {
        PayloadChunking.EstimateValueBytes(new[] { 1, 2 }).Should().Be(60 + 50 * 2);
    }

    [TestMethod]
    public void EstimateValueBytes_Scalars_ReturnsScalarOverhead()
    {
        PayloadChunking.EstimateValueBytes(true).Should().Be(40);
        PayloadChunking.EstimateValueBytes(42).Should().Be(40);
        PayloadChunking.EstimateValueBytes(null).Should().Be(40);
    }

    // -----------------------------------------------------------------------
    // EstimateActivityBytes
    // -----------------------------------------------------------------------

    [TestMethod]
    public void EstimateActivityBytes_GrowsWithAttributeContent()
    {
        var small = CreateActivity("test", new Dictionary<string, object?> { ["key"] = "val" });
        var large = CreateActivity("test", new Dictionary<string, object?> { ["key"] = new string('x', 10_000) });

        PayloadChunking.EstimateActivityBytes(large)
            .Should().BeGreaterThan(PayloadChunking.EstimateActivityBytes(small));
    }

    [TestMethod]
    public void EstimateActivityBytes_OverEstimatesActualJsonSize()
    {
        // A representative span: many attributes including some large strings.
        var attrs = new Dictionary<string, object?>
        {
            ["gen_ai.system"] = "openai",
            ["gen_ai.tool.arguments"] = new string('x', 1000),
            ["gen_ai.tool.call_result"] = new string('y', 1000),
        };
        var activity = CreateActivity("test", attrs);

        // The estimator targets the eventual OTLP JSON span shape (with traceId, spanId,
        // status, scope wrapper, etc.). A simple Dictionary serialization is a strict
        // lower bound, so the estimate should comfortably exceed it.
        var dictJson = System.Text.Json.JsonSerializer.Serialize(attrs);
        var actualLowerBound = System.Text.Encoding.UTF8.GetByteCount(dictJson);

        PayloadChunking.EstimateActivityBytes(activity)
            .Should().BeGreaterThan(actualLowerBound);
    }

    [TestMethod]
    public void EstimateActivityBytes_NullActivity_Throws()
    {
        var act = () => PayloadChunking.EstimateActivityBytes(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------
    // ChunkBySize
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ChunkBySize_EmptyInput_ReturnsEmptyOutput()
    {
        var result = PayloadChunking.ChunkBySize(Array.Empty<int>(), x => x, 900_000);
        result.Should().BeEmpty();
    }

    [TestMethod]
    public void ChunkBySize_SmallItemsFitInOneChunk()
    {
        var items = Enumerable.Range(0, 10).Select(i => 100).ToList();
        var chunks = PayloadChunking.ChunkBySize(items, x => x, 900_000);
        chunks.Should().HaveCount(1);
        chunks[0].Should().HaveCount(10);
    }

    [TestMethod]
    public void ChunkBySize_SplitsWhenCumulativeExceedsLimitAndPreservesOrder()
    {
        var items = Enumerable.Range(0, 5).Select(i => (Id: $"s{i}", Size: 300_000L)).ToList();
        var chunks = PayloadChunking.ChunkBySize(items, x => x.Size, 900_000);
        chunks.Count.Should().BeGreaterOrEqualTo(2);
        chunks.SelectMany(c => c).Select(x => x.Id)
            .Should().Equal("s0", "s1", "s2", "s3", "s4");
    }

    [TestMethod]
    public void ChunkBySize_OversizeSingleItemGetsItsOwnChunk()
    {
        var chunks = PayloadChunking.ChunkBySize(
            new[] { (Id: "big", Size: 2_000_000L) },
            x => x.Size,
            900_000);
        chunks.Should().HaveCount(1);
        chunks[0][0].Id.Should().Be("big");
    }

    [TestMethod]
    public void ChunkBySize_MultiItemChunksRespectLimitAndAreNonEmpty()
    {
        var items = Enumerable.Range(0, 5).Select(i => (Id: $"s{i}", Size: 200_000L)).ToList();
        var chunks = PayloadChunking.ChunkBySize(items, x => x.Size, 500_000);

        foreach (var chunk in chunks)
        {
            chunk.Count.Should().BeGreaterThan(0);
            if (chunk.Count > 1)
            {
                chunk.Sum(x => x.Size).Should().BeLessOrEqualTo(500_000);
            }
        }
    }

    [TestMethod]
    public void ChunkBySize_NullItems_Throws()
    {
        var act = () => PayloadChunking.ChunkBySize<int>(null!, x => x, 100);
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void ChunkBySize_NullGetSize_Throws()
    {
        var act = () => PayloadChunking.ChunkBySize<int>(new[] { 1 }, null!, 100);
        act.Should().Throw<ArgumentNullException>();
    }
}
