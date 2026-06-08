// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Internal;
using Xunit;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Tests;

public class CheckpointBatcherTests
{
    private static SdkOperationUpdate Update(string id) => new()
    {
        Id = id,
        Type = "STEP",
        Action = "SUCCEED"
    };

    [Fact]
    public async Task EnqueueAsync_AwaitsUntilBatchFlushes()
    {
        var flushedTokens = new List<string?>();
        var batcher = new CheckpointBatcher("token-0",
            (token, ops, ct) =>
            {
                flushedTokens.Add(token);
                return Task.FromResult<string?>("token-1");
            });

        await batcher.EnqueueAsync(Update("0-step"));

        Assert.Equal(new string?[] { "token-0" }, flushedTokens);
        Assert.Equal("token-1", batcher.CheckpointToken);

        await batcher.DrainAsync();
    }

    [Fact]
    public async Task MultipleEnqueueAsync_BatchedWithinWindow()
    {
        var batches = new List<int>();
        var batcher = new CheckpointBatcher("token-0",
            (token, ops, ct) =>
            {
                batches.Add(ops.Count);
                return Task.FromResult<string?>(token);
            },
            new CheckpointBatcherConfig { FlushInterval = TimeSpan.FromMilliseconds(50) });

        // Fire several enqueues concurrently and await all — they should
        // coalesce into a single batch since FlushInterval > 0.
        var tasks = Enumerable.Range(0, 5)
            .Select(i => batcher.EnqueueAsync(Update($"{i}-step")))
            .ToArray();

        await Task.WhenAll(tasks);
        await batcher.DrainAsync();

        Assert.Single(batches);
        Assert.Equal(5, batches[0]);
    }

    [Fact]
    public async Task EnqueueAsync_OverflowOps_SplitsBatches()
    {
        var batches = new List<int>();
        var batcher = new CheckpointBatcher("token-0",
            (token, ops, ct) =>
            {
                batches.Add(ops.Count);
                return Task.FromResult<string?>(token);
            },
            new CheckpointBatcherConfig
            {
                MaxBatchOperations = 3,
                FlushInterval = TimeSpan.FromMilliseconds(100)
            });

        var tasks = Enumerable.Range(0, 7)
            .Select(i => batcher.EnqueueAsync(Update($"{i}-step")))
            .ToArray();

        await Task.WhenAll(tasks);
        await batcher.DrainAsync();

        // 7 items, max 3 per batch → 3, 3, 1 (or some permutation summing to 7
        // with no batch over 3).
        Assert.Equal(7, batches.Sum());
        Assert.All(batches, count => Assert.True(count <= 3));
        Assert.True(batches.Count >= 3);
    }

