using System.Collections.Generic;

namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// The retry strategy associated with a job.
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_RetryStrategy.html
    /// </summary>
    public class RetryStrategy
    {
        /// <summary>
        /// The number of times to move a job to the RUNNABLE status. You may specify between 1 and 10 attempts.
        /// If the value of attempts is greater than one, the job is retried if it fails until it has moved to
        /// RUNNABLE that many times.
        /// </summary>
        public int Attempts { get; set; }

        /// <summary>
        /// Array of up to 5 objects that specify conditions under which the job should be retried or failed. 
        /// If this parameter is specified, then the <c>attempts</c> parameter must also be specified.
        /// </summary>
        public List<EvaluateOnExit> EvaluateOnExit { get; set; }
    }
}