using System;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using Newtonsoft.Json;

namespace Amazon.Lambda.TestTool.Runtime.LambdaMocks
{
    public class LocalLambdaContext : ILambdaContext
    {
        public string AwsRequestId { get; set; }
        [Newtonsoft.Json.JsonConverter(typeof(NewtonsoftConcreteTypeConverter<LocalClientContext>))]
        public IClientContext ClientContext { get; set; }
        public string FunctionName { get; set; }
        public string FunctionVersion { get; set; }
        [Newtonsoft.Json.JsonConverter(typeof(NewtonsoftConcreteTypeConverter<LocalCognitoIdentity>))]
        public ICognitoIdentity Identity { get; set; }
        public string InvokedFunctionArn { get; set; }
        public ILambdaLogger Logger { get; set; }
        public string LogGroupName { get; set; }
        public string LogStreamName { get; set; }
        public int MemoryLimitInMB { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public TimeSpan RemainingTime { get; set; }
    }

    public class LocalClientContext : IClientContext
    {
        public IDictionary<string, string> Environment { get; set; }

        [Newtonsoft.Json.JsonConverter(typeof(NewtonsoftConcreteTypeConverter<LocalClientApplication>))]
        public IClientApplication Client { get; set; }
        public IDictionary<string, string> Custom { get; set; }
    }

    public class LocalClientApplication : IClientApplication
    {
        public string AppPackageName { get; set; }
        public string AppTitle { get; set; }
        public string AppVersionCode { get; set; }
        public string AppVersionName { get; set; }
        public string InstallationId { get; set; }
    }

    public class LocalCognitoIdentity : ICognitoIdentity
    {
        public string IdentityId { get; set; }
        public string IdentityPoolId { get; set; }
    }

    public class NewtonsoftConcreteTypeConverter<TConcrete> : Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            //need to be able to deserialize/convert any property
            return true;
        }

        public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            return serializer.Deserialize<TConcrete>(reader);
        }

        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}