#pragma warning disable 618
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.Lambda.CloudWatchEvents.BatchEvents;
using Amazon.Lambda.CloudWatchEvents.ECSEvents;
using Amazon.Lambda.CloudWatchEvents.S3Events;
using Amazon.Lambda.CloudWatchEvents.ScheduledEvents;
using Amazon.Lambda.CloudWatchEvents.TranscribeEvents;
using Amazon.Lambda.CloudWatchEvents.TranslateEvents;
using Amazon.Lambda.CloudWatchLogsEvents;
using Amazon.Lambda.CognitoEvents;
using Amazon.Lambda.ConfigEvents;
using Amazon.Lambda.ConnectEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.KafkaEvents;
using Amazon.Lambda.KinesisAnalyticsEvents;
using Amazon.Lambda.KinesisEvents;
using Amazon.Lambda.KinesisFirehoseEvents;
using Amazon.Lambda.LexEvents;
using Amazon.Lambda.LexV2Events;
using Amazon.Lambda.MQEvents;
using Amazon.Lambda.S3Events;
using Amazon.Lambda.Serialization.Json;
using Amazon.Lambda.SimpleEmailEvents;
using Amazon.Lambda.SNSEvents;
using Amazon.Lambda.SQSEvents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using JsonSerializer = Amazon.Lambda.Serialization.Json.JsonSerializer;

namespace Amazon.Lambda.Tests
{

    public class EventTest
    {

        // This utility method takes care of removing the BOM that System.Text.Json doesn't like.
        public MemoryStream LoadJsonTestFile(string filename)
        {
            var json = File.ReadAllText(filename);
            return new MemoryStream(UTF8Encoding.UTF8.GetBytes(json));
        }

        public string SerializeJson<T>(ILambdaSerializer serializer, T response)
        {
            string serializedJson;
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(response, stream);

                stream.Position = 0;
                serializedJson = Encoding.UTF8.GetString(stream.ToArray());
            }
            return serializedJson;
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void HttpApiV2Format(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("http-api-v2-request.json"))
            {
                var request = serializer.Deserialize<APIGatewayHttpApiV2ProxyRequest>(fileStream);

                Assert.Equal("2.0", request.Version);
                Assert.Equal("$default", request.RouteKey);
                Assert.Equal("/my/path", request.RawPath);
                Assert.Equal("parameter1=value1&parameter1=value2&parameter2=value", request.RawQueryString);

                Assert.Equal(2, request.Cookies.Length);
                Assert.Equal("cookie1", request.Cookies[0]);
                Assert.Equal("cookie2", request.Cookies[1]);

                Assert.Equal(2, request.QueryStringParameters.Count);
                Assert.Equal("value1,value2", request.QueryStringParameters["parameter1"]);
                Assert.Equal("value", request.QueryStringParameters["parameter2"]);

                Assert.Equal("Hello from Lambda", request.Body);
                Assert.True(request.IsBase64Encoded);

                Assert.Equal(2, request.StageVariables.Count);
                Assert.Equal("value1", request.StageVariables["stageVariable1"]);
                Assert.Equal("value2", request.StageVariables["stageVariable2"]);

                Assert.Single(request.PathParameters);
                Assert.Equal("value1", request.PathParameters["parameter1"]);

                var rc = request.RequestContext;
                Assert.NotNull(rc);
                Assert.Equal("123456789012", rc.AccountId);
                Assert.Equal("api-id", rc.ApiId);
                Assert.Equal("id.execute-api.us-east-1.amazonaws.com", rc.DomainName);
                Assert.Equal("domain-id", rc.DomainPrefix);
                Assert.Equal("request-id", rc.RequestId);
                Assert.Equal("route-id", rc.RouteId);
                Assert.Equal("$default-route", rc.RouteKey);
                Assert.Equal("$default-stage", rc.Stage);
                Assert.Equal("12/Mar/2020:19:03:58 +0000", rc.Time);
                Assert.Equal(1583348638390, rc.TimeEpoch);

                var clientCert = request.RequestContext.Authentication.ClientCert;
                Assert.Equal("CERT_CONTENT", clientCert.ClientCertPem);
                Assert.Equal("www.example.com", clientCert.SubjectDN);
                Assert.Equal("Example issuer", clientCert.IssuerDN);
                Assert.Equal("a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1", clientCert.SerialNumber);

                Assert.Equal("May 28 12:30:02 2019 GMT", clientCert.Validity.NotBefore);
                Assert.Equal("Aug  5 09:36:04 2021 GMT", clientCert.Validity.NotAfter);

                var auth = rc.Authorizer;
                Assert.NotNull(auth);
                Assert.Equal(2, auth.Jwt.Claims.Count);
                Assert.Equal("value1", auth.Jwt.Claims["claim1"]);
                Assert.Equal("value2", auth.Jwt.Claims["claim2"]);
                Assert.Equal(2, auth.Jwt.Scopes.Length);
                Assert.Equal("scope1", auth.Jwt.Scopes[0]);
                Assert.Equal("scope2", auth.Jwt.Scopes[1]);

                var http = rc.Http;
                Assert.NotNull(http);
                Assert.Equal("POST", http.Method);
                Assert.Equal("/my/path", http.Path);
                Assert.Equal("HTTP/1.1", http.Protocol);
                Assert.Equal("IP", http.SourceIp);
                Assert.Equal("agent", http.UserAgent);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void HttpApiV2FormatLambdaAuthorizer(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("http-api-v2-request-lambda-authorizer.json"))
            {
                var request = serializer.Deserialize<APIGatewayHttpApiV2ProxyRequest>(fileStream);
                Assert.Equal("value", request.RequestContext.Authorizer.Lambda["key"]?.ToString());
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void HttpApiV2FormatIAMAuthorizer(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("http-api-v2-request-iam-authorizer.json"))
            {
                var request = serializer.Deserialize<APIGatewayHttpApiV2ProxyRequest>(fileStream);
                var iam = request.RequestContext.Authorizer.IAM;
                Assert.NotNull(iam);
                Assert.Equal("ARIA2ZJZYVUEREEIHAKY", iam.AccessKey);
                Assert.Equal("1234567890", iam.AccountId);
                Assert.Equal("AROA7ZJZYVRE7C3DUXHH6:CognitoIdentityCredentials", iam.CallerId);
                Assert.Equal("foo", iam.CognitoIdentity.AMR[0]);
                Assert.Equal("us-east-1:3f291106-8703-466b-8f2b-3ecee1ca56ce", iam.CognitoIdentity.IdentityId);
                Assert.Equal("us-east-1:4f291106-8703-466b-8f2b-3ecee1ca56ce", iam.CognitoIdentity.IdentityPoolId);
                Assert.Equal("AwsOrgId", iam.PrincipalOrgId);
                Assert.Equal("arn:aws:iam::1234567890:user/Admin", iam.UserARN);
                Assert.Equal("AROA2ZJZYVRE7Y3TUXHH6", iam.UserId);

            }
        }

        [Fact]
        public void SetHeadersToHttpApiV2Response()
        {
            var response = new APIGatewayHttpApiV2ProxyResponse();
            Assert.Null(response.Headers);

            response.SetHeaderValues("name1", "value1", false);
            Assert.Single(response.Headers);
            Assert.Equal("value1", response.Headers["name1"]);

            response.SetHeaderValues("name1", "value2", true);
            Assert.Equal("value1,value2", response.Headers["name1"]);

            response.SetHeaderValues("name1", "value3", false);
            Assert.Equal("value3", response.Headers["name1"]);
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void S3ObjectLambdaEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("s3-object-lambda-event.json"))
            {
                var s3Event = serializer.Deserialize<S3ObjectLambdaEvent>(fileStream);

                Assert.Equal("requestId", s3Event.XAmzRequestId);
                Assert.Equal("https://my-s3-ap-111122223333.s3-accesspoint.us-east-1.amazonaws.com/example?X-Amz-Security-Token=<snip>", s3Event.GetObjectContext.InputS3Url);
                Assert.Equal("io-use1-001", s3Event.GetObjectContext.OutputRoute);
                Assert.Equal("OutputToken", s3Event.GetObjectContext.OutputToken);

                Assert.Equal("arn:aws:s3-object-lambda:us-east-1:111122223333:accesspoint/example-object-lambda-ap", s3Event.Configuration.AccessPointArn);
                Assert.Equal("arn:aws:s3:us-east-1:111122223333:accesspoint/example-ap", s3Event.Configuration.SupportingAccessPointArn);
                Assert.Equal("{}", s3Event.Configuration.Payload);

                Assert.Equal("https://object-lambda-111122223333.s3-object-lambda.us-east-1.amazonaws.com/example", s3Event.UserRequest.Url);
                Assert.Equal("object-lambda-111122223333.s3-object-lambda.us-east-1.amazonaws.com", s3Event.UserRequest.Headers["Host"]);

                Assert.Equal("AssumedRole", s3Event.UserIdentity.Type);
                Assert.Equal("principalId", s3Event.UserIdentity.PrincipalId);
                Assert.Equal("arn:aws:sts::111122223333:assumed-role/Admin/example", s3Event.UserIdentity.Arn);
                Assert.Equal("111122223333", s3Event.UserIdentity.AccountId);
                Assert.Equal("accessKeyId", s3Event.UserIdentity.AccessKeyId);

                Assert.Equal("false", s3Event.UserIdentity.SessionContext.Attributes.MfaAuthenticated);
                Assert.Equal("Wed Mar 10 23:41:52 UTC 2021", s3Event.UserIdentity.SessionContext.Attributes.CreationDate);

                Assert.Equal("Role", s3Event.UserIdentity.SessionContext.SessionIssuer.Type);
                Assert.Equal("principalId", s3Event.UserIdentity.SessionContext.SessionIssuer.PrincipalId);
                Assert.Equal("arn:aws:iam::111122223333:role/Admin", s3Event.UserIdentity.SessionContext.SessionIssuer.Arn);
                Assert.Equal("111122223333", s3Event.UserIdentity.SessionContext.SessionIssuer.AccountId);
                Assert.Equal("Admin", s3Event.UserIdentity.SessionContext.SessionIssuer.UserName);

                Assert.Equal("1.00", s3Event.ProtocolVersion);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void S3PutTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("s3-event.json"))
            {
                var s3Event = serializer.Deserialize<S3Event>(fileStream);

                Assert.Equal(2, s3Event.Records.Count);
                var record = s3Event.Records[0];
                Assert.Equal("2.0", record.EventVersion);
                Assert.Equal(DateTime.Parse("1970-01-01T00:00:00.000Z").ToUniversalTime(), record.EventTime.ToUniversalTime());
                Assert.Equal("127.0.0.1", record.RequestParameters.SourceIPAddress);
                Assert.Equal("testConfigRule", record.S3.ConfigurationId);
                Assert.Equal("0123456789abcdef0123456789abcdef", record.S3.Object.ETag);
                Assert.Equal("HappyFace.jpg", record.S3.Object.Key);
                Assert.Equal(1024, record.S3.Object.Size);
                Assert.Equal("arn:aws:s3:::mybucket", record.S3.Bucket.Arn);
                Assert.Equal("sourcebucket", record.S3.Bucket.Name);
                Assert.Equal("EXAMPLE", record.S3.Bucket.OwnerIdentity.PrincipalId);
                Assert.Equal("1.0", record.S3.S3SchemaVersion);
                Assert.Equal("EXAMPLE123/5678abcdefghijklambdaisawesome/mnopqrstuvwxyzABCDEFGH", record.ResponseElements.XAmzId2);
                Assert.Equal("EXAMPLE123456789", record.ResponseElements.XAmzRequestId);
                Assert.Equal("us-east-1", record.AwsRegion);
                Assert.Equal("ObjectCreated:Put", record.EventName);
                Assert.Equal("EXAMPLE", record.UserIdentity.PrincipalId);
                Assert.Equal("aws:s3", record.EventSource);

                // In the events file the key is New+File.jpg simulating the key being url encoded.
                Assert.Equal("New File.jpg", s3Event.Records[1].S3.Object.KeyDecoded);

                Handle(s3Event);
            }
        }

