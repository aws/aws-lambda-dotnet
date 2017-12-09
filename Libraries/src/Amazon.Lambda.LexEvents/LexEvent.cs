using System;
using System.Collections.Generic;

namespace Amazon.Lambda.LexEvents
{
    /// <summary>
    /// This class represents the input event from Amazon Lex. It used as the input parameter
    /// for Lambda functions.
    /// http://docs.aws.amazon.com/lex/latest/dg/lambda-input-response-format.html
    /// </summary>
    public class LexEvent
    {
        /// <summary>
        /// The version of the message that identifies the format of the event data going into the 
        /// Lambda function and the expected format of the response from a Lambda function.
        /// </summary>
        public string MessageVersion { get; set; }

        /// <summary>
        /// To indicate why Amazon Lex is invoking the Lambda function
        /// </summary>
        public string InvocationSource { get; set; }

        /// <summary>
        /// This value is provided by the client application. Amazon Lex passes it to the Lambda function.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// The text used to process the request.
        /// </summary>
        public string InputTranscript { get; set; }
        
        /// <summary>
        /// Application-specific session attributes that the client sent in the request. If you want 
        /// Amazon Lex to include them in the response to the client, your Lambda function should 
        /// send these back to Amazon Lex in response.
        /// </summary>
        public IDictionary<string, string> SessionAttributes { get; set; }

        /// <summary>
        /// The Lex bot invoking the Lambda function
        /// </summary>
        public LexBot Bot { get; set; }

        /// <summary>
        /// For each user input, the client sends the request to Amazon Lex using one of the runtime API operations, 
        /// PostContent or PostText. From the API request parameters, Amazon Lex determines whether the response 
        /// to the client (user) is text or voice, and sets this field accordingly.
        /// <para>
        /// The Lambda function can use this information to generate an appropriate message. 
        /// For example, if the client expects a voice response, your Lambda function could return 
        /// Speech Synthesis Markup LanguageSpeech Synthesis Markup Language (SSML) instead of text.
        /// </para>
        /// </summary>
        public string OutputDialogMode { get; set; }

        /// <summary>
        /// Provides the intent name, slots, and confirmationStatus fields.
        /// </summary>
        public LexCurrentIntent CurrentIntent { get; set; }


        /// <summary>
        /// The class representing the current intent for the Lambda function to process.
        /// </summary>
        public class LexCurrentIntent
        {
            /// <summary>
            /// The intent's name
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// List of slots that are configured for the intent and values that are recognized by 
            /// Amazon Lex in the user conversation from the beginning. Otherwise, the values are null.
            /// </summary>
            public IDictionary<string, string> Slots { get; set; }

            /// <summary>
            /// The ConfirmationStatus provides the user response to a confirmation prompt, if there is one. 
            /// </summary>
            public string ConfirmationStatus { get; set; }
        }


        /// <summary>
        /// The class identifies the Lex bot that is invoking the Lambda function.
        /// </summary>
        public class LexBot
        {
            /// <summary>
            /// The name of the Lex bot
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// The alias of the Lex bot
            /// </summary>
            public string Alias { get; set; }

            /// <summary>
            /// The version of the Lex bot
            /// </summary>
            public string Version { get; set; }
        }
    }
}
