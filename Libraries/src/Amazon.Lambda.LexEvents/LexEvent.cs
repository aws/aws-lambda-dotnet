using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

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
        /// Provides the intent name, slots, and confirmationStatus fields.
        /// </summary>
        public LexCurrentIntent CurrentIntent { get; set; }

        /// <summary>
        /// Provides additional information about a slot value.
        /// </summary>
        public IDictionary<string, SlotDetail> SlotDetails { get; set; }

        /// <summary>
        /// The Lex bot invoking the Lambda function
        /// </summary>
        public LexBot Bot { get; set; }

        /// <summary>
        /// This value is provided by the client application. Amazon Lex passes it to the Lambda function.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// The text used to process the request.
        /// </summary>
        public string InputTranscript { get; set; }

        /// <summary>
        /// To indicate why Amazon Lex is invoking the Lambda function
        /// </summary>
        public string InvocationSource { get; set; }

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
        /// The version of the message that identifies the format of the event data going into the 
        /// Lambda function and the expected format of the response from a Lambda function.
        /// </summary>
        public string MessageVersion { get; set; }

        /// <summary>
        /// Application-specific session attributes that the client sent in the request. If you want 
        /// Amazon Lex to include them in the response to the client, your Lambda function should 
        /// send these back to Amazon Lex in response.
        /// </summary>
        public IDictionary<string, string> SessionAttributes { get; set; }

        /// <summary>
        /// Request-specific attributes that the client sends in the request. Use request attributes to 
        /// pass information that doesn't need to persist for the entire session.
        /// </summary>
        public IDictionary<string, string> RequestAttributes { get; set; }

        /// <summary>
        /// A List of alternative intents that are returned when the bot is in advanced mode
        /// </summary>
        public IList<LexCurrentIntent> AlternativeIntents { get; set; }

        /// <summary>
        /// The result of an Amazon Comprehend sentiment analysis of the last utterance.
        /// </summary>
        public LexSentimentResponseType SentimentResponse { get; set; }
        /// <summary>
        /// If included, sets values for one or more recent intents. You can include information for up to three intents.
        /// </summary>
        public IList<LexRecentIntentSummaryViewType> RecentIntentSummaryView { get; set; }


        /// <summary>
        /// Gets and sets the property ActiveContexts.
        /// A list of active contexts for the session.A context can be set when an intent is fulfilled or by calling the PostContent, PostText, or PutSession operation.
        /// You can use a context to control the intents that can follow up an intent, or to modify the operation of your application.
        /// </summary>
        public IList<LexActiveContext> ActiveContexts { get; set; }



        /// <summary>
        /// Class representing the sentiment response.
        /// </summary>
        public class LexSentimentResponseType
        {
            /// <summary>
            /// Gets and sets the SentimentLabel
            /// </summary>
            public string SentimentLabel { get; set; }

            /// <summary>
            /// Gets and sets the SentimentScore
            /// </summary>
            public string SentimentScore { get; set; }
        }

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
            /// Provides additional information about a slot value.
            /// </summary>
            public IDictionary<string, SlotDetail> SlotDetails { get; set; }

            /// <summary>
            /// Gets and sets the property NluIntentConfidence.
            /// </summary>
            public double? NluIntentConfidenceScore { get; set; }



            /// <summary>
            /// The ConfirmationStatus provides the user response to a confirmation prompt, if there is one. 
            /// </summary>
            public string ConfirmationStatus { get; set; }
        }

        /// <summary>
        /// The class representing the information for a SlotDetail
        /// </summary>
        public class SlotDetail
        {
            /// <summary>
            /// The resolutions array contains a list of additional values recognized for the slot.
            /// </summary>
            public IList<Dictionary<string, string>> Resolutions { get; set; }

            /// <summary>
            /// The originalValue field contains the value that was entered by the user for the slot.
            /// </summary>
            public string OriginalValue { get; set; }
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
