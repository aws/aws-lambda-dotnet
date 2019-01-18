namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// Details on a Docker volume mount point that is used in a job's container properties.
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_MountPoint.html
    /// </summary>
    public class MountPoint
    {
        /// <summary>
        /// The path on the container at which to mount the host volume.
        /// </summary>
        public string ContainerPath { get; set; }

        /// <summary>
        /// If this value is true, the container has read-only access to the volume; otherwise,
        /// the container can write to the volume. The default value is false.
        /// </summary>
        public bool ReadOnly { get; set; }

        /// <summary>
        /// The name of the volume to mount.
        /// </summary>
        public string SourceVolume { get; set; }
    }
}