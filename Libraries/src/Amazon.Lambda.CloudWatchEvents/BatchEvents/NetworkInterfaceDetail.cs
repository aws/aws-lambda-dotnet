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
    public class NetworkInterfaceDetail
    {
        /// <summary>
        /// The attachment ID for the network interface.
        /// </summary>
        public string AttachmentId { get; set; }

        /// <summary>
        /// The private IPv6 address for the network interface.
        /// </summary>
        public string Ipv6Address { get; set; }

        /// <summary>
        /// The private IPv4 address for the network interface.
        /// </summary>
        public string PrivateIpv4Address { get; set; }
    }
}
