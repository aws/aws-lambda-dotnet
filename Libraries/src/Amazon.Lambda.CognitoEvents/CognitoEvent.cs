namespace Amazon.Lambda.CognitoEvents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// AWS Cognito event
    /// http://docs.aws.amazon.com/cognito/latest/developerguide/cognito-events.html
    /// http://docs.aws.amazon.com/lambda/latest/dg/eventsources.html#eventsources-cognito-sync-trigger
    /// </summary>
    public class CognitoEvent
    {
        /// <summary>
        /// The data set name of the event.
        /// </summary>
        public string DatasetName { get; set; }

        /// <summary>
        /// The map of data set records for the event.
        /// </summary>
        public IDictionary<string, DatasetRecord> DatasetRecords { get; set; }

        /// <summary>
        /// The event type.
        /// </summary>
        public string EventType { get; set; }

        /// <summary>
        /// The identity pool ID associated with the data set.
        /// </summary>
        public string IdentityId { get; set; }

        /// <summary>
        /// The identity pool ID associated with the data set.
        /// </summary>
        public string IdentityPoolId { get; set; }

        /// <summary>
        /// The region in which data set resides.
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// The event version.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Data set records for the event.
        /// </summary>
        public class DatasetRecord
        {
            /// <summary>
            /// The record's new value.
            /// </summary>
            public string NewValue { get; set; }

            /// <summary>
            /// The record's old value.
            /// </summary>
            public string OldValue { get; set; }

            /// <summary>
            /// The operation associated with the record.
            /// For a new record or any updates to existing record it is set to "replace".
            /// For deleting a record it is set to "remove".
            /// </summary>
            public string Op { get; set; }
        }
    }
}
