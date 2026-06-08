// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Background batcher for outbound checkpoint updates. Operations are enqueued
/// via <see cref="EnqueueAsync"/>; a single worker drains the queue and flushes
/// each batch via the supplied <c>flushAsync</c> delegate. Each <c>EnqueueAsync</c>
/// call awaits the flush of its containing batch (sync semantics).
/// </summary>
/// <remarks>
/// Fire-and-forget semantics are achieved by simply not awaiting the returned
/// Task. Errors still surface deterministically via <c>_terminalError</c>: the
/// next sync <see cref="EnqueueAsync"/> or <see cref="DrainAsync"/> rethrows.
/// Callers using fire-and-forget should observe the discarded Task's exception
/// (see <c>StepOperation.FireAndForget</c>) so it doesn't trip the runtime's
/// <c>UnobservedTaskException</c> event.
/// </remarks>
internal sealed class CheckpointBatcher : IAsyncDisposable
{
    private readonly Func<string?, IReadOnlyList<SdkOperationUpdate>, CancellationToken, Task<string?>> _flushAsync;
    private readonly CheckpointBatcherConfig _config;
    private readonly Channel<BatchItem> _channel;
    private readonly Task _worker;
    private readonly CancellationTokenSource _shutdownCts = new();

    private string? _checkpointToken;
    private Exception? _terminalError;
    private int _disposed;

    // Per-update wire-footprint estimate constants. Deliberate over-estimates:
    // flushing slightly early is safe, flushing late risks a request-too-large.
    private const int PerOpEnvelopeOverheadBytes = 512;
    private const int StackFrameOverheadBytes = 8;

    /// <summary>
    /// Cheap UTF-8 byte estimate of one update's wire footprint — variable string
    /// fields plus a fixed envelope. No JSON is produced (AOT-safe). Payload is
    /// counted at 2x because it is already-serialized JSON re-escaped as a string
    /// value, which roughly doubles for escape-heavy content.
    /// </summary>
    private static int EstimateUpdateBytes(SdkOperationUpdate u)
    {
        var size = PerOpEnvelopeOverheadBytes;
        // int arithmetic is safe: payloads are bounded by the 6MB Lambda
        // invocation-payload cap, so the 2x multiply can never overflow a 32-bit int.
        if (u.Payload != null) size += System.Text.Encoding.UTF8.GetByteCount(u.Payload) * 2;
        size += ByteCount(u.Id) + ByteCount(u.ParentId) + ByteCount(u.Name);
        if (u.Error != null)
        {
            size += ByteCount(u.Error.ErrorType) + ByteCount(u.Error.ErrorMessage) + ByteCount(u.Error.ErrorData);
            if (u.Error.StackTrace != null)
                foreach (var line in u.Error.StackTrace)
                    size += ByteCount(line) + StackFrameOverheadBytes;
        }
        return size;
    }

    private static int ByteCount(string? s) => s == null ? 0 : System.Text.Encoding.UTF8.GetByteCount(s);

    public CheckpointBatcher(
        string? initialCheckpointToken,
        Func<string?, IReadOnlyList<SdkOperationUpdate>, CancellationToken, Task<string?>> flushAsync,
        CheckpointBatcherConfig? config = null)
    {
        _checkpointToken = initialCheckpointToken;
        _flushAsync = flushAsync;
        _config = config ?? new CheckpointBatcherConfig();
        _channel = Channel.CreateUnbounded<BatchItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _worker = Task.Run(() => RunWorkerAsync(_shutdownCts.Token));
    }

    /// <summary>
    /// The most recent checkpoint token returned by the service. Updated after
    /// every successful batch flush.
    /// </summary>
    public string? CheckpointToken => Volatile.Read(ref _checkpointToken);

