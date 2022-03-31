using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.LexV2Events
{
    /// <summary>
    /// This class represents response from the Lambda function.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/lambda.html#lambda-response-format
    /// </summary>
    [DataContract]
    public class LexV2Response
    {
        /// <summary>
        /// The current state of the conversation with the user. The actual contents of the structure depends on the type of dialog action.
        /// </summary>
        [DataMember(Name = "sessionState", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("sessionState")]
        #endif
        public LexV2SessionState SessionState { get; set; }

        /// <summary>
        /// One or more messages that Amazon Lex V2 shows to the customer to perform the next turn of the conversation. Required if <c>dialogAction.type</c> is <c>ElicitIntent</c>. 
        /// If you don't supply messages, Amazon Lex V2 uses the appropriate message defined when the bot was created.
        /// </summary>
        [DataMember(Name = "messages", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("messages")]
        #endif
        public IList<LexV2Message> Messages { get; set; }

        /// <summary>
        /// Request-specific attributes that the client sends in the request.
        /// </summary>
        [DataMember(Name = "requestAttributes", EmitDefaultValue = false)]
        #if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("requestAttributes")]
        #endif
        public IDictionary<string, string> RequestAttributes { get; set; }
    }
}
