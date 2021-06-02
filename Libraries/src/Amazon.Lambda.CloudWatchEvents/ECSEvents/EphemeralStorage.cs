namespace Amazon.Lambda.CloudWatchEvents.ECSEvents
{
    /// <summary>
    /// The amount of ephemeral storage to allocate for the task. 
    /// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_EphemeralStorage.html
    /// </summary>
    public class EphemeralStorage
    {
        /// <summary>
        /// The total amount, in GiB, of ephemeral storage to set for the task. The minimum supported value is 21 GiB and the maximum supported value is 200 GiB.
        /// </summary>
        public int SizeInGiB { get; set; }
    }
}
