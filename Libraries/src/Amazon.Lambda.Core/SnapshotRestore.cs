using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
namespace Amazon.Lambda.Core
{
#if NET8_0_OR_GREATER
    /// <summary>
    /// Static class to register callback hooks to during the snapshot and restore phases of Lambda SnapStart. Hooks 
    /// should be registered as part of the constructor of the type containing the function handler or before the 
    /// `LambdaBootstrap` is started in executable assembly Lambda functions.    
    /// </summary>
    public static class SnapshotRestore
    {
        // We don't want Amazon.Lambda.Core to have any dependencies because the packaged handler code
        // that gets uploaded to AWS Lambda could have a version mismatch with the version that is already
        // included in the managed runtime. This class allows us to define a simple API that both the
        // RuntimeClient and handler code can use to register and then call these actions without
        // depending on a specific version of SnapshotRestore.Registry.
        private static readonly ConcurrentQueue<Func<ValueTask>> BeforeSnapshotRegistry = new();
        private static readonly ConcurrentQueue<Func<ValueTask>> AfterRestoreRegistry = new();

        internal static void CopyBeforeSnapshotCallbacksToRegistry(Action<Func<ValueTask>> restoreHooksRegistryMethod)
        {
            // To preserve the order of registry, BeforeSnapshotRegistry in Core needs to be a Queue
            // These callbacks will be added to the Stack that SnapshotRestore.Registry maintains
            while (BeforeSnapshotRegistry.TryDequeue(out var registeredAction))
            {
                restoreHooksRegistryMethod?.Invoke(registeredAction);
            }
        }

        internal static void CopyAfterRestoreCallbacksToRegistry(Action<Func<ValueTask>> restoreHooksRegistryMethod)
        {
            while (AfterRestoreRegistry.TryDequeue(out var registeredAction))
            {
                restoreHooksRegistryMethod?.Invoke(registeredAction);
            }
        }
        
        /// <summary>
        /// Register callback hook to be called before Lambda creates a snapshot of the running process. This can be used to warm code in the .NET process or close connections before the snapshot is taken.
        /// </summary>
        /// <param name="beforeSnapshotAction"></param>
        public static void RegisterBeforeSnapshot(Func<ValueTask> beforeSnapshotAction)
        {
            BeforeSnapshotRegistry.Enqueue(beforeSnapshotAction);
        }

        /// <summary>
        /// Register callback hook to be called after Lambda restores a snapshot of the running process. This can be used to ensure uniqueness after restoration. For example reseeding random number generators.
        /// </summary>
        /// <param name="afterRestoreAction"></param>
        public static void RegisterAfterRestore(Func<ValueTask> afterRestoreAction)
        {
            AfterRestoreRegistry.Enqueue(afterRestoreAction);
        }       
    }
#endif
}