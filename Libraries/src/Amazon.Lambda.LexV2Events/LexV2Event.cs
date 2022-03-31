using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.LexV2Events
{
    /// <summary>
    /// This class represents the input event from Amazon Lex V2. It used as the input parameter
    /// for Lambda functions.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/lambda.html#lambda-input-format
    /// </summary>
    public class LexV2Event
    {
        /// <summary>
        /// Message Version
        /// </summary>
        public string MessageVersion { get; set; }

        /// <summary>
        /// Indicates the action that called the Lambda function. When the source is DialogCodeHook, the Lambda function was called after input from the user. 
        /// When the source is FulfillmentCodeHook the Lambda function was called after all required slots have been filled and the intent is ready for fulfillment.
        /// </summary>
        public string InvocationSource { get; set; }

        /// <summary>
        /// Input Mode. Could be one of <c>DTMF</c>, <c>Speech</c> or <c>Text</c>.
        /// </summary>
        public string InputMode { get; set; }

        /// <summary>
        /// Indicates the type of response. Could be one of <c>CustomPayload</c>, <c>ImageResponseCard</c>, <c>PlainText</c> or <c>SSML</c>.
        /// </summary>
        public string ResponseContentType { get; set; }

        /// <summary>
        /// The identifier of the session in use.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// The text that was used to process the input from the user. 
        /// For text or DTMF input, this is the text that the user typed. 
        /// For speech input, this is the text that was recognized from the speech.
        /// </summary>
        public string InputTranscript { get; set; }

        /// <summary>
        /// The LexV2 bot invoking the Lambda function.
        /// </summary>
        public LexV2Bot Bot { get; set; }

        /// <summary>
        /// One or more intents that Amazon Lex V2 considers possible matches to the user's utterance.
        /// </summary>
        public IList<LexV2Interpretation> Interpretations { get; set; }

        /// <summary>
        /// The next state of the dialog between the user and the bot if the Lambda function doesn't change the flow. 
        /// Only present when the <c>invocationSource</c> field is <c>DialogCodeHook</c> and when the predicted dialog action is <c>ElicitSlot</c>.
        /// </summary>
        public LexV2ProposedNextState ProposedNextState { get; set; }

        /// <summary>
        /// Request-specific attributes that the client sends in the request. Use request attributes to pass information 
        /// that doesn't need to persist for the entire session.
        /// </summary>
        public IDictionary<string, string> RequestAttributes { get; set; }

        /// <summary>
        /// The current state of the conversation between the user and your Amazon Lex V2 bot.
        /// </summary>
        public LexV2SessionState SessionState { get; set; }

        /// <summary>
        /// One or more transcriptions that Amazon Lex V2 considers possible matches to the user's audio utterance.
        /// </summary>
        public IList<LexV2Transcription> Transcriptions { get; set; }
    }
}