        private void Handle(S3Event s3Event)
        {
            foreach (var record in s3Event.Records)
            {
                var s3 = record.S3;
                Console.WriteLine($"[{record.EventSource} - {record.EventTime}] Bucket = {s3.Bucket.Name}, Key = {s3.Object.Key}");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void KinesisTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-event.json"))
            {
                var kinesisEvent = serializer.Deserialize<KinesisEvent>(fileStream);
                Assert.Equal(2, kinesisEvent.Records.Count);
                var record = kinesisEvent.Records[0];
                Assert.Equal("shardId-000000000000:49568167373333333333333333333333333333333333333333333333", record.EventId);
                Assert.Equal("1.0", record.EventVersion);
                Assert.Equal("s1", record.Kinesis.PartitionKey);
                var dataBytes = record.Kinesis.Data.ToArray();
                Assert.Equal("SGVsbG8gV29ybGQ=", Convert.ToBase64String(dataBytes));
                Assert.Equal("Hello World", Encoding.UTF8.GetString(dataBytes));
                Assert.Equal("1.0", record.Kinesis.KinesisSchemaVersion);
                Assert.Equal("49568167373333333333333333333333333333333333333333333333", record.Kinesis.SequenceNumber);
                Assert.Equal("arn:aws:iam::123456789012:role/LambdaRole", record.InvokeIdentityArn);
                Assert.Equal("aws:kinesis:record", record.EventName);
                Assert.Equal("arn:aws:kinesis:us-east-1:123456789012:stream/simple-stream", record.EventSourceARN);
                Assert.Equal("aws:kinesis", record.EventSource);
                Assert.Equal("us-east-1", record.AwsRegion);
                // Starting with .NET 7 the precision of the underlying AddSeconds method was changed.
                // https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/7.0/datetime-add-precision
                Assert.Equal(636162383234769999, record.Kinesis.ApproximateArrivalTimestamp.Value.ToUniversalTime().Ticks);

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

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void KinesisBatchItemFailuresTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-batchitemfailures-response.json"))
            {
                var kinesisStreamsEventResponse = serializer.Deserialize<KinesisEvents.StreamsEventResponse>(fileStream);

                Assert.Single(kinesisStreamsEventResponse.BatchItemFailures);
                Assert.Equal("1405400000000002063282832", kinesisStreamsEventResponse.BatchItemFailures[0].ItemIdentifier);

                var ms = new MemoryStream();
                serializer.Serialize(kinesisStreamsEventResponse, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("kinesis-batchitemfailures-response.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void KinesisTimeWindowTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-timewindow-event.json"))
            {
                var kinesisTimeWindowEvent = serializer.Deserialize<KinesisTimeWindowEvent>(fileStream);

                Assert.Equal("shardId-000000000006", kinesisTimeWindowEvent.ShardId);
                Assert.Equal("arn:aws:kinesis:us-east-1:123456789012:stream/lambda-stream", kinesisTimeWindowEvent.EventSourceARN);
                Assert.False(kinesisTimeWindowEvent.IsFinalInvokeForWindow);
                Assert.False(kinesisTimeWindowEvent.IsWindowTerminatedEarly);
                Assert.Equal(2, kinesisTimeWindowEvent.State.Count);
                Assert.True(kinesisTimeWindowEvent.State.ContainsKey("1"));
                Assert.Equal("282", kinesisTimeWindowEvent.State["1"]);
                Assert.True(kinesisTimeWindowEvent.State.ContainsKey("2"));
                Assert.Equal("715", kinesisTimeWindowEvent.State["2"]);
                Assert.NotNull(kinesisTimeWindowEvent.Window);
                Assert.Equal(637430942400000000, kinesisTimeWindowEvent.Window.Start.Ticks);
                Assert.Equal(637430943600000000, kinesisTimeWindowEvent.Window.End.Ticks);

                Assert.Single(kinesisTimeWindowEvent.Records);
                var record = kinesisTimeWindowEvent.Records[0];
                Assert.Equal("shardId-000000000006:49590338271490256608559692538361571095921575989136588898", record.EventId);
                Assert.Equal("aws:kinesis:record", record.EventName);
                Assert.Equal("1.0", record.EventVersion);
                Assert.Equal("aws:kinesis", record.EventSource);
                Assert.Equal("arn:aws:iam::123456789012:role/lambda-kinesis-role", record.InvokeIdentityArn);
                Assert.Equal("us-east-1", record.AwsRegion);
                Assert.Equal("arn:aws:kinesis:us-east-1:123456789012:stream/lambda-stream", record.EventSourceARN);

                Assert.Equal("1.0", record.Kinesis.KinesisSchemaVersion);
                Assert.Equal("1", record.Kinesis.PartitionKey);
                Assert.Equal("49590338271490256608559692538361571095921575989136588898", record.Kinesis.SequenceNumber);
                var dataBytes = record.Kinesis.Data.ToArray();
                Assert.Equal("SGVsbG8sIHRoaXMgaXMgYSB0ZXN0Lg==", Convert.ToBase64String(dataBytes));
                Assert.Equal("Hello, this is a test.", Encoding.UTF8.GetString(dataBytes));
                Assert.Equal(637430942750000000, record.Kinesis.ApproximateArrivalTimestamp.Value.ToUniversalTime().Ticks);

                Handle(kinesisTimeWindowEvent);
            }
        }

        private void Handle(KinesisTimeWindowEvent kinesisTimeWindowEvent)
        {
            foreach (var record in kinesisTimeWindowEvent.Records)
            {
                var kinesisRecord = record.Kinesis;
                var dataBytes = kinesisRecord.Data.ToArray();
                var dataText = Encoding.UTF8.GetString(dataBytes);
                Assert.Equal("Hello, this is a test.", dataText);
                Console.WriteLine($"[{record.EventName}] Data = '{dataText}'.");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void KinesisTimeWindowResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-timewindow-response.json"))
            {
                var kinesisTimeWindowResponse = serializer.Deserialize<KinesisTimeWindowResponse>(fileStream);

                Assert.Equal(2, kinesisTimeWindowResponse.State.Count);
                Assert.True(kinesisTimeWindowResponse.State.ContainsKey("1"));
                Assert.Equal("282", kinesisTimeWindowResponse.State["1"]);
                Assert.True(kinesisTimeWindowResponse.State.ContainsKey("2"));
                Assert.Equal("715", kinesisTimeWindowResponse.State["2"]);
                Assert.Empty(kinesisTimeWindowResponse.BatchItemFailures);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void DynamoDbUpdateTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            Stream json = LoadJsonTestFile("dynamodb-event.json");
            var dynamodbEvent = serializer.Deserialize<DynamoDBEvent>(json);
            Assert.Equal(2, dynamodbEvent.Records.Count);

            var record = dynamodbEvent.Records[0];
            Assert.Equal("f07f8ca4b0b26cb9c4e5e77e69f274ee", record.EventID);
            Assert.Equal("1.1", record.EventVersion);
            Assert.Equal(2, record.Dynamodb.Keys.Count);
            Assert.Equal("binary", record.Dynamodb.Keys["key"].S);
            Assert.Equal("data", record.Dynamodb.Keys["val"].S);
            Assert.Null(record.UserIdentity);
            Assert.Null(record.Dynamodb.OldImage);
            Assert.Equal("data", record.Dynamodb.NewImage["val"].S);
            Assert.Equal("binary", record.Dynamodb.NewImage["key"].S);
            Assert.Null(record.Dynamodb.NewImage["key"].BOOL);
            Assert.Null(record.Dynamodb.NewImage["key"].L);
            Assert.Null(record.Dynamodb.NewImage["key"].M);
            Assert.Null(record.Dynamodb.NewImage["key"].N);
            Assert.Null(record.Dynamodb.NewImage["key"].NS);
            Assert.Null(record.Dynamodb.NewImage["key"].NULL);
            Assert.Null(record.Dynamodb.NewImage["key"].SS);
            Assert.Equal("AAEqQQ==", MemoryStreamToBase64String(record.Dynamodb.NewImage["asdf1"].B));
            Assert.Equal(2, record.Dynamodb.NewImage["asdf2"].BS.Count);
            Assert.Equal("AAEqQQ==", MemoryStreamToBase64String(record.Dynamodb.NewImage["asdf2"].BS[0]));
            Assert.Equal("QSoBAA==", MemoryStreamToBase64String(record.Dynamodb.NewImage["asdf2"].BS[1]));
            Assert.Equal("NEW_AND_OLD_IMAGES", record.Dynamodb.StreamViewType);
            Assert.Equal("1405400000000002063282832", record.Dynamodb.SequenceNumber);
            Assert.Equal(54, record.Dynamodb.SizeBytes);
            Assert.Equal("us-east-1", record.AwsRegion);
            Assert.Equal("INSERT", record.EventName);
            Assert.Equal("arn:aws:dynamodb:us-east-1:123456789012:table/Example-Table/stream/2016-12-01T00:00:00.000", record.EventSourceArn);
            Assert.Equal("aws:dynamodb", record.EventSource);
            var recordDateTime = record.Dynamodb.ApproximateCreationDateTime;
            Assert.Equal(636162388200000000, recordDateTime.Ticks);

            var topLevelList = record.Dynamodb.NewImage["misc1"].L;
            Assert.Empty(topLevelList);

            var nestedMap = record.Dynamodb.NewImage["misc2"].M;
            Assert.NotNull(nestedMap);
            Assert.Empty(nestedMap["ItemsEmpty"].L);
            Assert.Equal(3, nestedMap["ItemsNonEmpty"].L.Count);
            Assert.False(nestedMap["ItemBoolean"].BOOL);
            Assert.True(nestedMap["ItemNull"].NULL);
            Assert.Equal(3, nestedMap["ItemNumberSet"].NS.Count);
            Assert.Equal(2, nestedMap["ItemStringSet"].SS.Count);

            var secondRecord = dynamodbEvent.Records[1];
            Assert.NotNull(secondRecord.UserIdentity);
            Assert.Equal("dynamodb.amazonaws.com", secondRecord.UserIdentity.PrincipalId);
            Assert.Equal("Service", secondRecord.UserIdentity.Type);
            Assert.Null(secondRecord.Dynamodb.NewImage);
            Assert.NotNull(secondRecord.Dynamodb.OldImage["asdf1"].B);
            Assert.Null(secondRecord.Dynamodb.OldImage["asdf1"].S);
            Assert.Null(secondRecord.Dynamodb.OldImage["asdf1"].L);
            Assert.Null(secondRecord.Dynamodb.OldImage["asdf1"].M);
            Assert.Null(secondRecord.Dynamodb.OldImage["asdf1"].N);
            Assert.Null(secondRecord.Dynamodb.OldImage["asdf1"].NS);
            Assert.Null(secondRecord.Dynamodb.OldImage["asdf1"].NULL);
            Assert.Null(secondRecord.Dynamodb.OldImage["asdf1"].SS);

            Handle(dynamodbEvent);
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void DynamoDbWithMillisecondsTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            Stream json = LoadJsonTestFile("dynamodb-with-ms-event.json");
            var dynamodbEvent = serializer.Deserialize<DynamoDBEvent>(json);
            Assert.Equal(2, dynamodbEvent.Records.Count);

            var record = dynamodbEvent.Records[0];
            Assert.Equal("f07f8ca4b0b26cb9c4e5e77e69f274ee", record.EventID);
            Assert.Equal("1.1", record.EventVersion);
            Assert.Equal(2, record.Dynamodb.Keys.Count);
            Assert.Equal("binary", record.Dynamodb.Keys["key"].S);
            Assert.Equal("data", record.Dynamodb.Keys["val"].S);
            Assert.Null(record.UserIdentity);
            Assert.Null(record.Dynamodb.OldImage);
            Assert.Equal("data", record.Dynamodb.NewImage["val"].S);
            Assert.Equal("binary", record.Dynamodb.NewImage["key"].S);
            Assert.Null(record.Dynamodb.NewImage["key"].BOOL);
            Assert.Null(record.Dynamodb.NewImage["key"].L);
            Assert.Null(record.Dynamodb.NewImage["key"].M);
            Assert.Null(record.Dynamodb.NewImage["key"].N);
            Assert.Null(record.Dynamodb.NewImage["key"].NS);
            Assert.Null(record.Dynamodb.NewImage["key"].NULL);
            Assert.Null(record.Dynamodb.NewImage["key"].SS);
            Assert.Equal("AAEqQQ==", MemoryStreamToBase64String(record.Dynamodb.NewImage["asdf1"].B));
            Assert.Equal(2, record.Dynamodb.NewImage["asdf2"].BS.Count);
            Assert.Equal("AAEqQQ==", MemoryStreamToBase64String(record.Dynamodb.NewImage["asdf2"].BS[0]));
            Assert.Equal("QSoBAA==", MemoryStreamToBase64String(record.Dynamodb.NewImage["asdf2"].BS[1]));
            Assert.Equal("NEW_AND_OLD_IMAGES", record.Dynamodb.StreamViewType);
            Assert.Equal("1405400000000002063282832", record.Dynamodb.SequenceNumber);
            Assert.Equal(54, record.Dynamodb.SizeBytes);
            Assert.Equal("us-east-1", record.AwsRegion);
            Assert.Equal("INSERT", record.EventName);
            Assert.Equal("arn:aws:dynamodb:us-east-1:123456789012:table/Example-Table/stream/2016-12-01T00:00:00.000", record.EventSourceArn);
            Assert.Equal("aws:dynamodb", record.EventSource);
            var recordDateTime = record.Dynamodb.ApproximateCreationDateTime;
            Assert.Equal(636162388200000000, recordDateTime.Ticks);

            var topLevelList = record.Dynamodb.NewImage["misc1"].L;
            Assert.Empty(topLevelList);

            var nestedMap = record.Dynamodb.NewImage["misc2"].M;
            Assert.NotNull(nestedMap);
            Assert.Empty(nestedMap["ItemsEmpty"].L);
            Assert.Equal(3, nestedMap["ItemsNonEmpty"].L.Count);
            Assert.False(nestedMap["ItemBoolean"].BOOL);
            Assert.True(nestedMap["ItemNull"].NULL);
            Assert.Equal(3, nestedMap["ItemNumberSet"].NS.Count);
            Assert.Equal(2, nestedMap["ItemStringSet"].SS.Count);

            var secondRecord = dynamodbEvent.Records[1];
            Assert.NotNull(secondRecord.UserIdentity);
            Assert.Equal("dynamodb.amazonaws.com", secondRecord.UserIdentity.PrincipalId);
            Assert.Equal("Service", secondRecord.UserIdentity.Type);
            Assert.Null(secondRecord.Dynamodb.NewImage);
            Assert.NotNull(secondRecord.Dynamodb.OldImage["asdf1"].B);
            Assert.Null(secondRecord.Dynamodb.OldImage["asdf1"].S);
            Assert.Null(secondRecord.Dynamodb.OldImage["asdf1"].L);
            Assert.Null(secondRecord.Dynamodb.OldImage["asdf1"].M);
            Assert.Null(secondRecord.Dynamodb.OldImage["asdf1"].N);
            Assert.Null(secondRecord.Dynamodb.OldImage["asdf1"].NS);
            Assert.Null(secondRecord.Dynamodb.OldImage["asdf1"].NULL);
            Assert.Null(secondRecord.Dynamodb.OldImage["asdf1"].SS);

            Handle(dynamodbEvent);
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void DynamoDbBatchItemFailuresTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("dynamodb-batchitemfailures-response.json"))
            {
                var dynamoDbStreamsEventResponse = serializer.Deserialize<DynamoDBEvents.StreamsEventResponse>(fileStream);

                Assert.Single(dynamoDbStreamsEventResponse.BatchItemFailures);
                Assert.Equal("1405400000000002063282832", dynamoDbStreamsEventResponse.BatchItemFailures[0].ItemIdentifier);

                var ms = new MemoryStream();
                serializer.Serialize(dynamoDbStreamsEventResponse, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("dynamodb-batchitemfailures-response.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
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

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void DynamoDBTimeWindowTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("dynamodb-timewindow-event.json"))
            {
                var dynamoDBTimeWindowEvent = serializer.Deserialize<DynamoDBTimeWindowEvent>(fileStream);

                Assert.Equal("shard123456789", dynamoDBTimeWindowEvent.ShardId);
                Assert.Equal("stream-ARN", dynamoDBTimeWindowEvent.EventSourceArn);
                Assert.False(dynamoDBTimeWindowEvent.IsFinalInvokeForWindow);
                Assert.False(dynamoDBTimeWindowEvent.IsWindowTerminatedEarly);
                Assert.Single(dynamoDBTimeWindowEvent.State);
                Assert.True(dynamoDBTimeWindowEvent.State.ContainsKey("1"));
                Assert.Equal("state1", dynamoDBTimeWindowEvent.State["1"]);
                Assert.NotNull(dynamoDBTimeWindowEvent.Window);
                Assert.Equal(637317252000000000, dynamoDBTimeWindowEvent.Window.Start.Ticks);
                Assert.Equal(637317255000000000, dynamoDBTimeWindowEvent.Window.End.Ticks);

                Assert.Equal(3, dynamoDBTimeWindowEvent.Records.Count);

                var record1 = dynamoDBTimeWindowEvent.Records[0];
                Assert.Equal("1", record1.EventID);
                Assert.Equal("INSERT", record1.EventName);
                Assert.Equal("1.0", record1.EventVersion);
                Assert.Equal("aws:dynamodb", record1.EventSource);
                Assert.Equal("us-east-1", record1.AwsRegion);
                Assert.Equal("stream-ARN", record1.EventSourceArn);
                Assert.Single(record1.Dynamodb.Keys);
                Assert.Equal("101", record1.Dynamodb.Keys["Id"].N);
                Assert.Equal("111", record1.Dynamodb.SequenceNumber);
                Assert.Equal(26, record1.Dynamodb.SizeBytes);
                Assert.Equal("NEW_IMAGE", record1.Dynamodb.StreamViewType);
                Assert.Equal(2, record1.Dynamodb.NewImage.Count);
                Assert.Equal("New item!", record1.Dynamodb.NewImage["Message"].S);
                Assert.Equal("101", record1.Dynamodb.NewImage["Id"].N);
                Assert.Null(record1.Dynamodb.OldImage);

                var record2 = dynamoDBTimeWindowEvent.Records[1];
                Assert.Equal("2", record2.EventID);
                Assert.Equal("MODIFY", record2.EventName);
                Assert.Equal("1.0", record2.EventVersion);
                Assert.Equal("aws:dynamodb", record2.EventSource);
                Assert.Equal("us-east-1", record2.AwsRegion);
                Assert.Equal("stream-ARN", record2.EventSourceArn);
                Assert.Single(record2.Dynamodb.Keys);
                Assert.Equal("101", record2.Dynamodb.Keys["Id"].N);
                Assert.Equal("222", record2.Dynamodb.SequenceNumber);
                Assert.Equal(59, record2.Dynamodb.SizeBytes);
                Assert.Equal("NEW_AND_OLD_IMAGES", record2.Dynamodb.StreamViewType);
                Assert.Equal(2, record2.Dynamodb.NewImage.Count);
                Assert.Equal("This item has changed", record2.Dynamodb.NewImage["Message"].S);
                Assert.Equal("101", record2.Dynamodb.NewImage["Id"].N);
                Assert.Equal(2, record2.Dynamodb.OldImage.Count);
                Assert.Equal("New item!", record2.Dynamodb.OldImage["Message"].S);
                Assert.Equal("101", record2.Dynamodb.OldImage["Id"].N);

                var record3 = dynamoDBTimeWindowEvent.Records[2];
                Assert.Equal("3", record3.EventID);
                Assert.Equal("REMOVE", record3.EventName);
                Assert.Equal("1.0", record3.EventVersion);
                Assert.Equal("aws:dynamodb", record3.EventSource);
                Assert.Equal("us-east-1", record3.AwsRegion);
                Assert.Equal("stream-ARN", record3.EventSourceArn);
                Assert.Single(record3.Dynamodb.Keys);
                Assert.Equal("101", record3.Dynamodb.Keys["Id"].N);
                Assert.Equal("333", record3.Dynamodb.SequenceNumber);
                Assert.Equal(38, record3.Dynamodb.SizeBytes);
                Assert.Equal("NEW_AND_OLD_IMAGES", record3.Dynamodb.StreamViewType);
                Assert.Null(record3.Dynamodb.NewImage);
                Assert.Equal(2, record3.Dynamodb.OldImage.Count);
                Assert.Equal("This item has changed", record3.Dynamodb.OldImage["Message"].S);
                Assert.Equal("101", record3.Dynamodb.OldImage["Id"].N);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void DynamoDBTimeWindowResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("dynamodb-timewindow-response.json"))
            {
                var dynamoDBTimeWindowResponse = serializer.Deserialize<DynamoDBTimeWindowResponse>(fileStream);

                Assert.Equal(2, dynamoDBTimeWindowResponse.State.Count);
                Assert.True(dynamoDBTimeWindowResponse.State.ContainsKey("1"));
                Assert.Equal("282", dynamoDBTimeWindowResponse.State["1"]);
                Assert.True(dynamoDBTimeWindowResponse.State.ContainsKey("2"));
                Assert.Equal("715", dynamoDBTimeWindowResponse.State["2"]);
                Assert.Empty(dynamoDBTimeWindowResponse.BatchItemFailures);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-event.json"))
            {
                var cognitoEvent = serializer.Deserialize<CognitoEvent>(fileStream);
                Assert.Equal(2, cognitoEvent.Version);
                Assert.Equal("SyncTrigger", cognitoEvent.EventType);
                Assert.Equal("us-east-1", cognitoEvent.Region);
                Assert.Equal("datasetName", cognitoEvent.DatasetName);
                Assert.Equal("identityPoolId", cognitoEvent.IdentityPoolId);
                Assert.Equal("identityId", cognitoEvent.IdentityId);
                Assert.Single(cognitoEvent.DatasetRecords);
                Assert.True(cognitoEvent.DatasetRecords.ContainsKey("SampleKey1"));
                Assert.Equal("newValue1", cognitoEvent.DatasetRecords["SampleKey1"].NewValue);
                Assert.Equal("oldValue1", cognitoEvent.DatasetRecords["SampleKey1"].OldValue);
                Assert.Equal("replace", cognitoEvent.DatasetRecords["SampleKey1"].Op);

                Handle(cognitoEvent);
            }
        }

        private static void Handle(CognitoEvent cognitoEvent)
        {
            foreach (var datasetKVP in cognitoEvent.DatasetRecords)
            {
                var datasetName = datasetKVP.Key;
                var datasetRecord = datasetKVP.Value;

                Console.WriteLine($"[{cognitoEvent.EventType}-{datasetName}] {datasetRecord.OldValue} -> {datasetRecord.Op} -> {datasetRecord.NewValue}");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoPreSignUpEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-presignup-event.json"))
            {
                var cognitoPreSignupEvent = serializer.Deserialize<CognitoPreSignupEvent>(fileStream);

                AssertBaseClass(cognitoPreSignupEvent);

                Assert.Equal(2, cognitoPreSignupEvent.Request.ValidationData.Count);
                Assert.Equal("validation_1", cognitoPreSignupEvent.Request.ValidationData.ToArray()[0].Key);
                Assert.Equal("validation_value_1", cognitoPreSignupEvent.Request.ValidationData.ToArray()[0].Value);
                Assert.Equal("validation_2", cognitoPreSignupEvent.Request.ValidationData.ToArray()[1].Key);
                Assert.Equal("validation_value_2", cognitoPreSignupEvent.Request.ValidationData.ToArray()[1].Value);

                Assert.Equal(2, cognitoPreSignupEvent.Request.ClientMetadata.Count);
                Assert.Equal("metadata_1", cognitoPreSignupEvent.Request.ClientMetadata.ToArray()[0].Key);
                Assert.Equal("metadata_value_1", cognitoPreSignupEvent.Request.ClientMetadata.ToArray()[0].Value);
                Assert.Equal("metadata_2", cognitoPreSignupEvent.Request.ClientMetadata.ToArray()[1].Key);
                Assert.Equal("metadata_value_2", cognitoPreSignupEvent.Request.ClientMetadata.ToArray()[1].Value);

                Assert.True(cognitoPreSignupEvent.Response.AutoConfirmUser);
                Assert.True(cognitoPreSignupEvent.Response.AutoVerifyPhone);
                Assert.True(cognitoPreSignupEvent.Response.AutoVerifyEmail);

                var ms = new MemoryStream();
                serializer.Serialize<CognitoPreSignupEvent>(cognitoPreSignupEvent, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("cognito-presignup-event.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoPostConfirmationEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-presignup-event.json"))
            {
                var cognitoPostConfirmationEvent = serializer.Deserialize<CognitoPostConfirmationEvent>(fileStream);

                AssertBaseClass(cognitoPostConfirmationEvent);

                Assert.Equal(2, cognitoPostConfirmationEvent.Request.ClientMetadata.Count);
                Assert.Equal("metadata_1", cognitoPostConfirmationEvent.Request.ClientMetadata.ToArray()[0].Key);
                Assert.Equal("metadata_value_1", cognitoPostConfirmationEvent.Request.ClientMetadata.ToArray()[0].Value);
                Assert.Equal("metadata_2", cognitoPostConfirmationEvent.Request.ClientMetadata.ToArray()[1].Key);
                Assert.Equal("metadata_value_2", cognitoPostConfirmationEvent.Request.ClientMetadata.ToArray()[1].Value);

                var ms = new MemoryStream();
                serializer.Serialize<CognitoPostConfirmationEvent>(cognitoPostConfirmationEvent, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("cognito-postconfirmation-event.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoPreAuthenticationEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-preauthentication-event.json"))
            {
                var cognitoPreAuthenticationEvent = serializer.Deserialize<CognitoPreAuthenticationEvent>(fileStream);

                AssertBaseClass(cognitoPreAuthenticationEvent);

                Assert.Equal(2, cognitoPreAuthenticationEvent.Request.ValidationData.Count);
                Assert.Equal("validation_1", cognitoPreAuthenticationEvent.Request.ValidationData.ToArray()[0].Key);
                Assert.Equal("validation_value_1", cognitoPreAuthenticationEvent.Request.ValidationData.ToArray()[0].Value);
                Assert.Equal("validation_2", cognitoPreAuthenticationEvent.Request.ValidationData.ToArray()[1].Key);
                Assert.Equal("validation_value_2", cognitoPreAuthenticationEvent.Request.ValidationData.ToArray()[1].Value);

                Assert.True(cognitoPreAuthenticationEvent.Request.UserNotFound);

                var ms = new MemoryStream();
                serializer.Serialize<CognitoPreAuthenticationEvent>(cognitoPreAuthenticationEvent, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("cognito-preauthentication-event.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoPostAuthenticationEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-postauthentication-event.json"))
            {
                var cognitoPostAuthenticationEvent = serializer.Deserialize<CognitoPostAuthenticationEvent>(fileStream);

                AssertBaseClass(cognitoPostAuthenticationEvent);

                Assert.Equal(2, cognitoPostAuthenticationEvent.Request.ClientMetadata.Count);
                Assert.Equal("client_1", cognitoPostAuthenticationEvent.Request.ClientMetadata.ToArray()[0].Key);
                Assert.Equal("client_value_1", cognitoPostAuthenticationEvent.Request.ClientMetadata.ToArray()[0].Value);
                Assert.Equal("client_2", cognitoPostAuthenticationEvent.Request.ClientMetadata.ToArray()[1].Key);
                Assert.Equal("client_value_2", cognitoPostAuthenticationEvent.Request.ClientMetadata.ToArray()[1].Value);

                Assert.Equal(2, cognitoPostAuthenticationEvent.Request.ValidationData.Count);
                Assert.Equal("validation_1", cognitoPostAuthenticationEvent.Request.ValidationData.ToArray()[0].Key);
                Assert.Equal("validation_value_1", cognitoPostAuthenticationEvent.Request.ValidationData.ToArray()[0].Value);
                Assert.Equal("validation_2", cognitoPostAuthenticationEvent.Request.ValidationData.ToArray()[1].Key);
                Assert.Equal("validation_value_2", cognitoPostAuthenticationEvent.Request.ValidationData.ToArray()[1].Value);

                Assert.True(cognitoPostAuthenticationEvent.Request.NewDevicedUsed);

                MemoryStream ms = new MemoryStream();
                serializer.Serialize<CognitoPostAuthenticationEvent>(cognitoPostAuthenticationEvent, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("cognito-postauthentication-event.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoDefineAuthChallengeEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-defineauthchallenge-event.json"))
            {
                var cognitoDefineAuthChallengeEvent = serializer.Deserialize<CognitoDefineAuthChallengeEvent>(fileStream);

                AssertBaseClass(cognitoDefineAuthChallengeEvent);

                Assert.Equal(2, cognitoDefineAuthChallengeEvent.Request.ClientMetadata.Count);
                Assert.Equal("metadata_1", cognitoDefineAuthChallengeEvent.Request.ClientMetadata.ToArray()[0].Key);
                Assert.Equal("metadata_value_1", cognitoDefineAuthChallengeEvent.Request.ClientMetadata.ToArray()[0].Value);
                Assert.Equal("metadata_2", cognitoDefineAuthChallengeEvent.Request.ClientMetadata.ToArray()[1].Key);
                Assert.Equal("metadata_value_2", cognitoDefineAuthChallengeEvent.Request.ClientMetadata.ToArray()[1].Value);

                Assert.Equal(2, cognitoDefineAuthChallengeEvent.Request.Session.Count);

                var session0 = cognitoDefineAuthChallengeEvent.Request.Session[0];
                Assert.Equal("challenge1", session0.ChallengeName);
                Assert.True(session0.ChallengeResult);

                Assert.Equal("challenge_metadata1", session0.ChallengeMetadata);
                
                var session1 = cognitoDefineAuthChallengeEvent.Request.Session[1];
                Assert.Equal("challenge2", session1.ChallengeName);
                Assert.False(session1.ChallengeResult);
                Assert.Equal("challenge_metadata2", session1.ChallengeMetadata);

                Assert.True(cognitoDefineAuthChallengeEvent.Request.UserNotFound);

                Assert.Equal("challenge", cognitoDefineAuthChallengeEvent.Response.ChallengeName);
                Assert.True(cognitoDefineAuthChallengeEvent.Response.IssueTokens);
                Assert.True(cognitoDefineAuthChallengeEvent.Response.FailAuthentication);

                var ms = new MemoryStream();
                serializer.Serialize<CognitoDefineAuthChallengeEvent>(cognitoDefineAuthChallengeEvent, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("cognito-defineauthchallenge-event.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoDefineAuthChallengeEventWithNullValuesTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-defineauthchallenge-event-with-null-values.json"))
            {
                var cognitoDefineAuthChallengeEvent = serializer.Deserialize<CognitoDefineAuthChallengeEvent>(fileStream);

                AssertBaseClass(cognitoDefineAuthChallengeEvent);

                Assert.Equal(2, cognitoDefineAuthChallengeEvent.Request.ClientMetadata.Count);
                Assert.Equal("metadata_1", cognitoDefineAuthChallengeEvent.Request.ClientMetadata.ToArray()[0].Key);
                Assert.Equal("metadata_value_1", cognitoDefineAuthChallengeEvent.Request.ClientMetadata.ToArray()[0].Value);
                Assert.Equal("metadata_2", cognitoDefineAuthChallengeEvent.Request.ClientMetadata.ToArray()[1].Key);
                Assert.Equal("metadata_value_2", cognitoDefineAuthChallengeEvent.Request.ClientMetadata.ToArray()[1].Value);

                Assert.Equal(2, cognitoDefineAuthChallengeEvent.Request.Session.Count);

                var session0 = cognitoDefineAuthChallengeEvent.Request.Session[0];
                Assert.Equal("challenge1", session0.ChallengeName);
                Assert.True(session0.ChallengeResult);
                Assert.Null(session0.ChallengeMetadata);

                var session1 = cognitoDefineAuthChallengeEvent.Request.Session[1];
                Assert.Equal("challenge2", session1.ChallengeName);
                Assert.False(session1.ChallengeResult);
                Assert.Null(session1.ChallengeMetadata);

                Assert.True(cognitoDefineAuthChallengeEvent.Request.UserNotFound);

                Assert.Null(cognitoDefineAuthChallengeEvent.Response.ChallengeName);
                Assert.Null(cognitoDefineAuthChallengeEvent.Response.IssueTokens);
                Assert.Null(cognitoDefineAuthChallengeEvent.Response.FailAuthentication);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoCreateAuthChallengeEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-createauthchallenge-event.json"))
            {
                var cognitoCreateAuthChallengeEvent = serializer.Deserialize<CognitoCreateAuthChallengeEvent>(fileStream);

                AssertBaseClass(cognitoCreateAuthChallengeEvent);

                Assert.Equal("challenge", cognitoCreateAuthChallengeEvent.Request.ChallengeName);

                Assert.Equal(2, cognitoCreateAuthChallengeEvent.Request.ClientMetadata.Count);
                Assert.Equal("metadata_1", cognitoCreateAuthChallengeEvent.Request.ClientMetadata.ToArray()[0].Key);
                Assert.Equal("metadata_value_1", cognitoCreateAuthChallengeEvent.Request.ClientMetadata.ToArray()[0].Value);
                Assert.Equal("metadata_2", cognitoCreateAuthChallengeEvent.Request.ClientMetadata.ToArray()[1].Key);
                Assert.Equal("metadata_value_2", cognitoCreateAuthChallengeEvent.Request.ClientMetadata.ToArray()[1].Value);

                Assert.Equal(2, cognitoCreateAuthChallengeEvent.Request.Session.Count);

                var session0 = cognitoCreateAuthChallengeEvent.Request.Session[0];
                Assert.Equal("challenge1", session0.ChallengeName);
                Assert.True(session0.ChallengeResult);

                Assert.Equal("challenge_metadata1", session0.ChallengeMetadata);

                var session1 = cognitoCreateAuthChallengeEvent.Request.Session[1];
                Assert.Equal("challenge2", session1.ChallengeName);
                Assert.False(session1.ChallengeResult);
                Assert.Equal("challenge_metadata2", session1.ChallengeMetadata);

                Assert.True(cognitoCreateAuthChallengeEvent.Request.UserNotFound);

                Assert.Equal(2, cognitoCreateAuthChallengeEvent.Response.PublicChallengeParameters.Count);
                Assert.Equal("public_1", cognitoCreateAuthChallengeEvent.Response.PublicChallengeParameters.ToArray()[0].Key);
                Assert.Equal("public_value_1", cognitoCreateAuthChallengeEvent.Response.PublicChallengeParameters.ToArray()[0].Value);
                Assert.Equal("public_2", cognitoCreateAuthChallengeEvent.Response.PublicChallengeParameters.ToArray()[1].Key);
                Assert.Equal("public_value_2", cognitoCreateAuthChallengeEvent.Response.PublicChallengeParameters.ToArray()[1].Value);

                Assert.Equal(2, cognitoCreateAuthChallengeEvent.Response.PrivateChallengeParameters.Count);
                Assert.Equal("private_1", cognitoCreateAuthChallengeEvent.Response.PrivateChallengeParameters.ToArray()[0].Key);
                Assert.Equal("private_value_1", cognitoCreateAuthChallengeEvent.Response.PrivateChallengeParameters.ToArray()[0].Value);
                Assert.Equal("private_2", cognitoCreateAuthChallengeEvent.Response.PrivateChallengeParameters.ToArray()[1].Key);
                Assert.Equal("private_value_2", cognitoCreateAuthChallengeEvent.Response.PrivateChallengeParameters.ToArray()[1].Value);

                Assert.Equal("challenge", cognitoCreateAuthChallengeEvent.Response.ChallengeMetadata);

                var ms = new MemoryStream();
                serializer.Serialize<CognitoCreateAuthChallengeEvent>(cognitoCreateAuthChallengeEvent, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("cognito-createauthchallenge-event.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoVerifyAuthChallengeEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-verifyauthchallenge-event.json"))
            {
                var cognitoVerifyAuthChallengeEvent = serializer.Deserialize<CognitoVerifyAuthChallengeEvent>(fileStream);

                AssertBaseClass(cognitoVerifyAuthChallengeEvent);

                Assert.Equal("answer_value", cognitoVerifyAuthChallengeEvent.Request.ChallengeAnswer);

                Assert.Equal(2, cognitoVerifyAuthChallengeEvent.Request.ClientMetadata.Count);
                Assert.Equal("metadata_1", cognitoVerifyAuthChallengeEvent.Request.ClientMetadata.ToArray()[0].Key);
                Assert.Equal("metadata_value_1", cognitoVerifyAuthChallengeEvent.Request.ClientMetadata.ToArray()[0].Value);
                Assert.Equal("metadata_2", cognitoVerifyAuthChallengeEvent.Request.ClientMetadata.ToArray()[1].Key);
                Assert.Equal("metadata_value_2", cognitoVerifyAuthChallengeEvent.Request.ClientMetadata.ToArray()[1].Value);


                Assert.Equal(2, cognitoVerifyAuthChallengeEvent.Request.PrivateChallengeParameters.Count);
                Assert.Equal("private_1", cognitoVerifyAuthChallengeEvent.Request.PrivateChallengeParameters.ToArray()[0].Key);
                Assert.Equal("private_value_1", cognitoVerifyAuthChallengeEvent.Request.PrivateChallengeParameters.ToArray()[0].Value);
                Assert.Equal("private_2", cognitoVerifyAuthChallengeEvent.Request.PrivateChallengeParameters.ToArray()[1].Key);
                Assert.Equal("private_value_2", cognitoVerifyAuthChallengeEvent.Request.PrivateChallengeParameters.ToArray()[1].Value);

                Assert.True(cognitoVerifyAuthChallengeEvent.Request.UserNotFound);
            
                Assert.True(cognitoVerifyAuthChallengeEvent.Response.AnswerCorrect);

                var ms = new MemoryStream();
                serializer.Serialize<CognitoVerifyAuthChallengeEvent>(cognitoVerifyAuthChallengeEvent, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("cognito-verifyauthchallenge-event.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }


        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoVerifyAuthChallengeEventWithNullValuesTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-verifyauthchallenge-event-with-null-values.json"))
            {
                var cognitoVerifyAuthChallengeEvent = serializer.Deserialize<CognitoVerifyAuthChallengeEvent>(fileStream);

                AssertBaseClass(cognitoVerifyAuthChallengeEvent);

                Assert.Equal("answer_value", cognitoVerifyAuthChallengeEvent.Request.ChallengeAnswer);

                Assert.Null(cognitoVerifyAuthChallengeEvent.Request.ClientMetadata);

                Assert.Equal(2, cognitoVerifyAuthChallengeEvent.Request.PrivateChallengeParameters.Count);
                Assert.Equal("private_1", cognitoVerifyAuthChallengeEvent.Request.PrivateChallengeParameters.ToArray()[0].Key);
                Assert.Equal("private_value_1", cognitoVerifyAuthChallengeEvent.Request.PrivateChallengeParameters.ToArray()[0].Value);
                Assert.Equal("private_2", cognitoVerifyAuthChallengeEvent.Request.PrivateChallengeParameters.ToArray()[1].Key);
                Assert.Equal("private_value_2", cognitoVerifyAuthChallengeEvent.Request.PrivateChallengeParameters.ToArray()[1].Value);

                Assert.True(cognitoVerifyAuthChallengeEvent.Request.UserNotFound);

                Assert.Null(cognitoVerifyAuthChallengeEvent.Response.AnswerCorrect);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoPreTokenGenerationEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-pretokengeneration-event.json"))
            {
                var cognitoPreTokenGenerationEvent = serializer.Deserialize<CognitoPreTokenGenerationEvent>(fileStream);

                AssertBaseClass(cognitoPreTokenGenerationEvent);

                Assert.Equal(2, cognitoPreTokenGenerationEvent.Request.GroupConfiguration.GroupsToOverride.Count);
                Assert.Equal("group1", cognitoPreTokenGenerationEvent.Request.GroupConfiguration.GroupsToOverride[0]);
                Assert.Equal("group2", cognitoPreTokenGenerationEvent.Request.GroupConfiguration.GroupsToOverride[1]);

                Assert.Equal(2, cognitoPreTokenGenerationEvent.Request.GroupConfiguration.IamRolesToOverride.Count);
                Assert.Equal("role1", cognitoPreTokenGenerationEvent.Request.GroupConfiguration.IamRolesToOverride[0]);
                Assert.Equal("role2", cognitoPreTokenGenerationEvent.Request.GroupConfiguration.IamRolesToOverride[1]);

                Assert.Equal("role", cognitoPreTokenGenerationEvent.Request.GroupConfiguration.PreferredRole);

                Assert.Equal(2, cognitoPreTokenGenerationEvent.Request.ClientMetadata.Count);
                Assert.Equal("metadata_1", cognitoPreTokenGenerationEvent.Request.ClientMetadata.ToArray()[0].Key);
                Assert.Equal("metadata_value_1", cognitoPreTokenGenerationEvent.Request.ClientMetadata.ToArray()[0].Value);
                Assert.Equal("metadata_2", cognitoPreTokenGenerationEvent.Request.ClientMetadata.ToArray()[1].Key);
                Assert.Equal("metadata_value_2", cognitoPreTokenGenerationEvent.Request.ClientMetadata.ToArray()[1].Value);

                Assert.Equal(2, cognitoPreTokenGenerationEvent.Response.ClaimsOverrideDetails.ClaimsToAddOrOverride.Count);
                Assert.Equal("claim_1", cognitoPreTokenGenerationEvent.Response.ClaimsOverrideDetails.ClaimsToAddOrOverride.ToArray()[0].Key);
                Assert.Equal("claim_1_value_1", cognitoPreTokenGenerationEvent.Response.ClaimsOverrideDetails.ClaimsToAddOrOverride.ToArray()[0].Value);
                Assert.Equal("claim_2", cognitoPreTokenGenerationEvent.Response.ClaimsOverrideDetails.ClaimsToAddOrOverride.ToArray()[1].Key);
                Assert.Equal("claim_1_value_2", cognitoPreTokenGenerationEvent.Response.ClaimsOverrideDetails.ClaimsToAddOrOverride.ToArray()[1].Value);

                Assert.Equal(2, cognitoPreTokenGenerationEvent.Response.ClaimsOverrideDetails.ClaimsToSuppress.Count);
                Assert.Equal("suppress1", cognitoPreTokenGenerationEvent.Response.ClaimsOverrideDetails.ClaimsToSuppress[0]);
                Assert.Equal("suppress2", cognitoPreTokenGenerationEvent.Response.ClaimsOverrideDetails.ClaimsToSuppress[1]);

                Assert.Equal(2, cognitoPreTokenGenerationEvent.Response.ClaimsOverrideDetails.GroupOverrideDetails.GroupsToOverride.Count);
                Assert.Equal("group1", cognitoPreTokenGenerationEvent.Response.ClaimsOverrideDetails.GroupOverrideDetails.GroupsToOverride[0]);
                Assert.Equal("group2", cognitoPreTokenGenerationEvent.Response.ClaimsOverrideDetails.GroupOverrideDetails.GroupsToOverride[1]);

                Assert.Equal(2, cognitoPreTokenGenerationEvent.Response.ClaimsOverrideDetails.GroupOverrideDetails.IamRolesToOverride.Count);
                Assert.Equal("role1", cognitoPreTokenGenerationEvent.Response.ClaimsOverrideDetails.GroupOverrideDetails.IamRolesToOverride[0]);
                Assert.Equal("role2", cognitoPreTokenGenerationEvent.Response.ClaimsOverrideDetails.GroupOverrideDetails.IamRolesToOverride[1]);

                Assert.Equal("role", cognitoPreTokenGenerationEvent.Response.ClaimsOverrideDetails.GroupOverrideDetails.PreferredRole);

                var ms = new MemoryStream();
                serializer.Serialize<CognitoPreTokenGenerationEvent>(cognitoPreTokenGenerationEvent, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("cognito-pretokengeneration-event.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoPreTokenGenerationV2EventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-pretokengenerationv2-event.json"))
            {
                var cognitoPreTokenGenerationV2Event = serializer.Deserialize<CognitoPreTokenGenerationV2Event>(fileStream);

                AssertBaseClass(cognitoPreTokenGenerationV2Event, eventVersion: "2");

                Assert.Equal(2, cognitoPreTokenGenerationV2Event.Request.GroupConfiguration.GroupsToOverride.Count);
                Assert.Equal("group1", cognitoPreTokenGenerationV2Event.Request.GroupConfiguration.GroupsToOverride[0]);
                Assert.Equal("group2", cognitoPreTokenGenerationV2Event.Request.GroupConfiguration.GroupsToOverride[1]);

                Assert.Equal(2, cognitoPreTokenGenerationV2Event.Request.GroupConfiguration.IamRolesToOverride.Count);
                Assert.Equal("role1", cognitoPreTokenGenerationV2Event.Request.GroupConfiguration.IamRolesToOverride[0]);
                Assert.Equal("role2", cognitoPreTokenGenerationV2Event.Request.GroupConfiguration.IamRolesToOverride[1]);

                Assert.Equal("role", cognitoPreTokenGenerationV2Event.Request.GroupConfiguration.PreferredRole);

                Assert.Equal(2, cognitoPreTokenGenerationV2Event.Request.ClientMetadata.Count);
                Assert.Equal("metadata_1", cognitoPreTokenGenerationV2Event.Request.ClientMetadata.ToArray()[0].Key);
                Assert.Equal("metadata_value_1", cognitoPreTokenGenerationV2Event.Request.ClientMetadata.ToArray()[0].Value);
                Assert.Equal("metadata_2", cognitoPreTokenGenerationV2Event.Request.ClientMetadata.ToArray()[1].Key);
                Assert.Equal("metadata_value_2", cognitoPreTokenGenerationV2Event.Request.ClientMetadata.ToArray()[1].Value);

                Assert.Equal(2, cognitoPreTokenGenerationV2Event.Request.UserAttributes.Count);
                Assert.Equal("attribute_1", cognitoPreTokenGenerationV2Event.Request.UserAttributes.ToArray()[0].Key);
                Assert.Equal("attribute_value_1", cognitoPreTokenGenerationV2Event.Request.UserAttributes.ToArray()[0].Value);
                Assert.Equal("attribute_2", cognitoPreTokenGenerationV2Event.Request.UserAttributes.ToArray()[1].Key);
                Assert.Equal("attribute_value_2", cognitoPreTokenGenerationV2Event.Request.UserAttributes.ToArray()[1].Value);
                
                Assert.Equal(2, cognitoPreTokenGenerationV2Event.Request.Scopes.Count);
                Assert.Equal("scope_1", cognitoPreTokenGenerationV2Event.Request.Scopes.ToArray()[0]);
                Assert.Equal("scope_2", cognitoPreTokenGenerationV2Event.Request.Scopes.ToArray()[1]);
                
                // Value comparison would vary across different serializers. Skip it for now and validate the complete JSON later.
                Assert.Equal(5, cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.IdTokenGeneration.ClaimsToAddOrOverride.Count);
                Assert.Equal("id_claim_1", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.IdTokenGeneration.ClaimsToAddOrOverride.ToArray()[0].Key);
                Assert.Equal("id_claim_2", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.IdTokenGeneration.ClaimsToAddOrOverride.ToArray()[1].Key);
                Assert.Equal("id_claim_3", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.IdTokenGeneration.ClaimsToAddOrOverride.ToArray()[2].Key);
                Assert.Equal("id_claim_4", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.IdTokenGeneration.ClaimsToAddOrOverride.ToArray()[3].Key);
                Assert.Equal("id_claim_5", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.IdTokenGeneration.ClaimsToAddOrOverride.ToArray()[4].Key);
                
                Assert.Equal(2, cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.IdTokenGeneration.ClaimsToSuppress.Count);
                Assert.Equal("suppress1", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.IdTokenGeneration.ClaimsToSuppress[0]);
                Assert.Equal("suppress2", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.IdTokenGeneration.ClaimsToSuppress[1]);

                // Value comparison would vary across different serializers. Skip it for now and validate the complete JSON later.
                Assert.Equal(5, cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.AccessTokenGeneration.ClaimsToAddOrOverride.Count);
                Assert.Equal("access_claim_1", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.AccessTokenGeneration.ClaimsToAddOrOverride.ToArray()[0].Key);
                Assert.Equal("access_claim_2", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.AccessTokenGeneration.ClaimsToAddOrOverride.ToArray()[1].Key);
                Assert.Equal("access_claim_3", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.AccessTokenGeneration.ClaimsToAddOrOverride.ToArray()[2].Key);
                Assert.Equal("access_claim_4", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.AccessTokenGeneration.ClaimsToAddOrOverride.ToArray()[3].Key);
                Assert.Equal("access_claim_5", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.AccessTokenGeneration.ClaimsToAddOrOverride.ToArray()[4].Key);
                
                Assert.Equal(2, cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.AccessTokenGeneration.ClaimsToSuppress.Count);
                Assert.Equal("suppress1", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.AccessTokenGeneration.ClaimsToSuppress[0]);
                Assert.Equal("suppress2", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.AccessTokenGeneration.ClaimsToSuppress[1]);
                Assert.Equal(2, cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.AccessTokenGeneration.ScopesToAdd.Count);
                Assert.Equal("add1", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.AccessTokenGeneration.ScopesToAdd[0]);
                Assert.Equal("add2", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.AccessTokenGeneration.ScopesToAdd[1]);
                Assert.Equal(2, cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.AccessTokenGeneration.ScopesToSuppress.Count);
                Assert.Equal("suppress1", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.AccessTokenGeneration.ScopesToSuppress[0]);
                Assert.Equal("suppress2", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.AccessTokenGeneration.ScopesToSuppress[1]);
                
                Assert.Equal(2, cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.GroupOverrideDetails.GroupsToOverride.Count);
                Assert.Equal("group1", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.GroupOverrideDetails.GroupsToOverride[0]);
                Assert.Equal("group2", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.GroupOverrideDetails.GroupsToOverride[1]);

                Assert.Equal(2, cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.GroupOverrideDetails.IamRolesToOverride.Count);
                Assert.Equal("role1", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.GroupOverrideDetails.IamRolesToOverride[0]);
                Assert.Equal("role2", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.GroupOverrideDetails.IamRolesToOverride[1]);

                Assert.Equal("role", cognitoPreTokenGenerationV2Event.Response.ClaimsAndScopeOverrideDetails.GroupOverrideDetails.PreferredRole);

                var ms = new MemoryStream();
                serializer.Serialize<CognitoPreTokenGenerationV2Event>(cognitoPreTokenGenerationV2Event, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("cognito-pretokengenerationv2-event.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }
        
        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoMigrateUserEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-migrateuser-event.json"))
            {
                var cognitoMigrateUserEvent = serializer.Deserialize<CognitoMigrateUserEvent>(fileStream);

                AssertBaseClass(cognitoMigrateUserEvent);

                Assert.Equal("username", cognitoMigrateUserEvent.Request.UserName);
                Assert.Equal("pwd", cognitoMigrateUserEvent.Request.Password);

                Assert.Equal(2, cognitoMigrateUserEvent.Request.ValidationData.Count);
                Assert.Equal("validation_1", cognitoMigrateUserEvent.Request.ValidationData.ToArray()[0].Key);
                Assert.Equal("validation_value_1", cognitoMigrateUserEvent.Request.ValidationData.ToArray()[0].Value);
                Assert.Equal("validation_2", cognitoMigrateUserEvent.Request.ValidationData.ToArray()[1].Key);
                Assert.Equal("validation_value_2", cognitoMigrateUserEvent.Request.ValidationData.ToArray()[1].Value);

                Assert.Equal(2, cognitoMigrateUserEvent.Request.ClientMetadata.Count);
                Assert.Equal("metadata_1", cognitoMigrateUserEvent.Request.ClientMetadata.ToArray()[0].Key);
                Assert.Equal("metadata_value_1", cognitoMigrateUserEvent.Request.ClientMetadata.ToArray()[0].Value);
                Assert.Equal("metadata_2", cognitoMigrateUserEvent.Request.ClientMetadata.ToArray()[1].Key);
                Assert.Equal("metadata_value_2", cognitoMigrateUserEvent.Request.ClientMetadata.ToArray()[1].Value);

                Assert.Equal(2, cognitoMigrateUserEvent.Response.UserAttributes.Count);
                Assert.Equal("attribute_1", cognitoMigrateUserEvent.Response.UserAttributes.ToArray()[0].Key);
                Assert.Equal("attribute_value_1", cognitoMigrateUserEvent.Response.UserAttributes.ToArray()[0].Value);
                Assert.Equal("attribute_2", cognitoMigrateUserEvent.Response.UserAttributes.ToArray()[1].Key);
                Assert.Equal("attribute_value_2", cognitoMigrateUserEvent.Response.UserAttributes.ToArray()[1].Value);

                Assert.Equal("action", cognitoMigrateUserEvent.Response.MessageAction);
                Assert.Equal("status", cognitoMigrateUserEvent.Response.FinalUserStatus);
                Assert.True(cognitoMigrateUserEvent.Response.ForceAliasCreation);

                Assert.Equal(2, cognitoMigrateUserEvent.Response.DesiredDeliveryMediums.Count);
                Assert.Equal("medium1", cognitoMigrateUserEvent.Response.DesiredDeliveryMediums[0]);
                Assert.Equal("medium2", cognitoMigrateUserEvent.Response.DesiredDeliveryMediums[1]);
                Assert.True(cognitoMigrateUserEvent.Response.ForceAliasCreation);


                var ms = new MemoryStream();
                serializer.Serialize<CognitoMigrateUserEvent>(cognitoMigrateUserEvent, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("cognito-migrateuser-event.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoCustomMessageEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-custommessage-event.json"))
            {
                var cognitoCustomMessageEvent = serializer.Deserialize<CognitoCustomMessageEvent>(fileStream);

                AssertBaseClass(cognitoCustomMessageEvent);

                Assert.Equal("code", cognitoCustomMessageEvent.Request.CodeParameter);
                Assert.Equal("username", cognitoCustomMessageEvent.Request.UsernameParameter);

                Assert.Equal(2, cognitoCustomMessageEvent.Request.ClientMetadata.Count);
                Assert.Equal("metadata_1", cognitoCustomMessageEvent.Request.ClientMetadata.ToArray()[0].Key);
                Assert.Equal("metadata_value_1", cognitoCustomMessageEvent.Request.ClientMetadata.ToArray()[0].Value);
                Assert.Equal("metadata_2", cognitoCustomMessageEvent.Request.ClientMetadata.ToArray()[1].Key);
                Assert.Equal("metadata_value_2", cognitoCustomMessageEvent.Request.ClientMetadata.ToArray()[1].Value);

                Assert.Equal("sms", cognitoCustomMessageEvent.Response.SmsMessage);
                Assert.Equal("email", cognitoCustomMessageEvent.Response.EmailMessage);
                Assert.Equal("subject", cognitoCustomMessageEvent.Response.EmailSubject);

                var ms = new MemoryStream();
                serializer.Serialize<CognitoCustomMessageEvent>(cognitoCustomMessageEvent, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("cognito-custommessage-event.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoCustomEmailSenderEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-customemailsender-event.json"))
            {
                var cognitoCustomEmailSenderEvent = serializer.Deserialize<CognitoCustomEmailSenderEvent>(fileStream);

                AssertBaseClass(cognitoCustomEmailSenderEvent);

                Assert.Equal("code", cognitoCustomEmailSenderEvent.Request.Code);
                Assert.Equal("type", cognitoCustomEmailSenderEvent.Request.Type);

                var ms = new MemoryStream();
                serializer.Serialize<CognitoCustomEmailSenderEvent>(cognitoCustomEmailSenderEvent, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("cognito-customemailsender-event.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CognitoCustomSmsSenderEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-customsmssender-event.json"))
            {
                var cognitoCustomSmsSenderEvent = serializer.Deserialize<CognitoCustomSmsSenderEvent>(fileStream);

                AssertBaseClass(cognitoCustomSmsSenderEvent);

                Assert.Equal("code", cognitoCustomSmsSenderEvent.Request.Code);
                Assert.Equal("type", cognitoCustomSmsSenderEvent.Request.Type);

                MemoryStream ms = new MemoryStream();
                serializer.Serialize<CognitoCustomSmsSenderEvent>(cognitoCustomSmsSenderEvent, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("cognito-customsmssender-event.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        private static void AssertBaseClass<TRequest, TResponse>(CognitoTriggerEvent<TRequest, TResponse> cognitoTriggerEvent, string eventVersion = "1")
            where TRequest : CognitoTriggerRequest, new()
            where TResponse : CognitoTriggerResponse, new()
        {
            Assert.Equal(eventVersion, cognitoTriggerEvent.Version);
            Assert.Equal("us-east-1", cognitoTriggerEvent.Region);
            Assert.Equal("us-east-1_id", cognitoTriggerEvent.UserPoolId);
            Assert.Equal("username_uuid", cognitoTriggerEvent.UserName);
            Assert.NotNull(cognitoTriggerEvent.CallerContext);
            Assert.Equal("version", cognitoTriggerEvent.CallerContext.AwsSdkVersion);
            Assert.Equal("client_id", cognitoTriggerEvent.CallerContext.ClientId);
            Assert.Equal("trigger_source", cognitoTriggerEvent.TriggerSource);

            Assert.NotNull(cognitoTriggerEvent.Request);
            Assert.Equal(2, cognitoTriggerEvent.Request.UserAttributes.Count);
            Assert.Equal("attribute_1", cognitoTriggerEvent.Request.UserAttributes.ToArray()[0].Key);
            Assert.Equal("attribute_value_1", cognitoTriggerEvent.Request.UserAttributes.ToArray()[0].Value);
            Assert.Equal("attribute_2", cognitoTriggerEvent.Request.UserAttributes.ToArray()[1].Key);
            Assert.Equal("attribute_value_2", cognitoTriggerEvent.Request.UserAttributes.ToArray()[1].Value);

            Assert.NotNull(cognitoTriggerEvent.Response);
        }

        const string ConfigInvokingEvent = "{\"configSnapshotId\":\"00000000-0000-0000-0000-000000000000\",\"s3ObjectKey\":\"AWSLogs/000000000000/Config/us-east-1/2016/2/24/ConfigSnapshot/000000000000_Config_us-east-1_ConfigSnapshot_20160224T182319Z_00000000-0000-0000-0000-000000000000.json.gz\",\"s3Bucket\":\"config-bucket\",\"notificationCreationTime\":\"2016-02-24T18:23:20.328Z\",\"messageType\":\"ConfigurationSnapshotDeliveryCompleted\",\"recordVersion\":\"1.1\"}";

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void ConfigTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("config-event.json"))
            {
                var configEvent = serializer.Deserialize<ConfigEvent>(fileStream);
                Assert.Equal("config-rule-0123456", configEvent.ConfigRuleId);
                Assert.Equal("1.0", configEvent.Version);
                Assert.Equal("periodic-config-rule", configEvent.ConfigRuleName);
                Assert.Equal("arn:aws:config:us-east-1:012345678912:config-rule/config-rule-0123456", configEvent.ConfigRuleArn);
                Assert.Equal(ConfigInvokingEvent, configEvent.InvokingEvent);
                Assert.Equal("myResultToken", configEvent.ResultToken);
                Assert.False(configEvent.EventLeftScope);
                Assert.Equal("{\"<myParameterKey>\":\"<myParameterValue>\"}", configEvent.RuleParameters);
                Assert.Equal("arn:aws:iam::012345678912:role/config-role", configEvent.ExecutionRoleArn);
                Assert.Equal("012345678912", configEvent.AccountId);

                Handle(configEvent);
            }
        }

        private static void Handle(ConfigEvent configEvent)
        {
            Console.WriteLine($"AWS Config rule - {configEvent.ConfigRuleName}");
            Console.WriteLine($"Invoking event JSON - {configEvent.InvokingEvent}");
            Console.WriteLine($"Event version - {configEvent.Version}");
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void ConnectContactFlowTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("connect-contactflow-event.json"))
            {
                var contactFlowEvent = serializer.Deserialize<ContactFlowEvent>(fileStream);
                Assert.Equal("ContactFlowEvent", contactFlowEvent.Name);
                Assert.NotNull(contactFlowEvent.Details);

                Assert.NotNull(contactFlowEvent.Details.ContactData);
                Assert.NotNull(contactFlowEvent.Details.ContactData.Attributes);
                Assert.Empty(contactFlowEvent.Details.ContactData.Attributes);
                Assert.Equal("VOICE", contactFlowEvent.Details.ContactData.Channel);
                Assert.Equal("4a573372-1f28-4e26-b97b-XXXXXXXXXXX", contactFlowEvent.Details.ContactData.ContactId);
                Assert.NotNull(contactFlowEvent.Details.ContactData.CustomerEndpoint);
                Assert.Equal("+1234567890", contactFlowEvent.Details.ContactData.CustomerEndpoint.Address);
                Assert.Equal("TELEPHONE_NUMBER", contactFlowEvent.Details.ContactData.CustomerEndpoint.Type);
                Assert.Equal("4a573372-1f28-4e26-b97b-XXXXXXXXXXX", contactFlowEvent.Details.ContactData.InitialContactId);
                Assert.Equal("INBOUND | OUTBOUND | TRANSFER | CALLBACK", contactFlowEvent.Details.ContactData.InitiationMethod);
                Assert.Equal("arn:aws:connect:aws-region:1234567890:instance/c8c0e68d-2200-4265-82c0-XXXXXXXXXX", contactFlowEvent.Details.ContactData.InstanceARN);
                Assert.Equal("4a573372-1f28-4e26-b97b-XXXXXXXXXXX", contactFlowEvent.Details.ContactData.PreviousContactId);
                Assert.NotNull(contactFlowEvent.Details.ContactData.Queue);
                Assert.Equal("arn:aws:connect:eu-west-2:111111111111:instance/cccccccc-bbbb-dddd-eeee-ffffffffffff/queue/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", contactFlowEvent.Details.ContactData.Queue.Arn);
                Assert.Equal("PasswordReset", contactFlowEvent.Details.ContactData.Queue.Name);
                Assert.NotNull(contactFlowEvent.Details.ContactData.SystemEndpoint);
                Assert.Equal("+1234567890", contactFlowEvent.Details.ContactData.SystemEndpoint.Address);
                Assert.Equal("TELEPHONE_NUMBER", contactFlowEvent.Details.ContactData.SystemEndpoint.Type);

                Assert.NotNull(contactFlowEvent.Details.Parameters);
                Assert.Single(contactFlowEvent.Details.Parameters);
                Assert.Equal("sentAttributeValue", contactFlowEvent.Details.Parameters["sentAttributeKey"]);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void SimpleEmailTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("simple-email-event-lambda.json"))
            {
                var sesEvent = serializer.Deserialize<SimpleEmailEvent<SimpleEmailEvents.Actions.LambdaReceiptAction>>(fileStream);

                Assert.Single(sesEvent.Records);
                var record = sesEvent.Records[0];

                Assert.Equal("1.0", record.EventVersion);
                Assert.Equal("aws:ses", record.EventSource);

                Assert.Single(record.Ses.Mail.CommonHeaders.From);
                Assert.Equal("Amazon Web Services <aws@amazon.com>", record.Ses.Mail.CommonHeaders.From[0]);
                Assert.Single(record.Ses.Mail.CommonHeaders.To);
                Assert.Equal("lambda@amazon.com", record.Ses.Mail.CommonHeaders.To[0]);
                Assert.Equal("aws@amazon.com", record.Ses.Mail.CommonHeaders.ReturnPath);
                Assert.Equal("<CAEddw6POFV_On91m+ZoL_SN8B_M2goDe_Ni355owhc7QSjPQSQ@amazon.com>", record.Ses.Mail.CommonHeaders.MessageId);
                Assert.Equal("Mon, 5 Dec 2016 18:40:08 -0800", record.Ses.Mail.CommonHeaders.Date);
                Assert.Equal("Test Subject", record.Ses.Mail.CommonHeaders.Subject);
                Assert.Equal("aws@amazon.com", record.Ses.Mail.Source);
                Assert.Equal(DateTime.Parse("2016-12-06T02:40:08.000Z").ToUniversalTime(), record.Ses.Mail.Timestamp.ToUniversalTime());
                Assert.Single(record.Ses.Mail.Destination);
                Assert.Equal("lambda@amazon.com", record.Ses.Mail.Destination[0]);
                Assert.Equal(10, record.Ses.Mail.Headers.Count);
                Assert.Equal("Return-Path", record.Ses.Mail.Headers[0].Name);
                Assert.Equal("<aws@amazon.com>", record.Ses.Mail.Headers[0].Value);
                Assert.Equal("Received", record.Ses.Mail.Headers[1].Name);
                Assert.Equal("from mx.amazon.com (mx.amazon.com [127.0.0.1]) by inbound-smtp.us-east-1.amazonaws.com with SMTP id 6n4thuhcbhpfiuf25gshf70rss364fuejrvmqko1 for lambda@amazon.com; Tue, 06 Dec 2016 02:40:10 +0000 (UTC)", record.Ses.Mail.Headers[1].Value);
                Assert.Equal("DKIM-Signature", record.Ses.Mail.Headers[2].Name);
                Assert.Equal("v=1; a=rsa-sha256; c=relaxed/relaxed; d=iatn.net; s=amazon; h=mime-version:from:date:message-id:subject:to; bh=chlJxa/vZ11+0O9lf4tKDM/CcPjup2nhhdITm+hSf3c=; b=SsoNPK0wX7umtWnw8pln3YSib+E09XO99d704QdSc1TR1HxM0OTti/UaFxVD4e5b0+okBqo3rgVeWgNZ0sWZEUhBaZwSL3kTd/nHkcPexeV0XZqEgms1vmbg75F6vlz9igWflO3GbXyTRBNMM0gUXKU/686hpVW6aryEIfM/rLY=", record.Ses.Mail.Headers[2].Value);
                Assert.Equal("MIME-Version", record.Ses.Mail.Headers[3].Name);
                Assert.Equal("1.0", record.Ses.Mail.Headers[3].Value);
                Assert.Equal("From", record.Ses.Mail.Headers[4].Name);
                Assert.Equal("Amazon Web Services <aws@amazon.com>", record.Ses.Mail.Headers[4].Value);
                Assert.Equal("Date", record.Ses.Mail.Headers[5].Name);
                Assert.Equal("Mon, 5 Dec 2016 18:40:08 -0800", record.Ses.Mail.Headers[5].Value);
                Assert.Equal("Message-ID", record.Ses.Mail.Headers[6].Name);
                Assert.Equal("<CAEddw6POFV_On91m+ZoL_SN8B_M2goDe_Ni355owhc7QSjPQSQ@amazon.com>", record.Ses.Mail.Headers[6].Value);
                Assert.Equal("Subject", record.Ses.Mail.Headers[7].Name);
                Assert.Equal("Test Subject", record.Ses.Mail.Headers[7].Value);
                Assert.Equal("To", record.Ses.Mail.Headers[8].Name);
                Assert.Equal("lambda@amazon.com", record.Ses.Mail.Headers[8].Value);
                Assert.Equal("Content-Type", record.Ses.Mail.Headers[9].Name);
                Assert.Equal("multipart/alternative; boundary=94eb2c0742269658b10542f452a9", record.Ses.Mail.Headers[9].Value);
                Assert.False(record.Ses.Mail.HeadersTruncated);
                Assert.Equal("6n4thuhcbhpfiuf25gshf70rss364fuejrvmqko1", record.Ses.Mail.MessageId);

                Assert.Single(record.Ses.Receipt.Recipients);
                Assert.Equal("lambda@amazon.com", record.Ses.Receipt.Recipients[0]);
                Assert.Equal(DateTime.Parse("2016-12-06T02:40:08.000Z").ToUniversalTime(), record.Ses.Receipt.Timestamp.ToUniversalTime());
                Assert.Equal("PASS", record.Ses.Receipt.SpamVerdict.Status);
                Assert.Equal("PASS", record.Ses.Receipt.DKIMVerdict.Status);
                Assert.Equal("PASS", record.Ses.Receipt.SPFVerdict.Status);
                Assert.Equal("PASS", record.Ses.Receipt.VirusVerdict.Status);
                Assert.Equal("PASS", record.Ses.Receipt.DMARCVerdict.Status);
                Assert.Equal(574, record.Ses.Receipt.ProcessingTimeMillis);

                Handle(sesEvent);
            }
        }
        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void SimpleEmailLambdaActionTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("simple-email-event-lambda.json"))
            {
                var sesEvent = serializer.Deserialize<SimpleEmailEvent<SimpleEmailEvents.Actions.LambdaReceiptAction>>(fileStream);

                Assert.Single(sesEvent.Records);
                var record = sesEvent.Records[0];

                Assert.Equal("Lambda", record.Ses.Receipt.Action.Type);
                Assert.Equal("Event", record.Ses.Receipt.Action.InvocationType);
                Assert.Equal("arn:aws:lambda:us-east-1:000000000000:function:my-ses-lambda-function", record.Ses.Receipt.Action.FunctionArn);

                Handle(sesEvent);
            }
        }
        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void SimpleEmailS3ActionTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("simple-email-event-s3.json"))
            {
                var sesEvent = serializer.Deserialize<SimpleEmailEvent<SimpleEmailEvents.Actions.S3ReceiptAction>>(fileStream);

                Assert.Single(sesEvent.Records);
                var record = sesEvent.Records[0];

                Assert.Equal("S3", record.Ses.Receipt.Action.Type);
                Assert.Equal("arn:aws:sns:eu-west-1:123456789:ses-email-received", record.Ses.Receipt.Action.TopicArn);
                Assert.Equal("my-ses-inbox", record.Ses.Receipt.Action.BucketName);
                Assert.Equal("important", record.Ses.Receipt.Action.ObjectKeyPrefix);
                Assert.Equal("important/fiddlyfaddlyhiddlyhoodly", record.Ses.Receipt.Action.ObjectKey);

                Handle(sesEvent);
            }
        }
        private static void Handle<TReceiptAction>(SimpleEmailEvent<TReceiptAction> sesEvent)
            where TReceiptAction : SimpleEmailEvents.Actions.IReceiptAction
        {
            foreach (var record in sesEvent.Records)
            {
                var sesRecord = record.Ses;
                Console.WriteLine($"[{record.EventSource} {sesRecord.Mail.Timestamp}] Subject = {sesRecord.Mail.CommonHeaders.Subject}");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void SNSTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("sns-event.json"))
            {
                var snsEvent = serializer.Deserialize<SNSEvent>(fileStream);

                Assert.Single(snsEvent.Records);
                var record = snsEvent.Records[0];
                Assert.Equal("1.0", record.EventVersion);
                Assert.Equal("arn:aws:sns:EXAMPLE", record.EventSubscriptionArn);
                Assert.Equal("aws:sns", record.EventSource);
                Assert.Equal("1", record.Sns.SignatureVersion);
                Assert.Equal(DateTime.Parse("1970-01-01T00:00:00.000Z").ToUniversalTime(), record.Sns.Timestamp.ToUniversalTime());
                Assert.Equal("EXAMPLE", record.Sns.Signature);
                Assert.Equal("EXAMPLE", record.Sns.SigningCertUrl);
                Assert.Equal("95df01b4-ee98-5cb9-9903-4c221d41eb5e", record.Sns.MessageId);
                Assert.Equal("Hello from SNS!", record.Sns.Message);
                Assert.True(record.Sns.MessageAttributes.ContainsKey("Test"));
                Assert.Equal("String", record.Sns.MessageAttributes["Test"].Type);
                Assert.Equal("TestString", record.Sns.MessageAttributes["Test"].Value);
                Assert.True(record.Sns.MessageAttributes.ContainsKey("TestBinary"));
                Assert.Equal("Binary", record.Sns.MessageAttributes["TestBinary"].Type);
                Assert.Equal("TestBinary", record.Sns.MessageAttributes["TestBinary"].Value);
                Assert.Equal("Notification", record.Sns.Type);
                Assert.Equal("EXAMPLE", record.Sns.UnsubscribeUrl);
                Assert.Equal("arn:aws:sns:EXAMPLE", record.Sns.TopicArn);
                Assert.Equal("TestInvoke", record.Sns.Subject);

                Handle(snsEvent);
            }
        }

        private static void Handle(SNSEvent snsEvent)
        {
            foreach (var record in snsEvent.Records)
            {
                var snsRecord = record.Sns;
                Console.WriteLine($"[{record.EventSource} {snsRecord.Timestamp}] Message = {snsRecord.Message}");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void SQSTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("sqs-event.json"))
            {
                var sqsEvent = serializer.Deserialize<SQSEvent>(fileStream);

                Assert.Single(sqsEvent.Records);
                var record = sqsEvent.Records[0];
                Assert.Equal("MessageID", record.MessageId);
                Assert.Equal("MessageReceiptHandle", record.ReceiptHandle);
                Assert.Equal("Message Body", record.Body);
                Assert.Equal("fce0ea8dd236ccb3ed9b37dae260836f", record.Md5OfBody);
                Assert.Equal("582c92c5c5b6ac403040a4f3ab3115c9", record.Md5OfMessageAttributes);
                Assert.Equal("arn:aws:sqs:us-west-2:123456789012:SQSQueue", record.EventSourceArn);
                Assert.Equal("aws:sqs", record.EventSource);
                Assert.Equal("us-west-2", record.AwsRegion);
                Assert.Equal("2", record.Attributes["ApproximateReceiveCount"]);
                Assert.Equal("1520621625029", record.Attributes["SentTimestamp"]);
                Assert.Equal("AROAIWPX5BD2BHG722MW4:sender", record.Attributes["SenderId"]);
                Assert.Equal("1520621634884", record.Attributes["ApproximateFirstReceiveTimestamp"]);

                Assert.Equal(2, record.MessageAttributes.Count);
                {
                    var attribute1 = record.MessageAttributes["Attribute1"];
                    Assert.NotNull(attribute1);

                    Assert.Equal("123", attribute1.StringValue);
                    Assert.Equal("Smaug", new StreamReader(attribute1.BinaryValue).ReadToEnd());
                    Assert.Equal(2, attribute1.StringListValues.Count);
                    Assert.Equal("a1", attribute1.StringListValues[0]);
                    Assert.Equal("a2", attribute1.StringListValues[1]);

                    Assert.Equal(2, attribute1.BinaryListValues.Count);
                    Assert.Equal("Vermithrax", new StreamReader(attribute1.BinaryListValues[0]).ReadToEnd());
                    Assert.Equal("Pejorative", new StreamReader(attribute1.BinaryListValues[1]).ReadToEnd());

                    Assert.Equal("Number", attribute1.DataType);
                }

                {
                    var attribute2 = record.MessageAttributes["Attribute2"];
                    Assert.NotNull(attribute2);
                    Assert.Equal("AttributeValue2", attribute2.StringValue);
                    Assert.Equal(2, attribute2.StringListValues.Count);
                    Assert.Equal("b1", attribute2.StringListValues[0]);
                    Assert.Equal("b2", attribute2.StringListValues[1]);

                    Assert.Equal("String", attribute2.DataType);
                    Assert.Null(attribute2.BinaryValue);
                }

                Handle(sqsEvent);
            }
        }

        private static void Handle(SQSEvent sqsEvent)
        {
            foreach (var record in sqsEvent.Records)
            {
                Console.WriteLine($"[{record.EventSource}] Body = {record.Body}");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void SQSBatchResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("sqs-response.json"))
            {
                var sqsBatchResponse = serializer.Deserialize<SQSBatchResponse>(fileStream);

                Assert.Equal(2, sqsBatchResponse.BatchItemFailures.Count);
                {
                    var item1 = sqsBatchResponse.BatchItemFailures[0];
                    Assert.NotNull(item1);
                    Assert.Equal("MessageID_1", item1.ItemIdentifier);
                }

                var item2 = sqsBatchResponse.BatchItemFailures[1];
                {
                    Assert.NotNull(item2);
                    Assert.Equal("MessageID_2", item2.ItemIdentifier);
                }

                MemoryStream ms = new MemoryStream();
                serializer.Serialize<SQSBatchResponse>(sqsBatchResponse, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("sqs-response.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void APIGatewayProxyRequestTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("proxy-event.json"))
            {
                var proxyEvent = serializer.Deserialize<APIGatewayProxyRequest>(fileStream);

                Assert.Equal("/{proxy+}", proxyEvent.Resource);
                Assert.Equal("/hello/world", proxyEvent.Path);
                Assert.Equal("POST", proxyEvent.HttpMethod);
                Assert.Equal("{\r\n\t\"a\": 1\r\n}", proxyEvent.Body);

                var headers = proxyEvent.Headers;
                Assert.Equal("*/*", headers["Accept"]);
                Assert.Equal("gzip, deflate", headers["Accept-Encoding"]);
                Assert.Equal("no-cache", headers["cache-control"]);
                Assert.Equal("https", headers["CloudFront-Forwarded-Proto"]);

                var queryStringParameters = proxyEvent.QueryStringParameters;
                Assert.Equal("me", queryStringParameters["name"]);

                var pathParameters = proxyEvent.PathParameters;
                Assert.Equal("hello/world", pathParameters["proxy"]);

                var stageVariables = proxyEvent.StageVariables;
                Assert.Equal("stageVariableValue", stageVariables["stageVariableName"]);

                var requestContext = proxyEvent.RequestContext;
                Assert.Equal("12345678912", requestContext.AccountId);
                Assert.Equal("roq9wj", requestContext.ResourceId);
                Assert.Equal("testStage", requestContext.Stage);
                Assert.Equal("deef4878-7910-11e6-8f14-25afc3e9ae33", requestContext.RequestId);
                Assert.Equal("d034bc98-beed-4fdf-9e85-11bfc15bf734", requestContext.ConnectionId);
                Assert.Equal("somerandomdomain.net", requestContext.DomainName);
                Assert.Equal(1519166937665, requestContext.RequestTimeEpoch);
                Assert.Equal("20/Feb/2018:22:48:57 +0000", requestContext.RequestTime);

                var identity = requestContext.Identity;
                Assert.Equal("theCognitoIdentityPoolId", identity.CognitoIdentityPoolId);
                Assert.Equal("theAccountId", identity.AccountId);
                Assert.Equal("theCognitoIdentityId", identity.CognitoIdentityId);
                Assert.Equal("theCaller", identity.Caller);
                Assert.Equal("theApiKey", identity.ApiKey);
                Assert.Equal("192.168.196.186", identity.SourceIp);
                Assert.Equal("theCognitoAuthenticationType", identity.CognitoAuthenticationType);
                Assert.Equal("theCognitoAuthenticationProvider", identity.CognitoAuthenticationProvider);
                Assert.Equal("theUserArn", identity.UserArn);
                Assert.Equal("PostmanRuntime/2.4.5", identity.UserAgent);
                Assert.Equal("theUser", identity.User);
                Assert.Equal("IAM_user_access_key", identity.AccessKey);

                var clientCert = identity.ClientCert;
                Assert.Equal("CERT_CONTENT", clientCert.ClientCertPem);
                Assert.Equal("www.example.com", clientCert.SubjectDN);
                Assert.Equal("Example issuer", clientCert.IssuerDN);
                Assert.Equal("a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1", clientCert.SerialNumber);

                Assert.Equal("May 28 12:30:02 2019 GMT", clientCert.Validity.NotBefore);
                Assert.Equal("Aug  5 09:36:04 2021 GMT", clientCert.Validity.NotAfter);

                Handle(proxyEvent);
            }
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

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void APIGatewayProxyResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            var response = new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string> { { "Header1", "Value1" }, { "Header2", "Value2" } },
                Body = "theBody"
            };

            string serializedJson;
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.Serialize(response, stream);

                stream.Position = 0;
                serializedJson = Encoding.UTF8.GetString(stream.ToArray());
            }

            var root = Newtonsoft.Json.JsonConvert.DeserializeObject(serializedJson) as JObject;
            Assert.Equal(200, root["statusCode"]);
            Assert.Equal("theBody", root["body"]);

            Assert.NotNull(root["headers"]);
            var headers = root["headers"] as JObject;
            Assert.Equal("Value1", headers["Header1"]);
            Assert.Equal("Value2", headers["Header2"]);
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void APIGatewayAuthorizerResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            var context = new APIGatewayCustomAuthorizerContextOutput
            {
                ["field1"] = "value1",
                ["field2"] = "value2"
            };

            var response = new APIGatewayCustomAuthorizerResponse
            {
                PrincipalID = "prin1",
                UsageIdentifierKey = "usageKey",
                Context = context,
                PolicyDocument = new APIGatewayCustomAuthorizerPolicy
                {
                    Version = "2012-10-17",
                    Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>
                    {
                        new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement
                        {
                            Action = ["execute-api:Invoke"],
                            Effect = "Allow",
                            Resource = new HashSet<string>{ "*" }
                        }
                    }
                }
            };

            string serializedJson;
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.Serialize(response, stream);

                stream.Position = 0;
                serializedJson = Encoding.UTF8.GetString(stream.ToArray());
            }

            var root = Newtonsoft.Json.JsonConvert.DeserializeObject(serializedJson) as JObject;

            Assert.Equal("prin1", root["principalId"]);
            Assert.Equal("usageKey", root["usageIdentifierKey"]);
            Assert.Equal("value1", root["context"]["field1"]);
            Assert.Equal("value2", root["context"]["field2"]);

            Assert.Equal("2012-10-17", root["policyDocument"]["Version"]);
            Assert.Equal("execute-api:Invoke", root["policyDocument"]["Statement"][0]["Action"][0]);
            Assert.Equal("Allow", root["policyDocument"]["Statement"][0]["Effect"]);
            Assert.Equal("*", root["policyDocument"]["Statement"][0]["Resource"][0]);
            Assert.Null(root["policyDocument"]["Statement"][0]["Condition"]);
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void APIGatewayAuthorizerWithSimpleIAMConditionResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            var context = new APIGatewayCustomAuthorizerContextOutput
            {
                ["field1"] = "value1",
                ["field2"] = "value2"
            };

            var response = new APIGatewayCustomAuthorizerResponse
            {
                PrincipalID = "prin1",
                UsageIdentifierKey = "usageKey",
                Context = context,
                PolicyDocument = new APIGatewayCustomAuthorizerPolicy
                {
                    Version = "2012-10-17",
                    Statement =
                    [
                        new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement
                        {
                            Action = new HashSet<string>{ "execute-api:Invoke" },
                            Effect = "Allow",
                            Resource = new HashSet<string>{ "*" },
                            Condition = new Dictionary<string, IDictionary<string, object>>()
                            {
                                {  "StringEquals", new Dictionary<string, object>()
                                    { 
                                        { "aws:PrincipalTag/job-category", "iamuser-admin" }
                                    }
                                }
                            }
                        }
                    ]
                }
            };

            string serializedJson;
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.Serialize(response, stream);

                stream.Position = 0;
                serializedJson = Encoding.UTF8.GetString(stream.ToArray());
            }

            var root = Newtonsoft.Json.JsonConvert.DeserializeObject(serializedJson) as JObject;

            Assert.Equal("prin1", root["principalId"]);
            Assert.Equal("usageKey", root["usageIdentifierKey"]);
            Assert.Equal("value1", root["context"]["field1"]);
            Assert.Equal("value2", root["context"]["field2"]);

            Assert.Equal("2012-10-17", root["policyDocument"]["Version"]);
            Assert.Equal("execute-api:Invoke", root["policyDocument"]["Statement"][0]["Action"][0]);
            Assert.Equal("Allow", root["policyDocument"]["Statement"][0]["Effect"]);
            Assert.Equal("*", root["policyDocument"]["Statement"][0]["Resource"][0]);
            Assert.Equal("iamuser-admin", root["policyDocument"]["Statement"][0]["Condition"]["StringEquals"]["aws:PrincipalTag/job-category"].ToString());
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void APIGatewayAuthorizerWithMultiValueIAMConditionResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            var context = new APIGatewayCustomAuthorizerContextOutput
            {
                ["field1"] = "value1",
                ["field2"] = "value2"
            };

            var response = new APIGatewayCustomAuthorizerResponse
            {
                PrincipalID = "prin1",
                UsageIdentifierKey = "usageKey",
                Context = context,
                PolicyDocument = new APIGatewayCustomAuthorizerPolicy
                {
                    Version = "2012-10-17",
                    Statement =
                    [
                        new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement
                        {
                            Action = new HashSet<string>{ "execute-api:Invoke" },
                            Effect = "Allow",
                            Resource = new HashSet<string>{ "*" },
                            Condition = new Dictionary<string, IDictionary<string, object>>()
                            {
                                {  
                                    "StringEquals", 
                                    new Dictionary<string, object>()
                                    {
                                        { "aws:PrincipalTag/department", new List<string>{ "finance", "hr", "legal" } },
                                        { "aws:PrincipalTag/role", new List<string>{ "audit", "security" } }
                                    }
                                },
                                { 
                                    "ArnLike", 
                                    new Dictionary<string, object>()
                                    {
                                        { "aws:PrincipalArn", new List<string>{ "arn:aws:iam::XXXXXXXXXXXX:user/User1", "arn:aws:iam::XXXXXXXXXXXX:user/User2" } }
                                    }
                                }
                            }
                        }
                    ]
                }
            };

            string serializedJson;
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.Serialize(response, stream);

                stream.Position = 0;
                serializedJson = Encoding.UTF8.GetString(stream.ToArray());
            }

            var root = Newtonsoft.Json.JsonConvert.DeserializeObject(serializedJson) as JObject;

            Assert.Equal("prin1", root["principalId"]);
            Assert.Equal("usageKey", root["usageIdentifierKey"]);
            Assert.Equal("value1", root["context"]["field1"]);
            Assert.Equal("value2", root["context"]["field2"]);

            Assert.Equal("2012-10-17", root["policyDocument"]["Version"]);
            Assert.Equal("execute-api:Invoke", root["policyDocument"]["Statement"][0]["Action"][0]);
            Assert.Equal("Allow", root["policyDocument"]["Statement"][0]["Effect"]);
            Assert.Equal("*", root["policyDocument"]["Statement"][0]["Resource"][0]);
            Assert.Equal(3, root["policyDocument"]["Statement"][0]["Condition"]["StringEquals"]["aws:PrincipalTag/department"].Values<string>().ToList().Count);
            Assert.Equal("finance", root["policyDocument"]["Statement"][0]["Condition"]["StringEquals"]["aws:PrincipalTag/department"][0]);
            Assert.Equal("hr", root["policyDocument"]["Statement"][0]["Condition"]["StringEquals"]["aws:PrincipalTag/department"][1]);
            Assert.Equal("legal", root["policyDocument"]["Statement"][0]["Condition"]["StringEquals"]["aws:PrincipalTag/department"][2]);
            Assert.Equal(2, root["policyDocument"]["Statement"][0]["Condition"]["StringEquals"]["aws:PrincipalTag/role"].Values<string>().ToList().Count);
            Assert.Equal("audit", root["policyDocument"]["Statement"][0]["Condition"]["StringEquals"]["aws:PrincipalTag/role"][0]);
            Assert.Equal("security", root["policyDocument"]["Statement"][0]["Condition"]["StringEquals"]["aws:PrincipalTag/role"][1]);
            Assert.Equal(2, root["policyDocument"]["Statement"][0]["Condition"]["ArnLike"]["aws:PrincipalArn"].Values<string>().ToList().Count);
            Assert.Equal("arn:aws:iam::XXXXXXXXXXXX:user/User1", root["policyDocument"]["Statement"][0]["Condition"]["ArnLike"]["aws:PrincipalArn"][0]);
            Assert.Equal("arn:aws:iam::XXXXXXXXXXXX:user/User2", root["policyDocument"]["Statement"][0]["Condition"]["ArnLike"]["aws:PrincipalArn"][1]);
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void APIGatewayAuthorizerResponseNotResourceTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            var context = new APIGatewayCustomAuthorizerContextOutput
            {
                ["field1"] = "value1",
                ["field2"] = "value2"
            };

            var response = new APIGatewayCustomAuthorizerResponse
            {
                PrincipalID = "prin1",
                UsageIdentifierKey = "usageKey",
                Context = context,
                PolicyDocument = new APIGatewayCustomAuthorizerPolicy
                {
                    Version = "2012-10-17",
                    Statement =
                    [
                        new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement
                        {
                            Action = new HashSet<string>{ "execute-api:Invoke" },
                            Effect = "Deny",
                            NotResource =
                            [
                                "arn:aws:execute-api:us-east-1:1234567890:abcdef1234/Prod/GET/resource1",
                                "arn:aws:execute-api:us-east-1:1234567890:abcdef1234/Prod/GET/resource2"
                            ]
                        }
                    ]
                }
            };

            string serializedJson;
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.Serialize(response, stream);

                stream.Position = 0;
                serializedJson = Encoding.UTF8.GetString(stream.ToArray());
            }

            var root = Newtonsoft.Json.JsonConvert.DeserializeObject(serializedJson) as JObject;

            Assert.Equal("prin1", root["principalId"]);
            Assert.Equal("usageKey", root["usageIdentifierKey"]);
            Assert.Equal("value1", root["context"]["field1"]);
            Assert.Equal("value2", root["context"]["field2"]);

            Assert.Equal("2012-10-17", root["policyDocument"]["Version"]);
            Assert.Equal("execute-api:Invoke", root["policyDocument"]["Statement"][0]["Action"][0]);
            Assert.Equal("Deny", root["policyDocument"]["Statement"][0]["Effect"]);

            var allowedResources = root["policyDocument"]["Statement"][0]["NotResource"];
            Assert.Equal(2, allowedResources.Count());
            Assert.Contains("arn:aws:execute-api:us-east-1:1234567890:abcdef1234/Prod/GET/resource1", allowedResources);
            Assert.Contains("arn:aws:execute-api:us-east-1:1234567890:abcdef1234/Prod/GET/resource2", allowedResources);

            Assert.Null(root["policyDocument"]["Statement"][0]["Condition"]);
            Assert.Null(root["policyDocument"]["Statement"][0]["Resource"]);
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void WebSocketApiConnectTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("websocket-api-connect-request.json"))
            {
                var proxyEvent = serializer.Deserialize<APIGatewayProxyRequest>(fileStream);

                Assert.Null(proxyEvent.Resource);
                Assert.Null(proxyEvent.Path);
                Assert.Null(proxyEvent.HttpMethod);
                Assert.Null(proxyEvent.Body);

                var headers = proxyEvent.Headers;
                Assert.Equal("headerValue1", headers["HeaderAuth1"]);
                Assert.Equal("lg10ltpf4f.execute-api.us-east-2.amazonaws.com", headers["Host"]);
                Assert.Equal("permessage-deflate; client_max_window_bits", headers["Sec-WebSocket-Extensions"]);
                Assert.Equal("BvlrrFKoKAPDYOlwBcGKWw==", headers["Sec-WebSocket-Key"]);
                Assert.Equal("13", headers["Sec-WebSocket-Version"]);
                Assert.Equal("Root=1-625d9ad1-37a5d33a61dd9be33ae3a247", headers["X-Amzn-Trace-Id"]);
                Assert.Equal("52.95.4.0", headers["X-Forwarded-For"]);
                Assert.Equal("443", headers["X-Forwarded-Port"]);
                Assert.Equal("https", headers["X-Forwarded-Proto"]);

                var multiValueHeaders = proxyEvent.MultiValueHeaders;
                Assert.Single(multiValueHeaders["HeaderAuth1"]);
                Assert.Equal("headerValue1", multiValueHeaders["HeaderAuth1"][0]);
                Assert.Single(multiValueHeaders["Host"]);
                Assert.Equal("lg10ltpf4f.execute-api.us-east-2.amazonaws.com", multiValueHeaders["Host"][0]);
                Assert.Single(multiValueHeaders["Sec-WebSocket-Extensions"]);
                Assert.Equal("permessage-deflate; client_max_window_bits", multiValueHeaders["Sec-WebSocket-Extensions"][0]);
                Assert.Single(multiValueHeaders["Sec-WebSocket-Key"]);
                Assert.Equal("BvlrrFKoKAPDYOlwBcGKWw==", multiValueHeaders["Sec-WebSocket-Key"][0]);
                Assert.Single(multiValueHeaders["Sec-WebSocket-Version"]);
                Assert.Equal("13", multiValueHeaders["Sec-WebSocket-Version"][0]);
                Assert.Single(multiValueHeaders["X-Amzn-Trace-Id"]);
                Assert.Equal("Root=1-625d9ad1-37a5d33a61dd9be33ae3a247", multiValueHeaders["X-Amzn-Trace-Id"][0]);
                Assert.Single(multiValueHeaders["X-Forwarded-For"]);
                Assert.Equal("52.95.4.0", multiValueHeaders["X-Forwarded-For"][0]);
                Assert.Single(multiValueHeaders["X-Forwarded-Port"]);
                Assert.Equal("443", multiValueHeaders["X-Forwarded-Port"][0]);
                Assert.Single(multiValueHeaders["X-Forwarded-Proto"]);
                Assert.Equal("https", multiValueHeaders["X-Forwarded-Proto"][0]);

                var requestContext = proxyEvent.RequestContext;
                Assert.Equal("$connect", requestContext.RouteKey);
                Assert.Equal("CONNECT", requestContext.EventType);
                Assert.Equal("QyUg1HJgCYcFvbw=", requestContext.ExtendedRequestId);
                Assert.Equal("18/Apr/2022:17:07:29 +0000", requestContext.RequestTime);
                Assert.Equal("IN", requestContext.MessageDirection);
                Assert.Equal("production", requestContext.Stage);
                Assert.Equal(1650301649973, requestContext.ConnectedAt);
                Assert.Equal(1650301649973, requestContext.RequestTimeEpoch);
                Assert.Equal("QyUg1HJgCYcFvbw=", requestContext.RequestId);
                Assert.Equal("lg10ltpf4f.execute-api.us-east-2.amazonaws.com", requestContext.DomainName);
                Assert.Equal("QyUg1czHCYcCHXw=", requestContext.ConnectionId);
                Assert.Equal("lg10ltpf4f", requestContext.ApiId);

                Assert.False(proxyEvent.IsBase64Encoded);

                var identity = requestContext.Identity;
                Assert.Equal("52.95.4.0", identity.SourceIp);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void ApplicationLoadBalancerRequestSingleValueTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("alb-request-single-value.json"))
            {
                var evnt = serializer.Deserialize<ApplicationLoadBalancerRequest>(fileStream);

                Assert.Equal("/", evnt.Path);
                Assert.Equal("GET", evnt.HttpMethod);
                Assert.Equal("not really base64", evnt.Body);
                Assert.True(evnt.IsBase64Encoded);

                Assert.Equal(2, evnt.QueryStringParameters.Count);
                Assert.Equal("value1", evnt.QueryStringParameters["query1"]);
                Assert.Equal("value2", evnt.QueryStringParameters["query2"]);

                Assert.Equal("value1", evnt.Headers["head1"]);
                Assert.Equal("value2", evnt.Headers["head2"]);


                var requestContext = evnt.RequestContext;
                Assert.Equal("arn:aws:elasticloadbalancing:region:123456789012:targetgroup/my-target-group/6d0ecf831eec9f09", requestContext.Elb.TargetGroupArn);
            }
        }


        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void ApplicationLoadBalancerRequestMultiValueTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("alb-request-multi-value.json"))
            {
                var evnt = serializer.Deserialize<ApplicationLoadBalancerRequest>(fileStream);

                Assert.Equal("/", evnt.Path);
                Assert.Equal("GET", evnt.HttpMethod);
                Assert.Equal("not really base64", evnt.Body);
                Assert.True(evnt.IsBase64Encoded);

                Assert.Equal(2, evnt.MultiValueQueryStringParameters.Count);
                Assert.Equal(2, evnt.MultiValueQueryStringParameters["query1"].Count);
                Assert.Equal("q1-value1", evnt.MultiValueQueryStringParameters["query1"][0]);
                Assert.Equal("q1-value2", evnt.MultiValueQueryStringParameters["query1"][1]);
                Assert.Equal(2, evnt.MultiValueQueryStringParameters["query2"].Count);
                Assert.Equal("q2-value1", evnt.MultiValueQueryStringParameters["query2"][0]);
                Assert.Equal("q2-value2", evnt.MultiValueQueryStringParameters["query2"][1]);

                Assert.Equal(2, evnt.MultiValueHeaders["head1"].Count);
                Assert.Equal(2, evnt.MultiValueHeaders["head1"].Count);
                Assert.Equal("h1-value1", evnt.MultiValueHeaders["head1"][0]);
                Assert.Equal("h1-value2", evnt.MultiValueHeaders["head1"][1]);
                Assert.Equal(2, evnt.MultiValueHeaders["head2"].Count);
                Assert.Equal("h2-value1", evnt.MultiValueHeaders["head2"][0]);
                Assert.Equal("h2-value2", evnt.MultiValueHeaders["head2"][1]);


                var requestContext = evnt.RequestContext;
                Assert.Equal("arn:aws:elasticloadbalancing:region:123456789012:targetgroup/my-target-group/6d0ecf831eec9f09", requestContext.Elb.TargetGroupArn);
            }
        }


        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void ApplicationLoadBalancerSingleHeaderResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;

            var response = new ApplicationLoadBalancerResponse()
            {
                Headers = new Dictionary<string, string>
                {
                    {"Head1", "h1-value1"},
                    {"Head2", "h2-value1"}
                },
                IsBase64Encoded = true,
                Body = "not really base64",
                StatusCode = 200,
                StatusDescription = "200 OK"
            };

            string serializedJson;
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.Serialize(response, stream);

                stream.Position = 0;
                serializedJson = Encoding.UTF8.GetString(stream.ToArray());
            }

            var root = Newtonsoft.Json.JsonConvert.DeserializeObject(serializedJson) as JObject;

            Assert.Equal("h1-value1", root["headers"]["Head1"]);
            Assert.Equal("h2-value1", root["headers"]["Head2"]);

            Assert.True((bool)root["isBase64Encoded"]);
            Assert.Equal("not really base64", (string)root["body"]);
            Assert.Equal(200, (int)root["statusCode"]);
            Assert.Equal("200 OK", (string)root["statusDescription"]);
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void ApplicationLoadBalancerMultiHeaderResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            var response = new ApplicationLoadBalancerResponse()
            {
                MultiValueHeaders = new Dictionary<string, IList<string>>
                {
                    {"Head1", new List<string>{"h1-value1" } },
                    {"Head2", new List<string>{"h2-value1", "h2-value2" } }
                },
                IsBase64Encoded = true,
                Body = "not really base64",
                StatusCode = 200,
                StatusDescription = "200 OK"
            };

            string serializedJson;
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.Serialize(response, stream);

                stream.Position = 0;
                serializedJson = Encoding.UTF8.GetString(stream.ToArray());
            }

            JObject root = Newtonsoft.Json.JsonConvert.DeserializeObject(serializedJson) as JObject;

            Assert.Single(root["multiValueHeaders"]["Head1"]);
            Assert.Equal("h1-value1", root["multiValueHeaders"]["Head1"].First());

            Assert.Equal(2, root["multiValueHeaders"]["Head2"].Count());
            Assert.Equal("h2-value1", root["multiValueHeaders"]["Head2"].First());
            Assert.Equal("h2-value2", root["multiValueHeaders"]["Head2"].Last());

            Assert.True((bool)root["isBase64Encoded"]);
            Assert.Equal("not really base64", (string)root["body"]);
            Assert.Equal(200, (int)root["statusCode"]);
            Assert.Equal("200 OK", (string)root["statusDescription"]);
        }


        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void LexEvent(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("lex-event.json"))
            {
                var lexEvent = serializer.Deserialize<LexEvent>(fileStream);
                Assert.Equal("1.0", lexEvent.MessageVersion);
                Assert.Equal("FulfillmentCodeHook or DialogCodeHook", lexEvent.InvocationSource);
                Assert.Equal("User ID specified in the POST request to Amazon Lex.", lexEvent.UserId);
                Assert.Equal(2, lexEvent.SessionAttributes.Count);
                Assert.Equal("value1", lexEvent.SessionAttributes["key1"]);
                Assert.Equal("value2", lexEvent.SessionAttributes["key2"]);
                Assert.Equal("bot name", lexEvent.Bot.Name);
                Assert.Equal("bot alias", lexEvent.Bot.Alias);
                Assert.Equal("bot version", lexEvent.Bot.Version);
                Assert.Equal("Text or Voice, based on ContentType request header in runtime API request", lexEvent.OutputDialogMode);
                Assert.Equal("intent-name", lexEvent.CurrentIntent.Name);
                Assert.Equal(2, lexEvent.CurrentIntent.Slots.Count);
                Assert.Equal("value1", lexEvent.CurrentIntent.Slots["slot name1"]);
                Assert.Equal("value2", lexEvent.CurrentIntent.Slots["slot name2"]);
                Assert.Equal("None, Confirmed, or Denied (intent confirmation, if configured)", lexEvent.CurrentIntent.ConfirmationStatus);
                Assert.Equal("Text used to process the request", lexEvent.InputTranscript);
                Assert.Null(lexEvent.CurrentIntent.NluIntentConfidenceScore);

                Assert.Equal(2, lexEvent.RequestAttributes.Count);
                Assert.Equal("value1", lexEvent.RequestAttributes["key1"]);
                Assert.Equal("value2", lexEvent.RequestAttributes["key2"]);

                Assert.Equal(2, lexEvent.CurrentIntent.SlotDetails.Count);
                Assert.Equal("resolved value1", lexEvent.CurrentIntent.SlotDetails["slot name1"].Resolutions[0]["value1"]);
                Assert.Equal("resolved value2", lexEvent.CurrentIntent.SlotDetails["slot name1"].Resolutions[1]["value2"]);
                Assert.Equal("original text", lexEvent.CurrentIntent.SlotDetails["slot name1"].OriginalValue);

                Assert.Equal("resolved value1", lexEvent.CurrentIntent.SlotDetails["slot name2"].Resolutions[0]["value1"]);
                Assert.Equal("resolved value2", lexEvent.CurrentIntent.SlotDetails["slot name2"].Resolutions[1]["value2"]);
                Assert.Equal("original text", lexEvent.CurrentIntent.SlotDetails["slot name2"].OriginalValue);

                Assert.Equal("intent-name", lexEvent.AlternativeIntents[0].Name);
                Assert.Equal(5.5, lexEvent.AlternativeIntents[0].NluIntentConfidenceScore);

                Assert.Equal("intent-name", lexEvent.AlternativeIntents[1].Name);
                Assert.Null(lexEvent.AlternativeIntents[1].NluIntentConfidenceScore);

                Assert.Equal("Name", lexEvent.RecentIntentSummaryView[0].IntentName);
                Assert.Equal("Label", lexEvent.RecentIntentSummaryView[0].CheckpointLabel);
                Assert.Equal("value1", lexEvent.RecentIntentSummaryView[0].Slots["key1"]);
                Assert.Equal("Confirmed", lexEvent.RecentIntentSummaryView[0].ConfirmationStatus);
                Assert.Equal("ElicitIntent", lexEvent.RecentIntentSummaryView[0].DialogActionType);
                Assert.Equal("Fulfilled", lexEvent.RecentIntentSummaryView[0].FulfillmentState);
                Assert.Equal("NextSlot", lexEvent.RecentIntentSummaryView[0].SlotToElicit);

                Assert.Equal("name", lexEvent.ActiveContexts[0].Name);
                Assert.Equal(100, lexEvent.ActiveContexts[0].TimeToLive.TimeToLiveInSeconds);
                Assert.Equal(5, lexEvent.ActiveContexts[0].TimeToLive.TurnsToLive);
                Assert.Equal("value", lexEvent.ActiveContexts[0].Parameters["key"]);

                Assert.Equal("sentiment", lexEvent.SentimentResponse.SentimentLabel);
                Assert.Equal("score", lexEvent.SentimentResponse.SentimentScore);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void LexResponse(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("lex-response.json"))
            {
                var lexResponse = serializer.Deserialize<LexResponse>(fileStream);

                Assert.Equal(2, lexResponse.SessionAttributes.Count);
                Assert.Equal("value1", lexResponse.SessionAttributes["key1"]);
                Assert.Equal("value2", lexResponse.SessionAttributes["key2"]);
                Assert.Equal("ElicitIntent, ElicitSlot, ConfirmIntent, Delegate, or Close", lexResponse.DialogAction.Type);
                Assert.Equal("Fulfilled or Failed", lexResponse.DialogAction.FulfillmentState);
                Assert.Equal("PlainText or SSML", lexResponse.DialogAction.Message.ContentType);
                Assert.Equal("message to convey to the user", lexResponse.DialogAction.Message.Content);
                Assert.Equal("intent-name", lexResponse.DialogAction.IntentName);
                Assert.Equal(3, lexResponse.DialogAction.Slots.Count);
                Assert.Equal("value1", lexResponse.DialogAction.Slots["slot-name1"]);
                Assert.Equal("value2", lexResponse.DialogAction.Slots["slot-name2"]);
                Assert.Equal("value3", lexResponse.DialogAction.Slots["slot-name3"]);
                Assert.Equal("slot-name", lexResponse.DialogAction.SlotToElicit);
                Assert.Equal(3, lexResponse.DialogAction.ResponseCard.Version);
                Assert.Equal("application/vnd.amazonaws.card.generic", lexResponse.DialogAction.ResponseCard.ContentType);
                Assert.Single(lexResponse.DialogAction.ResponseCard.GenericAttachments);
                Assert.Equal("card-title", lexResponse.DialogAction.ResponseCard.GenericAttachments[0].Title);
                Assert.Equal("card-sub-title", lexResponse.DialogAction.ResponseCard.GenericAttachments[0].SubTitle);
                Assert.Equal("URL of the image to be shown", lexResponse.DialogAction.ResponseCard.GenericAttachments[0].ImageUrl);
                Assert.Equal("URL of the attachment to be associated with the card", lexResponse.DialogAction.ResponseCard.GenericAttachments[0].AttachmentLinkUrl);
                Assert.Single(lexResponse.DialogAction.ResponseCard.GenericAttachments[0].Buttons);
                Assert.Equal("button-text", lexResponse.DialogAction.ResponseCard.GenericAttachments[0].Buttons[0].Text);
                Assert.Equal("value sent to server on button click", lexResponse.DialogAction.ResponseCard.GenericAttachments[0].Buttons[0].Value);

                Assert.Equal("name", lexResponse.ActiveContexts[0].Name);
                Assert.Equal(100, lexResponse.ActiveContexts[0].TimeToLive.TimeToLiveInSeconds);
                Assert.Equal(5, lexResponse.ActiveContexts[0].TimeToLive.TurnsToLive);
                Assert.Equal("value", lexResponse.ActiveContexts[0].Parameters["key"]);

                Assert.Equal("Name", lexResponse.RecentIntentSummaryView[0].IntentName);
                Assert.Equal("Label", lexResponse.RecentIntentSummaryView[0].CheckpointLabel);
                Assert.Equal("value1", lexResponse.RecentIntentSummaryView[0].Slots["key1"]);
                Assert.Equal("Confirmed", lexResponse.RecentIntentSummaryView[0].ConfirmationStatus);
                Assert.Equal("ElicitIntent", lexResponse.RecentIntentSummaryView[0].DialogActionType);
                Assert.Equal("Fulfilled", lexResponse.RecentIntentSummaryView[0].FulfillmentState);
                Assert.Equal("NextSlot", lexResponse.RecentIntentSummaryView[0].SlotToElicit);

                MemoryStream ms = new MemoryStream();
                serializer.Serialize<LexResponse>(lexResponse, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("lex-response.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");

            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void LexV2Event(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("lexv2-event.json"))
            {
                var lexV2Event = serializer.Deserialize<LexV2Event>(fileStream);
                Assert.Equal("1.0", lexV2Event.MessageVersion);
                Assert.Equal("DialogCodeHook", lexV2Event.InvocationSource);
                Assert.Equal("DTMF", lexV2Event.InputMode);
                Assert.Equal("ImageResponseCard", lexV2Event.ResponseContentType);
                Assert.Equal("test_session", lexV2Event.SessionId);
                Assert.Equal("test_input_transcript", lexV2Event.InputTranscript);
                Assert.Equal("UFIDGBA6DE", lexV2Event.Bot.Id);
                Assert.Equal("testbot", lexV2Event.Bot.Name);
                Assert.Equal("TSTALIASID", lexV2Event.Bot.AliasId);
                Assert.Equal("en_US", lexV2Event.Bot.LocaleId);
                Assert.Equal("1.0", lexV2Event.Bot.Version);

                Assert.Equal(2, lexV2Event.Interpretations.Count);
                Assert.Equal("TestAction", lexV2Event.Interpretations[0].Intent.Name);
                Assert.Equal(3, lexV2Event.Interpretations[0].Intent.Slots.Count);
                Assert.Equal("List", lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Shape);
                Assert.Equal("Action Value", lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Value.OriginalValue);
                Assert.Equal("Action Value", lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Value.InterpretedValue);
                Assert.Single(lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Value.ResolvedValues);
                Assert.Equal("action value", lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Value.ResolvedValues[0]);
                Assert.Single(lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Values);
                Assert.Equal("Scalar", lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Values[0].Shape);
                Assert.Equal("Action Value", lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Values[0].Value.OriginalValue);
                Assert.Equal("Action Value", lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Values[0].Value.InterpretedValue);
                Assert.Single(lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Values[0].Value.ResolvedValues);
                Assert.Equal("action value", lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Values[0].Value.ResolvedValues[0]);
                Assert.Null(lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Values[0].Values);
                Assert.Null(lexV2Event.Interpretations[0].Intent.Slots["ActionDate"]);
                Assert.Null(lexV2Event.Interpretations[0].Intent.Slots["ActionTime"]);
                Assert.Equal("InProgress", lexV2Event.Interpretations[0].Intent.State);
                Assert.Equal("None", lexV2Event.Interpretations[0].Intent.ConfirmationState);
                Assert.Equal(0.79, lexV2Event.Interpretations[0].NluConfidence);
                Assert.Equal("testsentiment", lexV2Event.Interpretations[0].SentimentResponse.Sentiment);
                Assert.Equal(0.1, lexV2Event.Interpretations[0].SentimentResponse.SentimentScore.Mixed);
                Assert.Equal(0.1, lexV2Event.Interpretations[0].SentimentResponse.SentimentScore.Negative);
                Assert.Equal(0.5, lexV2Event.Interpretations[0].SentimentResponse.SentimentScore.Neutral);
                Assert.Equal(0.9, lexV2Event.Interpretations[0].SentimentResponse.SentimentScore.Positive);
                Assert.Equal("FallbackIntent", lexV2Event.Interpretations[1].Intent.Name);
                Assert.Empty(lexV2Event.Interpretations[1].Intent.Slots);

                Assert.Equal("ActionDate", lexV2Event.ProposedNextState.DialogAction.SlotToElicit);
                Assert.Equal("ConfirmIntent", lexV2Event.ProposedNextState.DialogAction.Type);
                Assert.Equal("NextIntent", lexV2Event.ProposedNextState.Intent.Name);
                Assert.Equal("None", lexV2Event.ProposedNextState.Intent.ConfirmationState);
                Assert.Empty(lexV2Event.ProposedNextState.Intent.Slots);
                Assert.Equal("Waiting", lexV2Event.ProposedNextState.Intent.State);

                Assert.Equal(2, lexV2Event.RequestAttributes.Count);
                Assert.Equal("value1", lexV2Event.RequestAttributes["key1"]);
                Assert.Equal("value2", lexV2Event.RequestAttributes["key2"]);

                Assert.Single(lexV2Event.SessionState.ActiveContexts);
                Assert.Equal(2, lexV2Event.SessionState.ActiveContexts[0].ContextAttributes.Count);
                Assert.Equal("contextattributevalue1", lexV2Event.SessionState.ActiveContexts[0].ContextAttributes["contextattribute1"]);
                Assert.Equal("contextattributevalue2", lexV2Event.SessionState.ActiveContexts[0].ContextAttributes["contextattribute2"]);
                Assert.Equal("testcontext", lexV2Event.SessionState.ActiveContexts[0].Name);
                Assert.Equal(12, lexV2Event.SessionState.ActiveContexts[0].TimeToLive.TimeToLiveInSeconds);
                Assert.Equal(20, lexV2Event.SessionState.ActiveContexts[0].TimeToLive.TurnsToLive);
                Assert.Equal("ElicitSlot", lexV2Event.SessionState.DialogAction.Type);
                Assert.Equal("Date", lexV2Event.SessionState.DialogAction.SlotToElicit);
                Assert.Equal("TestAction", lexV2Event.SessionState.Intent.Name);
                Assert.Equal(3, lexV2Event.SessionState.Intent.Slots.Count);
                Assert.Equal("List", lexV2Event.SessionState.Intent.Slots["ActionType"].Shape);
                Assert.Equal("Action Value", lexV2Event.SessionState.Intent.Slots["ActionType"].Value.OriginalValue);
                Assert.Equal("Action Value", lexV2Event.SessionState.Intent.Slots["ActionType"].Value.InterpretedValue);
                Assert.Single(lexV2Event.SessionState.Intent.Slots["ActionType"].Value.ResolvedValues);
                Assert.Equal("action value", lexV2Event.SessionState.Intent.Slots["ActionType"].Value.ResolvedValues[0]);
                Assert.Single(lexV2Event.SessionState.Intent.Slots["ActionType"].Values);
                Assert.Equal("Scalar", lexV2Event.SessionState.Intent.Slots["ActionType"].Values[0].Shape);
                Assert.Equal("Action Value", lexV2Event.SessionState.Intent.Slots["ActionType"].Values[0].Value.OriginalValue);
                Assert.Equal("Action Value", lexV2Event.SessionState.Intent.Slots["ActionType"].Values[0].Value.InterpretedValue);
                Assert.Single(lexV2Event.SessionState.Intent.Slots["ActionType"].Values[0].Value.ResolvedValues);
                Assert.Equal("action value", lexV2Event.SessionState.Intent.Slots["ActionType"].Values[0].Value.ResolvedValues[0]);
                Assert.Null(lexV2Event.SessionState.Intent.Slots["ActionType"].Values[0].Values);
                Assert.Null(lexV2Event.SessionState.Intent.Slots["ActionDate"]);
                Assert.Null(lexV2Event.SessionState.Intent.Slots["ActionTime"]);
                Assert.Equal("InProgress", lexV2Event.SessionState.Intent.State);
                Assert.Equal("None", lexV2Event.SessionState.Intent.ConfirmationState);
                Assert.Equal("85f22c97-b5d3-4a74-9e3d-95446768ecaa", lexV2Event.SessionState.OriginatingRequestId);
                Assert.Single(lexV2Event.SessionState.RuntimeHints.SlotHints);
                Assert.Single(lexV2Event.SessionState.RuntimeHints.SlotHints["hint1"]);
                Assert.Equal(2, lexV2Event.SessionState.RuntimeHints.SlotHints["hint1"]["detail1"].RuntimeHintValues.Count);
                Assert.Equal("hintvalue1_1", lexV2Event.SessionState.RuntimeHints.SlotHints["hint1"]["detail1"].RuntimeHintValues[0].Phrase);
                Assert.Equal("hintvalue1_2", lexV2Event.SessionState.RuntimeHints.SlotHints["hint1"]["detail1"].RuntimeHintValues[1].Phrase);
                Assert.Equal(2, lexV2Event.SessionState.SessionAttributes.Count);
                Assert.Equal("sessionvalue1", lexV2Event.SessionState.SessionAttributes["sessionattribute1"]);
                Assert.Equal("sessionvalue2", lexV2Event.SessionState.SessionAttributes["sessionattribute2"]);

                Assert.Single(lexV2Event.Transcriptions);
                Assert.Equal("testtranscription", lexV2Event.Transcriptions[0].Transcription);
                Assert.Equal(0.8, lexV2Event.Transcriptions[0].TranscriptionConfidence);
                Assert.Equal("TestAction", lexV2Event.Transcriptions[0].ResolvedContext.Intent);
                Assert.Single(lexV2Event.Transcriptions[0].ResolvedSlots);
                Assert.Equal("List", lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Shape);
                Assert.Equal("Action Value", lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Value.OriginalValue);
                Assert.Equal("Action Value", lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Value.InterpretedValue);
                Assert.Single(lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Value.ResolvedValues);
                Assert.Equal("action value", lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Value.ResolvedValues[0]);
                Assert.Single(lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Values);
                Assert.Equal("Scalar", lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Values[0].Shape);
                Assert.Equal("Action Value", lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Values[0].Value.OriginalValue);
                Assert.Equal("Action Value", lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Values[0].Value.InterpretedValue);
                Assert.Single(lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Values[0].Value.ResolvedValues);
                Assert.Equal("action value", lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Values[0].Value.ResolvedValues[0]);
                Assert.Null(lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Values[0].Values);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void LexV2Response(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("lexv2-response.json"))
            {
                var lexV2Response = serializer.Deserialize<LexV2Response>(fileStream);

                Assert.Single(lexV2Response.Messages);
                Assert.Equal("Test Content", lexV2Response.Messages[0].Content);
                Assert.Equal("ImageResponseCard", lexV2Response.Messages[0].ContentType);
                Assert.Single(lexV2Response.Messages[0].ImageResponseCard.Buttons);
                Assert.Equal("Take Action", lexV2Response.Messages[0].ImageResponseCard.Buttons[0].Text);
                Assert.Equal("takeaction", lexV2Response.Messages[0].ImageResponseCard.Buttons[0].Value);
                Assert.Equal("http://somedomain.com/testimage.png", lexV2Response.Messages[0].ImageResponseCard.ImageUrl);
                Assert.Equal("Click button to take action", lexV2Response.Messages[0].ImageResponseCard.Subtitle);
                Assert.Equal("Take Action", lexV2Response.Messages[0].ImageResponseCard.Title);
                Assert.Single(lexV2Response.SessionState.ActiveContexts);
                Assert.Equal(2, lexV2Response.SessionState.ActiveContexts[0].ContextAttributes.Count);
                Assert.Equal("contextattributevalue1", lexV2Response.SessionState.ActiveContexts[0].ContextAttributes["contextattribute1"]);
                Assert.Equal("contextattributevalue2", lexV2Response.SessionState.ActiveContexts[0].ContextAttributes["contextattribute2"]);
                Assert.Equal("testcontext", lexV2Response.SessionState.ActiveContexts[0].Name);
                Assert.Equal(12, lexV2Response.SessionState.ActiveContexts[0].TimeToLive.TimeToLiveInSeconds);
                Assert.Equal(20, lexV2Response.SessionState.ActiveContexts[0].TimeToLive.TurnsToLive);
                Assert.Equal("ElicitSlot", lexV2Response.SessionState.DialogAction.Type);
                Assert.Equal("Date", lexV2Response.SessionState.DialogAction.SlotToElicit);
                Assert.Equal("TestAction", lexV2Response.SessionState.Intent.Name);
                Assert.Equal(3, lexV2Response.SessionState.Intent.Slots.Count);
                Assert.Equal("List", lexV2Response.SessionState.Intent.Slots["ActionType"].Shape);
                Assert.Equal("Action Value", lexV2Response.SessionState.Intent.Slots["ActionType"].Value.OriginalValue);
                Assert.Equal("Action Value", lexV2Response.SessionState.Intent.Slots["ActionType"].Value.InterpretedValue);
                Assert.Single(lexV2Response.SessionState.Intent.Slots["ActionType"].Value.ResolvedValues);
                Assert.Equal("action value", lexV2Response.SessionState.Intent.Slots["ActionType"].Value.ResolvedValues[0]);
                Assert.Single(lexV2Response.SessionState.Intent.Slots["ActionType"].Values);
                Assert.Equal("Scalar", lexV2Response.SessionState.Intent.Slots["ActionType"].Values[0].Shape);
                Assert.Equal("Action Value", lexV2Response.SessionState.Intent.Slots["ActionType"].Values[0].Value.OriginalValue);
                Assert.Equal("Action Value", lexV2Response.SessionState.Intent.Slots["ActionType"].Values[0].Value.InterpretedValue);
                Assert.Single(lexV2Response.SessionState.Intent.Slots["ActionType"].Values[0].Value.ResolvedValues);
                Assert.Equal("action value", lexV2Response.SessionState.Intent.Slots["ActionType"].Values[0].Value.ResolvedValues[0]);
                Assert.Null(lexV2Response.SessionState.Intent.Slots["ActionType"].Values[0].Values);
                Assert.Null(lexV2Response.SessionState.Intent.Slots["ActionDate"]);
                Assert.Null(lexV2Response.SessionState.Intent.Slots["ActionTime"]);
                Assert.Equal("InProgress", lexV2Response.SessionState.Intent.State);
                Assert.Equal("None", lexV2Response.SessionState.Intent.ConfirmationState);
                Assert.Equal("85f22c97-b5d3-4a74-9e3d-95446768ecaa", lexV2Response.SessionState.OriginatingRequestId);
                Assert.Single(lexV2Response.SessionState.RuntimeHints.SlotHints);
                Assert.Single(lexV2Response.SessionState.RuntimeHints.SlotHints["hint1"]);
                Assert.Equal(2, lexV2Response.SessionState.RuntimeHints.SlotHints["hint1"]["detail1"].RuntimeHintValues.Count);
                Assert.Equal("hintvalue1_1", lexV2Response.SessionState.RuntimeHints.SlotHints["hint1"]["detail1"].RuntimeHintValues[0].Phrase);
                Assert.Equal("hintvalue1_2", lexV2Response.SessionState.RuntimeHints.SlotHints["hint1"]["detail1"].RuntimeHintValues[1].Phrase);
                Assert.Equal(2, lexV2Response.SessionState.SessionAttributes.Count);
                Assert.Equal("sessionvalue1", lexV2Response.SessionState.SessionAttributes["sessionattribute1"]);
                Assert.Equal("sessionvalue2", lexV2Response.SessionState.SessionAttributes["sessionattribute2"]);
                Assert.Equal(2, lexV2Response.RequestAttributes.Count);
                Assert.Equal("value1", lexV2Response.RequestAttributes["key1"]);
                Assert.Equal("value2", lexV2Response.RequestAttributes["key2"]);

                var ms = new MemoryStream();
                serializer.Serialize<LexV2Response>(lexV2Response, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("lexv2-response.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void KinesisFirehoseEvent(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-firehose-event.json"))
            {
                var kinesisEvent = serializer.Deserialize<KinesisFirehoseEvent>(fileStream);
                Assert.Equal("00540a87-5050-496a-84e4-e7d92bbaf5e2", kinesisEvent.InvocationId);
                Assert.Equal("arn:aws:firehose:us-east-1:AAAAAAAAAAAA:deliverystream/lambda-test", kinesisEvent.DeliveryStreamArn);
                Assert.Equal("us-east-1", kinesisEvent.Region);
                Assert.Single(kinesisEvent.Records);

                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisEvent.Records[0].RecordId);
                Assert.Equal("aGVsbG8gd29ybGQ=", kinesisEvent.Records[0].Base64EncodedData);
                Assert.Equal(1493276938812, kinesisEvent.Records[0].ApproximateArrivalEpoch);

            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void KinesisFirehoseResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-firehose-response.json"))
            {
                var kinesisResponse = serializer.Deserialize<KinesisFirehoseResponse>(fileStream);

                Assert.Single(kinesisResponse.Records);
                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisResponse.Records[0].RecordId);
                Assert.Equal(KinesisFirehoseResponse.TRANSFORMED_STATE_OK, kinesisResponse.Records[0].Result);
                Assert.Equal("SEVMTE8gV09STEQ=", kinesisResponse.Records[0].Base64EncodedData);
                Assert.Equal("iamValue1", kinesisResponse.Records[0].Metadata.PartitionKeys["iamKey1"]);
                Assert.Equal("iamValue2", kinesisResponse.Records[0].Metadata.PartitionKeys["iamKey2"]);


                MemoryStream ms = new MemoryStream();
                serializer.Serialize<KinesisFirehoseResponse>(kinesisResponse, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("kinesis-firehose-response.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");

            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void KinesisAnalyticsOutputDeliveryEvent(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-analytics-outputdelivery-event.json"))
            {
                var kinesisAnalyticsEvent = serializer.Deserialize<KinesisAnalyticsOutputDeliveryEvent>(fileStream);
                Assert.Equal("00540a87-5050-496a-84e4-e7d92bbaf5e2", kinesisAnalyticsEvent.InvocationId);
                Assert.Equal("arn:aws:kinesisanalytics:us-east-1:12345678911:application/lambda-test", kinesisAnalyticsEvent.ApplicationArn);

                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisAnalyticsEvent.Records[0].RecordId);
                Assert.Equal("aGVsbG8gd29ybGQ=", kinesisAnalyticsEvent.Records[0].Base64EncodedData);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void KinesisAnalyticsOutputDeliveryResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-analytics-outputdelivery-response.json"))
            {
                var kinesisAnalyticsResponse = serializer.Deserialize<KinesisAnalyticsOutputDeliveryResponse>(fileStream);

                Assert.Single(kinesisAnalyticsResponse.Records);
                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisAnalyticsResponse.Records[0].RecordId);
                Assert.Equal(KinesisAnalyticsOutputDeliveryResponse.OK, kinesisAnalyticsResponse.Records[0].Result);

                MemoryStream ms = new MemoryStream();
                serializer.Serialize<KinesisAnalyticsOutputDeliveryResponse>(kinesisAnalyticsResponse, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("kinesis-analytics-outputdelivery-response.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void KinesisAnalyticsInputProcessingEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-analytics-inputpreprocessing-event.json"))
            {
                var kinesisAnalyticsEvent = serializer.Deserialize<KinesisAnalyticsStreamsInputPreprocessingEvent>(fileStream);
                Assert.Equal("00540a87-5050-496a-84e4-e7d92bbaf5e2", kinesisAnalyticsEvent.InvocationId);
                Assert.Equal("arn:aws:kinesis:us-east-1:AAAAAAAAAAAA:stream/lambda-test", kinesisAnalyticsEvent.StreamArn);
                Assert.Equal("arn:aws:kinesisanalytics:us-east-1:12345678911:application/lambda-test", kinesisAnalyticsEvent.ApplicationArn);
                Assert.Single(kinesisAnalyticsEvent.Records);

                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisAnalyticsEvent.Records[0].RecordId);
                Assert.Equal("aGVsbG8gd29ybGQ=", kinesisAnalyticsEvent.Records[0].Base64EncodedData);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void KinesisAnalyticsInputProcessingResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-analytics-inputpreprocessing-response.json"))
            {
                var kinesisAnalyticsResponse = serializer.Deserialize<KinesisAnalyticsInputPreprocessingResponse>(fileStream);

                Assert.Single(kinesisAnalyticsResponse.Records);
                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisAnalyticsResponse.Records[0].RecordId);
                Assert.Equal(KinesisAnalyticsInputPreprocessingResponse.OK, kinesisAnalyticsResponse.Records[0].Result);
                Assert.Equal("SEVMTE8gV09STEQ=", kinesisAnalyticsResponse.Records[0].Base64EncodedData);



                MemoryStream ms = new MemoryStream();
                serializer.Serialize<KinesisAnalyticsInputPreprocessingResponse>(kinesisAnalyticsResponse, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("kinesis-analytics-inputpreprocessing-response.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");

            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void KinesisAnalyticsStreamsInputProcessingEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-analytics-streamsinputpreprocessing-event.json"))
            {
                var kinesisAnalyticsEvent = serializer.Deserialize<KinesisAnalyticsStreamsInputPreprocessingEvent>(fileStream);
                Assert.Equal("00540a87-5050-496a-84e4-e7d92bbaf5e2", kinesisAnalyticsEvent.InvocationId);
                Assert.Equal("arn:aws:kinesis:us-east-1:AAAAAAAAAAAA:stream/lambda-test", kinesisAnalyticsEvent.StreamArn);
                Assert.Equal("arn:aws:kinesisanalytics:us-east-1:12345678911:application/lambda-test", kinesisAnalyticsEvent.ApplicationArn);
                Assert.Single(kinesisAnalyticsEvent.Records);

                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisAnalyticsEvent.Records[0].RecordId);
                Assert.Equal("aGVsbG8gd29ybGQ=", kinesisAnalyticsEvent.Records[0].Base64EncodedData);

                Assert.NotNull(kinesisAnalyticsEvent.Records[0].RecordMetadata);
                Assert.Equal("shardId-000000000003", kinesisAnalyticsEvent.Records[0].RecordMetadata.ShardId);
                Assert.Equal("7400791606", kinesisAnalyticsEvent.Records[0].RecordMetadata.PartitionKey);
                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisAnalyticsEvent.Records[0].RecordMetadata.SequenceNumber);
                Assert.Equal(1520280173, kinesisAnalyticsEvent.Records[0].RecordMetadata.ApproximateArrivalEpoch);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void KinesisAnalyticsFirehoseInputProcessingEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-analytics-firehoseinputpreprocessing-event.json"))
            {
                var kinesisAnalyticsEvent = serializer.Deserialize<KinesisAnalyticsFirehoseInputPreprocessingEvent>(fileStream);
                Assert.Equal("00540a87-5050-496a-84e4-e7d92bbaf5e2", kinesisAnalyticsEvent.InvocationId);
                Assert.Equal("arn:aws:firehose:us-east-1:AAAAAAAAAAAA:deliverystream/lambda-test", kinesisAnalyticsEvent.StreamArn);
                Assert.Equal("arn:aws:kinesisanalytics:us-east-1:12345678911:application/lambda-test", kinesisAnalyticsEvent.ApplicationArn);
                Assert.Single(kinesisAnalyticsEvent.Records);

                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisAnalyticsEvent.Records[0].RecordId);
                Assert.Equal("aGVsbG8gd29ybGQ=", kinesisAnalyticsEvent.Records[0].Base64EncodedData);

                Assert.NotNull(kinesisAnalyticsEvent.Records[0].RecordMetadata);
                Assert.Equal(1520280173, kinesisAnalyticsEvent.Records[0].RecordMetadata.ApproximateArrivalEpoch);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CloudWatchLogEvent(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("logs-event.json"))
            {
                var evnt = serializer.Deserialize<CloudWatchLogsEvent>(fileStream);

                Assert.NotNull(evnt.Awslogs);

                var data = evnt.Awslogs.DecodeData();
                Assert.NotNull(data);

                var jobject = JsonConvert.DeserializeObject(data) as JObject;
                Assert.Equal("DATA_MESSAGE", jobject["messageType"].ToString());

            }
        }

        private string MemoryStreamToBase64String(MemoryStream ms)
        {
            var data = ms.ToArray();
            return Convert.ToBase64String(data);
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void BatchJobStateChangeEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("batch-job-state-change-event.json"))
            {
                var jobStateChangeEvent = serializer.Deserialize<BatchJobStateChangeEvent>(fileStream);

                Assert.Equal("0", jobStateChangeEvent.Version);
                Assert.Equal("c8f9c4b5-76e5-d76a-f980-7011e206042b", jobStateChangeEvent.Id);
                Assert.Equal("Batch Job State Change", jobStateChangeEvent.DetailType);
                Assert.Equal("aws.batch", jobStateChangeEvent.Source);
                Assert.Equal("aws_account_id", jobStateChangeEvent.Account);
                Assert.Equal(DateTime.Parse("2017-10-23T17:56:03Z").ToUniversalTime(), jobStateChangeEvent.Time.ToUniversalTime());
                Assert.Equal("us-east-1", jobStateChangeEvent.Region);
                Assert.Single(jobStateChangeEvent.Resources);
                Assert.Equal("arn:aws:batch:us-east-1:aws_account_id:job/4c7599ae-0a82-49aa-ba5a-4727fcce14a8", jobStateChangeEvent.Resources[0]);
                Assert.IsType<Job>(jobStateChangeEvent.Detail);
                Assert.Equal("event-test", jobStateChangeEvent.Detail.JobName);
                Assert.Equal("4c7599ae-0a82-49aa-ba5a-4727fcce14a8", jobStateChangeEvent.Detail.JobId);
                Assert.Equal("arn:aws:batch:us-east-1:aws_account_id:job-queue/HighPriority", jobStateChangeEvent.Detail.JobQueue);
                Assert.Equal("RUNNABLE", jobStateChangeEvent.Detail.Status);
                Assert.Empty(jobStateChangeEvent.Detail.Attempts);
                Assert.Equal(1508781340401, jobStateChangeEvent.Detail.CreatedAt);
                Assert.Equal(1, jobStateChangeEvent.Detail.RetryStrategy.Attempts);
                Assert.Single(jobStateChangeEvent.Detail.RetryStrategy.EvaluateOnExit);
                Assert.Equal("EXIT", jobStateChangeEvent.Detail.RetryStrategy.EvaluateOnExit[0].Action);
                Assert.Equal("*", jobStateChangeEvent.Detail.RetryStrategy.EvaluateOnExit[0].OnExitCode);
                Assert.Equal("*", jobStateChangeEvent.Detail.RetryStrategy.EvaluateOnExit[0].OnReason);
                Assert.Equal("*", jobStateChangeEvent.Detail.RetryStrategy.EvaluateOnExit[0].OnStatusReason);
                Assert.Empty(jobStateChangeEvent.Detail.DependsOn);
                Assert.Equal("arn:aws:batch:us-east-1:aws_account_id:job-definition/first-run-job-definition:1", jobStateChangeEvent.Detail.JobDefinition);
                Assert.Single(jobStateChangeEvent.Detail.Parameters);
                Assert.Equal("abc", jobStateChangeEvent.Detail.Parameters["test"]);
                Assert.Equal("busybox", jobStateChangeEvent.Detail.Container.Image);
                Assert.NotNull(jobStateChangeEvent.Detail.Container.ResourceRequirements);
                Assert.Equal(2, jobStateChangeEvent.Detail.Container.ResourceRequirements.Count);
                Assert.Equal("MEMORY", jobStateChangeEvent.Detail.Container.ResourceRequirements[0].Type);
                Assert.Equal("2000", jobStateChangeEvent.Detail.Container.ResourceRequirements[0].Value);
                Assert.Equal("VCPU", jobStateChangeEvent.Detail.Container.ResourceRequirements[1].Type);
                Assert.Equal("2", jobStateChangeEvent.Detail.Container.ResourceRequirements[1].Value);
                Assert.Equal(2, jobStateChangeEvent.Detail.Container.Vcpus);
                Assert.Equal(2000, jobStateChangeEvent.Detail.Container.Memory);
                Assert.Equal(2, jobStateChangeEvent.Detail.Container.Command.Count);
                Assert.Equal("echo", jobStateChangeEvent.Detail.Container.Command[0]);
                Assert.Equal("'hello world'", jobStateChangeEvent.Detail.Container.Command[1]);
                Assert.Equal(2, jobStateChangeEvent.Detail.Container.Volumes.Count);
                Assert.Equal("myhostsource", jobStateChangeEvent.Detail.Container.Volumes[0].Name);
                Assert.Equal("/data", jobStateChangeEvent.Detail.Container.Volumes[0].Host.SourcePath);
                Assert.Equal("efs", jobStateChangeEvent.Detail.Container.Volumes[1].Name);
                Assert.Equal("fsap-XXXXXXXXXXXXXXXXX", jobStateChangeEvent.Detail.Container.Volumes[1].EfsVolumeConfiguration.AuthorizationConfig.AccessPointId);
                Assert.Equal("ENABLED", jobStateChangeEvent.Detail.Container.Volumes[1].EfsVolumeConfiguration.AuthorizationConfig.Iam);
                Assert.Equal("fs-XXXXXXXXX", jobStateChangeEvent.Detail.Container.Volumes[1].EfsVolumeConfiguration.FileSystemId);
                Assert.Equal("/", jobStateChangeEvent.Detail.Container.Volumes[1].EfsVolumeConfiguration.RootDirectory);
                Assert.Equal("ENABLED", jobStateChangeEvent.Detail.Container.Volumes[1].EfsVolumeConfiguration.TransitEncryption);
                Assert.Equal(12345, jobStateChangeEvent.Detail.Container.Volumes[1].EfsVolumeConfiguration.TransitEncryptionPort);
                Assert.NotNull(jobStateChangeEvent.Detail.Container.Environment);
                Assert.Single(jobStateChangeEvent.Detail.Container.Environment);
                Assert.Equal("MANAGED_BY_AWS", jobStateChangeEvent.Detail.Container.Environment[0].Name);
                Assert.Equal("STARTED_BY_STEP_FUNCTIONS", jobStateChangeEvent.Detail.Container.Environment[0].Value);
                Assert.Equal(2, jobStateChangeEvent.Detail.Container.MountPoints.Count);
                Assert.Equal("/data", jobStateChangeEvent.Detail.Container.MountPoints[0].ContainerPath);
                Assert.True(jobStateChangeEvent.Detail.Container.MountPoints[0].ReadOnly);
                Assert.Equal("myhostsource", jobStateChangeEvent.Detail.Container.MountPoints[0].SourceVolume);
                Assert.Equal("/mount/efs", jobStateChangeEvent.Detail.Container.MountPoints[1].ContainerPath);
                Assert.Equal("efs", jobStateChangeEvent.Detail.Container.MountPoints[1].SourceVolume);
                Assert.Single(jobStateChangeEvent.Detail.Container.Ulimits);
                Assert.Equal(2048, jobStateChangeEvent.Detail.Container.Ulimits[0].HardLimit);
                Assert.Equal("nofile", jobStateChangeEvent.Detail.Container.Ulimits[0].Name);
                Assert.Equal(2048, jobStateChangeEvent.Detail.Container.Ulimits[0].SoftLimit);
                Assert.NotNull(jobStateChangeEvent.Detail.Container.LinuxParameters);
                Assert.Single(jobStateChangeEvent.Detail.Container.LinuxParameters.Devices);
                Assert.Equal("/dev/sda", jobStateChangeEvent.Detail.Container.LinuxParameters.Devices[0].ContainerPath);
                Assert.Equal("/dev/xvdc", jobStateChangeEvent.Detail.Container.LinuxParameters.Devices[0].HostPath);
                Assert.Single(jobStateChangeEvent.Detail.Container.LinuxParameters.Devices[0].Permissions);
                Assert.Equal("MKNOD", jobStateChangeEvent.Detail.Container.LinuxParameters.Devices[0].Permissions[0]);
                Assert.True(jobStateChangeEvent.Detail.Container.LinuxParameters.InitProcessEnabled);
                Assert.Equal(64, jobStateChangeEvent.Detail.Container.LinuxParameters.SharedMemorySize);
                Assert.Equal(1024, jobStateChangeEvent.Detail.Container.LinuxParameters.MaxSwap);
                Assert.Equal(55, jobStateChangeEvent.Detail.Container.LinuxParameters.Swappiness);
                Assert.Single(jobStateChangeEvent.Detail.Container.LinuxParameters.Tmpfs);
                Assert.Equal("/run", jobStateChangeEvent.Detail.Container.LinuxParameters.Tmpfs[0].ContainerPath);
                Assert.Equal(65536, jobStateChangeEvent.Detail.Container.LinuxParameters.Tmpfs[0].Size);
                Assert.Equal(2, jobStateChangeEvent.Detail.Container.LinuxParameters.Tmpfs[0].MountOptions.Count);
                Assert.Equal("noexec", jobStateChangeEvent.Detail.Container.LinuxParameters.Tmpfs[0].MountOptions[0]);
                Assert.Equal("nosuid", jobStateChangeEvent.Detail.Container.LinuxParameters.Tmpfs[0].MountOptions[1]);
                Assert.NotNull(jobStateChangeEvent.Detail.Container.LogConfiguration);
                Assert.Equal("json-file", jobStateChangeEvent.Detail.Container.LogConfiguration.LogDriver);
                Assert.Equal(2, jobStateChangeEvent.Detail.Container.LogConfiguration.Options.Count);
                Assert.Equal("10m", jobStateChangeEvent.Detail.Container.LogConfiguration.Options["max-size"]);
                Assert.Equal("3", jobStateChangeEvent.Detail.Container.LogConfiguration.Options["max-file"]);
                Assert.Single(jobStateChangeEvent.Detail.Container.LogConfiguration.SecretOptions);
                Assert.Equal("apikey", jobStateChangeEvent.Detail.Container.LogConfiguration.SecretOptions[0].Name);
                Assert.Equal("ddApiKey", jobStateChangeEvent.Detail.Container.LogConfiguration.SecretOptions[0].ValueFrom);
                Assert.Single(jobStateChangeEvent.Detail.Container.Secrets);
                Assert.Equal("DATABASE_PASSWORD", jobStateChangeEvent.Detail.Container.Secrets[0].Name);
                Assert.Equal("arn:aws:ssm:us-east-1:awsExampleAccountID:parameter/awsExampleParameter", jobStateChangeEvent.Detail.Container.Secrets[0].ValueFrom);
                Assert.NotNull(jobStateChangeEvent.Detail.Container.NetworkConfiguration);
                Assert.Equal("ENABLED", jobStateChangeEvent.Detail.Container.NetworkConfiguration.AssignPublicIp);
                Assert.NotNull(jobStateChangeEvent.Detail.Container.FargatePlatformConfiguration);
                Assert.Equal("LATEST", jobStateChangeEvent.Detail.Container.FargatePlatformConfiguration.PlatformVersion);
                Assert.NotNull(jobStateChangeEvent.Detail.NodeProperties);
                Assert.Equal(0, jobStateChangeEvent.Detail.NodeProperties.MainNode);
                Assert.Equal(0, jobStateChangeEvent.Detail.NodeProperties.NumNodes);
                Assert.Single(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties);
                Assert.Equal("0:1", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].TargetNodes);
                Assert.NotNull(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container);
                Assert.Equal("busybox", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Image);
                Assert.Equal(2, jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.ResourceRequirements.Count);
                Assert.Equal("MEMORY", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.ResourceRequirements[0].Type);
                Assert.Equal("2000", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.ResourceRequirements[0].Value);
                Assert.Equal("VCPU", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.ResourceRequirements[1].Type);
                Assert.Equal("2", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.ResourceRequirements[1].Value);
                Assert.Equal(2, jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Vcpus);
                Assert.Equal(2000, jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Memory);
                Assert.Equal(2, jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Command.Count);
                Assert.Equal("echo", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Command[0]);
                Assert.Equal("'hello world'", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Command[1]);
                Assert.Equal(2, jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes.Count);
                Assert.Equal("myhostsource", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[0].Name);
                Assert.Equal("/data", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[0].Host.SourcePath);
                Assert.Equal("efs", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[1].Name);
                Assert.Equal("fsap-XXXXXXXXXXXXXXXXX", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[1].EfsVolumeConfiguration.AuthorizationConfig.AccessPointId);
                Assert.Equal("ENABLED", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[1].EfsVolumeConfiguration.AuthorizationConfig.Iam);
                Assert.Equal("fs-XXXXXXXXX", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[1].EfsVolumeConfiguration.FileSystemId);
                Assert.Equal("/", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[1].EfsVolumeConfiguration.RootDirectory);
                Assert.Equal("ENABLED", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[1].EfsVolumeConfiguration.TransitEncryption);
                Assert.Equal(12345, jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[1].EfsVolumeConfiguration.TransitEncryptionPort);
                Assert.Single(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Environment);
                Assert.Equal("MANAGED_BY_AWS", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Environment[0].Name);
                Assert.Equal("STARTED_BY_STEP_FUNCTIONS", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Environment[0].Value);
                Assert.Equal(2, jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.MountPoints.Count);
                Assert.Equal("/data", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.MountPoints[0].ContainerPath);
                Assert.True(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.MountPoints[0].ReadOnly);
                Assert.Equal("myhostsource", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.MountPoints[0].SourceVolume);
                Assert.Equal("/mount/efs", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.MountPoints[1].ContainerPath);
                Assert.Equal("efs", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.MountPoints[1].SourceVolume);
                Assert.Single(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Ulimits);
                Assert.Equal(2048, jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Ulimits[0].HardLimit);
                Assert.Equal("nofile", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Ulimits[0].Name);
                Assert.Equal(2048, jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Ulimits[0].SoftLimit);
                Assert.Equal("arn:aws:iam::awsExampleAccountID:role/awsExampleRoleName", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.ExecutionRoleArn);
                Assert.Equal("p3.2xlarge", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.InstanceType);
                Assert.Equal("testuser", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.User);
                Assert.Equal("arn:aws:iam::awsExampleAccountID:role/awsExampleRoleName", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.JobRoleArn);
                Assert.Single(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Devices);
                Assert.Equal("/dev/xvdc", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Devices[0].HostPath);
                Assert.Equal("/dev/sda", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Devices[0].ContainerPath);
                Assert.Single(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Devices[0].Permissions);
                Assert.Equal("MKNOD", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Devices[0].Permissions[0]);
                Assert.True(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.InitProcessEnabled);
                Assert.Equal(64, jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.SharedMemorySize);
                Assert.Equal(1024, jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.MaxSwap);
                Assert.Equal(55, jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Swappiness);
                Assert.Single(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Tmpfs);
                Assert.Equal("/run", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Tmpfs[0].ContainerPath);
                Assert.Equal(65536, jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Tmpfs[0].Size);
                Assert.Equal(2, jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Tmpfs[0].MountOptions.Count);
                Assert.Equal("noexec", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Tmpfs[0].MountOptions[0]);
                Assert.Equal("nosuid", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Tmpfs[0].MountOptions[1]);
                Assert.Equal("awslogs", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LogConfiguration.LogDriver);
                Assert.Equal("awslogs-wordpress", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LogConfiguration.Options["awslogs-group"]);
                Assert.Equal("awslogs-example", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LogConfiguration.Options["awslogs-stream-prefix"]);
                Assert.Single(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LogConfiguration.SecretOptions);
                Assert.Equal("apikey", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LogConfiguration.SecretOptions[0].Name);
                Assert.Equal("ddApiKey", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LogConfiguration.SecretOptions[0].ValueFrom);
                Assert.Single(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Secrets);
                Assert.Equal("DATABASE_PASSWORD", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Secrets[0].Name);
                Assert.Equal("arn:aws:ssm:us-east-1:awsExampleAccountID:parameter/awsExampleParameter", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Secrets[0].ValueFrom);
                Assert.Equal("DISABLED", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.NetworkConfiguration.AssignPublicIp);
                Assert.Equal("LATEST", jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.FargatePlatformConfiguration.PlatformVersion);
                Assert.True(jobStateChangeEvent.Detail.PropagateTags);
                Assert.Equal(90, jobStateChangeEvent.Detail.Timeout.AttemptDurationSeconds);
                Assert.Equal(3, jobStateChangeEvent.Detail.Tags.Count);
                Assert.Equal("Batch", jobStateChangeEvent.Detail.Tags["Service"]);
                Assert.Equal("JobDefinitionTag", jobStateChangeEvent.Detail.Tags["Name"]);
                Assert.Equal("MergeTag", jobStateChangeEvent.Detail.Tags["Expected"]);
                Assert.Single(jobStateChangeEvent.Detail.PlatformCapabilities);
                Assert.Equal("FARGATE", jobStateChangeEvent.Detail.PlatformCapabilities[0]);

                Handle(jobStateChangeEvent);
            }
        }

        private void Handle(BatchJobStateChangeEvent jobStateChangeEvent)
        {
            Console.WriteLine($"[{jobStateChangeEvent.Source} {jobStateChangeEvent.Time}] {jobStateChangeEvent.DetailType}");
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void ScheduledEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("scheduled-event.json"))
            {
                var scheduledEvent = serializer.Deserialize<ScheduledEvent>(fileStream);
                Assert.Equal("0", scheduledEvent.Version);
                Assert.Equal("cdc73f9d-aea9-11e3-9d5a-835b769c0d9c", scheduledEvent.Id);
                Assert.Equal("Scheduled Event", scheduledEvent.DetailType);
                Assert.Equal("aws.events", scheduledEvent.Source);
                Assert.Equal("123456789012", scheduledEvent.Account);
                Assert.Equal(DateTime.Parse("1970-01-01T00:00:00Z").ToUniversalTime(), scheduledEvent.Time.ToUniversalTime());
                Assert.Equal("us-east-1", scheduledEvent.Region);
                Assert.Single(scheduledEvent.Resources);
                Assert.Equal("arn:aws:events:us-east-1:123456789012:rule/my-schedule", scheduledEvent.Resources[0]);
                Assert.IsType<Detail>(scheduledEvent.Detail);

                Handle(scheduledEvent);
            }
        }

        private void Handle(ScheduledEvent scheduledEvent)
        {
            Console.WriteLine($"[{scheduledEvent.Source} {scheduledEvent.Time}] {scheduledEvent.DetailType}");
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void ECSContainerInstanceStateChangeEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("ecs-container-state-change-event.json"))
            {
                var ecsEvent = serializer.Deserialize<ECSContainerInstanceStateChangeEvent>(fileStream);

                Assert.Equal("0", ecsEvent.Version);
                Assert.Equal("8952ba83-7be2-4ab5-9c32-6687532d15a2", ecsEvent.Id);
                Assert.Equal("ECS Container Instance State Change", ecsEvent.DetailType);
                Assert.Equal("aws.ecs", ecsEvent.Source);
                Assert.Equal("111122223333", ecsEvent.Account);
                Assert.Equal(DateTime.Parse("2016-12-06T16:41:06Z").ToUniversalTime(), ecsEvent.Time.ToUniversalTime());
                Assert.Equal("us-east-1", ecsEvent.Region);
                Assert.Single(ecsEvent.Resources);
                Assert.Equal("arn:aws:ecs:us-east-1:111122223333:container-instance/b54a2a04-046f-4331-9d74-3f6d7f6ca315", ecsEvent.Resources[0]);
                Assert.IsType<ContainerInstance>(ecsEvent.Detail);
                Assert.True(ecsEvent.Detail.AgentConnected);
                Assert.Equal(14, ecsEvent.Detail.Attributes.Count);
                Assert.Equal("com.amazonaws.ecs.capability.logging-driver.syslog", ecsEvent.Detail.Attributes[0].Name);
                Assert.Equal("arn:aws:ecs:us-east-1:111122223333:cluster/default", ecsEvent.Detail.ClusterArn);
                Assert.Equal("arn:aws:ecs:us-east-1:111122223333:container-instance/b54a2a04-046f-4331-9d74-3f6d7f6ca315", ecsEvent.Detail.ContainerInstanceArn);
                Assert.Equal("i-f3a8506b", ecsEvent.Detail.Ec2InstanceId);
                Assert.Equal(4, ecsEvent.Detail.RegisteredResources.Count);
                Assert.Equal("CPU", ecsEvent.Detail.RegisteredResources[0].Name);
                Assert.Equal("INTEGER", ecsEvent.Detail.RegisteredResources[0].Type);
                Assert.Equal(2048, ecsEvent.Detail.RegisteredResources[0].IntegerValue);
                Assert.Equal("22", ecsEvent.Detail.RegisteredResources[2].StringSetValue[0]);
                Assert.Equal(4, ecsEvent.Detail.RemainingResources.Count);
                Assert.Equal("CPU", ecsEvent.Detail.RemainingResources[0].Name);
                Assert.Equal("INTEGER", ecsEvent.Detail.RemainingResources[0].Type);
                Assert.Equal(1988, ecsEvent.Detail.RemainingResources[0].IntegerValue);
                Assert.Equal("22", ecsEvent.Detail.RemainingResources[2].StringSetValue[0]);
                Assert.Equal("ACTIVE", ecsEvent.Detail.Status);
                Assert.Equal(14801, ecsEvent.Detail.Version);
                Assert.Equal("aebcbca", ecsEvent.Detail.VersionInfo.AgentHash);
                Assert.Equal("1.13.0", ecsEvent.Detail.VersionInfo.AgentVersion);
                Assert.Equal("DockerVersion: 1.11.2", ecsEvent.Detail.VersionInfo.DockerVersion);
                Assert.Equal(DateTime.Parse("2016-12-06T16:41:06.991Z").ToUniversalTime(), ecsEvent.Detail.UpdatedAt.ToUniversalTime());

                Handle(ecsEvent);
            }
        }


        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void ECSTaskStateChangeEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("ecs-task-state-change-event.json"))
            {
                var ecsEvent = serializer.Deserialize<ECSTaskStateChangeEvent>(fileStream);

                Assert.Equal("0", ecsEvent.Version);
                Assert.Equal("3317b2af-7005-947d-b652-f55e762e571a", ecsEvent.Id);
                Assert.Equal("ECS Task State Change", ecsEvent.DetailType);
                Assert.Equal("aws.ecs", ecsEvent.Source);
                Assert.Equal("111122223333", ecsEvent.Account);
                Assert.Equal(DateTime.Parse("2020-01-23T17:57:58Z").ToUniversalTime(), ecsEvent.Time.ToUniversalTime());
                Assert.Equal("us-west-2", ecsEvent.Region);
                Assert.NotNull(ecsEvent.Resources);
                Assert.Single(ecsEvent.Resources);
                Assert.Equal("arn:aws:ecs:us-west-2:111122223333:task/FargateCluster/c13b4cb40f1f4fe4a2971f76ae5a47ad", ecsEvent.Resources[0]);
                Assert.NotNull(ecsEvent.Detail);
                Assert.IsType<Task>(ecsEvent.Detail);

                Assert.NotNull(ecsEvent.Detail.Attachments);
                Assert.Single(ecsEvent.Detail.Attachments);
                Assert.Equal("1789bcae-ddfb-4d10-8ebe-8ac87ddba5b8", ecsEvent.Detail.Attachments[0].Id);
                Assert.Equal("eni", ecsEvent.Detail.Attachments[0].Type);
                Assert.Equal("ATTACHED", ecsEvent.Detail.Attachments[0].Status);
                Assert.NotNull(ecsEvent.Detail.Attachments[0].Details);
                Assert.Equal(4, ecsEvent.Detail.Attachments[0].Details.Count);
                Assert.Equal("subnetId", ecsEvent.Detail.Attachments[0].Details[0].Name);
                Assert.Equal("subnet-abcd1234", ecsEvent.Detail.Attachments[0].Details[0].Value);
                Assert.Equal("networkInterfaceId", ecsEvent.Detail.Attachments[0].Details[1].Name);
                Assert.Equal("eni-abcd1234", ecsEvent.Detail.Attachments[0].Details[1].Value);
                Assert.Equal("macAddress", ecsEvent.Detail.Attachments[0].Details[2].Name);
                Assert.Equal("0a:98:eb:a7:29:ba", ecsEvent.Detail.Attachments[0].Details[2].Value);
                Assert.Equal("privateIPv4Address", ecsEvent.Detail.Attachments[0].Details[3].Name);
                Assert.Equal("10.0.0.139", ecsEvent.Detail.Attachments[0].Details[3].Value);

                Assert.Equal("us-west-2c", ecsEvent.Detail.AvailabilityZone);
                Assert.Equal("arn:aws:ecs:us-west-2:111122223333:cluster/FargateCluster", ecsEvent.Detail.ClusterArn);

                Assert.NotNull(ecsEvent.Detail.Containers);
                Assert.Single(ecsEvent.Detail.Containers);
                Assert.Equal("arn:aws:ecs:us-west-2:111122223333:container/cf159fd6-3e3f-4a9e-84f9-66cbe726af01", ecsEvent.Detail.Containers[0].ContainerArn);
                Assert.Equal(0, ecsEvent.Detail.Containers[0].ExitCode);
                Assert.Equal("RUNNING", ecsEvent.Detail.Containers[0].LastStatus);
                Assert.Equal("FargateApp", ecsEvent.Detail.Containers[0].Name);
                Assert.Equal("111122223333.dkr.ecr.us-west-2.amazonaws.com/hello-repository:latest", ecsEvent.Detail.Containers[0].Image);
                Assert.Equal("sha256:74b2c688c700ec95a93e478cdb959737c148df3fbf5ea706abe0318726e885e6", ecsEvent.Detail.Containers[0].ImageDigest);
                Assert.Equal("ad64cbc71c7fb31c55507ec24c9f77947132b03d48d9961115cf24f3b7307e1e", ecsEvent.Detail.Containers[0].RuntimeId);
                Assert.Equal("arn:aws:ecs:us-west-2:111122223333:task/FargateCluster/c13b4cb40f1f4fe4a2971f76ae5a47ad", ecsEvent.Detail.Containers[0].TaskArn);
                Assert.NotNull(ecsEvent.Detail.Containers[0].NetworkInterfaces);
                Assert.Single(ecsEvent.Detail.Containers[0].NetworkInterfaces);
                Assert.Equal("1789bcae-ddfb-4d10-8ebe-8ac87ddba5b8", ecsEvent.Detail.Containers[0].NetworkInterfaces[0].AttachmentId);
                Assert.Equal("10.0.0.139", ecsEvent.Detail.Containers[0].NetworkInterfaces[0].PrivateIpv4Address);
                Assert.Equal("0", ecsEvent.Detail.Containers[0].Cpu);

                Assert.Equal(DateTime.Parse("2020-01-23T17:57:34.402Z").ToUniversalTime(), ecsEvent.Detail.CreatedAt.ToUniversalTime());
                Assert.Equal("FARGATE", ecsEvent.Detail.LaunchType);
                Assert.Equal("256", ecsEvent.Detail.Cpu);
                Assert.Equal("512", ecsEvent.Detail.Memory);
                Assert.Equal("RUNNING", ecsEvent.Detail.DesiredStatus);
                Assert.Equal("family:sample-fargate", ecsEvent.Detail.Group);
                Assert.Equal("RUNNING", ecsEvent.Detail.LastStatus);

                Assert.Single(ecsEvent.Detail.Overrides.ContainerOverrides);
                Assert.Equal("FargateApp", ecsEvent.Detail.Overrides.ContainerOverrides[0].Name);
                Assert.Single(ecsEvent.Detail.Overrides.ContainerOverrides[0].Environment);
                Assert.Equal("testname", ecsEvent.Detail.Overrides.ContainerOverrides[0].Environment[0].Name);
                Assert.Equal("testvalue", ecsEvent.Detail.Overrides.ContainerOverrides[0].Environment[0].Value);

                Assert.Equal("CONNECTED", ecsEvent.Detail.Connectivity);
                Assert.Equal(DateTime.Parse("2020-01-23T17:57:38.453Z").ToUniversalTime(), ecsEvent.Detail.ConnectivityAt.ToUniversalTime());
                Assert.Equal(DateTime.Parse("2020-01-23T17:57:52.103Z").ToUniversalTime(), ecsEvent.Detail.PullStartedAt.ToUniversalTime());
                Assert.Equal(DateTime.Parse("2020-01-23T17:57:58.103Z").ToUniversalTime(), ecsEvent.Detail.StartedAt.ToUniversalTime());
                Assert.Equal(DateTime.Parse("2020-01-23T17:57:55.103Z").ToUniversalTime(), ecsEvent.Detail.PullStoppedAt.ToUniversalTime());
                Assert.Equal(DateTime.Parse("2020-01-23T17:57:58.103Z").ToUniversalTime(), ecsEvent.Detail.UpdatedAt.ToUniversalTime());
                Assert.Equal("arn:aws:ecs:us-west-2:111122223333:task/FargateCluster/c13b4cb40f1f4fe4a2971f76ae5a47ad", ecsEvent.Detail.TaskArn);
                Assert.Equal("arn:aws:ecs:us-west-2:111122223333:task-definition/sample-fargate:1", ecsEvent.Detail.TaskDefinitionArn);
                Assert.Equal(4, ecsEvent.Detail.Version);
                Assert.Equal("1.3.0", ecsEvent.Detail.PlatformVersion);

                Handle(ecsEvent);
            }
        }

        private void Handle(ECSContainerInstanceStateChangeEvent ecsEvent)
        {
            Console.WriteLine($"[{ecsEvent.Source} {ecsEvent.Time}] {ecsEvent.DetailType}");
        }

        private void Handle(ECSTaskStateChangeEvent ecsEvent)
        {
            Console.WriteLine($"[{ecsEvent.Source} {ecsEvent.Time}] {ecsEvent.DetailType}");
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void KafkaEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kafka-event.json"))
            {
                var kafkaEvent = serializer.Deserialize<KafkaEvent>(fileStream);
                Assert.NotNull(kafkaEvent);
                Assert.Equal("aws:kafka", kafkaEvent.EventSource);
                Assert.Equal("arn:aws:kafka:us-east-1:123456789012:cluster/vpc-3432434/4834-3547-3455-9872-7929", kafkaEvent.EventSourceArn);
                Assert.Equal("b-2.demo-cluster-1.a1bcde.c1.kafka.us-east-1.amazonaws.com:9092,b-1.demo-cluster-1.a1bcde.c1.kafka.us-east-1.amazonaws.com:9092", kafkaEvent.BootstrapServers);

                Assert.NotNull(kafkaEvent.Records);
                Assert.Single(kafkaEvent.Records);

                var record = kafkaEvent.Records.FirstOrDefault();
                Assert.Equal("mytopic-0", record.Key);

                Assert.Single(record.Value);
                var eventRecord = record.Value.FirstOrDefault();
                Assert.Equal("mytopic", eventRecord.Topic);
                Assert.Equal(12, eventRecord.Partition);
                Assert.Equal(3043205, eventRecord.Offset);
                Assert.Equal(1545084650987, eventRecord.Timestamp);
                Assert.Equal("CREATE_TIME", eventRecord.TimestampType);

                Assert.Equal("Hello, this is a test.", new StreamReader(eventRecord.Value).ReadToEnd());

                Assert.Equal(8, eventRecord.Headers.Count);
                var eventRecordHeader = eventRecord.Headers.FirstOrDefault();
                Assert.NotNull(eventRecordHeader);
                Assert.Single(eventRecordHeader);
                var eventRecordHeaderValue = eventRecordHeader.FirstOrDefault();
                Assert.Equal("headerKey", eventRecordHeaderValue.Key);

                // Convert sbyte[] to byte[] array.
                var tempHeaderValueByteArray = new byte[eventRecordHeaderValue.Value.Length];
                Buffer.BlockCopy(eventRecordHeaderValue.Value, 0, tempHeaderValueByteArray, 0, tempHeaderValueByteArray.Length);

                Assert.Equal("headerValue", Encoding.UTF8.GetString(tempHeaderValueByteArray));

                Handle(kafkaEvent);
            }
        }

        private void Handle(KafkaEvent kafkaEvent)
        {
            foreach (var record in kafkaEvent.Records)
            {
                foreach (var eventRecord in record.Value)
                {
                    var valueBytes = eventRecord.Value.ToArray();
                    var valueText = Encoding.UTF8.GetString(valueBytes);
                    Console.WriteLine($"[{record.Key}] Value = '{valueText}'.");
                }
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void ActiveMQEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("amazonmq-activemq.json"))
            {
                var activemqEvent = serializer.Deserialize<ActiveMQEvent>(fileStream);
                Assert.NotNull(activemqEvent);
                Assert.Equal("aws:amq", activemqEvent.EventSource);
                Assert.Equal("arn:aws:mq:us-west-2:112556298976:broker:test:b-9bcfa592-423a-4942-879d-eb284b418fc8", activemqEvent.EventSourceArn);

                Assert.Equal(2, activemqEvent.Messages.Count);
                Assert.Equal("ID:b-9bcfa592-423a-4942-879d-eb284b418fc8-1.mq.us-west-2.amazonaws.com-37557-1234520418293-4:1:1:1:1", activemqEvent.Messages[0].MessageId);
                Assert.Equal("jms/text-message", activemqEvent.Messages[0].MessageType);
                Assert.Equal("ABC:AAAA", Encoding.UTF8.GetString(Convert.FromBase64String(activemqEvent.Messages[0].Data)));
                Assert.Equal("myJMSCoID", activemqEvent.Messages[0].ConnectionId);
                Assert.False(activemqEvent.Messages[0].Redelivered);
                Assert.Null(activemqEvent.Messages[0].Persistent);
                Assert.Equal("testQueue", activemqEvent.Messages[0].Destination.PhysicalName);
                Assert.NotNull(activemqEvent.Messages[0].Timestamp);
                Assert.NotNull(activemqEvent.Messages[0].BrokerInTime);
                Assert.NotNull(activemqEvent.Messages[0].BrokerOutTime);
                Assert.Equal("testValue", activemqEvent.Messages[0].Properties["testKey"]);

                Assert.Equal("ID:b-9bcfa592-423a-4942-879d-eb284b418fc8-1.mq.us-west-2.amazonaws.com-37557-1234520418293-4:1:1:1:1", activemqEvent.Messages[1].MessageId);
                Assert.Equal("jms/bytes-message", activemqEvent.Messages[1].MessageType);
                Assert.NotNull(Convert.FromBase64String(activemqEvent.Messages[1].Data));
                Assert.Equal("myJMSCoID1", activemqEvent.Messages[1].ConnectionId);
                Assert.Null(activemqEvent.Messages[1].Redelivered);
                Assert.False(activemqEvent.Messages[1].Persistent);
                Assert.Equal("testQueue", activemqEvent.Messages[1].Destination.PhysicalName);
                Assert.NotNull(activemqEvent.Messages[1].Timestamp);
                Assert.NotNull(activemqEvent.Messages[1].BrokerInTime);
                Assert.NotNull(activemqEvent.Messages[1].BrokerOutTime);
                Assert.Equal("testValue", activemqEvent.Messages[1].Properties["testKey"]);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void RabbitMQEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("amazonmq-rabbitmq.json"))
            {
                var rabbitmqEvent = serializer.Deserialize<RabbitMQEvent>(fileStream);
                Assert.NotNull(rabbitmqEvent);
                Assert.Equal("aws:rmq", rabbitmqEvent.EventSource);
                Assert.Equal("arn:aws:mq:us-west-2:112556298976:broker:pizzaBroker:b-9bcfa592-423a-4942-879d-eb284b418fc8", rabbitmqEvent.EventSourceArn);

                Assert.Single(rabbitmqEvent.RmqMessagesByQueue);
                Assert.Equal(2, rabbitmqEvent.RmqMessagesByQueue["pizzaQueue::/"].Count);

                var firstMessage = rabbitmqEvent.RmqMessagesByQueue["pizzaQueue::/"][0];
                Assert.NotNull(firstMessage.BasicProperties);
                Assert.Equal("text/plain", firstMessage.BasicProperties.ContentType);
                Assert.Null(firstMessage.BasicProperties.ContentEncoding);
                Assert.Equal(3, firstMessage.BasicProperties.Headers.Count);
                Assert.Equal(1, firstMessage.BasicProperties.DeliveryMode);
                Assert.Equal(34, firstMessage.BasicProperties.Priority);
                Assert.Null(firstMessage.BasicProperties.CorrelationId);
                Assert.Null(firstMessage.BasicProperties.ReplyTo);
                Assert.Equal("60000", firstMessage.BasicProperties.Expiration);
                Assert.Null(firstMessage.BasicProperties.MessageId);
                Assert.NotNull(firstMessage.BasicProperties.Timestamp);
                Assert.Null(firstMessage.BasicProperties.Type);
                Assert.Equal("AIDACKCEVSQ6C2EXAMPLE", firstMessage.BasicProperties.UserId);
                Assert.Null(firstMessage.BasicProperties.AppId);
                Assert.Null(firstMessage.BasicProperties.ClusterId);
                Assert.Equal(80, firstMessage.BasicProperties.BodySize);
                Assert.False(firstMessage.Redelivered);
                Assert.Equal("{\"timeout\":0,\"data\":\"CZrmf0Gw8Ov4bqLQxD4E\"}", Encoding.UTF8.GetString(Convert.FromBase64String(firstMessage.Data)));

                var secondMessage = rabbitmqEvent.RmqMessagesByQueue["pizzaQueue::/"][1];
                Assert.NotNull(secondMessage.BasicProperties);
                Assert.Null(secondMessage.BasicProperties.ContentType);
                Assert.Null(secondMessage.BasicProperties.ContentEncoding);
                Assert.Empty(secondMessage.BasicProperties.Headers);
                Assert.Equal(1, secondMessage.BasicProperties.DeliveryMode);
                Assert.Null(secondMessage.BasicProperties.Priority);
                Assert.Null(secondMessage.BasicProperties.CorrelationId);
                Assert.Null(secondMessage.BasicProperties.ReplyTo);
                Assert.Null(secondMessage.BasicProperties.Expiration);
                Assert.Null(secondMessage.BasicProperties.MessageId);
                Assert.Null(secondMessage.BasicProperties.Timestamp);
                Assert.Null(secondMessage.BasicProperties.Type);
                Assert.Null(secondMessage.BasicProperties.UserId);
                Assert.Null(secondMessage.BasicProperties.AppId);
                Assert.Null(secondMessage.BasicProperties.ClusterId);
                Assert.Equal(11, secondMessage.BasicProperties.BodySize);
                Assert.True(secondMessage.Redelivered);
                Assert.Equal("Hello World", Encoding.UTF8.GetString(Convert.FromBase64String(secondMessage.Data)));
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void APIGatewayCustomAuthorizerV2Request(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("custom-authorizer-v2-request.json"))
            {
                var request = serializer.Deserialize<APIGatewayCustomAuthorizerV2Request>(fileStream);

                Assert.Equal("REQUEST", request.Type);
                Assert.Equal("arn:aws:execute-api:us-east-1:123456789012:abcdef123/test/GET/request", request.RouteArn);
                Assert.Equal(new[] { "user1", "123" }, request.IdentitySource);
                Assert.Equal("$default", request.RouteKey);
                Assert.Equal("/my/path", request.RawPath);
                Assert.Equal("parameter1=value1&parameter1=value2&parameter2=value", request.RawQueryString);
                Assert.Equal(new[] { "cookie1", "cookie2" }, request.Cookies);

                Assert.Equal(new Dictionary<string, string>
                {
                    ["Header1"] = "value1",
                    ["Header2"] = "value2"
                }, request.Headers);

                Assert.Equal(new Dictionary<string, string>
                {
                    ["parameter1"] = "value1,value2",
                    ["parameter2"] = "value"
                }, request.QueryStringParameters);

                var requestContext = request.RequestContext;
                Assert.Equal("123456789012", requestContext.AccountId);
                Assert.Equal("api-id", requestContext.ApiId);

                var clientCert = requestContext.Authentication.ClientCert;
                Assert.Equal("CERT_CONTENT", clientCert.ClientCertPem);
                Assert.Equal("www.example.com", clientCert.SubjectDN);
                Assert.Equal("Example issuer", clientCert.IssuerDN);
                Assert.Equal("a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1", clientCert.SerialNumber);
                Assert.Equal("May 28 12:30:02 2019 GMT", clientCert.Validity.NotBefore);
                Assert.Equal("Aug  5 09:36:04 2021 GMT", clientCert.Validity.NotAfter);

                Assert.Equal("id.execute-api.us-east-1.amazonaws.com", requestContext.DomainName);
                Assert.Equal("id", requestContext.DomainPrefix);

                Assert.Equal("POST", requestContext.Http.Method);
                Assert.Equal("HTTP/1.1", requestContext.Http.Protocol);
                Assert.Equal("IP", requestContext.Http.SourceIp);
                Assert.Equal("agent", requestContext.Http.UserAgent);

                Assert.Equal("id", requestContext.RequestId);
                Assert.Equal("$default", requestContext.RouteKey);
                Assert.Equal("$default", requestContext.Stage);
                Assert.Equal("12/Mar/2020:19:03:58 +0000", requestContext.Time);
                Assert.Equal(1583348638390, requestContext.TimeEpoch);

                Assert.Equal(new Dictionary<string, string>
                {
                    ["parameter1"] = "value1"
                }, request.PathParameters);

                Assert.Equal(new Dictionary<string, string>
                {
                    ["stageVariable1"] = "value1",
                    ["stageVariable2"] = "value2"
                }, request.StageVariables);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void APIGatewayCustomAuthorizerV2SimpleResponse(Type serializerType)
        {
            var response = new APIGatewayCustomAuthorizerV2SimpleResponse
            {
                IsAuthorized = true,
                Context = new Dictionary<string, object>()
                {
                    ["exampleKey"] = "exampleValue"
                }
            };

            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            var json = SerializeJson(serializer, response);
            var actualObject = JObject.Parse(json);
            var expectedJObject = JObject.Parse(File.ReadAllText("custom-authorizer-v2-simple-response.json"));

            Assert.True(JToken.DeepEquals(actualObject, expectedJObject));
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void APIGatewayCustomAuthorizerV2IamResponse(Type serializerType)
        {
            var response = new APIGatewayCustomAuthorizerV2IamResponse
            {
                PrincipalID = "abcdef",
                PolicyDocument = new APIGatewayCustomAuthorizerPolicy
                {
                    Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>
                    {
                        new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement
                        {
                            Action = ["execute-api:Invoke"],
                            Effect = "Allow",
                            Resource = new HashSet<string>{ "arn:aws:execute-api:{regionId}:{accountId}:{apiId}/{stage}/{httpVerb}/[{resource}/[{child-resources}]]" }
                        }
                    }
                },
                Context = new Dictionary<string, object>()
                {
                    ["exampleKey"] = "exampleValue"
                }
            };

            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            var json = SerializeJson(serializer, response);
            var actualObject = JObject.Parse(json);
            var expectedJObject = JObject.Parse(File.ReadAllText("custom-authorizer-v2-iam-response.json"));

            Assert.True(JToken.DeepEquals(actualObject, expectedJObject));
        }

        [Fact]
        public void SerializeCanUseNamingStrategy()
        {
            var namingStrategy = new CamelCaseNamingStrategy();
            var serializer = new JsonSerializer(_ => { }, namingStrategy);

            var classUsingPascalCase = new ClassUsingPascalCase
            {
                SomeValue = 12,
                SomeOtherValue = "abcd",
            };

            var ms = new MemoryStream();

            serializer.Serialize(classUsingPascalCase, ms);
            ms.Position = 0;

            var serializedString = new StreamReader(ms).ReadToEnd();

            Assert.Equal(@"{""someValue"":12,""someOtherValue"":""abcd""}", serializedString);
        }

        [Fact]
        public void SerializeWithCamelCaseNamingStrategyCanDeserializeBothCamelAndPascalCase()
        {
            var namingStrategy = new CamelCaseNamingStrategy();
            var serializer = new JsonSerializer(_ => { }, namingStrategy);

            var camelCaseString = @"{""someValue"":12,""someOtherValue"":""abcd""}";
            var pascalCaseString = @"{""SomeValue"":12,""SomeOtherValue"":""abcd""}";

            var camelCaseObject = serializer.Deserialize<ClassUsingPascalCase>(new MemoryStream(Encoding.ASCII.GetBytes(camelCaseString)));
            var pascalCaseObject = serializer.Deserialize<ClassUsingPascalCase>(new MemoryStream(Encoding.ASCII.GetBytes(pascalCaseString)));

            Assert.Equal(12, camelCaseObject.SomeValue);
            Assert.Equal(12, pascalCaseObject.SomeValue);
            Assert.Equal("abcd", camelCaseObject.SomeOtherValue);
            Assert.Equal("abcd", pascalCaseObject.SomeOtherValue);
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CloudWatchEventsS3ObjectCreate(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cloudwatchevents-s3objectcreated.json"))
            {
                var request = serializer.Deserialize<S3ObjectCreateEvent>(fileStream);
                Assert.Equal("17793124-05d4-b198-2fde-7ededc63b103", request.Id);

                var detail = request.Detail;
                Assert.NotNull(detail);
                Assert.Equal("0", detail.Version);
                Assert.Equal("DOC-EXAMPLE-BUCKET1", detail.Bucket.Name);
                Assert.Equal("example-key", detail.Object.Key);
                Assert.Equal(5L, detail.Object.Size);
                Assert.Equal("b1946ac92492d2347c6235b4d2611184", detail.Object.ETag);
                Assert.Equal("IYV3p45BT0ac8hjHg1houSdS1a.Mro8e", detail.Object.VersionId);
                Assert.Equal("617f08299329d189", detail.Object.Sequencer);
                Assert.Equal("N4N7GDK58NMKJ12R", detail.RequestId);
                Assert.Equal("123456789012", detail.Requester);
                Assert.Equal("1.2.3.4", detail.SourceIpAddress);
                Assert.Equal("PutObject", detail.Reason);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CloudWatchEventsS3ObjectDelete(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cloudwatchevents-s3objectdeleted.json"))
            {
                var request = serializer.Deserialize<S3ObjectDeleteEvent>(fileStream);
                Assert.Equal("2ee9cc15-d022-99ea-1fb8-1b1bac4850f9", request.Id);

                var detail = request.Detail;
                Assert.NotNull(detail);
                Assert.Equal("0", detail.Version);
                Assert.Equal("DOC-EXAMPLE-BUCKET1", detail.Bucket.Name);
                Assert.Equal("example-key", detail.Object.Key);
                Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", detail.Object.ETag);
                Assert.Equal("1QW9g1Z99LUNbvaaYVpW9xDlOLU.qxgF", detail.Object.VersionId);
                Assert.Equal("617f0837b476e463", detail.Object.Sequencer);
                Assert.Equal("0BH729840619AG5K", detail.RequestId);
                Assert.Equal("123456789012", detail.Requester);
                Assert.Equal("1.2.3.4", detail.SourceIpAddress);
                Assert.Equal("DeleteObject", detail.Reason);
                Assert.Equal("Delete Marker Created", detail.DeletionType);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CloudWatchEventsS3ObjectRestore(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cloudwatchevents-s3objectrestore.json"))
            {
                var request = serializer.Deserialize<S3ObjectRestoreEvent>(fileStream);
                Assert.Equal("6924de0d-13e2-6bbf-c0c1-b903b753565e", request.Id);

                var detail = request.Detail;
                Assert.NotNull(detail);
                Assert.Equal("0", detail.Version);
                Assert.Equal("DOC-EXAMPLE-BUCKET1", detail.Bucket.Name);
                Assert.Equal("example-key", detail.Object.Key);
                Assert.Equal(5L, detail.Object.Size);
                Assert.Equal("b1946ac92492d2347c6235b4d2611184", detail.Object.ETag);
                Assert.Equal("KKsjUC1.6gIjqtvhfg5AdMI0eCePIiT3", detail.Object.VersionId);
                Assert.Equal("189F19CB7FB1B6A4", detail.RequestId);
                Assert.Equal("s3.amazonaws.com", detail.Requester);
                Assert.Equal("2021-11-13T00:00:00Z", detail.RestoreExpiryTime);
                Assert.Equal("GLACIER", detail.SourceStorageClass);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CloudWatchTranscribeJobStateChangeCompleted(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cloudwatchevents-transcribejobstatechangecompleted.json"))
            {
                var request = serializer.Deserialize<TranscribeJobStateChangeEvent>(fileStream);
                Assert.Equal("1de9a55a-39aa-d889-84eb-22d245492319", request.Id);

                var detail = request.Detail;
                Assert.NotNull(detail);
                Assert.Equal("51a3dfef-87fa-423a-8d3b-690ca9cae1f4", detail.TranscriptionJobName);
                Assert.Equal("COMPLETED", detail.TranscriptionJobStatus);
                Assert.Null(detail.FailureReason);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CloudWatchTranscribeJobStateChangeFailed(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cloudwatchevents-transcribejobstatechangefailed.json"))
            {
                var request = serializer.Deserialize<TranscribeJobStateChangeEvent>(fileStream);
                Assert.Equal("5505f4fc-979b-0304-3570-8fa0e3c09525", request.Id);

                var detail = request.Detail;
                Assert.NotNull(detail);
                Assert.Equal("d43d0b58-2129-46ba-b2e2-b53ec9d1b210", detail.TranscriptionJobName);
                Assert.Equal("FAILED", detail.TranscriptionJobStatus);
                Assert.Equal("The media format that you specified doesn't match the detected media format. Check the media format and try your request again.", detail.FailureReason);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CloudWatchTranslateTextTranslationJobStateChange(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cloudwatchevents-translatetexttranslationjobstatechange.json"))
            {
                var request = serializer.Deserialize<TranslateTextTranslationJobStateChangeEvent>(fileStream);
                Assert.Equal("8882c5af-c9da-4a58-99e2-91fbe33b9e52", request.Id);

                var detail = request.Detail;
                Assert.NotNull(detail);
                Assert.Equal("8ce682a1-9be8-4f2c-875c-f8ae2fe1b015", detail.JobId);
                Assert.Equal("COMPLETED", detail.JobStatus);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CloudWatchTranslateParallelDataStateChangeCreate(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cloudwatchevents-translateparalleldatastatechange-create.json"))
            {
                var request = serializer.Deserialize<TranslateParallelDataStateChangeEvent>(fileStream);
                Assert.Equal("e99030f3-a7a8-42f5-923a-684fbf9bff65", request.Id);

                var detail = request.Detail;
                Assert.NotNull(detail);
                Assert.Equal("CreateParallelData", detail.Operation);
                Assert.Equal("ExampleParallelData", detail.Name);
                Assert.Equal("ACTIVE", detail.Status);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public void CloudWatchTranslateParallelDataStateChangeUpdate(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cloudwatchevents-translateparalleldatastatechange-update.json"))
            {
                var request = serializer.Deserialize<TranslateParallelDataStateChangeEvent>(fileStream);
                Assert.Equal("920d9de3-fbd0-4cfb-87aa-e35b5f7cba8f", request.Id);

                var detail = request.Detail;
                Assert.NotNull(detail);
                Assert.Equal("UpdateParallelData", detail.Operation);
                Assert.Equal("ExampleParallelData2", detail.Name);
                Assert.Equal("ACTIVE", detail.Status);
                Assert.Equal("ACTIVE", detail.LatestUpdateAttemptStatus);
                Assert.Equal(DateTime.Parse("2023-03-02T03:31:47Z").ToUniversalTime(), detail.LatestUpdateAttemptAt);

            }
        }

        [Fact]
        public void TestJsonIncludeNullValueSerializer()
        {
            var serializer = new JsonIncludeNullValueSerializer();

            var response = new ClassUsingPascalCase
            {
                SomeValue = 123,
                SomeOtherValue = null
            };

            var ms = new MemoryStream();
            serializer.Serialize(response, ms);
            ms.Position = 0;
            var json = new StreamReader(ms).ReadToEnd();

            var serialized = JObject.Parse(json);
            Assert.Equal(123, serialized["SomeValue"]);
            Assert.Equal(JTokenType.Null, serialized["SomeOtherValue"].Type); // System.NullReferenceException is thrown if value is missing.
        }

        class ClassUsingPascalCase
        {
            public int SomeValue { get; set; }
            public string SomeOtherValue { get; set; }
        }
    }
}
#pragma warning restore 618
