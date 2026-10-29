using System.Collections.Concurrent;

namespace AWS.OCI.Builder;

/// <summary>
/// A thread-safe <see cref="IProgress{T}"/> sink that queues push progress events for the caller to read, instead
/// of invoking a callback.
/// </summary>
/// <remarks>
/// <para>
/// Use this when callbacks on arbitrary threads are a problem, for example from PowerShell, where a scriptblock
/// invoked on a thread-pool thread has no runspace. Pass it as <see cref="EcrPushOptions.Progress"/>, then call
/// <see cref="TryDequeue"/> or <see cref="DequeueAll"/> from your own thread while the push runs (and once more
/// after it completes) to drain the events in the order they were reported.
/// </para>
/// <para>
/// The queue is unbounded. A push reports one event each time the HTTP stack reads from the layer, plus a few per
/// layer. On .NET 10 those reads are 128 KiB, so a 1 GiB layer queues roughly 8,000 events (about half a
/// megabyte) if they are never read.
/// </para>
/// </remarks>
public sealed class LayerPushProgressQueue : IProgress<LayerPushProgress>
{
    private readonly ConcurrentQueue<LayerPushProgress> _events = new();

    /// <summary>The number of events waiting to be read.</summary>
    public int Count => _events.Count;

    /// <summary>Queues an event. Called by the push; safe to call from any thread.</summary>
    public void Report(LayerPushProgress value) => _events.Enqueue(value);

    /// <summary>Removes and returns the oldest queued event, if any.</summary>
    /// <param name="value">The event, when one was queued.</param>
    /// <returns><see langword="true"/> when an event was returned.</returns>
    public bool TryDequeue(out LayerPushProgress value) => _events.TryDequeue(out value);

    /// <summary>Removes and returns every queued event, oldest first (an empty array when none are queued).</summary>
    /// <remarks>Convenient from PowerShell: <c>foreach ($p in $queue.DequeueAll()) { ... }</c>.</remarks>
    public LayerPushProgress[] DequeueAll()
    {
        var drained = new List<LayerPushProgress>(_events.Count);
        while (_events.TryDequeue(out LayerPushProgress value))
        {
            drained.Add(value);
        }
        return drained.ToArray();
    }
}
