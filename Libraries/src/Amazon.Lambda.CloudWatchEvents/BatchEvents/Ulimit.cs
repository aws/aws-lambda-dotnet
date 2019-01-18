namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// The ulimit settings to pass to the container.
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_Ulimit.html
    /// </summary>
    public class Ulimit
    {
        /// <summary>
        /// The hard limit for the ulimit type.
        /// </summary>
        public int HardLimit { get; set; }

        /// <summary>
        /// The type of the ulimit.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The soft limit for the ulimit type.
        /// </summary>
        public int SoftLimit { get; set; }
    }
}