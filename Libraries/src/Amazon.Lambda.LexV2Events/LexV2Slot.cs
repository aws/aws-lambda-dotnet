using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.LexV2Events
{
    /// <summary>
    /// The class that represents a value that Amazon Lex V2 uses to fulfill an intent.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_Slot.html
    /// </summary>
    [DataContract]
    public class LexV2Slot
    {
        /// <summary>
        /// When the shape value is List, it indicates that the values field contains a list of slot values. 
        /// When the value is Scalar, it indicates that the value field contains a single value.
        /// </summary>
        [DataMember(Name = "shape", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("shape")]
        #endif
        public string Shape { get; set; }

        /// <summary>
        /// The current value of the slot.
        /// </summary>
        [DataMember(Name = "value", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("value")]
        #endif
        public LexV2SlotValue Value { get; set; }

        /// <summary>
        /// The resolutions array contains a list of additional values recognized for the slot.
        /// </summary> 
        [DataMember(Name = "values", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("values")]
        #endif
        public IList<LexV2Slot> Values { get; set; }
    }

    /// <summary>
    /// The class that represents the value of a slot.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_Value.html
    /// </summary>
    [DataContract]
    public class LexV2SlotValue
    {
        /// <summary>
        /// The value that Amazon Lex V2 determines for the slot. The actual value depends on the setting of the value selection strategy for the bot.
        /// </summary>
        [DataMember(Name = "interpretedValue", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("interpretedValue")]
        #endif
        public string InterpretedValue { get; set; }

        /// <summary>
        /// The text of the utterance from the user that was entered for the slot.
        /// </summary>
        [DataMember(Name = "originalValue", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("originalValue")]
        #endif
        public string OriginalValue { get; set; }

        /// <summary>
        /// A list of additional values that have been recognized for the slot.
        /// </summary>
        [DataMember(Name = "resolvedValues", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("resolvedValues")]
        #endif
        public IList<string> ResolvedValues { get; set; }
    }
}
