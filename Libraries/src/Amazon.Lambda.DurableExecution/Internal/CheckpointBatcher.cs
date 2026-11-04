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
/// next sync <see cref="EnqueueAsync"/> or <see cref="DrainAsync()"/> rethrows.
/// Callers using fire-and-forget should observe the discarded Task's exception
/// (see <c>StepOperation.FireAndForget</c>) so it doesn't trip the runtime's
/// <c>UnobservedTaskException</c> event.
/// A response with no checkpoint token stops all further flushes and suspends the invocation.
/// </remarks>
internal sealed class CheckpointBatcher : IAsyncDisposable
{
    private readonly Func<string?, IReadOnlyList<SdkOperationUpdate>, CancellationToken, Task<string?>> _flushAsync;
    private readonly CheckpointBatcherConfig _config;
    private readonly TerminationManager? _termination;
    private readonly Channel<BatchItem> _channel;
    private readonly Task _worker;
    private readonly CancellationTokenSource _shutdownCts = new();

    // Cancelled only when the worker is abandoned (Lambda Managed Instances
    // cleanup budget exceeded). Never disposed: it is cheap, and abandoned
    // awaiters may still be registered on it.
    private readonly CancellationTokenSource _abandonCts = new();

    private string? _checkpointToken;
    private Exception? _terminalError;
    private int _disposed;
    private int _abandoned;
    private int _tokenMissing;

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
        CheckpointBatcherConfig? config = null,
        TerminationManager? termination = null)
    {
        _checkpointToken = initialCheckpointToken;
        _flushAsync = flushAsync;
        _config = config ?? new CheckpointBatcherConfig();
        _termination = termination;
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

    /// <summary>True once a checkpoint response arrived without a token.</summary>
    public bool IsCheckpointTokenMissing => Volatile.Read(ref _tokenMissing) == 1;

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
            if (!IsCheckpointTokenMissing) throw new ObjectDisposedException(nameof(CheckpointBatcher));
        }

        // After a missing token this never completes (the invocation is suspended).
        // It is also released when the worker is abandoned: a stalled flush may
        // never return, and the waiting workflow must still be able to unwind
        // rather than being kept alive by that request.
        var completion = tcs.Task.WaitAsync(cancellationToken);
        try
        {
            await completion.WaitAsync(_abandonCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_abandonCts.IsCancellationRequested && !completion.IsCompleted)
        {
            // The worker may still fail this item later; observe that so it never
            // reaches TaskScheduler.UnobservedTaskException.
            ObserveFault(completion);
            throw new OperationCanceledException(
                "The checkpoint was not confirmed before the invocation's cleanup budget ran out.", _abandonCts.Token);
        }
    }

    private static void ObserveFault(Task task) =>
        task.ContinueWith(
            static t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

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

    /// <summary>
    /// Like <see cref="DrainAsync()"/>, but gives up after <paramref name="timeout"/>.
    /// Used for Lambda Managed Instances, where a stalled checkpoint call must not
    /// hold the invocation open past its deadline.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the worker finished. <c>false</c> when the timeout elapsed:
    /// the in-flight flush is cancelled and the worker is abandoned, so any
    /// checkpoint it had not yet sent is not persisted (replay re-runs that work).
    /// </returns>
    public async Task<bool> DrainAsync(TimeSpan timeout)
    {
        _channel.Writer.TryComplete();
        try
        {
            await _worker.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Abandon();
            return false;
        }
        catch
        {
            // Surfaced via _terminalError below.
        }

        var terminal = Volatile.Read(ref _terminalError);
        if (terminal != null) ExceptionDispatchInfo.Throw(terminal);
        return true;
    }

    private void Abandon()
    {
        if (Interlocked.Exchange(ref _abandoned, 1) != 0) return;

        // Release every EnqueueAsync awaiter, in flight or queued. Only the SDK's
        // own WaitAsync promises are registered on this token, so cancelling it
        // synchronously runs no foreign code.
        _abandonCts.Cancel();

        // Ask the in-flight flush to stop. CancelAsync runs the callbacks the
        // service client registered on the threadpool, so a callback that blocks
        // or throws cannot stall the drain past its budget. A flush that ignores
        // the token keeps running until it returns; its outcome is not awaited.
        // The source is never disposed once abandoned: the worker may still read it.
        ObserveFault(_shutdownCts.CancelAsync());
        ObserveFault(_worker);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _channel.Writer.TryComplete();

        // An abandoned worker may never finish. Don't wait for it, and leave its
        // cancellation source undisposed because the worker may still read the token.
        if (Volatile.Read(ref _abandoned) == 1) return;

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
            // Anything left in the batch/channel after the worker exits — fail it,
            // unless the token went missing (those awaiters stay suspended).
            if (!IsCheckpointTokenMissing)
            {
                var failure = Volatile.Read(ref _terminalError) ?? new ObjectDisposedException(nameof(CheckpointBatcher));
                foreach (var leftover in batch.Items)
                    leftover.Completion.TrySetException(failure);
                while (_channel.Reader.TryRead(out var item))
                    item.Completion.TrySetException(failure);
            }

            _channel.Writer.TryComplete();
        }
    }

    private async Task FlushBatchAsync(IReadOnlyList<BatchItem> batch, CancellationToken cancellationToken)
    {
        if (IsCheckpointTokenMissing) return;

        var updates = new SdkOperationUpdate[batch.Count];
        for (int i = 0; i < batch.Count; i++)
            updates[i] = batch[i].Update;

        try
        {
            // Never start a new request once the worker is shut down or abandoned.
            // If an abandoned flush ignored cancellation and later returned, the
            // worker would otherwise go on to send queued batches with this
            // invocation's checkpoint token, racing the invocation that replays it.
            // Throwing here routes through the catch below, which fails the batch.
            cancellationToken.ThrowIfCancellationRequested();

            var newToken = await _flushAsync(_checkpointToken, updates, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _checkpointToken, newToken);

            if (string.IsNullOrEmpty(newToken))
            {
                // Leave awaiters suspended so user code stops here.
                Volatile.Write(ref _tokenMissing, 1);
                _channel.Writer.TryComplete();
                _termination?.Terminate(TerminationReason.CheckpointTokenMissing);
                return;
            }

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
