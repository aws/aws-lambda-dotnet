using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using YamlDotNet.Serialization;

using Xunit;

namespace Amazon.Lambda.Tools.Test
{
    public class TemplateCodeUpdateYamlTest
    {
        static readonly string SERVERLESS_FUNCTION =
@"
AWSTemplateFormatVersion: '2010-09-09'
Transform: AWS::Serverless-2016-10-31
Resources:
  TheServerlessFunction:
    Type: AWS::Serverless::Function
    Properties:
      CodeUri: ./
      Handler: lambda.handler
      MemorySize: 1024
      Role: LambdaExecutionRole.Arn
      Runtime: dotnetcore1.0
      Timeout: 30
      Events:
        ProxyApiGreedy:
          Type: Api
          Properties:
            RestApiId: ApiGatewayApi
            Path: /{proxy+}
            Method: ANY
";

        static readonly string LAMBDA_FUNCTION =
@"
AWSTemplateFormatVersion: '2010-09-09'
Resources:
  TheLambdaFunction:
    Type: AWS::Lambda::Function
    Properties:
      Handler: lambda.handler
      MemorySize: 1024
      Role: LambdaExecutionRole.Arn
      Runtime: dotnetcore1.0
      Timeout: 30
      Code:
        S3Bucket: PlaceHolderObject
        S3Key: PlaceHolderKey
";

        const string S3_BUCKET = "myBucket";
        const string S3_OBJECT = "myObject";
        static readonly string S3_URL = $"s3://{S3_BUCKET}/{S3_OBJECT}";

        [Fact]
        public void ReplaceServerlessApiCodeLocation()
        {
            var updateTemplateBody = Utilities.UpdateCodeLocationInTemplate(SERVERLESS_FUNCTION, S3_BUCKET, S3_OBJECT);

            var root = new Deserializer().Deserialize(new StringReader(updateTemplateBody)) as IDictionary<object, object>;

            var resources = root["Resources"] as IDictionary<object, object>;
            var resource = resources["TheServerlessFunction"] as IDictionary<object, object>;
            var properties = resource["Properties"] as IDictionary<object, object>;
            Assert.Equal(S3_URL, properties["CodeUri"]);
        }

        [Fact]
        public void ReplaceLambdaFunctionCodeLocation()
        {
            var updateTemplateBody = Utilities.UpdateCodeLocationInTemplate(LAMBDA_FUNCTION, S3_BUCKET, S3_OBJECT);

            var root = new Deserializer().Deserialize(new StringReader(updateTemplateBody)) as IDictionary<object, object>;

            var resources = root["Resources"] as IDictionary<object, object>;
            var resource = resources["TheLambdaFunction"] as IDictionary<object, object>;
            var properties = resource["Properties"] as IDictionary<object, object>;
            var code = properties["Code"] as IDictionary<object, object>;

            Assert.Equal(S3_BUCKET, code["S3Bucket"]);
            Assert.Equal(S3_OBJECT, code["S3Key"]);
        }
    }
}
