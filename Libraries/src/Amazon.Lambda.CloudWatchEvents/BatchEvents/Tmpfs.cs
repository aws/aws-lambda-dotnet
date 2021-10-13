using System.Collections.Generic;

namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// The container path, mount options, and size of the tmpfs mount.
    /// </summary>
    public class Tmpfs
    {
        /// <summary>
        /// The absolute file path in the container where the tmpfs volume is mounted.
        /// </summary>
        public string ContainerPath { get; set; }

        /// <summary>
        /// The list of tmpfs volume mount options.
        /// </summary>
        public List<string> MountOptions { get; set; }

        /// <summary>
        /// The size (in MiB) of the tmpfs volume.
        /// </summary>
        public int Size { get; set; }
    }
}
