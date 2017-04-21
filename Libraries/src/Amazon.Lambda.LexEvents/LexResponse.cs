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
        [DataMember(Name = "sessionAttributes")]
        public IDictionary<string, string> SessionAttributes { get; set; }

        /// <summary>
        /// This is the only field that is required. The value of DialogAction.Type directs 
        /// Amazon Lex to the next course of action, and describes what to expect from the user 
        /// after Amazon Lex returns a response to the client.
        /// </summary>\
        [DataMember(Name = "dialogAction")]
        public LexDialogAction DialogAction { get; set; }

        /// <summary>
        /// The class representing the dialog action.
        /// </summary>
        [DataContract]
        public class LexDialogAction
        {
            /// <summary>
            /// The type of action for Lex to take with the response from the Lambda function.
            /// </summary>
            [DataMember(Name = "type")]
            public string Type { get; set; }

            /// <summary>
            /// The state of the fullfillment. "Fulfilled" or "Failed"
            /// </summary>
            [DataMember(Name = "fulfillmentState")]
            public string FulfillmentState { get; set; }

            /// <summary>
            /// The message to be sent to the user.
            /// </summary>
            [DataMember(Name = "message")]
            public LexMessage Message { get; set; }

            /// <summary>
            /// The intent name you want to confirm or elicit.
            /// </summary>
            [DataMember(Name = "intentName")]
            public string IntentName { get; set; }

            /// <summary>
            /// The values for all of the slots when response is of type "Delegate".
            /// </summary>
            [DataMember(Name = "slots")]
            public IDictionary<string, string> Slots { get; set; }

            /// <summary>
            /// The slot to elicit when the Type is "ElicitSlot"
            /// </summary>
            [DataMember(Name = "slotToElicit")]
            public string SlotToElicit { get; set; }

            /// <summary>
            /// The response card provides information back to the bot to display for the user.
            /// </summary>
            [DataMember(Name = "responseCard")]
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
            [DataMember(Name = "contentType")]
            public string ContentType { get; set; }

            /// <summary>
            /// The message to be asked to the user by the bot.
            /// </summary>
            [DataMember(Name = "content")]
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
            [DataMember(Name = "version")]
            public int? Version { get; set; }

            /// <summary>
            /// The content type of the response card. The default is "application/vnd.amazonaws.card.generic".
            /// </summary>
            [DataMember(Name = "contentType")]
            public string ContentType { get; set; } = "application/vnd.amazonaws.card.generic";

            /// <summary>
            /// The list of attachments sent back with the response card.
            /// </summary>
            [DataMember(Name = "genericAttachments")]
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
            [DataMember(Name = "title")]
            public string Title { get; set; }

            /// <summary>
            /// The card's sub title.
            /// </summary>
            [DataMember(Name = "subTitle")]
            public string SubTitle { get; set; }

            /// <summary>
            /// URL to an image to be shown.
            /// </summary>
            [DataMember(Name = "imageUrl")]
            public string ImageUrl { get; set; }

            /// <summary>
            /// URL of the attachment to be associated with the card.
            /// </summary>
            [DataMember(Name = "attachmentLinkUrl")]
            public string AttachmentLinkUrl { get; set; }

            /// <summary>
            /// The list of buttons to be displayed with the response card.
            /// </summary>
            [DataMember(Name = "buttons")]
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
            [DataMember(Name = "text")]
            public string Text { get; set; }

            /// <summary>
            /// The value of the button sent back to the server.
            /// </summary>
            [DataMember(Name = "value")]
            public string Value { get; set; }
        }
    }
}
