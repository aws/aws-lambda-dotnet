namespace Amazon.Lambda.CloudWatchLogsEvents
{
    /// <summary>
    /// AWS CloudWatch Logs event
    /// http://docs.aws.amazon.com/AmazonCloudWatch/latest/logs/Subscriptions.html
    /// http://docs.aws.amazon.com/lambda/latest/dg/eventsources.html#eventsources-cloudwatch-logs
    /// </summary>
    public class CloudWatchLogsEvent
    {
	    /// <summary>
	    /// The Log from the CloudWatch that is invoking the Lambda function.
	    /// </summary>
	    public  Log Awslogs { get; set; }

	    /// <summary>
	    /// The class identifies the Log from the CloudWatch that is invoking the Lambda function.
	    /// </summary>
	    public class Log
	    {
		    /// <summary>
			/// The data that are base64 encoded and gziped messages in LogStreams
		    /// </summary>
		    public  string Data { get; set; }
	    }
    }
}
