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
        [System.Text.Json.Serialization.JsonPropertyName("intentName")]
        public string IntentName { get; set; }

        /// <summary>
        /// Gets and sets the CheckpointLabel
        /// </summary>
        [DataMember(Name = "checkpointLabel", EmitDefaultValue = false)]
        [System.Text.Json.Serialization.JsonPropertyName("checkpointLabel")]
        public string CheckpointLabel { get; set; }

        /// <summary>
        /// Gets and sets the Slots
        /// </summary>
        [DataMember(Name = "slots", EmitDefaultValue = false)]
        [System.Text.Json.Serialization.JsonPropertyName("slots")]
        public IDictionary<string, string> Slots { get; set; }

        /// <summary>
        /// Gets and sets the ConfirmationStatus
        /// </summary>
        [DataMember(Name = "confirmationStatus", EmitDefaultValue = false)]
        [System.Text.Json.Serialization.JsonPropertyName("confirmationStatus")]
        public string ConfirmationStatus { get; set; }

        /// <summary>
        /// Gets and sets the DialogActionType
        /// </summary>
        [DataMember(Name = "dialogActionType", EmitDefaultValue = false)]
        [System.Text.Json.Serialization.JsonPropertyName("dialogActionType")]
        public string DialogActionType { get; set; }

        /// <summary>
        /// Gets and sets the FulfillmentState
        /// </summary>
        [DataMember(Name = "fulfillmentState", EmitDefaultValue = false)]
        [System.Text.Json.Serialization.JsonPropertyName("fulfillmentState")]
        public string FulfillmentState { get; set; }

        /// <summary>
        /// Gets and sets the SlotToElicit
        /// </summary>
        [DataMember(Name = "slotToElicit", EmitDefaultValue = false)]
        [System.Text.Json.Serialization.JsonPropertyName("slotToElicit")]
        public string SlotToElicit { get; set; }
    }
}
