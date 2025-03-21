using System.Collections.Generic;

namespace Amazon.Lambda.AppSyncEvents
{
    // Represents an AppSync authorization event
    public class AppSyncAuthorizerEvent
    {
        // The authorization token from the request
        public string AuthorizationToken { get; set; }

        // Headers from the request
        public Dictionary<string, string> RequestHeaders { get; set; }

        // Context information about the request
        public RequestContext RequestContext { get; set; }
    }

    // Request context for AppSync authorization

    public class RequestContext
    {
        // The ID of the AppSync API
        public string ApiId { get; set; }

        // The AWS account ID
        public string AccountId { get; set; }

        // Unique identifier for the request
        public string RequestId { get; set; }

        // The GraphQL query string
        public string QueryString { get; set; }

        // Name of the GraphQL operation
        public string OperationName { get; set; }

        /// Variables passed to the GraphQL operation.
        public Dictionary<string, object> Variables { get; set; }
    }
}
