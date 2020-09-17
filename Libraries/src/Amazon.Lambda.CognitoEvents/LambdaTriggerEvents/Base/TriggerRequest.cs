using System.Collections.Generic;

namespace Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.Base
{
    /// <summary>
    /// The request from the Amazon Cognito service.
    /// </summary>
    public abstract class TriggerRequest
    {
        /// <summary>
        /// One or more pairs of user attribute names and values. Each pair is in the form "name": "value".
        /// </summary>
        public IDictionary<string, string> UserAttributes { get; set; }
    }
}