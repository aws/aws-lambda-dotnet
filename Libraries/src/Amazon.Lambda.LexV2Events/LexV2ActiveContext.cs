using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.LexV2Events
{
    /// <summary>
    /// Contains information about the contexts that a user is using in a session.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_ActiveContext.html
    /// </summary>
    [DataContract]
    public class LexV2ActiveContext
    {
        /// <summary>
        /// A list of contexts active for the request. A context can be activated when a previous intent is fulfilled, or by including the context in the request.
        /// </summary>
        [DataMember(Name = "contextAttributes", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("contextAttributes")]
        #endif
        public IDictionary<string, string> ContextAttributes { get; set; }

        /// <summary>
        /// The name of the context.
        /// </summary>
        [DataMember(Name = "name", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("name")]
        #endif
        public string Name { get; set; }

        /// <summary>
        /// The number of turns that the context is active. You can specify up to 20 turns. Each request and response from the bot is a turn.

        /// </summary>
        [DataMember(Name = "timeToLive", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("timeToLive")]
        #endif
        public ActiveContextTimeToLive TimeToLive { get; set; }

        /// <summary>
        /// The time that a context is active. You can specify the time to live in seconds or in conversation turns.
        /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_ActiveContextTimeToLive.html
        /// </summary>
        [DataContract]
        public class ActiveContextTimeToLive
        {
            /// <summary>
            /// The number of seconds that the context is active. You can specify between 5 and 86400 seconds (24 hours).
            /// </summary>
            [DataMember(Name = "timeToLiveInSeconds", EmitDefaultValue = false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("timeToLiveInSeconds")]
#endif
            public int TimeToLiveInSeconds { get; set; }

            /// <summary>
            /// The number of turns that the context is active. You can specify up to 20 turns. Each request and response from the bot is a turn.

            /// </summary>
            [DataMember(Name = "turnsToLive", EmitDefaultValue = false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("turnsToLive")]
#endif
            public int TurnsToLive { get; set; }
        }
    }
}
