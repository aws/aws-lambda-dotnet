using Amazon.Lambda.CloudWatchEvents;

namespace Amazon.Lambda.CloudWatchEvents.S3Events
{
    /// <summary>
    /// This class represents an S3 object event sent via EventBridge.
    /// </summary>
    public class S3ObjectEvent : CloudWatchEvent<S3ObjectEventDetails> {}
}
