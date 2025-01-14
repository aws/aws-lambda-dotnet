namespace Amazon.Lambda.AppSyncEvents
{
    using System.Collections.Generic;
    
    /// <summary>
    /// Represents the event payload received from AWS AppSync.
    /// </summary>
    public class AppSyncEvent
    {
        /// <summary>
        /// Gets or sets the input arguments for the GraphQL operation.
        /// </summary>
        public Dictionary<string, object> Arguments { get; set; }

        /// <summary>
        /// Gets or sets information about the identity that triggered the event.
        /// </summary>
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
            public object DomainName { get; set; }
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