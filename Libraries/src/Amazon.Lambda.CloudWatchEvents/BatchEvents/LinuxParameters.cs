using System.Collections.Generic;

namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// Linux-specific modifications that are applied to the container, such as details for device mappings.
    /// </summary>
    public class LinuxParameters
    {
        /// <summary>
        /// Any host devices to expose to the container. This parameter maps to <c>Devices</c> in the 
        /// <see href="https://docs.docker.com/engine/api/v1.23/#create-a-container">Create a container</see> 
        /// section of the Docker Remote API and the <c>--device</c> option to <see href="https://docs.docker.com/engine/reference/run/">docker run</see>.
        /// </summary>
        public List<Device> Devices { get; set; }

        /// <summary>
        /// If true, run an <c>init</c> process inside the container that forwards signals and reaps processes. 
        /// This parameter maps to the <c>--init</c> option to <see href="https://docs.docker.com/engine/reference/run/">docker run</see>. 
        /// This parameter requires version 1.25 of the Docker Remote API or greater on your container instance. 
        /// To check the Docker Remote API version on your container instance, log into your container instance and run the following command: 
        /// <c>sudo docker version | grep "Server API version"</c>
        /// </summary>
        public bool InitProcessEnabled { get; set; }

        /// <summary>
        /// <para>The total amount of swap memory (in MiB) a container can use. This parameter is translated to the <c>--memory-swap</c> option to 
        /// <see href="https://docs.docker.com/engine/reference/run/">docker run</see> where the value is the sum of the container memory plus the <c>maxSwap</c> value. 
        /// For more information, see <see href="https://docs.docker.com/config/containers/resource_constraints/#--memory-swap-details"><c>--memory-swap</c> details</see> in the Docker documentation.
        /// </para>
        /// <para>If a <c>maxSwap</c> value of <c>0</c> is specified, the container doesn't use swap. Accepted values are <c>0</c> or any positive integer. 
        /// If the <c>maxSwap</c> parameter is omitted, the container doesn't use the swap configuration for the container instance it is running on. A <c>maxSwap</c> 
        /// value must be set for the <c>swappiness</c> parameter to be used.
        /// </para>
        /// </summary>
        public int MaxSwap { get; set; }

        /// <summary>
        /// The value for the size (in MiB) of the <c>/dev/shm</c> volume. This parameter maps to the <c>--shm-size</c> option to <see href="https://docs.docker.com/engine/reference/run/">docker run</see>.
        /// </summary>
        public int SharedMemorySize { get; set; }

        /// <summary>
        /// This allows you to tune a container's memory swappiness behavior. A <c>swappiness</c> value of <c>0</c> causes swapping not to happen unless absolutely necessary. 
        /// A <c>swappiness</c> value of <c>100</c> causes pages to be swapped very aggressively. Accepted values are whole numbers between <c>0</c> and <c>100</c>. 
        /// If the <c>swappiness</c> parameter isn't specified, a default value of <c>60</c> is used. If a value isn't specified for <c>maxSwap</c>, then this parameter is ignored. 
        /// If <c>maxSwap</c> is set to 0, the container doesn't use swap. This parameter maps to the <c>--memory-swappiness</c> option to <see href="https://docs.docker.com/engine/reference/run/">docker run</see>.
        /// </summary>
        public int Swappiness { get; set; }

        /// <summary>
        /// The container path, mount options, and size (in MiB) of the tmpfs mount. This parameter maps to the <c>--tmpfs</c> option to <see href="https://docs.docker.com/engine/reference/run/">docker run</see>.
        /// </summary>
        public List<Tmpfs> Tmpfs { get; set; }
    }
}
