using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.BedrockAgentEvents
{
    /// <summary>
    /// This class represents the response event for Amazon Bedrock Agent API.
    /// </summary>
    [DataContract]
    public class BedrockAgentApiResponse
    {
        /// <summary>
        /// The version of the message format.
        /// </summary>
        [DataMember(Name = "messageVersion")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("messageVersion")]
#endif
        public string MessageVersion { get; set; } = "1.0";

        /// <summary>
        /// The response details.
        /// </summary>
        [DataMember(Name = "response")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("response")]
#endif
        public Response Response { get; set; }

        /// <summary>
        /// Session attributes that persist for the entire session.
        /// </summary>
        [DataMember(Name = "sessionAttributes")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("sessionAttributes")]
#endif
        public Dictionary<string, string> SessionAttributes { get; set; }

        /// <summary>
        /// Prompt session attributes.
        /// </summary>
        [DataMember(Name = "promptSessionAttributes")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("promptSessionAttributes")]
#endif
        public Dictionary<string, string> PromptSessionAttributes { get; set; }

        /// <summary>
        /// Knowledge bases configuration.
        /// </summary>
        [DataMember(Name = "knowledgeBaseConfiguration")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("knowledgeBaseConfiguration")]
#endif
        public List<KnowledgeBaseConfiguration> KnowledgeBasesConfiguration { get; set; }
    }

    /// <summary>
    /// The response details.
    /// </summary>
    [DataContract]
    public class Response
    {
        /// <summary>
        /// The action group that was invoked.
        /// </summary>
        [DataMember(Name = "actionGroup")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("actionGroup")]
#endif
        public string ActionGroup { get; set; }

        /// <summary>
        /// The API path that was invoked.
        /// </summary>
        [DataMember(Name = "apiPath")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("apiPath")]
#endif
        public string ApiPath { get; set; }

        /// <summary>
        /// The HTTP method that was used.
        /// </summary>
        [DataMember(Name = "httpMethod")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("httpMethod")]
#endif
        public string HttpMethod { get; set; }

        /// <summary>
        /// The HTTP status code of the response.
        /// </summary>
        [DataMember(Name = "httpStatusCode")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("httpStatusCode")]
#endif
        public int HttpStatusCode { get; set; }

        /// <summary>
        /// The response body.
        /// </summary>
        [DataMember(Name = "responseBody")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("responseBody")]
#endif
        public Dictionary<string, ResponseContent> ResponseBody { get; set; }
    }

    /// <summary>
    /// The content of the response.
    /// </summary>
    [DataContract]
    public class ResponseContent
    {
        /// <summary>
        /// The body of the response as a JSON-formatted string.
        /// </summary>
        [DataMember(Name = "body")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("body")]
#endif
        public string Body { get; set; }
    }

    /// <summary>
    /// Configuration for a knowledge base.
    /// </summary>
    [DataContract]
    public class KnowledgeBaseConfiguration
    {
        /// <summary>
        /// The ID of the knowledge base.
        /// </summary>
        [DataMember(Name = "knowledgeBaseId")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("knowledgeBaseId")]
#endif
        public string KnowledgeBaseId { get; set; }

        /// <summary>
        /// The retrieval configuration for the knowledge base.
        /// </summary>
        [DataMember(Name = "retrievalConfiguration")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("retrievalConfiguration")]
#endif
        public RetrievalConfiguration RetrievalConfiguration { get; set; }
    }

    /// <summary>
    /// The retrieval configuration for a knowledge base.
    /// </summary>
    [DataContract]
    public class RetrievalConfiguration
    {
        /// <summary>
        /// The vector search configuration.
        /// </summary>
        [DataMember(Name = "vectorSearchConfiguration")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("vectorSearchConfiguration")]
#endif
        public VectorSearchConfiguration VectorSearchConfiguration { get; set; }
    }

    /// <summary>
    /// The vector search configuration.
    /// </summary>
    [DataContract]
    public class VectorSearchConfiguration
    {
        /// <summary>
        /// The number of results to return.
        /// </summary>
        [DataMember(Name = "numberOfResults")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("numberOfResults")]
#endif
        public int NumberOfResults { get; set; }

        /// <summary>
        /// The search type to use (HYBRID or SEMANTIC).
        /// </summary>
        [DataMember(Name = "overrideSearchType")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("overrideSearchType")]
#endif
        public string OverrideSearchType { get; set; }

        /// <summary>
        /// The filter to apply to the search.
        /// </summary>
        [DataMember(Name = "filter")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("filter")]
#endif
        public object Filter { get; set; }
    }
}
