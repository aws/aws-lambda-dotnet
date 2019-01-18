using System.Collections.Generic;

namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// An object representing the array properties of a job.
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_ArrayPropertiesDetail.html
    /// </summary>
    public class ArrayPropertiesDetail
    {
        /// <summary>
        /// The job index within the array that is associated with this job.
        /// This parameter is returned for array job children.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// The size of the array job. This parameter is returned for parent array jobs.
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// A summary of the number of array job children in each available job status.
        /// This parameter is returned for parent array jobs.
        /// </summary>
        public Dictionary<string, int> StatusSummary { get; set; }
    }
}