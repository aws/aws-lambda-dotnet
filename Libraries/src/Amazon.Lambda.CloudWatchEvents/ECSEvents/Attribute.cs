using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.CloudWatchEvents.ECSEvents
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

        /// <summary>
        /// The ID of the target. You can specify the short form ID for a resource or the full
        /// Amazon Resource Name (ARN).
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// The type of the target with which to attach the attribute. This parameter is required
        /// if you use the short form ID for a resource instead of the full ARN.
        /// </summary>
        public string TargetType { get; set; }

        /// <summary>
        /// The value of the attribute. Up to 128 letters (uppercase and lowercase), numbers,
        /// hyphens, underscores, periods, at signs (@), forward slashes, colons, and spaces are
        /// allowed.
        /// </summary>
        public string Value { get; set; }
    }
}
