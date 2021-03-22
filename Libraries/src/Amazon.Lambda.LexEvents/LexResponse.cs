namespace Amazon.Lambda.LexEvents
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// This class is used as the return for AWS Lambda functions that are invoked by Lex to handle Bot interactions.
    /// http://docs.aws.amazon.com/lex/latest/dg/lambda-input-response-format.html
    /// </summary>
    [DataContract]
    public class LexResponse
    {
        /// <summary>
        /// Application-specific session attributes. This is an optional field.
        /// </summary>
        [DataMember(Name = "sessionAttributes", EmitDefaultValue=false)]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("sessionAttributes")]
#endif
        public IDictionary<string, string> SessionAttributes { get; set; }

        /// <summary>
        /// This is the only field that is required. The value of DialogAction.Type directs 
        /// Amazon Lex to the next course of action, and describes what to expect from the user 
        /// after Amazon Lex returns a response to the client.
        /// </summary>\
        [DataMember(Name = "dialogAction", EmitDefaultValue=false)]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("dialogAction")]
#endif
        public LexDialogAction DialogAction { get; set; }

        /// <summary>
        /// If included, sets the value for one or more contexts. This is an optional field
        /// For example, you can include a context to make one or more intents that have that context as an input eligible for recognition in the next turn of the conversation.
        /// </summary>
        [DataMember(Name = "activeContexts", EmitDefaultValue=false)]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("activeContexts")]
#endif
        public IList<LexActiveContext> ActiveContexts { get; set; }

        /// <summary>
        /// If included, sets values for one or more recent intents. You can include information for up to three intents.
        /// </summary>
        [DataMember(Name = "recentIntentSummaryView", EmitDefaultValue = false)]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("recentIntentSummaryView")]
#endif
        public IList<LexRecentIntentSummaryViewType> RecentIntentSummaryView { get; set; }

        /// <summary>
        /// The class representing the dialog action.
        /// </summary>
        [DataContract]
        public class LexDialogAction
        {
            /// <summary>
            /// The type of action for Lex to take with the response from the Lambda function.
            /// </summary>
            [DataMember(Name = "type", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("type")]
#endif
            public string Type { get; set; }

            /// <summary>
            /// The state of the fullfillment. "Fulfilled" or "Failed"
            /// </summary>
            [DataMember(Name = "fulfillmentState", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("fulfillmentState")]
#endif
            public string FulfillmentState { get; set; }

            /// <summary>
            /// The message to be sent to the user.
            /// </summary>
            [DataMember(Name = "message", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("message")]
#endif
            public LexMessage Message { get; set; }

            /// <summary>
            /// The intent name you want to confirm or elicit.
            /// </summary>
            [DataMember(Name = "intentName", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("intentName")]
#endif
            public string IntentName { get; set; }

            /// <summary>
            /// The values for all of the slots when response is of type "Delegate".
            /// </summary>
            [DataMember(Name = "slots", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("slots")]
#endif
            public IDictionary<string, string> Slots { get; set; }

            /// <summary>
            /// The slot to elicit when the Type is "ElicitSlot"
            /// </summary>
            [DataMember(Name = "slotToElicit", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("slotToElicit")]
#endif
            public string SlotToElicit { get; set; }

            /// <summary>
            /// The response card provides information back to the bot to display for the user.
            /// </summary>
            [DataMember(Name = "responseCard", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("responseCard")]
#endif
            public LexResponseCard ResponseCard { get; set; }
        }

        /// <summary>
        /// The class represents the message that the bot will use.
        /// </summary>
        [DataContract]
        public class LexMessage
        {
            /// <summary>
            /// The content type of the message. PlainText or SSML
            /// </summary>
            [DataMember(Name = "contentType", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("contentType")]
#endif
            public string ContentType { get; set; }

            /// <summary>
            /// The message to be asked to the user by the bot.
            /// </summary>
            [DataMember(Name = "content", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("content")]
#endif
            public string Content { get; set; }
        }

        /// <summary>
        /// The class representing the response card sent back to the user.
        /// </summary>
        [DataContract]
        public class LexResponseCard
        {
            /// <summary>
            /// The version of the response card.
            /// </summary>
            [DataMember(Name = "version", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("version")]
#endif
            public int? Version { get; set; }

            /// <summary>
            /// The content type of the response card. The default is "application/vnd.amazonaws.card.generic".
            /// </summary>
            [DataMember(Name = "contentType", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("contentType")]
#endif
            public string ContentType { get; set; } = "application/vnd.amazonaws.card.generic";

            /// <summary>
            /// The list of attachments sent back with the response card.
            /// </summary>
            [DataMember(Name = "genericAttachments", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("genericAttachments")]
#endif
            public IList<LexGenericAttachments> GenericAttachments { get; set; }
        }

        /// <summary>
        /// The class representing generic attachments.
        /// </summary>
        [DataContract]
        public class LexGenericAttachments
        {
            /// <summary>
            /// The card's title.
            /// </summary>
            [DataMember(Name = "title", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("title")]
#endif
            public string Title { get; set; }

            /// <summary>
            /// The card's sub title.
            /// </summary>
            [DataMember(Name = "subTitle", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("subTitle")]
#endif
            public string SubTitle { get; set; }

            /// <summary>
            /// URL to an image to be shown.
            /// </summary>
            [DataMember(Name = "imageUrl", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("imageUrl")]
#endif
            public string ImageUrl { get; set; }

            /// <summary>
            /// URL of the attachment to be associated with the card.
            /// </summary>
            [DataMember(Name = "attachmentLinkUrl", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("attachmentLinkUrl")]
#endif
            public string AttachmentLinkUrl { get; set; }

            /// <summary>
            /// The list of buttons to be displayed with the response card.
            /// </summary>
            [DataMember(Name = "buttons", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("buttons")]
#endif
            public IList<LexButton> Buttons { get; set; }
        }

        /// <summary>
        /// The class representing a button in the response card.
        /// </summary>
        [DataContract]
        public class LexButton
        {
            /// <summary>
            /// The text for the button.
            /// </summary>
            [DataMember(Name = "text", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("text")]
#endif
            public string Text { get; set; }

            /// <summary>
            /// The value of the button sent back to the server.
            /// </summary>
            [DataMember(Name = "value", EmitDefaultValue=false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("value")]
#endif
            public string Value { get; set; }
        }
    }
}
