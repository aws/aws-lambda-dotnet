namespace Amazon.Lambda.CloudWatchEvents.ECSEvents
{
    /// <summary>
    /// An object representing the elastic network interface for tasks that use the awsvpc network mode. 
    /// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_NetworkInterface.html
    /// </summary>
    public class NetworkInterface
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
