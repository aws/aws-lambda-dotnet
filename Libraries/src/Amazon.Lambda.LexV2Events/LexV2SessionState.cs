using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.LexV2Events
{
    /// <summary>
    /// The class that represents the state of the user's session with Amazon Lex V2.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_SessionState.html
    /// </summary>
    [DataContract]
    public class LexV2SessionState
    {
        /// <summary>
        /// One or more contexts that indicate to Amazon Lex V2 the context of a request. When a context is active, Amazon Lex V2 considers 
        /// intents with the matching context as a trigger as the next intent in a session.
        /// </summary>
        [DataMember(Name = "activeContexts", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("activeContexts")]
        #endif
        public IList<LexV2ActiveContext> ActiveContexts { get; set; }

        /// <summary>
        /// The next step that Amazon Lex V2 should take in the conversation with a user.
        /// </summary>
        [DataMember(Name = "dialogAction", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("dialogAction")]
        #endif
        public LexV2DialogAction DialogAction { get; set; }

        /// <summary>
        /// The active intent that Amazon Lex V2 is processing.
        /// </summary>
        [DataMember(Name = "intent", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("intent")]
        #endif
        public LexV2Intent Intent { get; set; }

        /// <summary>
        /// A unique identifier for a specific request.
        /// </summary>
        [DataMember(Name = "originatingRequestId", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("originatingRequestId")]
        #endif
        public string OriginatingRequestId { get; set; }

        /// <summary>
        /// Hints for phrases that a customer is likely to use for a slot. Amazon Lex V2 uses the hints to help determine the correct value of a slot.
        /// </summary>
        [DataMember(Name = "runtimeHints", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("runtimeHints")]
        #endif
        public LexV2RuntimeHints RuntimeHints { get; set; }

        /// <summary>
        /// Map of key/value pairs representing session-specific context information. It contains application information passed between Amazon Lex V2 and a client application.
        /// </summary>
        [DataMember(Name = "sessionAttributes", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("sessionAttributes")]
        #endif
        public Dictionary<string, string> SessionAttributes { get; set; }
    }

    /// <summary>
    /// The class that represents hints to the phrases which can be provided to Amazon Lex V2 that a customer is likely to use for a slot.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_RuntimeHints.html
    /// </summary>
    [DataContract]
    public class LexV2RuntimeHints
    {
        /// <summary>
        /// A list of the slots in the intent that should have runtime hints added, and the phrases that should be added for each slot.
        /// </summary>
        [DataMember(Name = "slotHints", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("slotHints")]
        #endif
        public IDictionary<string, Dictionary<string, RuntimeHintDetails>> SlotHints { get; set; }
    }

    /// <summary>
    /// Provides an array of phrases that should be given preference when resolving values for a slot.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_RuntimeHintDetails.html
    /// </summary>
    [DataContract]
    public class RuntimeHintDetails
    {
        /// <summary>
        /// One or more strings that Amazon Lex V2 should look for in the input to the bot. Each phrase is given preference when deciding on slot values.
        /// </summary>
        [DataMember(Name = "runtimeHintValues", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("runtimeHintValues")]
        #endif
        public IList<RuntimeHintValue> RuntimeHintValues { get; set; }
    }

    /// <summary>
    /// Provides the phrase that Amazon Lex V2 should look for in the user's input to the bot.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_RuntimeHintValue.html
    /// </summary>
    [DataContract]
    public class RuntimeHintValue
    {
        /// <summary>
        /// The phrase that Amazon Lex V2 should look for in the user's input to the bot.
        /// </summary>
        [DataMember(Name = "phrase", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("phrase")]
        #endif
        public string Phrase { get; set; }
    }
}
