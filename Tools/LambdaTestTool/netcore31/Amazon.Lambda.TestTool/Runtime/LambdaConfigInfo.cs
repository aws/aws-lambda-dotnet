using System.Collections.Generic;

namespace Amazon.Lambda.TestTool.Runtime
{
    /// <summary>
    /// This class represents the config info for the available lambda functions gathered from the aws-lambda-tools-defaults.json or similiar files and possibly 
    /// an associated CloudFormation template.
    /// </summary>
    public class LambdaConfigInfo
    {
        public string AWSProfile { get; set; }
        public string AWSRegion { get; set; }
        
        public List<LambdaFunctionInfo> FunctionInfos { get; set; }
    }
}