using Amazon.Lambda.CloudWatchEvents;

namespace Amazon.Lambda.CloudWatchEvents.S3Events
{
    /// <summary>
    /// This class represents an S3 object restore event sent via EventBridge.
    /// </summary>
    public class S3ObjectRestoreEvent : CloudWatchEvent<S3ObjectRestore> {}
}
