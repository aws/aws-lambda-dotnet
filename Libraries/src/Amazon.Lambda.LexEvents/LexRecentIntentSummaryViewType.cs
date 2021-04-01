using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.LexEvents
{
    /// <summary>
    /// The class recent intent summary view.
    /// </summary>
    [DataContract]
    public class LexRecentIntentSummaryViewType
    {
        /// <summary>
        /// Gets and sets the IntentName
        /// </summary>
        [DataMember(Name = "intentName", EmitDefaultValue = false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("intentName")]
#endif
        public string IntentName { get; set; }

        /// <summary>
        /// Gets and sets the CheckpointLabel
        /// </summary>
        [DataMember(Name = "checkpointLabel", EmitDefaultValue = false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("checkpointLabel")]
#endif
        public string CheckpointLabel { get; set; }

        /// <summary>
        /// Gets and sets the Slots
        /// </summary>
        [DataMember(Name = "slots", EmitDefaultValue = false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("slots")]
#endif
        public IDictionary<string, string> Slots { get; set; }

        /// <summary>
        /// Gets and sets the ConfirmationStatus
        /// </summary>
        [DataMember(Name = "confirmationStatus", EmitDefaultValue = false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("confirmationStatus")]
#endif
        public string ConfirmationStatus { get; set; }

        /// <summary>
        /// Gets and sets the DialogActionType
        /// </summary>
        [DataMember(Name = "dialogActionType", EmitDefaultValue = false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("dialogActionType")]
#endif
        public string DialogActionType { get; set; }

        /// <summary>
        /// Gets and sets the FulfillmentState
        /// </summary>
        [DataMember(Name = "fulfillmentState", EmitDefaultValue = false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("fulfillmentState")]
#endif
        public string FulfillmentState { get; set; }

        /// <summary>
        /// Gets and sets the SlotToElicit
        /// </summary>
        [DataMember(Name = "slotToElicit", EmitDefaultValue = false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("slotToElicit")]
#endif
        public string SlotToElicit { get; set; }
    }
}
