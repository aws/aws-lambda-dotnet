using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SnapshotRestore.Registry;

/// <summary>
/// .NET Implementation for Registering BeforeSnapshot and AfterRestore hooks
/// for Snapstart
/// </summary>
public class RestoreHooksRegistry
{
    private ConcurrentStack<Func<ValueTask>> _beforeSnapshotRegistry = new();
    private ConcurrentQueue<Func<ValueTask>> _afterRestoreRegistry = new();

    private Action<string> _logger;

    /// <summary>
    /// Creates an instance of RestoreHooksRegistry.
    /// </summary>
    /// <param name="logger">An optional callback logger.</param>
    public RestoreHooksRegistry(Action<string> logger = null)
    {
        _logger = logger ?? (x => { });
    }

    /// <summary>
    /// Register a ValueTask by adding it into the Before Snapshot Registry
    /// </summary>
    /// <param name="func"></param>
    public void RegisterBeforeSnapshot(Func<ValueTask> func)
    {
        _beforeSnapshotRegistry.Push(func);
    }
    /// <summary>
    /// Register a ValueTask by adding it into the After Restore Registry
    /// </summary>
    /// <param name="func"></param>
    public void RegisterAfterRestore(Func<ValueTask> func)
    {
        _afterRestoreRegistry.Enqueue(func);
    }

    /// <summary>
    /// Invoke all the registered before snapshot callbacks.
    /// </summary>
    /// <returns></returns>
    public async Task InvokeBeforeSnapshotCallbacks()
    {
        if (_beforeSnapshotRegistry != null)
        {
            _logger($"Invoking {_beforeSnapshotRegistry.Count} beforeSnapshotCallables");
            while (_beforeSnapshotRegistry.TryPop(out var beforeSnapshotCallable))
            {
                _logger($"Calling beforeSnapshotCallable: {beforeSnapshotCallable.Method.Name}");
                await beforeSnapshotCallable();
            }
        }
    }

    /// <summary>
    /// Invoke all the registered after restore callbacks.
    /// </summary>
    /// <returns></returns>
    public async Task InvokeAfterRestoreCallbacks()
    {
        if (_afterRestoreRegistry != null)
        {
            _logger($"Invoking {_afterRestoreRegistry.Count} afterRestoreCallables");
            while (_afterRestoreRegistry.TryDequeue(out var afterRestoreCallable))
            {
                _logger($"Calling afterRestoreCallable: {afterRestoreCallable.Method.Name}");
                await afterRestoreCallable();
            }
        }
    }
}