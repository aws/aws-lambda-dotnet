using Amazon.Lambda.TestTool.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.TestTool.Services
{
    public interface IAwsProfileConfig
    {
        string ConfigFile();
        LambdaConfigInfo LambdaConfigInfo();
        IList<LambdaFunction> AvailableFunctions();
        IList<string> AvailableAWSProfiles();
    }
}
