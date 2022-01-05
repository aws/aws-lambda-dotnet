using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.SQSEvents
{
    /// This class can be used as the return type for Lambda functions that have partially
    /// succeeded by supplying a list of message IDs that have failed to process.
    /// https://docs.aws.amazon.com/lambda/latest/dg/with-sqs.html#services-sqs-batchfailurereporting
    [DataContract]
    public class SQSBatchResponse
    {
        /// <summary>
        /// Creates a new instance of <see cref="SQSBatchResponse"/>
        /// </summary>
        public SQSBatchResponse()
            : this(new List<BatchItemFailure>())
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="SQSBatchResponse"/>
        /// </summary>
        /// <param name="batchItemFailures">A list of batch item failures</param>
        public SQSBatchResponse(List<BatchItemFailure> batchItemFailures)
        {
            BatchItemFailures = batchItemFailures;
        }

        /// <summary>
        /// Gets or sets the message failures within the batch failures
        /// </summary>
        [DataMember(Name = "batchItemFailures")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("batchItemFailures")]
#endif
        public List<BatchItemFailure> BatchItemFailures { get; set; }

        /// <summary>
        /// Class representing a SQS message item failure
        /// </summary>
        [DataContract]
        public class BatchItemFailure
        {
            /// <summary>
            /// MessageId that failed processing
            /// </summary>
            [DataMember(Name = "itemIdentifier")]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("itemIdentifier")]
#endif
            public string ItemIdentifier { get; set; }
        }
    }
}
