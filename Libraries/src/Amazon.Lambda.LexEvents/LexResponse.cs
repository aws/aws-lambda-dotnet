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
        [System.Text.Json.Serialization.JsonPropertyName("sessionAttributes")]
        public IDictionary<string, string> SessionAttributes { get; set; }

        /// <summary>
        /// This is the only field that is required. The value of DialogAction.Type directs 
        /// Amazon Lex to the next course of action, and describes what to expect from the user 
        /// after Amazon Lex returns a response to the client.
        /// </summary>\
        [DataMember(Name = "dialogAction", EmitDefaultValue=false)]
        [System.Text.Json.Serialization.JsonPropertyName("dialogAction")]
        public LexDialogAction DialogAction { get; set; }

        /// <summary>
        /// If included, sets the value for one or more contexts. This is an optional field
        /// For example, you can include a context to make one or more intents that have that context as an input eligible for recognition in the next turn of the conversation.
        /// </summary>
        [DataMember(Name = "activeContexts", EmitDefaultValue=false)]
        [System.Text.Json.Serialization.JsonPropertyName("activeContexts")]
        public IList<LexActiveContext> ActiveContexts { get; set; }

        /// <summary>
        /// If included, sets values for one or more recent intents. You can include information for up to three intents.
        /// </summary>
        [DataMember(Name = "recentIntentSummaryView", EmitDefaultValue = false)]
        [System.Text.Json.Serialization.JsonPropertyName("recentIntentSummaryView")]
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
            [System.Text.Json.Serialization.JsonPropertyName("type")]
            public string Type { get; set; }

            /// <summary>
            /// The state of the fullfillment. "Fulfilled" or "Failed"
            /// </summary>
            [DataMember(Name = "fulfillmentState", EmitDefaultValue=false)]
            [System.Text.Json.Serialization.JsonPropertyName("fulfillmentState")]
            public string FulfillmentState { get; set; }

            /// <summary>
            /// The message to be sent to the user.
            /// </summary>
            [DataMember(Name = "message", EmitDefaultValue=false)]
            [System.Text.Json.Serialization.JsonPropertyName("message")]
            public LexMessage Message { get; set; }

            /// <summary>
            /// The intent name you want to confirm or elicit.
            /// </summary>
            [DataMember(Name = "intentName", EmitDefaultValue=false)]
            [System.Text.Json.Serialization.JsonPropertyName("intentName")]
            public string IntentName { get; set; }

            /// <summary>
            /// The values for all of the slots when response is of type "Delegate".
            /// </summary>
            [DataMember(Name = "slots", EmitDefaultValue=false)]
            [System.Text.Json.Serialization.JsonPropertyName("slots")]
            public IDictionary<string, string> Slots { get; set; }

            /// <summary>
            /// The slot to elicit when the Type is "ElicitSlot"
            /// </summary>
            [DataMember(Name = "slotToElicit", EmitDefaultValue=false)]
            [System.Text.Json.Serialization.JsonPropertyName("slotToElicit")]
            public string SlotToElicit { get; set; }

            /// <summary>
            /// The response card provides information back to the bot to display for the user.
            /// </summary>
            [DataMember(Name = "responseCard", EmitDefaultValue=false)]
            [System.Text.Json.Serialization.JsonPropertyName("responseCard")]
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
            [System.Text.Json.Serialization.JsonPropertyName("contentType")]
            public string ContentType { get; set; }

            /// <summary>
            /// The message to be asked to the user by the bot.
            /// </summary>
            [DataMember(Name = "content", EmitDefaultValue=false)]
            [System.Text.Json.Serialization.JsonPropertyName("content")]
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
            [System.Text.Json.Serialization.JsonPropertyName("version")]
            public int? Version { get; set; }

            /// <summary>
            /// The content type of the response card. The default is "application/vnd.amazonaws.card.generic".
            /// </summary>
            [DataMember(Name = "contentType", EmitDefaultValue=false)]
            [System.Text.Json.Serialization.JsonPropertyName("contentType")]
            public string ContentType { get; set; } = "application/vnd.amazonaws.card.generic";

            /// <summary>
            /// The list of attachments sent back with the response card.
            /// </summary>
            [DataMember(Name = "genericAttachments", EmitDefaultValue=false)]
            [System.Text.Json.Serialization.JsonPropertyName("genericAttachments")]
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
            [System.Text.Json.Serialization.JsonPropertyName("title")]
            public string Title { get; set; }

            /// <summary>
            /// The card's sub title.
            /// </summary>
            [DataMember(Name = "subTitle", EmitDefaultValue=false)]
            [System.Text.Json.Serialization.JsonPropertyName("subTitle")]
            public string SubTitle { get; set; }

            /// <summary>
            /// URL to an image to be shown.
            /// </summary>
            [DataMember(Name = "imageUrl", EmitDefaultValue=false)]
            [System.Text.Json.Serialization.JsonPropertyName("imageUrl")]
            public string ImageUrl { get; set; }

            /// <summary>
            /// URL of the attachment to be associated with the card.
            /// </summary>
            [DataMember(Name = "attachmentLinkUrl", EmitDefaultValue=false)]
            [System.Text.Json.Serialization.JsonPropertyName("attachmentLinkUrl")]
            public string AttachmentLinkUrl { get; set; }

            /// <summary>
            /// The list of buttons to be displayed with the response card.
            /// </summary>
            [DataMember(Name = "buttons", EmitDefaultValue=false)]
            [System.Text.Json.Serialization.JsonPropertyName("buttons")]
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
            [System.Text.Json.Serialization.JsonPropertyName("text")]
            public string Text { get; set; }

            /// <summary>
            /// The value of the button sent back to the server.
            /// </summary>
            [DataMember(Name = "value", EmitDefaultValue=false)]
            [System.Text.Json.Serialization.JsonPropertyName("value")]
            public string Value { get; set; }
        }
    }
}
