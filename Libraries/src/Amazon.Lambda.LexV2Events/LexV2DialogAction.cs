using System.Runtime.Serialization;

namespace Amazon.Lambda.LexV2Events
{
    /// <summary>
    /// The class that represents the next action that Amazon Lex V2 should take.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_DialogAction.html
    /// </summary>
    [DataContract]
    public class LexV2DialogAction
    {
        /// <summary>
        /// Configures the slot to use spell-by-letter or spell-by-word style. When you use a style on a slot, users can spell out their input to make it clear to your bot.
        /// </summary>
        [DataMember(Name = "slotElicitationStyle", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("slotElicitationStyle")]
        #endif
        public string SlotElicitationStyle { get; set; }

        /// <summary>
        /// The name of the slot that should be elicited from the user.
        /// </summary>
        [DataMember(Name = "slotToElicit", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("slotToElicit")]
        #endif
        public string SlotToElicit { get; set; }

        /// <summary>
        /// The next action that the bot should take in its interaction with the user. Could be one of <c>Close</c>, <c>ConfirmIntent</c>, <c>Delegate</c>, <c>ElicitIntent</c> or <c>ElicitSlot</c>.
        /// </summary>
        [DataMember(Name = "type", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("type")]
        #endif
        public string Type { get; set; }
    }
}
