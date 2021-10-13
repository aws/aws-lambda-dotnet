namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// The network configuration for jobs that are running on Fargate resources. Jobs that are running on EC2 resources must not specify this parameter.
    /// </summary>
    public class NetworkConfiguration
    {
        /// <summary>
        /// Indicates whether the job should have a public IP address. For a job that is running on Fargate resources in a private subnet to send outbound traffic to the internet 
        /// (for example, to pull container images), the private subnet requires a NAT gateway be attached to route requests to the internet. For more information, 
        /// see <see href="https://docs.aws.amazon.com/AmazonECS/latest/developerguide/task-networking.html">Amazon ECS task networking</see>. The default value is "DISABLED".
        /// </summary>
        public string AssignPublicIp { get; set; }
    }
}
