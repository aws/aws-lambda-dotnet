using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.LexV2Events
{
    /// <summary>
    /// The class that represents the current intent that Amazon Lex V2 is attempting to fulfill.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_Intent.html
    /// </summary>
    [DataContract]
    public class LexV2Intent
    {
        /// <summary>
        /// Contains information about whether fulfillment of the intent has been confirmed. Could be one of <c>Confirmed</c>, <c>Denied</c> or <c>None</c>.
        /// </summary>
        [DataMember(Name = "confirmationState", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("confirmationState")]
        #endif
        public string ConfirmationState { get; set; }

        /// <summary>
        /// The name of the intent.
        /// </summary>
        [DataMember(Name = "name", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("name")]
        #endif
        public string Name { get; set; }

        /// <summary>
        /// A map of all of the slots for the intent. The name of the slot maps to the value of the slot. If a slot has not been filled, the value is null.
        /// </summary> 
        [DataMember(Name = "slots", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
              [System.Text.Json.Serialization.JsonPropertyName("slots")]
        #endif
        public IDictionary<string, LexV2Slot> Slots { get; set; }

        /// <summary>
        /// Contains fulfillment information for the intent. Could be one of <c>Failed</c>, <c>Fulfilled</c>, <c>FulfillmentInProgress</c>, <c>InProgress</c>, <c>ReadyForFulfillment</c> or <c>Waiting</c>.
        /// </summary>
        [DataMember(Name = "state", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
              [System.Text.Json.Serialization.JsonPropertyName("state")]
        #endif
        public string State { get; set; }

        /// <summary>
        /// Only present when intent is KendraSearchIntent.
        /// </summary>
        [DataMember(Name = "kendraResponse", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
              [System.Text.Json.Serialization.JsonPropertyName("kendraResponse")]
        #endif
        public KendraResponse KendraResponse { get; set; }
    }
}
