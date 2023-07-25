using System.Collections.Generic;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.TestTool.Runtime.LambdaMocks
{
    public class LocalClientContext: IClientContext
    {
        public IDictionary<string, string> Environment { get; set; }

        public IClientApplication Client { get; set; }

        public IDictionary<string, string> Custom { get; set; }
    }
}