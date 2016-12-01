namespace Amazon.Lambda.Core
{
    using System.Collections.Generic;

    /// <summary>
    /// Information about client configuration and execution environment.
    /// </summary>
    public interface IClientContext
    {
        /// <summary>
        /// Environment information provided by mobile SDK. 
        /// </summary>
        IDictionary<string, string> Environment { get; }

        /// <summary>
        /// The client information provided by the AWS Mobile SDK.
        /// </summary>
        IClientApplication Client { get; }

        /// <summary>
        /// Custom values set by the client application.
        /// </summary>
        IDictionary<string, string> Custom { get; }
    }
}