using System.Collections.Generic;

namespace Amazon.Lambda.LexV2Events
{
    /// <summary>
    /// The class that represents the Amazon Lex V2 Transcription that possibly matches to the user's audio utterance.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/using-transcript-confidence-scores.html
    /// </summary>
    public class LexV2Transcription
    {
        /// <summary>
        /// Transcription Name.
        /// </summary>
        public string Transcription { get; set; }

        /// <summary>
        /// Transcription Confidence score to help determine if the transcription is the correct one. 
        /// Ranges between 0.00 and 1.00. Higher scores indicate higher confidence..
        /// </summary>
        public double? TranscriptionConfidence { get; set; }

        /// <summary>
        /// Resolved Context.
        /// </summary>
        public LexV2TranscriptionResolvedContext ResolvedContext { get; set; }

        /// <summary>
        /// A map of all of the resolved slots for the transcription.
        /// </summary> 
        public IDictionary<string, LexV2Slot> ResolvedSlots { get; set; }

        /// <summary>
        /// The class that represents a Transcription Resolved Intent.
        /// </summary>
        public class LexV2TranscriptionResolvedContext
        {
            /// <summary>
            /// The name of the intent.
            /// </summary>
            public string Intent { get; set; }
        }
    }
}
