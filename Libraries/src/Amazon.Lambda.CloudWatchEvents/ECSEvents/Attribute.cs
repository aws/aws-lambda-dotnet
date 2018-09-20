using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.CloudWatchEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_ContainerInstance.html
    /// </summary>
    public class Attribute
    {
        /// <summary>
        /// The attributes set for the container instance, either by the Amazon ECS container agent at instance
        /// registration or manually with the PutAttributes operation.
        /// </summary>
        public string Name { get; set; }
    }
}
