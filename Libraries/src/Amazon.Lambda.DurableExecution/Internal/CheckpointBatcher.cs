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
/// TODO: when Map / Parallel / ChildContext / WaitForCondition land — or when
/// AtLeastOncePerRetry step START gets a non-blocking variant — they will need
/// a fire-and-forget overload like
/// <c>Task EnqueueAsync(SdkOperationUpdate update, bool sync)</c> where
/// <c>sync=false</c> returns as soon as the item is queued. Java's
/// <c>sendOperationUpdate</c> vs <c>sendOperationUpdateAsync</c> is the model.
/// Today every call site is sync, so the API stays minimal.
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
        // TODO: also enforce _config.MaxBatchBytes here. Today we only cap by
        // operation count; an item whose serialized size pushes the batch over
        // ~750 KB will be sent and rejected service-side. See CheckpointBatcherConfig.
        var batch = new List<BatchItem>(_config.MaxBatchOperations);

        try
        {
            while (await _channel.Reader.WaitToReadAsync(shutdownToken).ConfigureAwait(false))
            {
                // Drain everything currently queued.
                while (_channel.Reader.TryRead(out var item))
                {
                    batch.Add(item);
                    if (batch.Count >= _config.MaxBatchOperations)
                    {
                        await FlushBatchAsync(batch, shutdownToken).ConfigureAwait(false);
                        batch.Clear();
                    }
                }

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
                            {
                                batch.Add(item);
                                if (batch.Count >= _config.MaxBatchOperations)
                                {
                                    await FlushBatchAsync(batch, shutdownToken).ConfigureAwait(false);
                                    batch.Clear();
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) when (!shutdownToken.IsCancellationRequested)
                    {
                        // Window elapsed; fall through to flush.
                    }
                }

                if (batch.Count > 0)
                {
                    await FlushBatchAsync(batch, shutdownToken).ConfigureAwait(false);
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
            // Anything left in the channel after the worker exits — fail it.
            var failure = Volatile.Read(ref _terminalError) ?? new ObjectDisposedException(nameof(CheckpointBatcher));
            foreach (var leftover in batch)
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

    private readonly record struct BatchItem(SdkOperationUpdate Update, TaskCompletionSource<bool> Completion);
}
