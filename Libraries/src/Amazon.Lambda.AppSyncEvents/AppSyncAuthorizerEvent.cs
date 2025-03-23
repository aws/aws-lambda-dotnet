using System.Collections.Generic;
namespace Amazon.Lambda.AppSyncEvents
{
    /// <summary>
    /// Represents an AWS AppSync authorization event that is sent to a Lambda authorizer
    /// for evaluating access permissions to the GraphQL API.
    /// </summary>
    public class AppSyncAuthorizerEvent
    {
        /// <summary>
        /// Gets or sets the authorization token received from the client request.
        /// This token is used to make authorization decisions.
        /// </summary>
        public string AuthorizationToken { get; set; }

        /// <summary>
        /// Gets or sets the headers from the client request.
        /// Contains key-value pairs of HTTP header names and their values.
        /// </summary>
        public Dictionary<string, string> RequestHeaders { get; set; }

        /// <summary>
        /// Gets or sets the context information about the AppSync request.
        /// Contains metadata about the API and the GraphQL operation being executed.
        /// </summary>
        public RequestContext RequestContext { get; set; }
    }

    /// <summary>
    /// Contains contextual information about the AppSync request being authorized.
    /// This class provides details about the API, account, and GraphQL operation.
    /// </summary>
    public class RequestContext
    {
        /// <summary>
        /// Gets or sets the unique identifier of the AppSync API.
        /// </summary>
        public string ApiId { get; set; }

        /// <summary>
        /// Gets or sets the AWS account ID where the AppSync API is deployed.
        /// </summary>
        public string AccountId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for this specific request.
        /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// Gets or sets the GraphQL query string containing the operation to be executed.
        /// </summary>
        public string QueryString { get; set; }

        /// <summary>
        /// Gets or sets the name of the GraphQL operation to be executed.
        /// This corresponds to the operation name in the GraphQL query.
        /// </summary>
        public string OperationName { get; set; }

        /// <summary>
        /// Gets or sets the variables passed to the GraphQL operation.
        /// Contains key-value pairs of variable names and their values.
        /// </summary>
        public Dictionary<string, object> Variables { get; set; }
    }
}
