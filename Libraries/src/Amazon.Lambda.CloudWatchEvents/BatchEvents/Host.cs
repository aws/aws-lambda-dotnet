namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// The contents of the host parameter determine whether your data volume persists on the host container
    /// instance and where it is stored. If the host parameter is empty, then the Docker daemon assigns a host
    /// path for your data volume, but the data is not guaranteed to persist after the containers associated with
    /// it stop running.
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_Host.html
    /// </summary>
    public class Host
    {
        /// <summary>
        /// The path on the host container instance that is presented to the container. If this parameter is empty,
        /// then the Docker daemon has assigned a host path for you. If the host parameter contains a sourcePath file
        /// location, then the data volume persists at the specified location on the host container instance until you
        /// delete it manually. If the sourcePath value does not exist on the host container instance, the Docker
        /// daemon creates it. If the location does exist, the contents of the source path folder are exported.
        /// </summary>
        public string SourcePath { get; set; }
    }
}