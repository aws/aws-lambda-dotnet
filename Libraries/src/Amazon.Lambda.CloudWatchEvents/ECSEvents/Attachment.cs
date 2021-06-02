using System.Collections.Generic;

namespace Amazon.Lambda.CloudWatchEvents.ECSEvents
{
    /// <summary>
    /// An object representing a container instance or task attachment.
    /// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_Attachment.html
    /// </summary>
    public class Attachment
    {
        /// <summary>
        /// Details of the attachment. For elastic network interfaces, this includes the
        /// network interface ID, the MAC address, the subnet ID, and the private IPv4 address. 
        /// Type of underlying object KeyValuePair (https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_KeyValuePair.html)
        /// </summary>
        public List<NameValue> Details { get; set; }

        /// <summary>
        /// The unique identifier for the attachment.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The status of the attachment. Valid values are PRECREATED, CREATED, ATTACHING,
        /// ATTACHED, DETACHING, DETACHED, and DELETED.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// The type of the attachment, such as ElasticNetworkInterface.
        /// </summary>
        public string Type { get; set; }
    }
}