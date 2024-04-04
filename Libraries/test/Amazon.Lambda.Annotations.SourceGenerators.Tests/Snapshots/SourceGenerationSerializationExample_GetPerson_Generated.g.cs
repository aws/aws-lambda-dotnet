using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Amazon.Lambda.Core;
using Amazon.Lambda.Annotations.APIGateway;

namespace TestExecutableServerlessApp
{
    public class SourceGenerationSerializationExample_GetPerson_Generated
    {
        private readonly SourceGenerationSerializationExample sourceGenerationSerializationExample;
        private readonly Amazon.Lambda.Serialization.SystemTextJson.SourceGeneratorLambdaJsonSerializer<TestExecutableServerlessApp.HttpApiJsonSerializerContext> serializer;

        public SourceGenerationSerializationExample_GetPerson_Generated()
        {
            SetExecutionEnvironment();
            sourceGenerationSerializationExample = new SourceGenerationSerializationExample();
            serializer = new Amazon.Lambda.Serialization.SystemTextJson.SourceGeneratorLambdaJsonSerializer<TestExecutableServerlessApp.HttpApiJsonSerializerContext>();
        }

        public System.IO.Stream GetPerson(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest __request__, Amazon.Lambda.Core.ILambdaContext __context__)
        {
            var httpResults = sourceGenerationSerializationExample.GetPerson(__context__);
            HttpResultSerializationOptions.ProtocolFormat serializationFormat = HttpResultSerializationOptions.ProtocolFormat.RestApi;
            HttpResultSerializationOptions.ProtocolVersion serializationVersion = HttpResultSerializationOptions.ProtocolVersion.V1;
            System.Text.Json.Serialization.JsonSerializerContext jsonContext = TestExecutableServerlessApp.HttpApiJsonSerializerContext.Default;
            var serializationOptions = new HttpResultSerializationOptions { Format = serializationFormat, Version = serializationVersion, JsonContext = jsonContext };
            var response = httpResults.Serialize(serializationOptions);
            return response;
        }

        private static void SetExecutionEnvironment()
        {
            const string envName = "AWS_EXECUTION_ENV";

            var envValue = new StringBuilder();

            // If there is an existing execution environment variable add the annotations package as a suffix.
            if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envName)))
            {
                envValue.Append($"{Environment.GetEnvironmentVariable(envName)}_");
            }

            envValue.Append("amazon-lambda-annotations_1.3.0.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}