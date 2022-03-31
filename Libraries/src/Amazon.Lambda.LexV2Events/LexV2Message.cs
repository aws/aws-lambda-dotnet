using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.LexV2Events
{
    /// <summary>
    /// The class that represents the container for text that is returned to the customer.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_Message.html
    /// </summary>
    [DataContract]
    public class LexV2Message
    {
        /// <summary>
        /// The text of the message.
        /// </summary>
        [DataMember(Name = "content", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("content")]
        #endif
        public string Content { get; set; }

        /// <summary>
        /// Indicates the type of response. Could be one of <c>CustomPayload</c>, <c>ImageResponseCard</c>, <c>PlainText</c> or <c>SSML</c>.
        /// </summary>
        [DataMember(Name = "contentType", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("contentType")]
        #endif
        public string ContentType { get; set; }

        /// <summary>
        /// A card that is shown to the user by a messaging platform. You define the contents of the card, the card is displayed by the platform.
        /// </summary>
        [DataMember(Name = "imageResponseCard", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("imageResponseCard")]
        #endif
        public LexV2ImageResponseCard ImageResponseCard { get; set; }
    }

    /// <summary>
    /// The class that represents a card that is shown to the user by a messaging platform.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_ImageResponseCard.html
    /// </summary>
    [DataContract]
    public class LexV2ImageResponseCard
    {
        /// <summary>
        /// A list of buttons that should be displayed on the response card. The arrangement of the buttons is determined by the platform that displays the button.
        /// </summary>
        [DataMember(Name = "buttons", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("buttons")]
        #endif
        public IList<LexV2Button> Buttons { get; set; }

        /// <summary>
        /// TheThe URL of an image to display on the response card. The image URL must be publicly available so that the platform displaying the response card has access to the image.
        /// </summary>
        [DataMember(Name = "imageUrl", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("imageUrl")]
        #endif
        public string ImageUrl { get; set; }

        /// <summary>
        /// The subtitle to display on the response card. The format of the subtitle is determined by the platform displaying the response card.
        /// </summary>
        [DataMember(Name = "subtitle", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("subtitle")]
        #endif
        public string Subtitle { get; set; }

        /// <summary>
        /// The title to display on the response card. The format of the title is determined by the platform displaying the response card.
        /// </summary>
        [DataMember(Name = "title", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("title")]
        #endif
        public string Title { get; set; }
    }

    /// <summary>
    /// The class that represents a button that appears on a response card show to the user.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_Button.html
    /// </summary>
    [DataContract]
    public class LexV2Button
    {
        /// <summary>
        /// The text that is displayed on the button.
        /// </summary>
        [DataMember(Name = "text", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("text")]
        #endif
        public string Text { get; set; }

        /// <summary>
        /// The value returned to Amazon Lex V2 when a user chooses the button.
        /// </summary>
        [DataMember(Name = "value", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("value")]
        #endif
        public string Value { get; set; }
    }
}
