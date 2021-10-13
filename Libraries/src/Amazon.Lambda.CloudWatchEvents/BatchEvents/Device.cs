using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// An object representing a container instance host device.
    /// </summary>
    public class Device
    {
        /// <summary>
        /// The path inside the container that's used to expose the host device. By default, the hostPath value is used.
        /// </summary>
        public string ContainerPath { get; set; }

        /// <summary>
        /// The path for the device on the host container instance.
        /// </summary>
        public string HostPath { get; set; }

        /// <summary>
        /// The explicit permissions to provide to the container for the device. By default, the container has permissions for read, write, and mknod for the device.
        /// </summary>
        public List<string> Permissions { get; set; }
    }
}
