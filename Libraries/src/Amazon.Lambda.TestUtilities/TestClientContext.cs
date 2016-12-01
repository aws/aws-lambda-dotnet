using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

namespace Amazon.Lambda.TestUtilities
{
    /// <summary>
    /// A test implementation of the IClientContext interface used for writing local tests of Lambda Functions.
    /// </summary>
    public class TestClientContext : IClientContext
    {
        /// <summary>
        /// The client information provided by the AWS Mobile SDK.
        /// </summary>
        public IClientApplication Client { get; set; }

        /// <summary>
        /// Custom values set by the client application.
        /// </summary>
        public IDictionary<string, string> Custom { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Environment information provided by mobile SDK. 
        /// </summary>
        public IDictionary<string, string> Environment { get; set; } = new Dictionary<string, string>();
    }
}
