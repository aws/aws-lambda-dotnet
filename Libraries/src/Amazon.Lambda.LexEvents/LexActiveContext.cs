using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.LexEvents
{
    /// <summary>
    /// One or more contexts that are active during this turn of a conversation with the user.
    /// </summary>
    [DataContract]
    public class LexActiveContext
    {
        /// <summary>
        /// The length of time or number of turns in the conversation with the user that the context remains active.
        /// </summary>
        [DataMember(Name = "timeToLive", EmitDefaultValue = false)]
        [System.Text.Json.Serialization.JsonPropertyName("timeToLive")]
        public TimeToLive TimeToLive { get; set; }

        /// <summary>
        /// The name of the context.
        /// </summary>
        [DataMember(Name = "name", EmitDefaultValue = false)]
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// A list of key/value pairs the contains the name and value of the slots from the intent that activated the context.
        /// </summary>
        [DataMember(Name = "parameters", EmitDefaultValue = false)]
        [System.Text.Json.Serialization.JsonPropertyName("parameters")]
        public IDictionary<string, string> Parameters { get; set; }
    }

    /// <summary>
    /// The length of time or number of turns in the conversation with the user that the context remains active.
    /// </summary>
    [DataContract]
    public class TimeToLive
    {
        /// <summary>
        /// The length of time that the context remains active.
        /// </summary>
        [DataMember(Name = "timeToLiveInSeconds", EmitDefaultValue = false)]
        [System.Text.Json.Serialization.JsonPropertyName("timeToLiveInSeconds")]
        public int TimeToLiveInSeconds { get; set; }

        /// <summary>
        /// The number of turns in the conversation with the user that the context remains active.
        /// </summary>
        [DataMember(Name = "turnsToLive", EmitDefaultValue = false)]
        [System.Text.Json.Serialization.JsonPropertyName("turnsToLive")]
        public int TurnsToLive { get; set; }
    }
}
