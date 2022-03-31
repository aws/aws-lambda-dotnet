namespace Amazon.Lambda.LexV2Events
{
    /// <summary>
    /// The class identifies the Lex V2 bot that is invoking the Lambda function.
    /// </summary>
    public class LexV2Bot
    {
        /// <summary>
        /// The unique identifier assigned to the bot.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The name of the bot.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The unique identifier assigned to the bot alias.
        /// </summary>
        public string AliasId { get; set; }

        /// <summary>
        /// The language and locale of the bot locale.
        /// </summary>
        public string LocaleId { get; set; }

        /// <summary>
        /// The numeric version of the bot.
        /// </summary>
        public string Version { get; set; }
    }
}
