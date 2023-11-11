namespace Amazon.Lambda.DynamoDBEvents
{
    using Amazon.DynamoDBv2.Model;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// AWS DynamoDB event
    /// http://docs.aws.amazon.com/lambda/latest/dg/with-ddb.html
    /// http://docs.aws.amazon.com/lambda/latest/dg/eventsources.html#eventsources-ddb-update
    /// </summary>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("DynamoDBEvent has a reference to the AWS SDK for .NET. The ConstantClass used to represent enums in the SDK is not supported in the Lambda serializer SourceGeneratorLambdaJsonSerializer for trimming scenarios.")]
#endif
    public class DynamoDBEvent
    {
        /// <summary>
        /// List of DynamoDB event records.
        /// </summary>
        public IList<DynamodbStreamRecord> Records { get; set; }

        /// <summary>
        /// DynamoDB stream record
        /// http://docs.aws.amazon.com/dynamodbstreams/latest/APIReference/API_StreamRecord.html
        /// </summary>
        public class DynamodbStreamRecord : Record
        {
            /// <summary>
            /// The event source arn of DynamoDB.
            /// </summary>
            public string EventSourceArn { get; set; }
        }

    }
}
