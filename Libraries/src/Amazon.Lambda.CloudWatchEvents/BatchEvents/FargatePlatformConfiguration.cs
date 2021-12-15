namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// The platform configuration for jobs that are running on Fargate resources. Jobs that run on EC2 resources must not specify this parameter.
    /// </summary>
    public class FargatePlatformConfiguration
    {
        /// <summary>
        /// The AWS Fargate platform version where the jobs are running. A platform version is specified only for jobs that are running on Fargate resources. 
        /// If one isn't specified, the LATEST platform version is used by default. This uses a recent, approved version of the AWS Fargate platform for compute resources.
        /// </summary>
        public string PlatformVersion { get; set; }
    }
}
