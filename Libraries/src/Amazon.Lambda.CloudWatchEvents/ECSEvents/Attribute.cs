namespace Amazon.Lambda.CloudWatchEvents.ECSEvents
{
    /// <summary>
    /// An attribute is a name-value pair associated with an Amazon ECS object. 
    /// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_Attribute.html
    /// </summary>
    public class Attribute
    {
        /// <summary>
        /// The name of the attribute. The name must contain between 1 and 128 characters 
        /// and name may contain letters (uppercase and lowercase), numbers, hyphens, 
        /// underscores, forward slashes, back slashes, or periods.
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
        /// The value of the attribute. The value must contain between 1 and 128 characters and may 
        /// contain letters (uppercase and lowercase), numbers, hyphens, underscores, periods, at signs (@), 
        /// forward slashes, back slashes, colons, or spaces. The value cannot contain any leading or trailing whitespace.
        /// </summary>
        public string Value { get; set; }
    }
}
