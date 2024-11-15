using System;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.Bootstrap;

namespace Amazon.Lambda.RuntimeSupport.Helpers
{
#if NET8_0_OR_GREATER
    /// <summary>
    /// Anywhere this class is used in RuntimeSupport it should be wrapped around a try/catch block catching TypeLoadException. 
    /// If the version of Amazon.Lambda.Core in the deployment bundle is out of date the type that is accessing SnapshotRestore
    /// will throw a TypeLoadException when the type is loaded. This extra layer for accessing SnapshotRestore is used so 
    /// classes like LambdaBootstrap can attempt accessing SnapshotRestore and catch the TypeLoadException if the type does not exist.
    /// If LambdaBootstrap was to directly access SnapshotRestore from Amazon.Lambda.Core a TypeLoadException would be thrown
    /// when LambdaBootstrap is loaded.
    /// </summary>
    internal static class SnapstartHelperInitializeWithSnapstartIsolatedAsync
    {
        /// <summary>
        /// This function will invoke the beforeSnapshot hooks, restore lambda context and run the afterRestore hooks.
        /// This will be used when SnapStart is enabled
        /// </summary>
        internal static async Task<bool> InitializeWithSnapstartAsync(IRuntimeApiClient client, object restoreHooksRegistry)
        {
            restoreHooksRegistry = restoreHooksRegistry  == null ? new SnapshotRestore.Registry.RestoreHooksRegistry() : restoreHooksRegistry;
            var logger = InternalLogger.GetDefaultLogger();
            try
            {
                await ((SnapshotRestore.Registry.RestoreHooksRegistry)restoreHooksRegistry).InvokeBeforeSnapshotCallbacks();
                await client.RestoreNextInvocationAsync();
            }
            catch (Exception ex)
            {
                client.ConsoleLogger.FormattedWriteLine(LogLevelLoggerWriter.LogLevel.Error.ToString(), ex, 
                    $"Failed to invoke before snapshot hooks: {ex}");                
                await client.ReportInitializationErrorAsync(ex, Constants.LAMBDA_ERROR_TYPE_BEFORE_SNAPSHOT);
                return false;
            }
            try
            {
                await ((SnapshotRestore.Registry.RestoreHooksRegistry)restoreHooksRegistry).InvokeAfterRestoreCallbacks();
            }
            catch (Exception ex)
            {
                client.ConsoleLogger.FormattedWriteLine(LogLevelLoggerWriter.LogLevel.Error.ToString(), ex, 
                    $"Failed to invoke after restore callables: {ex}");                
                await client.ReportRestoreErrorAsync(ex, Constants.LAMBDA_ERROR_TYPE_AFTER_RESTORE);
                return false;         
            }

            return true;
        }
    }
#endif
}
