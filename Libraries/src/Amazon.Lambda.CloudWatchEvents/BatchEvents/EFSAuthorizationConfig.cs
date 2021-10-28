namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// The authorization configuration details for the Amazon EFS file system.
    /// </summary>
    public class EFSAuthorizationConfig
    {
        /// <summary>
        /// The Amazon EFS access point ID to use. If an access point is specified, the root directory value specified in the <c>EFSVolumeConfiguration</c> must either be omitted or set to <c>/</c> which will enforce the path set on the EFS access point. 
        /// If an access point is used, transit encryption must be enabled in the <c>EFSVolumeConfiguration</c>.
        /// </summary>
        public string AccessPointId { get; set; }

        /// <summary>
        /// Whether or not to use the AWS Batch job IAM role defined in a job definition when mounting the Amazon EFS file system. If enabled, transit encryption must be enabled in the <c>EFSVolumeConfiguration</c>. 
        /// If this parameter is omitted, the default value of <c>DISABLED</c> is used.
        /// </summary>
        public string Iam { get; set; }
    }
}
