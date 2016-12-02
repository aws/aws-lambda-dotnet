namespace Amazon.Lambda.Tests
{
    using Amazon.Lambda;
    using Amazon.Lambda.Serialization.Json;
    using Amazon.Lambda.S3Events;
    using Amazon.Lambda.KinesisEvents;
    using Amazon.Lambda.DynamoDBEvents;
    using Amazon.Lambda.CognitoEvents;
    using Amazon.Lambda.ConfigEvents;
    using Amazon.Lambda.SNSEvents;
    using Amazon.Lambda.APIGatewayEvents;

    using Newtonsoft.Json.Linq;

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Xunit;
    using System.Linq;

    public class EventTest
    {
        string S3PutJson = "{ \"Records\": [ { \"eventVersion\": \"2.0\", \"eventTime\": \"1970-01-01T00:00:00.000Z\", \"requestParameters\": { \"sourceIPAddress\": \"127.0.0.1\" }, \"s3\": { \"configurationId\": \"testConfigRule\", \"object\": { \"eTag\": \"0123456789abcdef0123456789abcdef\", \"sequencer\": \"0A1B2C3D4E5F678901\", \"key\": \"HappyFace.jpg\", \"size\": 1024 }, \"bucket\": { \"arn\": \"arn:aws:s3:::mybucket\", \"name\": \"sourcebucket\", \"ownerIdentity\": { \"principalId\": \"EXAMPLE\" } }, \"s3SchemaVersion\": \"1.0\" }, \"responseElements\": { \"x-amz-id-2\": \"EXAMPLE123/5678abcdefghijklambdaisawesome/mnopqrstuvwxyzABCDEFGH\", \"x-amz-request-id\": \"EXAMPLE123456789\" }, \"awsRegion\": \"us-east-1\", \"eventName\": \"ObjectCreated:Put\", \"userIdentity\": { \"principalId\": \"EXAMPLE\" }, \"eventSource\": \"aws:s3\" } ] }";

        [Fact]
        public void S3PutTest()
        {
            Stream json = new MemoryStream(Encoding.UTF8.GetBytes(S3PutJson));
            json.Position = 0;
            var serializer = new JsonSerializer();
            var s3Event = serializer.Deserialize<S3Event>(json);
            Assert.Equal(s3Event.Records.Count, 1);
            var record = s3Event.Records[0];
            Assert.Equal(record.EventVersion, "2.0");
            Assert.Equal(record.EventTime.ToUniversalTime(), DateTime.Parse("1970-01-01T00:00:00.000Z").ToUniversalTime());
            Assert.Equal(record.RequestParameters.SourceIPAddress, "127.0.0.1");
            Assert.Equal(record.S3.ConfigurationId, "testConfigRule");
            Assert.Equal(record.S3.Object.ETag, "0123456789abcdef0123456789abcdef");
            Assert.Equal(record.S3.Object.Key, "HappyFace.jpg");
            Assert.Equal(record.S3.Object.Size, 1024);
            Assert.Equal(record.S3.Bucket.Arn, "arn:aws:s3:::mybucket");
            Assert.Equal(record.S3.Bucket.Name, "sourcebucket");
            Assert.Equal(record.S3.Bucket.OwnerIdentity.PrincipalId, "EXAMPLE");
            Assert.Equal(record.S3.S3SchemaVersion, "1.0");
            Assert.Equal(record.ResponseElements.XAmzId2, "EXAMPLE123/5678abcdefghijklambdaisawesome/mnopqrstuvwxyzABCDEFGH");
            Assert.Equal(record.ResponseElements.XAmzRequestId, "EXAMPLE123456789");
            Assert.Equal(record.AwsRegion, "us-east-1");
            Assert.Equal(record.EventName, "ObjectCreated:Put");
            Assert.Equal(record.UserIdentity.PrincipalId, "EXAMPLE");
            Assert.Equal(record.EventSource, "aws:s3");

            Handle(s3Event);
        }

        private void Handle(S3Event s3Event)
        {
            foreach (var record in s3Event.Records)
            {
                var s3 = record.S3;
                Console.WriteLine($"[{record.EventSource} - {record.EventTime}] Bucket = {s3.Bucket.Name}, Key = {s3.Object.Key}");
            }
        }

        [Fact]
        public void KinesisTest()
        {
            using (var fileStream = File.OpenRead("kinesis-event.json"))
            {
                var serializer = new JsonSerializer();
                var kinesisEvent = serializer.Deserialize<KinesisEvent>(fileStream);
                Assert.Equal(kinesisEvent.Records.Count, 2);
                var record = kinesisEvent.Records[0];
                Assert.Equal(record.EventId, "shardId-000000000000:49568167373333333333333333333333333333333333333333333333");
                Assert.Equal(record.EventVersion, "1.0");
                Assert.Equal(record.Kinesis.PartitionKey, "s1");
                var dataBytes = record.Kinesis.Data.ToArray();
                Assert.Equal(Convert.ToBase64String(dataBytes), "SGVsbG8gV29ybGQ=");
                Assert.Equal(Encoding.UTF8.GetString(dataBytes), "Hello World");
                Assert.Equal(record.Kinesis.KinesisSchemaVersion, "1.0");
                Assert.Equal(record.Kinesis.SequenceNumber, "49568167373333333333333333333333333333333333333333333333");
                Assert.Equal(record.InvokeIdentityArn, "arn:aws:iam::123456789012:role/LambdaRole");
                Assert.Equal(record.EventName, "aws:kinesis:record");
                Assert.Equal(record.EventSourceARN, "arn:aws:kinesis:us-east-1:123456789012:stream/simple-stream");
                Assert.Equal(record.EventSource, "aws:kinesis");
                Assert.Equal(record.AwsRegion, "us-east-1");
                Assert.Equal(636162383234770000, record.Kinesis.ApproximateArrivalTimestamp.ToUniversalTime().Ticks);

                Handle(kinesisEvent);
            }
        }

        private void Handle(KinesisEvent kinesisEvent)
        {
            foreach (var record in kinesisEvent.Records)
            {
                var kinesisRecord = record.Kinesis;
                var dataBytes = kinesisRecord.Data.ToArray();
                var dataText = Encoding.UTF8.GetString(dataBytes);
                Assert.Equal("Hello World", dataText);
                Console.WriteLine($"[{record.EventName}] Data = '{dataText}'.");
            }
        }

        [Fact]
        public void DynamoDbUpdateTest()
        {
            var jsonText = File.ReadAllText("dynamodb-event.json");
            Stream json = new MemoryStream(Encoding.UTF8.GetBytes(jsonText));
            json.Position = 0;
            var serializer = new JsonSerializer();
            var dynamodbEvent = serializer.Deserialize<DynamoDBEvent>(json);
            Assert.Equal(dynamodbEvent.Records.Count, 2);

            var record = dynamodbEvent.Records[0];
            Assert.Equal(record.EventID, "f07f8ca4b0b26cb9c4e5e77e69f274ee");
            Assert.Equal(record.EventVersion, "1.1");
            Assert.Equal(record.Dynamodb.Keys.Count, 2);
            Assert.Equal(record.Dynamodb.Keys["key"].S, "binary");
            Assert.Equal(record.Dynamodb.Keys["val"].S, "data");
            Assert.Equal(record.Dynamodb.NewImage["val"].S, "data");
            Assert.Equal(record.Dynamodb.NewImage["key"].S, "binary");
            Assert.Equal(MemoryStreamToBase64String(record.Dynamodb.NewImage["asdf1"].B), "AAEqQQ==");
            Assert.Equal(record.Dynamodb.NewImage["asdf2"].BS.Count, 2);
            Assert.Equal(MemoryStreamToBase64String(record.Dynamodb.NewImage["asdf2"].BS[0]), "AAEqQQ==");
            Assert.Equal(MemoryStreamToBase64String(record.Dynamodb.NewImage["asdf2"].BS[1]), "QSoBAA==");
            Assert.Equal(record.Dynamodb.StreamViewType, "NEW_AND_OLD_IMAGES");
            Assert.Equal(record.Dynamodb.SequenceNumber, "1405400000000002063282832");
            Assert.Equal(record.Dynamodb.SizeBytes, 54);
            Assert.Equal(record.AwsRegion, "us-east-1");
            Assert.Equal(record.EventName, "INSERT");
            Assert.Equal(record.EventSourceArn, "arn:aws:dynamodb:us-east-1:123456789012:table/Example-Table/stream/2016-12-01T00:00:00.000");
            Assert.Equal(record.EventSource, "aws:dynamodb");
            var recordDateTime = record.Dynamodb.ApproximateCreationDateTime;
            Assert.Equal(recordDateTime.Ticks, 636162388200000000);

            Handle(dynamodbEvent);
        }
        private static void Handle(DynamoDBEvent ddbEvent)
        {
            foreach (var record in ddbEvent.Records)
            {
                var ddbRecord = record.Dynamodb;
                var keys = string.Join(", ", ddbRecord.Keys.Keys);
                Console.WriteLine($"{record.EventID} - Keys = [{keys}], Size = {ddbRecord.SizeBytes} bytes");
            }
            Console.WriteLine($"Successfully processed {ddbEvent.Records.Count} records.");
        }

        String CognitoEvent = "{ \"datasetName\": \"datasetName\", \"eventType\": \"SyncTrigger\", \"region\": \"us-east-1\", \"identityId\": \"identityId\", \"datasetRecords\": { \"SampleKey1\": { \"newValue\": \"newValue1\", \"oldValue\": \"oldValue1\", \"op\": \"replace\" } }, \"identityPoolId\": \"identityPoolId\", \"version\": 2 }";

        [Fact]
        public void CognitoTest()
        {
            Stream json = new MemoryStream(Encoding.UTF8.GetBytes(CognitoEvent));
            json.Position = 0;
            var serializer = new JsonSerializer();
            var cognitoEvent = serializer.Deserialize<CognitoEvent>(json);
            Assert.Equal(cognitoEvent.Version, 2);
            Assert.Equal(cognitoEvent.EventType, "SyncTrigger");
            Assert.Equal(cognitoEvent.Region, "us-east-1");
            Assert.Equal(cognitoEvent.DatasetName, "datasetName");
            Assert.Equal(cognitoEvent.IdentityPoolId, "identityPoolId");
            Assert.Equal(cognitoEvent.IdentityId, "identityId");
            Assert.Equal(cognitoEvent.DatasetRecords.Count, 1);
            Assert.True(cognitoEvent.DatasetRecords.ContainsKey("SampleKey1"));
            Assert.Equal(cognitoEvent.DatasetRecords["SampleKey1"].NewValue, "newValue1");
            Assert.Equal(cognitoEvent.DatasetRecords["SampleKey1"].OldValue, "oldValue1");
            Assert.Equal(cognitoEvent.DatasetRecords["SampleKey1"].Op, "replace");

            Handle(cognitoEvent);
        }

        private static void Handle(CognitoEvent cognitoEvent)
        {
            foreach(var datasetKVP in cognitoEvent.DatasetRecords)
            {
                var datasetName = datasetKVP.Key;
                var datasetRecord = datasetKVP.Value;

                Console.WriteLine($"[{cognitoEvent.EventType}-{datasetName}] {datasetRecord.OldValue} -> {datasetRecord.Op} -> {datasetRecord.NewValue}");
            }
        }

        String ConfigEvent = "{ \"configRuleId\": \"config-rule-0123456\", \"version\": \"1.0\", \"configRuleName\": \"periodic-config-rule\", \"configRuleArn\": \"arn:aws:config:us-east-1:012345678912:config-rule/config-rule-0123456\", \"invokingEvent\": \"{\\\"configSnapshotId\\\":\\\"00000000-0000-0000-0000-000000000000\\\",\\\"s3ObjectKey\\\":\\\"AWSLogs/000000000000/Config/us-east-1/2016/2/24/ConfigSnapshot/000000000000_Config_us-east-1_ConfigSnapshot_20160224T182319Z_00000000-0000-0000-0000-000000000000.json.gz\\\",\\\"s3Bucket\\\":\\\"config-bucket\\\",\\\"notificationCreationTime\\\":\\\"2016-02-24T18:23:20.328Z\\\",\\\"messageType\\\":\\\"ConfigurationSnapshotDeliveryCompleted\\\",\\\"recordVersion\\\":\\\"1.1\\\"}\", \"resultToken\": \"myResultToken\", \"eventLeftScope\": false, \"ruleParameters\": \"{\\\"<myParameterKey>\\\":\\\"<myParameterValue>\\\"}\", \"executionRoleArn\": \"arn:aws:iam::012345678912:role/config-role\", \"accountId\": \"012345678912\" }";
        String ConfigInvokingEvent = "{\"configSnapshotId\":\"00000000-0000-0000-0000-000000000000\",\"s3ObjectKey\":\"AWSLogs/000000000000/Config/us-east-1/2016/2/24/ConfigSnapshot/000000000000_Config_us-east-1_ConfigSnapshot_20160224T182319Z_00000000-0000-0000-0000-000000000000.json.gz\",\"s3Bucket\":\"config-bucket\",\"notificationCreationTime\":\"2016-02-24T18:23:20.328Z\",\"messageType\":\"ConfigurationSnapshotDeliveryCompleted\",\"recordVersion\":\"1.1\"}";

        [Fact]
        public void ConfigTest()
        {
            Stream json = new MemoryStream(Encoding.UTF8.GetBytes(ConfigEvent));
            json.Position = 0;
            var serializer = new JsonSerializer();
            var configEvent = serializer.Deserialize<ConfigEvent>(json);
            Assert.Equal(configEvent.ConfigRuleId, "config-rule-0123456");
            Assert.Equal(configEvent.Version, "1.0");
            Assert.Equal(configEvent.ConfigRuleName, "periodic-config-rule");
            Assert.Equal(configEvent.ConfigRuleArn, "arn:aws:config:us-east-1:012345678912:config-rule/config-rule-0123456");
            Assert.Equal(configEvent.InvokingEvent, ConfigInvokingEvent);
            Assert.Equal(configEvent.ResultToken, "myResultToken");
            Assert.Equal(configEvent.EventLeftScope, false);
            Assert.Equal(configEvent.RuleParameters, "{\"<myParameterKey>\":\"<myParameterValue>\"}");
            Assert.Equal(configEvent.ExecutionRoleArn, "arn:aws:iam::012345678912:role/config-role");
            Assert.Equal(configEvent.AccountId, "012345678912");

            Handle(configEvent);
        }

        private static void Handle(ConfigEvent configEvent)
        {
            Console.WriteLine($"AWS Config rule - {configEvent.ConfigRuleName}");
            Console.WriteLine($"Invoking event JSON - {configEvent.InvokingEvent}");
            Console.WriteLine($"Event version - {configEvent.Version}");
        }

        string SNSJson = "{ \"Records\": [ { \"EventVersion\": \"1.0\", \"EventSubscriptionArn\": \"arn:aws:sns:EXAMPLE\", \"EventSource\": \"aws:sns\", \"Sns\": { \"SignatureVersion\": \"1\", \"Timestamp\": \"1970-01-01T00:00:00.000Z\", \"Signature\": \"EXAMPLE\", \"SigningCertUrl\": \"EXAMPLE\", \"MessageId\": \"95df01b4-ee98-5cb9-9903-4c221d41eb5e\", \"Message\": \"Hello from SNS!\", \"MessageAttributes\": { \"Test\": { \"Type\": \"String\", \"Value\": \"TestString\" }, \"TestBinary\": { \"Type\": \"Binary\", \"Value\": \"TestBinary\" } }, \"Type\": \"Notification\", \"UnsubscribeUrl\": \"EXAMPLE\", \"TopicArn\": \"arn:aws:sns:EXAMPLE\", \"Subject\": \"TestInvoke\" } } ] }";

        [Fact]
        public void SNSTest()
        {
            Stream json = new MemoryStream(Encoding.UTF8.GetBytes(SNSJson));
            json.Position = 0;
            var serializer = new JsonSerializer();
            var snsEvent = serializer.Deserialize<SNSEvent>(json);
            Assert.Equal(snsEvent.Records.Count, 1);
            var record = snsEvent.Records[0];
            Assert.Equal(record.EventVersion, "1.0");
            Assert.Equal(record.EventSubscriptionArn, "arn:aws:sns:EXAMPLE");
            Assert.Equal(record.EventSource, "aws:sns");
            Assert.Equal(record.Sns.SignatureVersion, "1");
            Assert.Equal(record.Sns.Timestamp.ToUniversalTime(), DateTime.Parse("1970-01-01T00:00:00.000Z").ToUniversalTime());
            Assert.Equal(record.Sns.Signature, "EXAMPLE");
            Assert.Equal(record.Sns.SigningCertUrl, "EXAMPLE");
            Assert.Equal(record.Sns.MessageId, "95df01b4-ee98-5cb9-9903-4c221d41eb5e");
            Assert.Equal(record.Sns.Message, "Hello from SNS!");
            Assert.True(record.Sns.MessageAttributes.ContainsKey("Test"));
            Assert.Equal(record.Sns.MessageAttributes["Test"].Type, "String");
            Assert.Equal(record.Sns.MessageAttributes["Test"].Value, "TestString");
            Assert.True(record.Sns.MessageAttributes.ContainsKey("TestBinary"));
            Assert.Equal(record.Sns.MessageAttributes["TestBinary"].Type, "Binary");
            Assert.Equal(record.Sns.MessageAttributes["TestBinary"].Value, "TestBinary");
            Assert.Equal(record.Sns.Type, "Notification");
            Assert.Equal(record.Sns.UnsubscribeUrl, "EXAMPLE");
            Assert.Equal(record.Sns.TopicArn, "arn:aws:sns:EXAMPLE");
            Assert.Equal(record.Sns.Subject, "TestInvoke");

            Handle(snsEvent);
        }
        private static void Handle(SNSEvent snsEvent)
        {
            foreach (var record in snsEvent.Records)
            {
                var snsRecord = record.Sns;
                Console.WriteLine($"[{record.EventSource} {snsRecord.Timestamp}] Message = {snsRecord.Message}");
            }
        }

        string APIGatewayProxyRequestJson = "{ \"resource\": \"/{proxy+}\",   \"path\": \"/hello/world\",   \"httpMethod\": \"POST\",   \"headers\": {     \"Accept\": \"*/*\",     \"Accept-Encoding\": \"gzip, deflate\",     \"cache-control\": \"no-cache\",     \"CloudFront-Forwarded-Proto\": \"https\",     \"CloudFront-Is-Desktop-Viewer\": \"true\",     \"CloudFront-Is-Mobile-Viewer\": \"false\",     \"CloudFront-Is-SmartTV-Viewer\": \"false\",     \"CloudFront-Is-Tablet-Viewer\": \"false\",     \"CloudFront-Viewer-Country\": \"US\",     \"Content-Type\": \"application/json\",     \"headerName\": \"headerValue\",     \"Host\": \"gy415nuibc.execute-api.us-east-1.amazonaws.com\",     \"Postman-Token\": \"9f583ef0-ed83-4a38-aef3-eb9ce3f7a57f\",     \"User-Agent\": \"PostmanRuntime/2.4.5\",     \"Via\": \"1.1 d98420743a69852491bbdea73f7680bd.cloudfront.net (CloudFront)\",     \"X-Amz-Cf-Id\": \"pn-PWIJc6thYnZm5P0NMgOUglL1DYtl0gdeJky8tqsg8iS_sgsKD1A==\",     \"X-Forwarded-For\": \"54.240.196.186, 54.182.214.83\",     \"X-Forwarded-Port\": \"443\",     \"X-Forwarded-Proto\": \"https\"   },   \"queryStringParameters\": {     \"name\": \"me\"   },   \"pathParameters\": {     \"proxy\": \"hello/world\"   },   \"stageVariables\": {     \"stageVariableName\": \"stageVariableValue\"   },   \"requestContext\": {     \"accountId\": \"12345678912\",     \"resourceId\": \"roq9wj\",     \"stage\": \"testStage\",     \"requestId\": \"deef4878-7910-11e6-8f14-25afc3e9ae33\",     \"identity\": {       \"cognitoIdentityPoolId\": \"theCognitoIdentityPoolId\",       \"accountId\": \"theAccountId\",       \"cognitoIdentityId\": \"theCognitoIdentityId\",       \"caller\": \"theCaller\",       \"apiKey\": \"theApiKey\",       \"sourceIp\": \"192.168.196.186\",       \"cognitoAuthenticationType\": \"theCognitoAuthenticationType\",       \"cognitoAuthenticationProvider\": \"theCognitoAuthenticationProvider\",       \"userArn\": \"theUserArn\",       \"userAgent\": \"PostmanRuntime/2.4.5\",       \"user\": \"theUser\"     },     \"resourcePath\": \"/{proxy+}\",     \"httpMethod\": \"POST\",     \"apiId\": \"gy415nuibc\"   },   \"body\": \"{\\r\\n\\t\\\"a\\\": 1\\r\\n}\" }";

        [Fact]
        public void APIGatewayProxyRequestTest()
        {
            Stream json = new MemoryStream(Encoding.UTF8.GetBytes(APIGatewayProxyRequestJson));
            json.Position = 0;
            var serializer = new JsonSerializer();
            var proxyEvent = serializer.Deserialize<APIGatewayProxyRequest>(json);

            Assert.Equal(proxyEvent.Resource, "/{proxy+}");
            Assert.Equal(proxyEvent.Path, "/hello/world");
            Assert.Equal(proxyEvent.HttpMethod, "POST");
            Assert.Equal(proxyEvent.Body, "{\r\n\t\"a\": 1\r\n}");

            var headers = proxyEvent.Headers;
            Assert.Equal(headers["Accept"], "*/*");
            Assert.Equal(headers["Accept-Encoding"], "gzip, deflate");
            Assert.Equal(headers["cache-control"], "no-cache");
            Assert.Equal(headers["CloudFront-Forwarded-Proto"], "https");

            var queryStringParameters = proxyEvent.QueryStringParameters;
            Assert.Equal(queryStringParameters["name"], "me");

            var pathParameters = proxyEvent.PathParameters;
            Assert.Equal(pathParameters["proxy"], "hello/world");

            var stageVariables = proxyEvent.StageVariables;
            Assert.Equal(stageVariables["stageVariableName"], "stageVariableValue");

            var requestContext = proxyEvent.RequestContext;
            Assert.Equal(requestContext.AccountId, "12345678912");
            Assert.Equal(requestContext.ResourceId, "roq9wj");
            Assert.Equal(requestContext.Stage, "testStage");
            Assert.Equal(requestContext.RequestId, "deef4878-7910-11e6-8f14-25afc3e9ae33");

            var identity = requestContext.Identity;
            Assert.Equal(identity.CognitoIdentityPoolId, "theCognitoIdentityPoolId");
            Assert.Equal(identity.AccountId, "theAccountId");
            Assert.Equal(identity.CognitoIdentityId, "theCognitoIdentityId");
            Assert.Equal(identity.Caller, "theCaller");
            Assert.Equal(identity.ApiKey, "theApiKey");
            Assert.Equal(identity.SourceIp, "192.168.196.186");
            Assert.Equal(identity.CognitoAuthenticationType, "theCognitoAuthenticationType");
            Assert.Equal(identity.CognitoAuthenticationProvider, "theCognitoAuthenticationProvider");
            Assert.Equal(identity.UserArn, "theUserArn");
            Assert.Equal(identity.UserAgent, "PostmanRuntime/2.4.5");
            Assert.Equal(identity.User, "theUser");

            Handle(proxyEvent);
        }

        private static APIGatewayProxyResponse Handle(APIGatewayProxyRequest apigProxyEvent)
        {
            Console.WriteLine($"Processing request data for request {apigProxyEvent.RequestContext.RequestId}.");
            Console.WriteLine($"Body size = {apigProxyEvent.Body.Length}.");
            var headerNames = string.Join(", ", apigProxyEvent.Headers.Keys);
            Console.WriteLine($"Specified headers = {headerNames}.");

            return new APIGatewayProxyResponse
            {
                Body = apigProxyEvent.Body,
                StatusCode = 200,
            };
        }

        [Fact]
        public void APIGatewayProxyResponseTest()
        {
            var response = new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string> { { "Header1", "Value1" }, { "Header2", "Value2" } },
                Body = "theBody"
            };

            string serializedJson;
            using (MemoryStream stream = new MemoryStream())
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(response, stream);

                stream.Position = 0;
                serializedJson = Encoding.UTF8.GetString(stream.ToArray());
            }

            JObject root = Newtonsoft.Json.JsonConvert.DeserializeObject(serializedJson) as JObject;
            Assert.Equal(root["statusCode"], 200);
            Assert.Equal(root["body"], "theBody");

            Assert.NotNull(root["headers"]);
            var headers = root["headers"] as JObject;
            Assert.Equal(headers["Header1"], "Value1");
            Assert.Equal(headers["Header2"], "Value2");
        }

        private string MemoryStreamToBase64String(MemoryStream ms)
        {
            var data = ms.ToArray();
            return Convert.ToBase64String(data);
        }
    }
}
