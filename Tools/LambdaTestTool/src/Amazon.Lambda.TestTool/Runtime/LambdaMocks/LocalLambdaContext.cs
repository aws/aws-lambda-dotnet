using System;
using System.Text.Json.Serialization;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.TestTool.Runtime.LambdaMocks
{
    public class LocalLambdaContext : ILambdaContext
    {
        public string AwsRequestId { get; set; }
        public IClientContext ClientContext { get; set; }
        public string FunctionName { get; set; }
        public string FunctionVersion { get; set; }
        public ICognitoIdentity Identity { get; set; }
        public string InvokedFunctionArn { get; set; }
        public ILambdaLogger Logger { get; set; }
        public string LogGroupName { get; set; }
        public string LogStreamName { get; set; }
        public int MemoryLimitInMB { get; set; }
        [JsonIgnore]
        public TimeSpan RemainingTime { get; set; }
    }
}