using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/batch/latest/userguide/batch_cwe_events.html
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_JobDetail.html
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_DescribeJobs.html
    /// </summary>
    public class NodeProperties
    {
        /// <summary>
        /// Specifies the node index for the main node of a multi-node parallel job.
        /// </summary>
        public int MainNode { get; set; }

        /// <summary>
        /// A list of node ranges and their properties associated with a multi-node parallel job.
        /// </summary>
        public List<NodeRangeProperty> NodeRangeProperties { get; set; }

        /// <summary>
        /// The number of nodes associated with a multi-node parallel job.
        /// </summary>
        public int NumNodes { get; set; }
    }
}
