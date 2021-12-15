namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{

    /// <summary>
    /// https://docs.aws.amazon.com/batch/latest/userguide/batch_cwe_events.html
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_JobDetail.html
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_DescribeJobs.html
    /// </summary>
    public class NodeDetails
    {
        /// <summary>
        /// Specifies whether the current node is the main node for a multi-node parallel job.
        /// </summary>
        public bool IsMainNode { get; set; }

        /// <summary>
        /// The node index for the node. Node index numbering begins at zero. This index is also
        /// available on the node with the <c>AWS_BATCH_JOB_NODE_INDEX</c> environment variable.
        /// </summary>
        public int NodeIndex { get; set; }
    }
}
