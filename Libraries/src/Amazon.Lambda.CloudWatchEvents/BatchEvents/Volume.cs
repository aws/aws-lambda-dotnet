namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// A data volume used in a job's container properties.
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_Volume.html
    /// </summary>
    public class Volume
    {
        /// <summary>
        /// This parameter is specified when you are using an Amazon Elastic File System file system for job storage. 
        /// Jobs that are running on Fargate resources must specify a <c>platformVersion</c> of at least <c>1.4.0</c>.
        /// </summary>
        public EFSVolumeConfiguration EfsVolumeConfiguration { get; set; }

        /// <summary>
        /// The contents of the host parameter determine whether your data volume persists on the host container
        /// instance and where it is stored. If the host parameter is empty, then the Docker daemon assigns a host
        /// path for your data volume. However, the data is not guaranteed to persist after the containers associated
        /// with it stop running.
        /// </summary>
        public Host Host { get; set; }

        /// <summary>
        /// The name of the volume. Up to 255 letters (uppercase and lowercase), numbers, hyphens, and underscores are
        /// allowed. This name is referenced in the sourceVolume parameter of container definition mountPoints.
        /// </summary>
        public string Name { get; set; }
    }
}