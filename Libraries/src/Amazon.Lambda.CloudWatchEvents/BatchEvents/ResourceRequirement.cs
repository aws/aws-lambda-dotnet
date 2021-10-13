namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// The type and amount of a resource to assign to a container. The supported resources include <c>GPU</c>, <c>MEMORY</c>, and <c>VCPU</c>.
    /// </summary>
    public class ResourceRequirement
    {
        /// <summary>
        /// The type of resource to assign to a container. The supported resources include <c>GPU</c>, <c>MEMORY</c>, and <c>VCPU</c>.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The quantity of the specified resource to reserve for the container. The values vary based on the <c>type</c> specified.
        /// </summary>
        public string Value { get; set; }
    }
}
