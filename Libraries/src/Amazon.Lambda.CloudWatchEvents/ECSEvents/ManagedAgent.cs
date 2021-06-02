using System;

namespace Amazon.Lambda.CloudWatchEvents.ECSEvents
{
    /// <summary>
    /// Details about the managed agent status for the container. 
    /// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_ManagedAgent.html
    /// </summary>
    public class ManagedAgent
    {
        /// <summary>
        /// The Unix timestamp for when the managed agent was last started.
        /// </summary>
        public DateTime LastStartedAt { get; set; }

        /// <summary>
        /// The last known status of the managed agent.
        /// </summary>
        public string LastStatus { get; set; }

        /// <summary>
        /// The name of the managed agent. When the execute command feature is enabled, the managed agent name is ExecuteCommandAgent.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The reason for why the managed agent is in the state it is in.
        /// </summary>
        public string Reason { get; set; }
    }
}
