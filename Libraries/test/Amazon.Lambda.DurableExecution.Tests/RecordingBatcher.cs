using Amazon.Lambda.DurableExecution.Internal;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Tests;

/// <summary>
/// Test helper: a <see cref="CheckpointBatcher"/> that records every flushed
/// update without making any network calls. Tests construct one of these in
/// place of a real batcher to inspect what would have been sent to the service.
/// </summary>
internal sealed class RecordingBatcher
{
    private readonly List<SdkOperationUpdate> _flushed = new();
    private readonly List<int> _flushBatchSizes = new();
    private readonly object _lock = new();

    public CheckpointBatcher Batcher { get; }

    public RecordingBatcher(CheckpointBatcherConfig? config = null)
    {
        Batcher = new CheckpointBatcher("test-token", Flush, config);
    }

    /// <summary>
    /// Cumulative list of every update that has been flushed, in order.
    /// </summary>
    public IReadOnlyList<SdkOperationUpdate> Flushed
    {
        get { lock (_lock) return _flushed.ToArray(); }
    }

    /// <summary>
    /// One entry per batch flushed, recording the batch size. With
    /// <see cref="CheckpointBatcherConfig.FlushInterval"/> = Zero (default),
    /// every <see cref="CheckpointBatcher.EnqueueAsync"/> produces one batch.
    /// </summary>
    public IReadOnlyList<int> FlushBatchSizes
    {
        get { lock (_lock) return _flushBatchSizes.ToArray(); }
    }

    private Task<string?> Flush(string? token, IReadOnlyList<SdkOperationUpdate> ops, CancellationToken ct)
    {
        lock (_lock)
        {
            _flushed.AddRange(ops);
            _flushBatchSizes.Add(ops.Count);
        }
        return Task.FromResult<string?>(token);
    }
}
