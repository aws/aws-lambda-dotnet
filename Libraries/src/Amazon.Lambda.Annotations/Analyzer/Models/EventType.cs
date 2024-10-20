namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// Type of Lambda event
    /// <see href="https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/sam-property-function-eventsource.html">Supported Lambda events</see>
    /// </summary>
    public enum EventType
    {
        API,
        S3,
        SQS,
        DynamoDB,
        Schedule
    }
}