    /// <summary>
    /// Queues <paramref name="update"/> for flushing. The returned Task completes
    /// when the batch containing this update has been successfully flushed to the
    /// service. If the worker has already encountered a terminal error, the
    /// exception is rethrown immediately.
    /// </summary>
    public async Task EnqueueAsync(SdkOperationUpdate update, CancellationToken cancellationToken = default)
    {
        var terminal = Volatile.Read(ref _terminalError);
        if (terminal != null) ExceptionDispatchInfo.Throw(terminal);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var item = new BatchItem(update, tcs);

        if (!_channel.Writer.TryWrite(item))
        {
            // Writer is completed (terminal error or disposed) — surface the cause.
            terminal = Volatile.Read(ref _terminalError);
            if (terminal != null) ExceptionDispatchInfo.Throw(terminal);
            throw new ObjectDisposedException(nameof(CheckpointBatcher));
        }

        await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes the channel and awaits the worker. Any items already enqueued are
    /// flushed; any subsequent <see cref="EnqueueAsync"/> call throws.
    /// </summary>
    public async Task DrainAsync()
    {
        _channel.Writer.TryComplete();
        try
        {
            await _worker.ConfigureAwait(false);
        }
        catch
        {
            // Surfaced via _terminalError below.
        }

        var terminal = Volatile.Read(ref _terminalError);
        if (terminal != null) ExceptionDispatchInfo.Throw(terminal);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _channel.Writer.TryComplete();
        _shutdownCts.Cancel();
        try { await _worker.ConfigureAwait(false); }
        catch { /* swallow on dispose */ }
        _shutdownCts.Dispose();
    }

    private async Task RunWorkerAsync(CancellationToken shutdownToken)
    {
        // Both caps are enforced: before adding an item that would push the batch
        // over MaxBatchOperations OR MaxBatchBytes, the current batch is flushed.
        // A lone item already over the byte cap is sent by itself (never loops).
        // The byte accumulator is seeded with a fixed reserve covering the request
        // prefix (checkpoint token + ARN + array framing) that the per-update
        // estimate does not include.
        const int RequestEnvelopeReserveBytes = 4 * 1024;
        var batch = new PendingBatch(_config.MaxBatchOperations);

        async Task AddItemAsync(BatchItem item)
        {
            var itemBytes = EstimateUpdateBytes(item.Update);
            if (batch.Count > 0 &&
                (batch.Count + 1 > _config.MaxBatchOperations ||
                 RequestEnvelopeReserveBytes + batch.Bytes + itemBytes > _config.MaxBatchBytes))
            {
                await FlushBatchAsync(batch.Items, shutdownToken).ConfigureAwait(false);
                batch.Clear();
            }

            batch.Add(item);

            // Lone item already over the cap: send it alone, do not loop.
            if (batch.Count == 1 &&
                RequestEnvelopeReserveBytes + batch.Bytes > _config.MaxBatchBytes)
            {
                await FlushBatchAsync(batch.Items, shutdownToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        try
        {
            while (await _channel.Reader.WaitToReadAsync(shutdownToken).ConfigureAwait(false))
            {
                while (_channel.Reader.TryRead(out var item))
                    await AddItemAsync(item).ConfigureAwait(false);

                // Optionally wait for late arrivals to coalesce into one batch.
                if (_config.FlushInterval > TimeSpan.Zero && batch.Count > 0)
                {
                    using var windowCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
                    windowCts.CancelAfter(_config.FlushInterval);
                    try
                    {
                        while (await _channel.Reader.WaitToReadAsync(windowCts.Token).ConfigureAwait(false))
                        {
                            while (_channel.Reader.TryRead(out var item))
                                await AddItemAsync(item).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) when (!shutdownToken.IsCancellationRequested)
                    {
                        // Window elapsed; fall through to flush.
                    }
                }

                if (batch.Count > 0)
                {
                    await FlushBatchAsync(batch.Items, shutdownToken).ConfigureAwait(false);
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
        {
            // Disposed mid-wait; fall through to drain.
        }
        catch (Exception ex)
        {
            // FlushBatchAsync's exception path already records _terminalError and
            // signals batch members. This catch covers anything else (channel,
            // logic). Make sure we still propagate.
            Volatile.Write(ref _terminalError, ex);
        }
        finally
        {
            // Anything left in the batch/channel after the worker exits — fail it.
            var failure = Volatile.Read(ref _terminalError) ?? new ObjectDisposedException(nameof(CheckpointBatcher));
            foreach (var leftover in batch.Items)
                leftover.Completion.TrySetException(failure);
            while (_channel.Reader.TryRead(out var item))
                item.Completion.TrySetException(failure);

            _channel.Writer.TryComplete();
        }
    }

    private async Task FlushBatchAsync(IReadOnlyList<BatchItem> batch, CancellationToken cancellationToken)
    {
        var updates = new SdkOperationUpdate[batch.Count];
        for (int i = 0; i < batch.Count; i++)
            updates[i] = batch[i].Update;

        try
        {
            var newToken = await _flushAsync(_checkpointToken, updates, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _checkpointToken, newToken);
            foreach (var item in batch)
                item.Completion.TrySetResult(true);
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _terminalError, ex);
            foreach (var item in batch)
                item.Completion.TrySetException(ex);
            _channel.Writer.TryComplete();
            // No rethrow: the worker loop exits via the completed channel and
            // RunWorkerAsync's finally handles any leftovers.
        }
    }

    /// <summary>Accumulates a batch plus its estimated byte footprint so the two
    /// never drift across the worker's add/flush/clear sites.</summary>
    private sealed class PendingBatch
    {
        public readonly List<BatchItem> Items;
        public long Bytes;
        public PendingBatch(int capacity) { Items = new List<BatchItem>(capacity); }
        public int Count => Items.Count;
        public void Add(BatchItem item) { Items.Add(item); Bytes += EstimateUpdateBytes(item.Update); }
        public void Clear() { Items.Clear(); Bytes = 0; }
    }

    private readonly record struct BatchItem(SdkOperationUpdate Update, TaskCompletionSource<bool> Completion);
}
