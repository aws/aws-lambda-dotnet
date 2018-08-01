namespace Amazon.Lambda.ECSEvents
{
    using System.Collections.Generic;

    /// <summary>
    /// Describes the resources available for a container instance.
    /// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_Resource.html
    /// </summary>
    public class Resource
    {
        /// <summary>
        /// The name of the resource, such as CPU, MEMORY, PORTS, PORTS_UDP, or a user-defined resource.
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// The type of the resource, such as INTEGER, DOUBLE, LONG, or STRINGSET.
        /// </summary>
        public string Type { get; set; }
        
        /// <summary>
        /// When the integerValue type is set, the value of the resource must be an integer.
        /// </summary>
        public int IntegerValue { get; set; }
        
        /// <summary>
        /// When the stringSetValue type is set, the value of the resource must be a string type.
        /// </summary>
        public List<string> StringSetValue { get; set; }
    }
}