    [Fact]
    public async Task FlushAsync_Throws_PropagatesToAllAwaiters()
    {
        var failure = new InvalidOperationException("service unavailable");
        var batcher = new CheckpointBatcher("token-0",
            (token, ops, ct) => Task.FromException<string?>(failure),
            new CheckpointBatcherConfig { FlushInterval = TimeSpan.FromMilliseconds(50) });

        var tasks = Enumerable.Range(0, 3)
            .Select(i => batcher.EnqueueAsync(Update($"{i}-step")))
            .ToArray();

        // Each awaiter should see the same exception.
        foreach (var t in tasks)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => t);
            Assert.Equal("service unavailable", ex.Message);
        }
    }

    [Fact]
    public async Task EnqueueAsync_AfterTerminalError_FailsFast()
    {
        var failure = new InvalidOperationException("kaboom");
        var batcher = new CheckpointBatcher("token-0",
            (token, ops, ct) => Task.FromException<string?>(failure));

        // First enqueue trips the terminal error.
        await Assert.ThrowsAsync<InvalidOperationException>(() => batcher.EnqueueAsync(Update("0-step")));

        // Subsequent enqueue should fail fast with the same exception.
        var second = await Assert.ThrowsAsync<InvalidOperationException>(() => batcher.EnqueueAsync(Update("1-step")));
        Assert.Equal("kaboom", second.Message);
    }

    [Fact]
    public async Task DrainAsync_FlushesRemainingItems()
    {
        var totalFlushed = 0;
        var batcher = new CheckpointBatcher("token-0",
            (token, ops, ct) =>
            {
                Interlocked.Add(ref totalFlushed, ops.Count);
                return Task.FromResult<string?>(token);
            });

        // Fire enqueues without awaiting them individually.
        var tasks = Enumerable.Range(0, 4)
            .Select(i => batcher.EnqueueAsync(Update($"{i}-step")))
            .ToArray();

        await batcher.DrainAsync();
        await Task.WhenAll(tasks);

        Assert.Equal(4, totalFlushed);
    }

    [Fact]
    public async Task DrainAsync_AfterTerminalError_Throws()
    {
        var failure = new InvalidOperationException("nope");
        var batcher = new CheckpointBatcher("token-0",
            (token, ops, ct) => Task.FromException<string?>(failure));

        // Trip the terminal error.
        await Assert.ThrowsAsync<InvalidOperationException>(() => batcher.EnqueueAsync(Update("0-step")));

        // Drain should rethrow.
        await Assert.ThrowsAsync<InvalidOperationException>(() => batcher.DrainAsync());
    }

    [Fact]
    public async Task EnqueueAsync_AfterDispose_Throws()
    {
        var batcher = new CheckpointBatcher("token-0",
            (token, ops, ct) => Task.FromResult<string?>(token));

        await batcher.DisposeAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => batcher.EnqueueAsync(Update("0-step")));
    }

    private static SdkOperationUpdate UpdateWithPayload(string id, int payloadBytes) => new()
    {
        Id = id,
        Type = "CONTEXT",
        Action = "SUCCEED",
        Payload = new string('p', payloadBytes)
    };

    [Fact]
    public async Task EnqueueAsync_ByteCap_SplitsBatchesByBytes()
    {
        var batchByteTotals = new List<long>();
        var batcher = new CheckpointBatcher("token-0",
            (token, ops, ct) =>
            {
                long sum = 0;
                foreach (var o in ops) sum += o.Payload?.Length ?? 0;
                batchByteTotals.Add(sum);
                return Task.FromResult<string?>(token);
            },
            new CheckpointBatcherConfig
            {
                MaxBatchBytes = 10 * 1024,
                FlushInterval = TimeSpan.FromMilliseconds(100)
            });

        // Three 6 KB payloads: at most one fits per 10 KB batch with overhead.
        var tasks = Enumerable.Range(0, 3)
            .Select(i => batcher.EnqueueAsync(UpdateWithPayload($"{i}", 6 * 1024)))
            .ToArray();
        await Task.WhenAll(tasks);
        await batcher.DrainAsync();

        Assert.True(batchByteTotals.Count >= 2, "expected the byte cap to split into multiple batches");
        Assert.All(batchByteTotals, total => Assert.True(total <= 10 * 1024));
    }

    [Fact]
    public async Task EnqueueAsync_SingleOversizedItem_SentAloneNoLoop()
    {
        var batches = new List<int>();
        var batcher = new CheckpointBatcher("token-0",
            (token, ops, ct) => { batches.Add(ops.Count); return Task.FromResult<string?>(token); },
            new CheckpointBatcherConfig { MaxBatchBytes = 4 * 1024 });

        await batcher.EnqueueAsync(UpdateWithPayload("huge", 50 * 1024));
        await batcher.DrainAsync();

        Assert.Single(batches);
        Assert.Equal(1, batches[0]);
    }

    [Fact]
    public async Task CheckpointToken_UpdatesAfterEachFlush()
    {
        var counter = 0;
        var batcher = new CheckpointBatcher("token-0",
            (token, ops, ct) =>
            {
                var next = $"token-{Interlocked.Increment(ref counter)}";
                return Task.FromResult<string?>(next);
            });

        await batcher.EnqueueAsync(Update("0-step"));
        Assert.Equal("token-1", batcher.CheckpointToken);

        await batcher.EnqueueAsync(Update("1-step"));
        Assert.Equal("token-2", batcher.CheckpointToken);

        await batcher.DrainAsync();
    }

    [Fact]
    public async Task ConcurrentEnqueueAsync_AllComplete()
    {
        var totalFlushed = 0;
        var batcher = new CheckpointBatcher("token-0",
            (token, ops, ct) =>
            {
                Interlocked.Add(ref totalFlushed, ops.Count);
                return Task.FromResult<string?>(token);
            },
            new CheckpointBatcherConfig { FlushInterval = TimeSpan.FromMilliseconds(20) });

        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() => batcher.EnqueueAsync(Update($"{i}-step"))))
            .ToArray();

        await Task.WhenAll(tasks);
        await batcher.DrainAsync();

        Assert.Equal(100, totalFlushed);
    }
}
