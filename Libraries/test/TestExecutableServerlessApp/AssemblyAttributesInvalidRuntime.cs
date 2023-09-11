using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
[assembly: LambdaGlobalProperties(GenerateMain = true, Runtime = "notavalidruntime")]