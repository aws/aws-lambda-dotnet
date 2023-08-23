namespace Amazon.Lambda.DynamoDBEvents
{
    using System;
    using System.Collections.Generic;

#if NETCOREAPP3_1_OR_GREATER
    using Amazon.Lambda.DynamoDBEvents.Converters;
    using System.Text.Json.Serialization;
#endif

    /// <summary>
    /// Represents an Amazon DynamodDB event when using time windows.
    /// https://docs.aws.amazon.com/lambda/latest/dg/with-ddb.html#services-ddb-windows
    /// </summary>
    public class DynamoDBTimeWindowEvent : DynamoDBEvent
    {
        /// <summary>
        /// Time window for the records in the event.
        /// </summary>
        public TimeWindow Window { get; set; }

        /// <summary>
        /// State being built up to this invoke in the time window.
        /// </summary>
#if NETCOREAPP3_1_OR_GREATER
        [JsonConverter(typeof(DictionaryLongToStringJsonConverter))]
#endif
        public Dictionary<string, string> State { get; set; }

        /// <summary>
        /// Shard Id of the records.
        /// </summary>
        public string ShardId { get; set; }

        /// <summary>
        /// DynamoDB stream Arn.
        /// </summary>
        public string EventSourceArn { get; set; }

        /// <summary>
        /// Set to true for the last invoke of the time window. Subsequent invoke will start a new time window along with a fresh state.
        /// </summary>
        public bool? IsFinalInvokeForWindow { get; set; }

        /// <summary>
        /// Set to true if window is terminated prematurely. Subsequent invoke will continue the same window with a fresh state.
        /// </summary>
        public bool? IsWindowTerminatedEarly { get; set; }

        /// <summary>
        /// Time window for the records in the event.
        /// </summary>
        public class TimeWindow
        {
            /// <summary>
            /// Window start instant.
            /// </summary>
            public DateTime Start { get; set; }

            /// <summary>
            /// Window end instant.
            /// </summary>
            public DateTime End { get; set; }
        }
    }
}
