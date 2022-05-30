using Amazon.Lambda.CloudWatchEvents;

namespace Amazon.Lambda.CloudWatchEvents.S3Events
{
    /// <summary>
    /// This class represents an S3 object delete event sent via EventBridge.
    /// </summary>
    public class S3ObjectDeleteEvent : CloudWatchEvent<S3ObjectDelete> {}
}
