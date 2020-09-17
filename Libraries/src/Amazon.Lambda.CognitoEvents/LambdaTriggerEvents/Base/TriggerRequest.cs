using System.Collections.Generic;

namespace Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.Base
{
    public abstract class TriggerRequest
    {
        public IDictionary<string, string> UserAttributes { get; set; }
    }
}