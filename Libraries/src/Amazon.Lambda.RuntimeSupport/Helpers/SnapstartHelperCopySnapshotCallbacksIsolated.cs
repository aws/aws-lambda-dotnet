using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.RuntimeSupport.Helpers
{
#if NET8_0_OR_GREATER
    internal static class SnapstartHelperCopySnapshotCallbacksIsolated
    {
        internal static object CopySnapshotCallbacks()
        {
            var logger = InternalLogger.GetDefaultLogger();
            var restoreHooksRegistry = new SnapshotRestore.Registry.RestoreHooksRegistry(logger.LogInformation);
            Core.SnapshotRestore.CopyBeforeSnapshotCallbacksToRegistry(restoreHooksRegistry.RegisterBeforeSnapshot);
            Core.SnapshotRestore.CopyAfterRestoreCallbacksToRegistry(restoreHooksRegistry.RegisterAfterRestore);

            return restoreHooksRegistry;
        }
    }
#endif
}
