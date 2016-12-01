using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

namespace Amazon.Lambda.TestUtilities
{
    /// <summary>
    /// A test implementation of the ICognitoIdentity interface used for writing local tests of Lambda Functions.
    /// </summary>
    public class TestCognitoIdentity : ICognitoIdentity
    {
        /// <summary>
        /// The Amazon Cognito identity ID.
        /// </summary>
        public string IdentityId { get; set; }

        /// <summary>
        /// The Amazon Cognito identity pool ID.
        /// </summary>
        public string IdentityPoolId { get; set; }
    }
}
