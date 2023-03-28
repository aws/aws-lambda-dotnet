using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.TestTool.Runtime.LambdaMocks
{
    public class LocalLambdaContext : ILambdaContext
    {
        public string AwsRequestId { get; set; }
        
        [JsonConverter(typeof(InterfaceConverter<LocalClientContext, IClientContext>))]
        public IClientContext ClientContext { get; set; }
        public string FunctionName { get; set; }
        public string FunctionVersion { get; set; }
        [JsonConverter(typeof(InterfaceConverter<LocalCognitoIdentity, ICognitoIdentity>))]
        public ICognitoIdentity Identity { get; set; }
        public string InvokedFunctionArn { get; set; }
        [JsonIgnore]
        public ILambdaLogger Logger { get; set; }
        public string LogGroupName { get; set; }
        public string LogStreamName { get; set; }
        public int MemoryLimitInMB { get; set; }
        public TimeSpan RemainingTime { get; set; }
        
        public class LocalCognitoIdentity : ICognitoIdentity
        {
            public string IdentityId { get; set; }
            public string IdentityPoolId { get; set; }
        }
    
        public class LocalClientContext : IClientContext
        {
            [JsonConverter(typeof(InterfaceConverter<Dictionary<string, string>, IDictionary<string, string>>))]
            public IDictionary<string, string> Environment { get; set; }
            [JsonConverter(typeof(InterfaceConverter<LocalClientApplication, IClientApplication>))]
            public IClientApplication Client { get; set; }
            [JsonConverter(typeof(InterfaceConverter<Dictionary<string, string>, IDictionary<string, string>>))]
            public IDictionary<string, string> Custom { get; set; }
        
            public class LocalClientApplication : IClientApplication
            {
                public string AppPackageName { get; set; }
                public string AppTitle { get; set; }
                public string AppVersionCode { get; set; }
                public string AppVersionName { get; set; }
                public string InstallationId { get; set; }
            }
        }
    
        public class InterfaceConverter<T, I> : JsonConverter<I> where T : class, I
        {
            public override I Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return JsonSerializer.Deserialize<T>(ref reader, options);
            }

            public override void Write(Utf8JsonWriter writer, I value, JsonSerializerOptions options) { }
        }
    }
}