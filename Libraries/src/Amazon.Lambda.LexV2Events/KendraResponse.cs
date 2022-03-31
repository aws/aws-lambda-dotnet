using System;
using System.Collections.Generic;

namespace Amazon.Lambda.LexV2Events
{
    /// <summary>
    /// A class that represents Kendra Response
    /// https://docs.aws.amazon.com/kendra/latest/dg/API_Query.html#API_Query_ResponseSyntax 
    /// </summary>
    public class KendraResponse
    {
        /// <summary>
        /// Contains the facet results. A <code>FacetResult</code> contains the counts for each
        /// attribute key that was specified in the <code>Facets</code> input parameter.
        /// </summary>
        public IList<FacetResult> FacetResults { get; set; }

        /// <summary>
        /// The unique identifier for the search. You use <code>QueryId</code> to identify the
        /// search when using the feedback API.
        /// </summary>
        public string QueryId { get; set; }

        /// <summary>
        /// Gets and sets the property ResultItems. 
        /// <para>
        /// The results of the search.
        /// </para>
        /// </summary>
        public IList<QueryResultItem> ResultItems { get; set; }

        /// <summary>
        /// A list of information related to suggested spell corrections for a query.
        /// </summary>
        public IList<SpellCorrectedQuery> SpellCorrectedQueries { get; set; }

        /// <summary>
        /// The total number of items found by the search; however, you can only retrieve up to
        /// 100 items. For example, if the search found 192 items, you can only retrieve the first
        /// 100 of the items.
        /// </summary>
        public int TotalNumberOfResults { get; set; }

        /// <summary>
        /// A list of warning codes and their messages on problems with your query.
        /// 
        /// <para>
        /// Amazon Kendra currently only supports one type of warning, which is a warning on invalid
        /// syntax used in the query. For examples of invalid query syntax, see <a href="https://docs.aws.amazon.com/kendra/latest/dg/searching-example.html#searching-index-query-syntax">Searching
        /// with advanced query syntax</a>.
        /// </para>
        /// </summary>
        public IList<Warning> Warnings { get; set; }

        /// <summary>
        /// The facet values for the documents in the response.
        /// </summary>
        public class FacetResult
        {
            /// <summary>
            /// The key for the facet values. This is the same as the <code>DocumentAttributeKey</code> provided in the query.
            /// </summary>
            public string DocumentAttributeKey { get; set; }

            /// <summary>
            /// An array of key/value pairs, where the key is the value of the attribute and the count is the number of documents that share the key value.
            /// </summary>
            public IList<DocumentAttributeValueCountPair> DocumentAttributeValueCountPairs { get; set; }

            /// <summary>
            /// The data type of the facet value. This is the same as the type defined for the index field when it was created.
            /// </summary>
            public string DocumentAttributeValueType { get; set; }
        }

        /// <summary>
        /// Provides the count of documents that match a particular attribute when doing a faceted
        /// search.
        /// </summary>
        public class DocumentAttributeValueCountPair
        {
            /// <summary>
            /// The number of documents in the response that have the attribute value for the key.
            /// </summary>
            public int? Count { get; set; }

            /// <summary>
            /// The value of the attribute. For example, "HR."
            /// </summary>
            public DocumentAttributeValue DocumentAttributeValue { get; set; }
        }

        /// <summary>
        /// The value of a custom document attribute. You can only provide one value for a custom
        /// attribute.
        /// </summary>
        public class DocumentAttributeValue
        {
            /// <summary>
            /// A date expressed as an ISO 8601 string.
            ///  
            /// <para>
            /// It is important for the time zone to be included in the ISO 8601 date-time format.
            /// For example, 2012-03-25T12:30:10+01:00 is the ISO 8601 date-time format for March
            /// 25th 2012 at 12:30PM (plus 10 seconds) in Central European Time.
            /// </para>
            /// </summary>
            public DateTime? DateValue { get; set; }

            /// <summary>
            /// A long integer value.
            /// </summary>
            public long? LongValue { get; set; }

            /// <summary>
            /// A list of strings. 
            /// </summary>
            public IList<string> StringListValue { get; set; }

            /// <summary>
            /// A string, such as "department".
            /// </summary>
            public string StringValue { get; set; }
        }

        /// <summary>
        /// A query with suggested spell corrections.
        /// </summary>
        public class SpellCorrectedQuery
        {
            /// <summary>
            /// The corrected misspelled word or words in a query.
            /// </summary>
            public IList<Correction> Corrections { get; set; }

            /// <summary>
            /// The query with the suggested spell corrections.
            /// </summary>
            public string SuggestedQueryText { get; set; }
        }

        /// <summary>
        /// A corrected misspelled word in a query.
        /// </summary>
        public class Correction
        {
            /// <summary>
            /// The zero-based location in the response string or text where the corrected word starts.
            /// </summary>
            public int? BeginOffset { get; set; }

            /// <summary>
            /// The string or text of a corrected misspelled word in a query.
            /// </summary>
            public string CorrectedTerm { get; set; }

            /// <summary>
            /// The zero-based location in the response string or text where the corrected word ends.
            /// </summary>
            public int? EndOffset { get; set; }

            /// <summary>
            /// The string or text of a misspelled word in a query.
            /// </summary>
            public string Term { get; set; }
        }

        /// <summary>
        /// The warning code and message that explains a problem with a query.
        /// </summary>
        public class Warning
        {
            /// <summary>
            /// The code used to show the type of warning for the query.
            /// </summary>
            public string Code { get; set; }

