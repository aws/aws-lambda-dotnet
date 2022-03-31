namespace Amazon.Lambda.LexV2Events
{
    /// <summary>
    /// An intent that Amazon Lex V2 determined might satisfy the user's utterance. The intents are ordered by the confidence score.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_Interpretation.html
    /// </summary>
    public class LexV2Interpretation
    {
        /// <summary>
        /// Intents that might satisfy the user's utterance.
        /// </summary>
        public LexV2Intent Intent { get; set; }

        /// <summary>
        /// Determines the threshold where Amazon Lex V2 will insert the <c>AMAZON.FallbackIntent</c>, <c>AMAZON.KendraSearchIntent</c>, or both when returning 
        /// alternative intents in a response. <c>AMAZON.FallbackIntent</c> and <c>AMAZON.KendraSearchIntent</c> are only inserted if they are configured for the bot.
        /// </summary>
        public LexV2ConfidenceScore NluConfidence { get; set; }

        /// <summary>
        /// The sentiment expressed in an utterance.
        /// </summary>
        /// 
        public LexV2SentimentResponse SentimentResponse { get; set; }
    }

    /// <summary>
    /// The class that represents a score that indicates the confidence that Amazon Lex V2 has that an intent is the one that satisfies the user's intent.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_ConfidenceScore.html
    /// </summary>
    public class LexV2ConfidenceScore
    {
        /// <summary>
        /// A score that indicates how confident Amazon Lex V2 is that an intent satisfies the user's intent. 
        /// Ranges between 0.00 and 1.00. Higher scores indicate higher confidence.
        /// </summary>
        public double? Score { get; set; }
    }

    /// <summary>
    /// Provides information about the sentiment expressed in a user's response in a conversation. Sentiments are determined using Amazon Comprehend. 
    /// Sentiments are only returned if they are enabled for the bot.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_SentimentResponse.html
    /// </summary>
    public class LexV2SentimentResponse
    {
        /// <summary>
        /// The overall sentiment expressed in the user's response. This is the sentiment most likely expressed by the user based on the analysis by Amazon Comprehend.
        /// </summary>
        public string Sentiment { get; set; }

        /// <summary>
        /// The individual sentiment responses for the utterance.
        /// </summary>
        public LexV2SentimentScore SentimentScore { get; set; }
    }

    /// <summary>
    /// The class that represents the individual sentiment responses for the utterance.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/API_runtime_SentimentScore.html
    /// </summary>
    public class LexV2SentimentScore
    {
        /// <summary>
        /// The level of confidence that Amazon Comprehend has in the accuracy of its detection of the <c>MIXED</c> sentiment.
        /// </summary>
        public double? Mixed { get; set; }

        /// <summary>
        /// The level of confidence that Amazon Comprehend has in the accuracy of its detection of the <c>NEGATIVE</c> sentiment.
        /// </summary>
        public double? Negative { get; set; }

        /// <summary>
        /// The level of confidence that Amazon Comprehend has in the accuracy of its detection of the <c>NEUTRAL</c> sentiment.
        /// </summary>
        public double? Neutral { get; set; }

        /// <summary>
        /// The level of confidence that Amazon Comprehend has in the accuracy of its detection of the <c>POSITIVE</c> sentiment.
        /// </summary>
        public double? Positive { get; set; }
    }
}
