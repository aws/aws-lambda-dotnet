namespace Amazon.Lambda.CloudWatchLogsEvents
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Runtime.Serialization;
    using System.Text;

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
        [DataContract]
        public class Log
	    {
            /// <summary>
            /// The data that are base64 encoded and gziped messages in LogStreams.
            /// </summary>
            [DataMember(Name = "data", IsRequired = false)]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("data")]
#endif
            public string EncodedData { get; set; }

            /// <summary>
            /// Decodes the data stored in the EncodedData property.
            /// </summary>
            public string DecodeData()
            {
                if (string.IsNullOrEmpty(this.EncodedData))
                    return this.EncodedData;

                var bytes = Convert.FromBase64String(this.EncodedData);
                var uncompressedStream = new MemoryStream();

                using (var stream = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress))
                {
                    stream.CopyTo(uncompressedStream);
                    uncompressedStream.Position = 0;
                }

                var decodedString = Encoding.UTF8.GetString(uncompressedStream.ToArray());
                return decodedString;
            }
	    }
    }
}
