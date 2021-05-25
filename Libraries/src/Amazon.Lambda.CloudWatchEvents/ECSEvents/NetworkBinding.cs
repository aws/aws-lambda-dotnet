namespace Amazon.Lambda.CloudWatchEvents.ECSEvents
{
    /// <summary>
    /// Details on the network bindings between a container and its host container instance. 
    /// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_NetworkBinding.html
    /// </summary>
    public class NetworkBinding
    {
        /// <summary>
        /// The IP address that the container is bound to on the container instance.
        /// </summary>
        public string BindIP { get; set; }

        /// <summary>
        /// The port number on the container that is used with the network binding.
        /// </summary>
        public int ContainerPort { get; set; }

        /// <summary>
        /// The port number on the host that is used with the network binding.
        /// </summary>
        public int HostPort { get; set; }

        /// <summary>
        /// The protocol used for the network binding.
        /// </summary>
        public string Protocol { get; set; }
    }
}
