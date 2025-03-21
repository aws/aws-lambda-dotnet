using System.Collections.Generic;

namespace Amazon.Lambda.AppSyncEvents
{
    /// <summary>
    /// Represents the event payload received from AWS AppSync.
    /// </summary>
    public class AppSyncResolverEvent<TArguments>
    {
        /// <summary>
        /// Gets or sets the input arguments for the GraphQL operation.
        /// </summary>
        public TArguments Arguments { get; set; }

        /// <summary>
        /// An object that contains information about the caller.
        /// Returns null for API_KEY authorization.
        /// Returns AppSyncIamIdentity for AWS_IAM authorization.
        /// Returns AppSyncCognitoIdentity for AMAZON_COGNITO_USER_POOLS authorization.
        /// For AWS_LAMBDA authorization, returns the object returned by your Lambda authorizer function.
        /// </summary>
        /// <remarks>
        /// The Identity object type depends on the authorization mode:
        /// - For API_KEY: null
        /// - For AWS_IAM: <see cref="AppSyncIamIdentity"/>
        /// - For AMAZON_COGNITO_USER_POOLS: <see cref="AppSyncCognitoIdentity"/>
        /// - For AWS_LAMBDA: <see cref="AppSyncLambdaIdentity"/>
        /// - For OPENID_CONNECT: <see cref="AppSyncOidcIdentity"/>
        /// </remarks>
        public object Identity { get; set; }

        /// <summary>
        /// Gets or sets information about the data source that originated the event.
        /// </summary>
        public object Source { get; set; }

        /// <summary>
        /// Gets or sets information about the HTTP request that triggered the event.
        /// </summary>
        public RequestContext Request { get; set; }

        /// <summary>
        /// Gets or sets information about the previous state of the data before the operation was executed.
        /// </summary>
        public object Prev { get; set; }

        /// <summary>
        /// Gets or sets information about the GraphQL operation being executed.
        /// </summary>
        public Information Info { get; set; }

        /// <summary>
        /// Gets or sets additional information that can be passed between Lambda functions during an AppSync pipeline.
        /// </summary>
        public Dictionary<string, object> Stash { get; set; }

        /// <summary>
        /// Represents information about the HTTP request that triggered the event.
        /// </summary>
        public class RequestContext
        {
            /// <summary>
            /// Gets or sets the headers of the HTTP request.
            /// </summary>
            public Dictionary<string, string> Headers { get; set; }

            /// <summary>
            /// Gets or sets the domain name associated with the request.
            /// </summary>
            public string DomainName { get; set; }
        }

        /// <summary>
        /// Represents information about the GraphQL operation being executed.
        /// </summary>
        public class Information
        {
            /// <summary>
            /// Gets or sets the name of the GraphQL field being executed.
            /// </summary>
            public string FieldName { get; set; }

            /// <summary>
            /// Gets or sets a list of fields being selected in the GraphQL operation.
            /// </summary>
            public List<string> SelectionSetList { get; set; }

            /// <summary>
            /// Gets or sets the GraphQL selection set for the operation.
            /// </summary>
            public string SelectionSetGraphQL { get; set; }

            /// <summary>
            /// Gets or sets the variables passed to the GraphQL operation.
            /// </summary>
            public Dictionary<string, object> Variables { get; set; }

            /// <summary>
            /// Gets or sets the parent type name for the GraphQL operation.
            /// </summary>
            public string ParentTypeName { get; set; }
        }
    }
}