            /// <summary>
            /// The message that explains the problem with the query.
            /// </summary>
            public string Message { get; set; }
        }

        /// <summary>
        /// A single query result.
        /// 
        ///  
        /// <para>
        /// A query result contains information about a document returned by the query. This includes
        /// the original location of the document, a list of attributes assigned to the document,
        /// and relevant text from the document that satisfies the query.
        /// </para>
        /// </summary>
        public class QueryResultItem
        {
            /// <summary>
            /// Gets and sets the property AdditionalAttributes. 
            /// <para>
            /// One or more additional attributes associated with the query result.
            /// </para>
            /// </summary>
            public IList<AdditionalResultAttribute> AdditionalAttributes { get; set; }

            /// <summary>
            /// An array of document attributes for the document that the query result maps to. For
            /// example, the document author (Author) or the source URI (SourceUri) of the document.
            /// </summary>
            public IList<DocumentAttribute> DocumentAttributes { get; set; }

            /// <summary>
            /// An extract of the text in the document. Contains information about highlighting the
            /// relevant terms in the excerpt.
            /// </summary>
            public TextWithHighlights DocumentExcerpt { get; set; }

            /// <summary>
            /// The unique identifier for the document.
            /// </summary>
            public string DocumentId { get; set; }

            /// <summary>
            /// The title of the document. Contains the text of the title and information for highlighting
            /// the relevant terms in the title.
            /// </summary>
            public TextWithHighlights DocumentTitle { get; set; }

            /// <summary>
            /// The URI of the original location of the document.
            /// </summary>
            public string DocumentURI { get; set; }

            /// <summary>
            /// A token that identifies a particular result from a particular query. Use this token
            /// to provide click-through feedback for the result. For more information, see <a href="https://docs.aws.amazon.com/kendra/latest/dg/submitting-feedback.html">
            /// Submitting feedback </a>.
            /// </summary>
            public string FeedbackToken { get; set; }

            /// <summary>
            /// The unique identifier for the query result.
            /// </summary>
            public string Id { get; set; }

            /// <summary>
            /// Indicates the confidence that Amazon Kendra has that a result matches the query that
            /// you provided. Each result is placed into a bin that indicates the confidence, <code>VERY_HIGH</code>,
            /// <code>HIGH</code>, <code>MEDIUM</code> and <code>LOW</code>. You can use the score
            /// to determine if a response meets the confidence needed for your application.
            ///
            /// <para>
            /// The field is only set to <code>LOW</code> when the <code>Type</code> field is set
            /// to <code>DOCUMENT</code> and Amazon Kendra is not confident that the result matches
            /// the query.
            /// </para>
            /// </summary>
            public ScoreAttributes ScoreAttributes { get; set; }

            /// <summary>
            /// The type of document. 
            /// </summary>
            public string Type { get; set; }
        }

        /// <summary>
        /// An attribute returned from an index query.
        /// </summary>
        public class AdditionalResultAttribute
        {
            /// <summary>
            /// The key that identifies the attribute.
            /// </summary>
            public string Key { get; set; }

            /// <summary>
            /// An object that contains the attribute value.
            /// </summary>
            public AdditionalResultAttributeValue Value { get; set; }

            /// <summary>
            /// The data type of the <code>Value</code> property.
            /// </summary>
            public string ValueType { get; set; }
        }

        /// <summary>
        /// An attribute returned with a document from a search.
        /// </summary>
        public class AdditionalResultAttributeValue
        {
            /// <summary>
            /// The text associated with the attribute and information about the highlight to apply
            /// to the text.
            /// </summary>
            public TextWithHighlights TextWithHighlightsValue { get; set; }
        }

        /// <summary>
        /// A custom attribute value assigned to a document.
        /// </summary>
        public class DocumentAttribute
        {
            /// <summary>
            /// The identifier for the attribute.
            /// </summary>
            public string Key { get; set; }

            /// <summary>
            /// The value of the attribute.
            /// </summary>
            public DocumentAttributeValue Value { get; set; }
        }

        /// <summary>
        /// Provides text and information about where to highlight the text.
        /// </summary>
        public class TextWithHighlights
        {
            /// <summary>
            /// The beginning and end of the text that should be highlighted.
            /// </summary>
            public IList<Highlight> Highlights { get; set; }

            /// <summary>
            /// The text to display to the user.
            /// </summary>
            public string Text { get; set; }
        }

        /// <summary>
        /// Provides information that you can use to highlight a search result so that your users
        /// can quickly identify terms in the response.
        /// </summary>
        public class Highlight
        {
            /// <summary>
            /// The zero-based location in the response string where the highlight starts.
            /// </summary>
            public int BeginOffset { get; set; }

            /// <summary>
            /// The zero-based location in the response string where the highlight ends.
            /// </summary>
            public int EndOffset { get; set; }

            /// <summary>
            /// Indicates whether the response is the best response. True if this is the best response;
            /// otherwise, false.
            /// </summary>
            public bool TopAnswer { get; set; }

            /// <summary>
            /// The highlight type. 
            /// </summary>
            public string Type { get; set; }
        }

        /// <summary>
        /// Provides a relative ranking that indicates how confident Amazon Kendra is that the
        /// response matches the query.
        /// </summary>
        public class ScoreAttributes
        {
            /// <summary>
            /// A relative ranking for how well the response matches the query.
            /// </summary>
            public string ScoreConfidence { get; set; }
        }
    }
}