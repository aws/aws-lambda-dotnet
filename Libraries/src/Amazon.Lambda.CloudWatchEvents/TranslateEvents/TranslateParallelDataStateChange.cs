using System;

namespace Amazon.Lambda.CloudWatchEvents.TranslateEvents
{
    /// <summary>
    /// This class represents the details of a Translate Parallel Data State Change 
    //  for CreateParallelData and UpdateParallelData events sent via EventBridge.
    /// For more see - https://docs.aws.amazon.com/translate/latest/dg/monitoring-with-eventbridge.html
    /// </summary>
    public class TranslateParallelDataStateChange
    {
        /// <summary>
        /// The CreateParallelData/UpdateParallelData operation.
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// The CreateParallelData/UpdateParallelData name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The CreateParallelData/UpdateParallelData status.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// The UpdateParallelData latest update attempt status.
        /// </summary>
        public string LatestUpdateAttemptStatus { get; set; }

        /// <summary>
        /// The UpdateParallelData latest update attempt at.
        /// </summary>
        public DateTime LatestUpdateAttemptAt { get; set; }
    }
}
