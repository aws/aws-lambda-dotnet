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
    public class NodeRangeProperty
    {
        /// <summary>
        /// The container details for the node range.
        /// </summary>
        public ContainerDetail Container { get; set; }

        /// <summary>
        /// The range of nodes, using node index values. A range of <code>0:3</code> indicates
        /// nodes with index values of <code>0</code> through <code>3</code>. If the starting
        /// range value is omitted (<code>:n</code>), then <code>0</code> is used to start the
        /// range. If the ending range value is omitted (<code>n:</code>), then the highest possible
        /// node index is used to end the range. Your accumulative node ranges must account for
        /// all nodes (0:n). You may nest node ranges, for example 0:10 and 4:5, in which case
        /// the 4:5 range properties override the 0:10 properties. 
        /// </summary>
        public string TargetNodes { get; set; }
    }
}
