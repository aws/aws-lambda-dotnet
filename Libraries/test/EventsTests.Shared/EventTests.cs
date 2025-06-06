#pragma warning disable 618
namespace Amazon.Lambda.Tests
{
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
    using Amazon.Runtime.Internal.Transform;
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
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.Serialize(response, stream);

                stream.Position = 0;
                serializedJson = Encoding.UTF8.GetString(stream.ToArray());
            }
            return serializedJson;
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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

                Assert.Equal(1, request.PathParameters.Count);
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER        
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER        
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void S3PutTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("s3-event.json"))
            {
                var s3Event = serializer.Deserialize<S3Event>(fileStream);

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
#if NETCOREAPP3_1_OR_GREATER        
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void KinesisTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-event.json"))
            {
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
#if NET8_0_OR_GREATER
                // Starting with .NET 7 the precision of the underlying AddSeconds method was changed.
                // https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/7.0/datetime-add-precision
                Assert.Equal(636162383234769999, record.Kinesis.ApproximateArrivalTimestamp.Value.ToUniversalTime().Ticks);
#else
                Assert.Equal(636162383234770000, record.Kinesis.ApproximateArrivalTimestamp.Value.ToUniversalTime().Ticks);
#endif

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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void KinesisBatchItemFailuresTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-batchitemfailures-response.json"))
            {
                var kinesisStreamsEventResponse = serializer.Deserialize<KinesisEvents.StreamsEventResponse>(fileStream);

                Assert.Equal(1, kinesisStreamsEventResponse.BatchItemFailures.Count);
                Assert.Equal("1405400000000002063282832", kinesisStreamsEventResponse.BatchItemFailures[0].ItemIdentifier);

                MemoryStream ms = new MemoryStream();
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void KinesisTimeWindowTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-timewindow-event.json"))
            {
                var kinesisTimeWindowEvent = serializer.Deserialize<KinesisTimeWindowEvent>(fileStream);

                Assert.Equal(kinesisTimeWindowEvent.ShardId, "shardId-000000000006");
                Assert.Equal(kinesisTimeWindowEvent.EventSourceARN, "arn:aws:kinesis:us-east-1:123456789012:stream/lambda-stream");
                Assert.False(kinesisTimeWindowEvent.IsFinalInvokeForWindow);
                Assert.False(kinesisTimeWindowEvent.IsWindowTerminatedEarly);
                Assert.Equal(kinesisTimeWindowEvent.State.Count, 2);
                Assert.True(kinesisTimeWindowEvent.State.ContainsKey("1"));
                Assert.Equal(kinesisTimeWindowEvent.State["1"], "282");
                Assert.True(kinesisTimeWindowEvent.State.ContainsKey("2"));
                Assert.Equal(kinesisTimeWindowEvent.State["2"], "715");
                Assert.NotNull(kinesisTimeWindowEvent.Window);
                Assert.Equal(637430942400000000, kinesisTimeWindowEvent.Window.Start.Ticks);
                Assert.Equal(637430943600000000, kinesisTimeWindowEvent.Window.End.Ticks);

                Assert.Equal(kinesisTimeWindowEvent.Records.Count, 1);
                var record = kinesisTimeWindowEvent.Records[0];
                Assert.Equal(record.EventId, "shardId-000000000006:49590338271490256608559692538361571095921575989136588898");
                Assert.Equal(record.EventName, "aws:kinesis:record");
                Assert.Equal(record.EventVersion, "1.0");
                Assert.Equal(record.EventSource, "aws:kinesis");
                Assert.Equal(record.InvokeIdentityArn, "arn:aws:iam::123456789012:role/lambda-kinesis-role");
                Assert.Equal(record.AwsRegion, "us-east-1");
                Assert.Equal(record.EventSourceARN, "arn:aws:kinesis:us-east-1:123456789012:stream/lambda-stream");

                Assert.Equal(record.Kinesis.KinesisSchemaVersion, "1.0");
                Assert.Equal(record.Kinesis.PartitionKey, "1");
                Assert.Equal(record.Kinesis.SequenceNumber, "49590338271490256608559692538361571095921575989136588898");
                var dataBytes = record.Kinesis.Data.ToArray();
                Assert.Equal(Convert.ToBase64String(dataBytes), "SGVsbG8sIHRoaXMgaXMgYSB0ZXN0Lg==");
                Assert.Equal(Encoding.UTF8.GetString(dataBytes), "Hello, this is a test.");
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void KinesisTimeWindowResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-timewindow-response.json"))
            {
                var kinesisTimeWindowResponse = serializer.Deserialize<KinesisTimeWindowResponse>(fileStream);

                Assert.Equal(kinesisTimeWindowResponse.State.Count, 2);
                Assert.True(kinesisTimeWindowResponse.State.ContainsKey("1"));
                Assert.Equal(kinesisTimeWindowResponse.State["1"], "282");
                Assert.True(kinesisTimeWindowResponse.State.ContainsKey("2"));
                Assert.Equal(kinesisTimeWindowResponse.State["2"], "715");
                Assert.Equal(kinesisTimeWindowResponse.BatchItemFailures.Count, 0);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void DynamoDbUpdateTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            Stream json = LoadJsonTestFile("dynamodb-event.json");
            var dynamodbEvent = serializer.Deserialize<DynamoDBEvent>(json);
            Assert.Equal(dynamodbEvent.Records.Count, 2);

            var record = dynamodbEvent.Records[0];
            Assert.Equal(record.EventID, "f07f8ca4b0b26cb9c4e5e77e69f274ee");
            Assert.Equal(record.EventVersion, "1.1");
            Assert.Equal(record.Dynamodb.Keys.Count, 2);
            Assert.Equal(record.Dynamodb.Keys["key"].S, "binary");
            Assert.Equal(record.Dynamodb.Keys["val"].S, "data");
            Assert.Null(record.UserIdentity);
            Assert.Null(record.Dynamodb.OldImage);
            Assert.Equal(record.Dynamodb.NewImage["val"].S, "data");
            Assert.Equal(record.Dynamodb.NewImage["key"].S, "binary");
            Assert.Null(record.Dynamodb.NewImage["key"].BOOL);
            Assert.Null(record.Dynamodb.NewImage["key"].L);
            Assert.Null(record.Dynamodb.NewImage["key"].M);
            Assert.Null(record.Dynamodb.NewImage["key"].N);
            Assert.Null(record.Dynamodb.NewImage["key"].NS);
            Assert.Null(record.Dynamodb.NewImage["key"].NULL);
            Assert.Null(record.Dynamodb.NewImage["key"].SS);
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

            var topLevelList = record.Dynamodb.NewImage["misc1"].L;
            Assert.Equal(0, topLevelList.Count);

            var nestedMap = record.Dynamodb.NewImage["misc2"].M;
            Assert.NotNull(nestedMap);
            Assert.Equal(0, nestedMap["ItemsEmpty"].L.Count);
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void DynamoDbWithMillisecondsTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            Stream json = LoadJsonTestFile("dynamodb-with-ms-event.json");
            var dynamodbEvent = serializer.Deserialize<DynamoDBEvent>(json);
            Assert.Equal(dynamodbEvent.Records.Count, 2);

            var record = dynamodbEvent.Records[0];
            Assert.Equal(record.EventID, "f07f8ca4b0b26cb9c4e5e77e69f274ee");
            Assert.Equal(record.EventVersion, "1.1");
            Assert.Equal(record.Dynamodb.Keys.Count, 2);
            Assert.Equal(record.Dynamodb.Keys["key"].S, "binary");
            Assert.Equal(record.Dynamodb.Keys["val"].S, "data");
            Assert.Null(record.UserIdentity);
            Assert.Null(record.Dynamodb.OldImage);
            Assert.Equal(record.Dynamodb.NewImage["val"].S, "data");
            Assert.Equal(record.Dynamodb.NewImage["key"].S, "binary");
            Assert.Null(record.Dynamodb.NewImage["key"].BOOL);
            Assert.Null(record.Dynamodb.NewImage["key"].L);
            Assert.Null(record.Dynamodb.NewImage["key"].M);
            Assert.Null(record.Dynamodb.NewImage["key"].N);
            Assert.Null(record.Dynamodb.NewImage["key"].NS);
            Assert.Null(record.Dynamodb.NewImage["key"].NULL);
            Assert.Null(record.Dynamodb.NewImage["key"].SS);
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

            var topLevelList = record.Dynamodb.NewImage["misc1"].L;
            Assert.Equal(0, topLevelList.Count);

            var nestedMap = record.Dynamodb.NewImage["misc2"].M;
            Assert.NotNull(nestedMap);
            Assert.Equal(0, nestedMap["ItemsEmpty"].L.Count);
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void DynamoDbBatchItemFailuresTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("dynamodb-batchitemfailures-response.json"))
            {
                var dynamoDbStreamsEventResponse = serializer.Deserialize<DynamoDBEvents.StreamsEventResponse>(fileStream);

                Assert.Equal(1, dynamoDbStreamsEventResponse.BatchItemFailures.Count);
                Assert.Equal("1405400000000002063282832", dynamoDbStreamsEventResponse.BatchItemFailures[0].ItemIdentifier);

                MemoryStream ms = new MemoryStream();
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void DynamoDBTimeWindowTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("dynamodb-timewindow-event.json"))
            {
                var dynamoDBTimeWindowEvent = serializer.Deserialize<DynamoDBTimeWindowEvent>(fileStream);

                Assert.Equal(dynamoDBTimeWindowEvent.ShardId, "shard123456789");
                Assert.Equal(dynamoDBTimeWindowEvent.EventSourceArn, "stream-ARN");
                Assert.False(dynamoDBTimeWindowEvent.IsFinalInvokeForWindow);
                Assert.False(dynamoDBTimeWindowEvent.IsWindowTerminatedEarly);
                Assert.Equal(dynamoDBTimeWindowEvent.State.Count, 1);
                Assert.True(dynamoDBTimeWindowEvent.State.ContainsKey("1"));
                Assert.Equal(dynamoDBTimeWindowEvent.State["1"], "state1");
                Assert.NotNull(dynamoDBTimeWindowEvent.Window);
                Assert.Equal(637317252000000000, dynamoDBTimeWindowEvent.Window.Start.Ticks);
                Assert.Equal(637317255000000000, dynamoDBTimeWindowEvent.Window.End.Ticks);

                Assert.Equal(dynamoDBTimeWindowEvent.Records.Count, 3);

                var record1 = dynamoDBTimeWindowEvent.Records[0];
                Assert.Equal(record1.EventID, "1");
                Assert.Equal(record1.EventName, "INSERT");
                Assert.Equal(record1.EventVersion, "1.0");
                Assert.Equal(record1.EventSource, "aws:dynamodb");
                Assert.Equal(record1.AwsRegion, "us-east-1");
                Assert.Equal(record1.EventSourceArn, "stream-ARN");
                Assert.Equal(record1.Dynamodb.Keys.Count, 1);
                Assert.Equal(record1.Dynamodb.Keys["Id"].N, "101");
                Assert.Equal(record1.Dynamodb.SequenceNumber, "111");
                Assert.Equal(record1.Dynamodb.SizeBytes, 26);
                Assert.Equal(record1.Dynamodb.StreamViewType, "NEW_IMAGE");
                Assert.Equal(record1.Dynamodb.NewImage.Count, 2);
                Assert.Equal(record1.Dynamodb.NewImage["Message"].S, "New item!");
                Assert.Equal(record1.Dynamodb.NewImage["Id"].N, "101");
                Assert.Null(record1.Dynamodb.OldImage);

                var record2 = dynamoDBTimeWindowEvent.Records[1];
                Assert.Equal(record2.EventID, "2");
                Assert.Equal(record2.EventName, "MODIFY");
                Assert.Equal(record2.EventVersion, "1.0");
                Assert.Equal(record2.EventSource, "aws:dynamodb");
                Assert.Equal(record2.AwsRegion, "us-east-1");
                Assert.Equal(record2.EventSourceArn, "stream-ARN");
                Assert.Equal(record2.Dynamodb.Keys.Count, 1);
                Assert.Equal(record2.Dynamodb.Keys["Id"].N, "101");
                Assert.Equal(record2.Dynamodb.SequenceNumber, "222");
                Assert.Equal(record2.Dynamodb.SizeBytes, 59);
                Assert.Equal(record2.Dynamodb.StreamViewType, "NEW_AND_OLD_IMAGES");
                Assert.Equal(record2.Dynamodb.NewImage.Count, 2);
                Assert.Equal(record2.Dynamodb.NewImage["Message"].S, "This item has changed");
                Assert.Equal(record2.Dynamodb.NewImage["Id"].N, "101");
                Assert.Equal(record2.Dynamodb.OldImage.Count, 2);
                Assert.Equal(record2.Dynamodb.OldImage["Message"].S, "New item!");
                Assert.Equal(record2.Dynamodb.OldImage["Id"].N, "101");

                var record3 = dynamoDBTimeWindowEvent.Records[2];
                Assert.Equal(record3.EventID, "3");
                Assert.Equal(record3.EventName, "REMOVE");
                Assert.Equal(record3.EventVersion, "1.0");
                Assert.Equal(record3.EventSource, "aws:dynamodb");
                Assert.Equal(record3.AwsRegion, "us-east-1");
                Assert.Equal(record3.EventSourceArn, "stream-ARN");
                Assert.Equal(record3.Dynamodb.Keys.Count, 1);
                Assert.Equal(record3.Dynamodb.Keys["Id"].N, "101");
                Assert.Equal(record3.Dynamodb.SequenceNumber, "333");
                Assert.Equal(record3.Dynamodb.SizeBytes, 38);
                Assert.Equal(record3.Dynamodb.StreamViewType, "NEW_AND_OLD_IMAGES");
                Assert.Null(record3.Dynamodb.NewImage);
                Assert.Equal(record3.Dynamodb.OldImage.Count, 2);
                Assert.Equal(record3.Dynamodb.OldImage["Message"].S, "This item has changed");
                Assert.Equal(record3.Dynamodb.OldImage["Id"].N, "101");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void DynamoDBTimeWindowResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("dynamodb-timewindow-response.json"))
            {
                var dynamoDBTimeWindowResponse = serializer.Deserialize<DynamoDBTimeWindowResponse>(fileStream);

                Assert.Equal(dynamoDBTimeWindowResponse.State.Count, 2);
                Assert.True(dynamoDBTimeWindowResponse.State.ContainsKey("1"));
                Assert.Equal(dynamoDBTimeWindowResponse.State["1"], "282");
                Assert.True(dynamoDBTimeWindowResponse.State.ContainsKey("2"));
                Assert.Equal(dynamoDBTimeWindowResponse.State["2"], "715");
                Assert.Equal(dynamoDBTimeWindowResponse.BatchItemFailures.Count, 0);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void CognitoTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-event.json"))
            {
                var cognitoEvent = serializer.Deserialize<CognitoEvent>(fileStream);
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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

                MemoryStream ms = new MemoryStream();
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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

                MemoryStream ms = new MemoryStream();
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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

                MemoryStream ms = new MemoryStream();
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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

                MemoryStream ms = new MemoryStream();
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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

                MemoryStream ms = new MemoryStream();
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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

                MemoryStream ms = new MemoryStream();
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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

                MemoryStream ms = new MemoryStream();
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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

                MemoryStream ms = new MemoryStream();
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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


                MemoryStream ms = new MemoryStream();
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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

                MemoryStream ms = new MemoryStream();
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void CognitoCustomEmailSenderEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("cognito-customemailsender-event.json"))
            {
                var cognitoCustomEmailSenderEvent = serializer.Deserialize<CognitoCustomEmailSenderEvent>(fileStream);

                AssertBaseClass(cognitoCustomEmailSenderEvent);

                Assert.Equal("code", cognitoCustomEmailSenderEvent.Request.Code);
                Assert.Equal("type", cognitoCustomEmailSenderEvent.Request.Type);

                MemoryStream ms = new MemoryStream();
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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

        String ConfigInvokingEvent = "{\"configSnapshotId\":\"00000000-0000-0000-0000-000000000000\",\"s3ObjectKey\":\"AWSLogs/000000000000/Config/us-east-1/2016/2/24/ConfigSnapshot/000000000000_Config_us-east-1_ConfigSnapshot_20160224T182319Z_00000000-0000-0000-0000-000000000000.json.gz\",\"s3Bucket\":\"config-bucket\",\"notificationCreationTime\":\"2016-02-24T18:23:20.328Z\",\"messageType\":\"ConfigurationSnapshotDeliveryCompleted\",\"recordVersion\":\"1.1\"}";

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void ConfigTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("config-event.json"))
            {
                var configEvent = serializer.Deserialize<ConfigEvent>(fileStream);
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
        }

        private static void Handle(ConfigEvent configEvent)
        {
            Console.WriteLine($"AWS Config rule - {configEvent.ConfigRuleName}");
            Console.WriteLine($"Invoking event JSON - {configEvent.InvokingEvent}");
            Console.WriteLine($"Event version - {configEvent.Version}");
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void ConnectContactFlowTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("connect-contactflow-event.json"))
            {
                var contactFlowEvent = serializer.Deserialize<ContactFlowEvent>(fileStream);
                Assert.Equal(contactFlowEvent.Name, "ContactFlowEvent");
                Assert.NotNull(contactFlowEvent.Details);

                Assert.NotNull(contactFlowEvent.Details.ContactData);
                Assert.NotNull(contactFlowEvent.Details.ContactData.Attributes);
                Assert.Equal(contactFlowEvent.Details.ContactData.Attributes.Count, 0);
                Assert.Equal(contactFlowEvent.Details.ContactData.Channel, "VOICE");
                Assert.Equal(contactFlowEvent.Details.ContactData.ContactId, "4a573372-1f28-4e26-b97b-XXXXXXXXXXX");
                Assert.NotNull(contactFlowEvent.Details.ContactData.CustomerEndpoint);
                Assert.Equal(contactFlowEvent.Details.ContactData.CustomerEndpoint.Address, "+1234567890");
                Assert.Equal(contactFlowEvent.Details.ContactData.CustomerEndpoint.Type, "TELEPHONE_NUMBER");
                Assert.Equal(contactFlowEvent.Details.ContactData.InitialContactId, "4a573372-1f28-4e26-b97b-XXXXXXXXXXX");
                Assert.Equal(contactFlowEvent.Details.ContactData.InitiationMethod, "INBOUND | OUTBOUND | TRANSFER | CALLBACK");
                Assert.Equal(contactFlowEvent.Details.ContactData.InstanceARN, "arn:aws:connect:aws-region:1234567890:instance/c8c0e68d-2200-4265-82c0-XXXXXXXXXX");
                Assert.Equal(contactFlowEvent.Details.ContactData.PreviousContactId, "4a573372-1f28-4e26-b97b-XXXXXXXXXXX");
                Assert.NotNull(contactFlowEvent.Details.ContactData.Queue);
                Assert.Equal(contactFlowEvent.Details.ContactData.Queue.Arn, "arn:aws:connect:eu-west-2:111111111111:instance/cccccccc-bbbb-dddd-eeee-ffffffffffff/queue/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
                Assert.Equal(contactFlowEvent.Details.ContactData.Queue.Name, "PasswordReset");
                Assert.NotNull(contactFlowEvent.Details.ContactData.SystemEndpoint);
                Assert.Equal(contactFlowEvent.Details.ContactData.SystemEndpoint.Address, "+1234567890");
                Assert.Equal(contactFlowEvent.Details.ContactData.SystemEndpoint.Type, "TELEPHONE_NUMBER");

                Assert.NotNull(contactFlowEvent.Details.Parameters);
                Assert.Equal(contactFlowEvent.Details.Parameters.Count, 1);
                Assert.Equal(contactFlowEvent.Details.Parameters["sentAttributeKey"], "sentAttributeValue");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void SimpleEmailTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("simple-email-event-lambda.json"))
            {
                var sesEvent = serializer.Deserialize<SimpleEmailEvent<SimpleEmailEvents.Actions.LambdaReceiptAction>>(fileStream);

                Assert.Equal(sesEvent.Records.Count, 1);
                var record = sesEvent.Records[0];

                Assert.Equal(record.EventVersion, "1.0");
                Assert.Equal(record.EventSource, "aws:ses");

                Assert.Equal(record.Ses.Mail.CommonHeaders.From.Count, 1);
                Assert.Equal(record.Ses.Mail.CommonHeaders.From[0], "Amazon Web Services <aws@amazon.com>");
                Assert.Equal(record.Ses.Mail.CommonHeaders.To.Count, 1);
                Assert.Equal(record.Ses.Mail.CommonHeaders.To[0], "lambda@amazon.com");
                Assert.Equal(record.Ses.Mail.CommonHeaders.ReturnPath, "aws@amazon.com");
                Assert.Equal(record.Ses.Mail.CommonHeaders.MessageId, "<CAEddw6POFV_On91m+ZoL_SN8B_M2goDe_Ni355owhc7QSjPQSQ@amazon.com>");
                Assert.Equal(record.Ses.Mail.CommonHeaders.Date, "Mon, 5 Dec 2016 18:40:08 -0800");
                Assert.Equal(record.Ses.Mail.CommonHeaders.Subject, "Test Subject");
                Assert.Equal(record.Ses.Mail.Source, "aws@amazon.com");
                Assert.Equal(record.Ses.Mail.Timestamp.ToUniversalTime(), DateTime.Parse("2016-12-06T02:40:08.000Z").ToUniversalTime());
                Assert.Equal(record.Ses.Mail.Destination.Count, 1);
                Assert.Equal(record.Ses.Mail.Destination[0], "lambda@amazon.com");
                Assert.Equal(record.Ses.Mail.Headers.Count, 10);
                Assert.Equal(record.Ses.Mail.Headers[0].Name, "Return-Path");
                Assert.Equal(record.Ses.Mail.Headers[0].Value, "<aws@amazon.com>");
                Assert.Equal(record.Ses.Mail.Headers[1].Name, "Received");
                Assert.Equal(record.Ses.Mail.Headers[1].Value, "from mx.amazon.com (mx.amazon.com [127.0.0.1]) by inbound-smtp.us-east-1.amazonaws.com with SMTP id 6n4thuhcbhpfiuf25gshf70rss364fuejrvmqko1 for lambda@amazon.com; Tue, 06 Dec 2016 02:40:10 +0000 (UTC)");
                Assert.Equal(record.Ses.Mail.Headers[2].Name, "DKIM-Signature");
                Assert.Equal(record.Ses.Mail.Headers[2].Value, "v=1; a=rsa-sha256; c=relaxed/relaxed; d=iatn.net; s=amazon; h=mime-version:from:date:message-id:subject:to; bh=chlJxa/vZ11+0O9lf4tKDM/CcPjup2nhhdITm+hSf3c=; b=SsoNPK0wX7umtWnw8pln3YSib+E09XO99d704QdSc1TR1HxM0OTti/UaFxVD4e5b0+okBqo3rgVeWgNZ0sWZEUhBaZwSL3kTd/nHkcPexeV0XZqEgms1vmbg75F6vlz9igWflO3GbXyTRBNMM0gUXKU/686hpVW6aryEIfM/rLY=");
                Assert.Equal(record.Ses.Mail.Headers[3].Name, "MIME-Version");
                Assert.Equal(record.Ses.Mail.Headers[3].Value, "1.0");
                Assert.Equal(record.Ses.Mail.Headers[4].Name, "From");
                Assert.Equal(record.Ses.Mail.Headers[4].Value, "Amazon Web Services <aws@amazon.com>");
                Assert.Equal(record.Ses.Mail.Headers[5].Name, "Date");
                Assert.Equal(record.Ses.Mail.Headers[5].Value, "Mon, 5 Dec 2016 18:40:08 -0800");
                Assert.Equal(record.Ses.Mail.Headers[6].Name, "Message-ID");
                Assert.Equal(record.Ses.Mail.Headers[6].Value, "<CAEddw6POFV_On91m+ZoL_SN8B_M2goDe_Ni355owhc7QSjPQSQ@amazon.com>");
                Assert.Equal(record.Ses.Mail.Headers[7].Name, "Subject");
                Assert.Equal(record.Ses.Mail.Headers[7].Value, "Test Subject");
                Assert.Equal(record.Ses.Mail.Headers[8].Name, "To");
                Assert.Equal(record.Ses.Mail.Headers[8].Value, "lambda@amazon.com");
                Assert.Equal(record.Ses.Mail.Headers[9].Name, "Content-Type");
                Assert.Equal(record.Ses.Mail.Headers[9].Value, "multipart/alternative; boundary=94eb2c0742269658b10542f452a9");
                Assert.Equal(record.Ses.Mail.HeadersTruncated, false);
                Assert.Equal(record.Ses.Mail.MessageId, "6n4thuhcbhpfiuf25gshf70rss364fuejrvmqko1");

                Assert.Equal(record.Ses.Receipt.Recipients.Count, 1);
                Assert.Equal(record.Ses.Receipt.Recipients[0], "lambda@amazon.com");
                Assert.Equal(record.Ses.Receipt.Timestamp.ToUniversalTime(), DateTime.Parse("2016-12-06T02:40:08.000Z").ToUniversalTime());
                Assert.Equal(record.Ses.Receipt.SpamVerdict.Status, "PASS");
                Assert.Equal(record.Ses.Receipt.DKIMVerdict.Status, "PASS");
                Assert.Equal(record.Ses.Receipt.SPFVerdict.Status, "PASS");
                Assert.Equal(record.Ses.Receipt.VirusVerdict.Status, "PASS");
                Assert.Equal(record.Ses.Receipt.DMARCVerdict.Status, "PASS");
                Assert.Equal(record.Ses.Receipt.ProcessingTimeMillis, 574);

                Handle(sesEvent);
            }
        }
        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void SimpleEmailLambdaActionTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("simple-email-event-lambda.json"))
            {
                var sesEvent = serializer.Deserialize<SimpleEmailEvent<SimpleEmailEvents.Actions.LambdaReceiptAction>>(fileStream);

                Assert.Equal(sesEvent.Records.Count, 1);
                var record = sesEvent.Records[0];

                Assert.Equal(record.Ses.Receipt.Action.Type, "Lambda");
                Assert.Equal(record.Ses.Receipt.Action.InvocationType, "Event");
                Assert.Equal(record.Ses.Receipt.Action.FunctionArn, "arn:aws:lambda:us-east-1:000000000000:function:my-ses-lambda-function");

                Handle(sesEvent);
            }
        }
        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void SimpleEmailS3ActionTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("simple-email-event-s3.json"))
            {
                var sesEvent = serializer.Deserialize<SimpleEmailEvent<SimpleEmailEvents.Actions.S3ReceiptAction>>(fileStream);

                Assert.Equal(sesEvent.Records.Count, 1);
                var record = sesEvent.Records[0];

                Assert.Equal(record.Ses.Receipt.Action.Type, "S3");
                Assert.Equal(record.Ses.Receipt.Action.TopicArn, "arn:aws:sns:eu-west-1:123456789:ses-email-received");
                Assert.Equal(record.Ses.Receipt.Action.BucketName, "my-ses-inbox");
                Assert.Equal(record.Ses.Receipt.Action.ObjectKeyPrefix, "important");
                Assert.Equal(record.Ses.Receipt.Action.ObjectKey, "important/fiddlyfaddlyhiddlyhoodly");

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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void SNSTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("sns-event.json"))
            {
                var snsEvent = serializer.Deserialize<SNSEvent>(fileStream);

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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void SQSTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("sqs-event.json"))
            {
                var sqsEvent = serializer.Deserialize<SQSEvent>(fileStream);

                Assert.Equal(sqsEvent.Records.Count, 1);
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void SQSBatchResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("sqs-response.json"))
            {
                var sqsBatchResponse = serializer.Deserialize<SQSBatchResponse>(fileStream);

                Assert.Equal(sqsBatchResponse.BatchItemFailures.Count, 2);
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void APIGatewayProxyRequestTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("proxy-event.json"))
            {
                var proxyEvent = serializer.Deserialize<APIGatewayProxyRequest>(fileStream);

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
                Assert.Equal(requestContext.ConnectionId, "d034bc98-beed-4fdf-9e85-11bfc15bf734");
                Assert.Equal(requestContext.DomainName, "somerandomdomain.net");
                Assert.Equal(1519166937665, requestContext.RequestTimeEpoch);
                Assert.Equal("20/Feb/2018:22:48:57 +0000", requestContext.RequestTime);

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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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

            JObject root = Newtonsoft.Json.JsonConvert.DeserializeObject(serializedJson) as JObject;
            Assert.Equal(root["statusCode"], 200);
            Assert.Equal(root["body"], "theBody");

            Assert.NotNull(root["headers"]);
            var headers = root["headers"] as JObject;
            Assert.Equal(headers["Header1"], "Value1");
            Assert.Equal(headers["Header2"], "Value2");
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void APIGatewayAuthorizerResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            var context = new APIGatewayCustomAuthorizerContextOutput();
            context["field1"] = "value1";
            context["field2"] = "value2";

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
                            Action = new HashSet<string>{ "execute-api:Invoke" },
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

            JObject root = Newtonsoft.Json.JsonConvert.DeserializeObject(serializedJson) as JObject;

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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void APIGatewayAuthorizerWithSimpleIAMConditionResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            var context = new APIGatewayCustomAuthorizerContextOutput();
            context["field1"] = "value1";
            context["field2"] = "value2";

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

            JObject root = Newtonsoft.Json.JsonConvert.DeserializeObject(serializedJson) as JObject;

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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void APIGatewayAuthorizerWithMultiValueIAMConditionResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            var context = new APIGatewayCustomAuthorizerContextOutput();
            context["field1"] = "value1";
            context["field2"] = "value2";

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

            JObject root = Newtonsoft.Json.JsonConvert.DeserializeObject(serializedJson) as JObject;

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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
                Assert.Equal(headers["HeaderAuth1"], "headerValue1");
                Assert.Equal(headers["Host"], "lg10ltpf4f.execute-api.us-east-2.amazonaws.com");
                Assert.Equal(headers["Sec-WebSocket-Extensions"], "permessage-deflate; client_max_window_bits");
                Assert.Equal(headers["Sec-WebSocket-Key"], "BvlrrFKoKAPDYOlwBcGKWw==");
                Assert.Equal(headers["Sec-WebSocket-Version"], "13");
                Assert.Equal(headers["X-Amzn-Trace-Id"], "Root=1-625d9ad1-37a5d33a61dd9be33ae3a247");
                Assert.Equal(headers["X-Forwarded-For"], "52.95.4.0");
                Assert.Equal(headers["X-Forwarded-Port"], "443");
                Assert.Equal(headers["X-Forwarded-Proto"], "https");

                var multiValueHeaders = proxyEvent.MultiValueHeaders;
                Assert.Equal(multiValueHeaders["HeaderAuth1"].Count, 1);
                Assert.Equal(multiValueHeaders["HeaderAuth1"][0], "headerValue1");
                Assert.Equal(multiValueHeaders["Host"].Count, 1);
                Assert.Equal(multiValueHeaders["Host"][0], "lg10ltpf4f.execute-api.us-east-2.amazonaws.com");
                Assert.Equal(multiValueHeaders["Sec-WebSocket-Extensions"].Count, 1);
                Assert.Equal(multiValueHeaders["Sec-WebSocket-Extensions"][0], "permessage-deflate; client_max_window_bits");
                Assert.Equal(multiValueHeaders["Sec-WebSocket-Key"].Count, 1);
                Assert.Equal(multiValueHeaders["Sec-WebSocket-Key"][0], "BvlrrFKoKAPDYOlwBcGKWw==");
                Assert.Equal(multiValueHeaders["Sec-WebSocket-Version"].Count, 1);
                Assert.Equal(multiValueHeaders["Sec-WebSocket-Version"][0], "13");
                Assert.Equal(multiValueHeaders["X-Amzn-Trace-Id"].Count, 1);
                Assert.Equal(multiValueHeaders["X-Amzn-Trace-Id"][0], "Root=1-625d9ad1-37a5d33a61dd9be33ae3a247");
                Assert.Equal(multiValueHeaders["X-Forwarded-For"].Count, 1);
                Assert.Equal(multiValueHeaders["X-Forwarded-For"][0], "52.95.4.0");
                Assert.Equal(multiValueHeaders["X-Forwarded-Port"].Count, 1);
                Assert.Equal(multiValueHeaders["X-Forwarded-Port"][0], "443");
                Assert.Equal(multiValueHeaders["X-Forwarded-Proto"].Count, 1);
                Assert.Equal(multiValueHeaders["X-Forwarded-Proto"][0], "https");

                var requestContext = proxyEvent.RequestContext;
                Assert.Equal(requestContext.RouteKey, "$connect");
                Assert.Equal(requestContext.EventType, "CONNECT");
                Assert.Equal(requestContext.ExtendedRequestId, "QyUg1HJgCYcFvbw=");
                Assert.Equal(requestContext.RequestTime, "18/Apr/2022:17:07:29 +0000");
                Assert.Equal(requestContext.MessageDirection, "IN");
                Assert.Equal(requestContext.Stage, "production");
                Assert.Equal(requestContext.ConnectedAt, 1650301649973);
                Assert.Equal(requestContext.RequestTimeEpoch, 1650301649973);
                Assert.Equal(requestContext.RequestId, "QyUg1HJgCYcFvbw=");
                Assert.Equal(requestContext.DomainName, "lg10ltpf4f.execute-api.us-east-2.amazonaws.com");
                Assert.Equal(requestContext.ConnectionId, "QyUg1czHCYcCHXw=");
                Assert.Equal(requestContext.ApiId, "lg10ltpf4f");

                Assert.False(proxyEvent.IsBase64Encoded);

                var identity = requestContext.Identity;
                Assert.Equal(identity.SourceIp, "52.95.4.0");
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void ApplicationLoadBalancerRequestSingleValueTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("alb-request-single-value.json"))
            {
                var evnt = serializer.Deserialize<ApplicationLoadBalancerRequest>(fileStream);

                Assert.Equal(evnt.Path, "/");
                Assert.Equal(evnt.HttpMethod, "GET");
                Assert.Equal(evnt.Body, "not really base64");
                Assert.True(evnt.IsBase64Encoded);

                Assert.Equal(2, evnt.QueryStringParameters.Count);
                Assert.Equal("value1", evnt.QueryStringParameters["query1"]);
                Assert.Equal("value2", evnt.QueryStringParameters["query2"]);

                Assert.Equal("value1", evnt.Headers["head1"]);
                Assert.Equal("value2", evnt.Headers["head2"]);


                var requestContext = evnt.RequestContext;
                Assert.Equal(requestContext.Elb.TargetGroupArn, "arn:aws:elasticloadbalancing:region:123456789012:targetgroup/my-target-group/6d0ecf831eec9f09");
            }
        }


        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void ApplicationLoadBalancerRequestMultiValueTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("alb-request-multi-value.json"))
            {
                var evnt = serializer.Deserialize<ApplicationLoadBalancerRequest>(fileStream);

                Assert.Equal(evnt.Path, "/");
                Assert.Equal(evnt.HttpMethod, "GET");
                Assert.Equal(evnt.Body, "not really base64");
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
                Assert.Equal(requestContext.Elb.TargetGroupArn, "arn:aws:elasticloadbalancing:region:123456789012:targetgroup/my-target-group/6d0ecf831eec9f09");
            }
        }


        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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

            JObject root = Newtonsoft.Json.JsonConvert.DeserializeObject(serializedJson) as JObject;

            Assert.Equal("h1-value1", root["headers"]["Head1"]);
            Assert.Equal("h2-value1", root["headers"]["Head2"]);

            Assert.True((bool)root["isBase64Encoded"]);
            Assert.Equal("not really base64", (string)root["body"]);
            Assert.Equal(200, (int)root["statusCode"]);
            Assert.Equal("200 OK", (string)root["statusDescription"]);
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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

            Assert.Equal(1, root["multiValueHeaders"]["Head1"].Count());
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
                Assert.Equal(1, lexResponse.DialogAction.ResponseCard.GenericAttachments.Count);
                Assert.Equal("card-title", lexResponse.DialogAction.ResponseCard.GenericAttachments[0].Title);
                Assert.Equal("card-sub-title", lexResponse.DialogAction.ResponseCard.GenericAttachments[0].SubTitle);
                Assert.Equal("URL of the image to be shown", lexResponse.DialogAction.ResponseCard.GenericAttachments[0].ImageUrl);
                Assert.Equal("URL of the attachment to be associated with the card", lexResponse.DialogAction.ResponseCard.GenericAttachments[0].AttachmentLinkUrl);
                Assert.Equal(1, lexResponse.DialogAction.ResponseCard.GenericAttachments[0].Buttons.Count);
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
                Assert.Equal(1, lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Value.ResolvedValues.Count);
                Assert.Equal("action value", lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Value.ResolvedValues[0]);
                Assert.Equal(1, lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Values.Count);
                Assert.Equal("Scalar", lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Values[0].Shape);
                Assert.Equal("Action Value", lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Values[0].Value.OriginalValue);
                Assert.Equal("Action Value", lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Values[0].Value.InterpretedValue);
                Assert.Equal(1, lexV2Event.Interpretations[0].Intent.Slots["ActionType"].Values[0].Value.ResolvedValues.Count);
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
                Assert.Equal(0, lexV2Event.Interpretations[1].Intent.Slots.Count);

                Assert.Equal("ActionDate", lexV2Event.ProposedNextState.DialogAction.SlotToElicit);
                Assert.Equal("ConfirmIntent", lexV2Event.ProposedNextState.DialogAction.Type);
                Assert.Equal("NextIntent", lexV2Event.ProposedNextState.Intent.Name);
                Assert.Equal("None", lexV2Event.ProposedNextState.Intent.ConfirmationState);
                Assert.Equal(0, lexV2Event.ProposedNextState.Intent.Slots.Count);
                Assert.Equal("Waiting", lexV2Event.ProposedNextState.Intent.State);

                Assert.Equal(2, lexV2Event.RequestAttributes.Count);
                Assert.Equal("value1", lexV2Event.RequestAttributes["key1"]);
                Assert.Equal("value2", lexV2Event.RequestAttributes["key2"]);

                Assert.Equal(1, lexV2Event.SessionState.ActiveContexts.Count);
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
                Assert.Equal(1, lexV2Event.SessionState.Intent.Slots["ActionType"].Value.ResolvedValues.Count);
                Assert.Equal("action value", lexV2Event.SessionState.Intent.Slots["ActionType"].Value.ResolvedValues[0]);
                Assert.Equal(1, lexV2Event.SessionState.Intent.Slots["ActionType"].Values.Count);
                Assert.Equal("Scalar", lexV2Event.SessionState.Intent.Slots["ActionType"].Values[0].Shape);
                Assert.Equal("Action Value", lexV2Event.SessionState.Intent.Slots["ActionType"].Values[0].Value.OriginalValue);
                Assert.Equal("Action Value", lexV2Event.SessionState.Intent.Slots["ActionType"].Values[0].Value.InterpretedValue);
                Assert.Equal(1, lexV2Event.SessionState.Intent.Slots["ActionType"].Values[0].Value.ResolvedValues.Count);
                Assert.Equal("action value", lexV2Event.SessionState.Intent.Slots["ActionType"].Values[0].Value.ResolvedValues[0]);
                Assert.Null(lexV2Event.SessionState.Intent.Slots["ActionType"].Values[0].Values);
                Assert.Null(lexV2Event.SessionState.Intent.Slots["ActionDate"]);
                Assert.Null(lexV2Event.SessionState.Intent.Slots["ActionTime"]);
                Assert.Equal("InProgress", lexV2Event.SessionState.Intent.State);
                Assert.Equal("None", lexV2Event.SessionState.Intent.ConfirmationState);
                Assert.Equal("85f22c97-b5d3-4a74-9e3d-95446768ecaa", lexV2Event.SessionState.OriginatingRequestId);
                Assert.Equal(1, lexV2Event.SessionState.RuntimeHints.SlotHints.Count);
                Assert.Equal(1, lexV2Event.SessionState.RuntimeHints.SlotHints["hint1"].Count);
                Assert.Equal(2, lexV2Event.SessionState.RuntimeHints.SlotHints["hint1"]["detail1"].RuntimeHintValues.Count);
                Assert.Equal("hintvalue1_1", lexV2Event.SessionState.RuntimeHints.SlotHints["hint1"]["detail1"].RuntimeHintValues[0].Phrase);
                Assert.Equal("hintvalue1_2", lexV2Event.SessionState.RuntimeHints.SlotHints["hint1"]["detail1"].RuntimeHintValues[1].Phrase);
                Assert.Equal(2, lexV2Event.SessionState.SessionAttributes.Count);
                Assert.Equal("sessionvalue1", lexV2Event.SessionState.SessionAttributes["sessionattribute1"]);
                Assert.Equal("sessionvalue2", lexV2Event.SessionState.SessionAttributes["sessionattribute2"]);

                Assert.Equal(1, lexV2Event.Transcriptions.Count);
                Assert.Equal("testtranscription", lexV2Event.Transcriptions[0].Transcription);
                Assert.Equal(0.8, lexV2Event.Transcriptions[0].TranscriptionConfidence);
                Assert.Equal("TestAction", lexV2Event.Transcriptions[0].ResolvedContext.Intent);
                Assert.Equal(1, lexV2Event.Transcriptions[0].ResolvedSlots.Count);
                Assert.Equal("List", lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Shape);
                Assert.Equal("Action Value", lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Value.OriginalValue);
                Assert.Equal("Action Value", lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Value.InterpretedValue);
                Assert.Equal(1, lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Value.ResolvedValues.Count);
                Assert.Equal("action value", lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Value.ResolvedValues[0]);
                Assert.Equal(1, lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Values.Count);
                Assert.Equal("Scalar", lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Values[0].Shape);
                Assert.Equal("Action Value", lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Values[0].Value.OriginalValue);
                Assert.Equal("Action Value", lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Values[0].Value.InterpretedValue);
                Assert.Equal(1, lexV2Event.Transcriptions[0].ResolvedSlots["ActionType"].Values[0].Value.ResolvedValues.Count);
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

                Assert.Equal(1, lexV2Response.Messages.Count);
                Assert.Equal("Test Content", lexV2Response.Messages[0].Content);
                Assert.Equal("ImageResponseCard", lexV2Response.Messages[0].ContentType);
                Assert.Equal(1, lexV2Response.Messages[0].ImageResponseCard.Buttons.Count);
                Assert.Equal("Take Action", lexV2Response.Messages[0].ImageResponseCard.Buttons[0].Text);
                Assert.Equal("takeaction", lexV2Response.Messages[0].ImageResponseCard.Buttons[0].Value);
                Assert.Equal("http://somedomain.com/testimage.png", lexV2Response.Messages[0].ImageResponseCard.ImageUrl);
                Assert.Equal("Click button to take action", lexV2Response.Messages[0].ImageResponseCard.Subtitle);
                Assert.Equal("Take Action", lexV2Response.Messages[0].ImageResponseCard.Title);
                Assert.Equal(1, lexV2Response.SessionState.ActiveContexts.Count);
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
                Assert.Equal(1, lexV2Response.SessionState.Intent.Slots["ActionType"].Value.ResolvedValues.Count);
                Assert.Equal("action value", lexV2Response.SessionState.Intent.Slots["ActionType"].Value.ResolvedValues[0]);
                Assert.Equal(1, lexV2Response.SessionState.Intent.Slots["ActionType"].Values.Count);
                Assert.Equal("Scalar", lexV2Response.SessionState.Intent.Slots["ActionType"].Values[0].Shape);
                Assert.Equal("Action Value", lexV2Response.SessionState.Intent.Slots["ActionType"].Values[0].Value.OriginalValue);
                Assert.Equal("Action Value", lexV2Response.SessionState.Intent.Slots["ActionType"].Values[0].Value.InterpretedValue);
                Assert.Equal(1, lexV2Response.SessionState.Intent.Slots["ActionType"].Values[0].Value.ResolvedValues.Count);
                Assert.Equal("action value", lexV2Response.SessionState.Intent.Slots["ActionType"].Values[0].Value.ResolvedValues[0]);
                Assert.Null(lexV2Response.SessionState.Intent.Slots["ActionType"].Values[0].Values);
                Assert.Null(lexV2Response.SessionState.Intent.Slots["ActionDate"]);
                Assert.Null(lexV2Response.SessionState.Intent.Slots["ActionTime"]);
                Assert.Equal("InProgress", lexV2Response.SessionState.Intent.State);
                Assert.Equal("None", lexV2Response.SessionState.Intent.ConfirmationState);
                Assert.Equal("85f22c97-b5d3-4a74-9e3d-95446768ecaa", lexV2Response.SessionState.OriginatingRequestId);
                Assert.Equal(1, lexV2Response.SessionState.RuntimeHints.SlotHints.Count);
                Assert.Equal(1, lexV2Response.SessionState.RuntimeHints.SlotHints["hint1"].Count);
                Assert.Equal(2, lexV2Response.SessionState.RuntimeHints.SlotHints["hint1"]["detail1"].RuntimeHintValues.Count);
                Assert.Equal("hintvalue1_1", lexV2Response.SessionState.RuntimeHints.SlotHints["hint1"]["detail1"].RuntimeHintValues[0].Phrase);
                Assert.Equal("hintvalue1_2", lexV2Response.SessionState.RuntimeHints.SlotHints["hint1"]["detail1"].RuntimeHintValues[1].Phrase);
                Assert.Equal(2, lexV2Response.SessionState.SessionAttributes.Count);
                Assert.Equal("sessionvalue1", lexV2Response.SessionState.SessionAttributes["sessionattribute1"]);
                Assert.Equal("sessionvalue2", lexV2Response.SessionState.SessionAttributes["sessionattribute2"]);
                Assert.Equal(2, lexV2Response.RequestAttributes.Count);
                Assert.Equal("value1", lexV2Response.RequestAttributes["key1"]);
                Assert.Equal("value2", lexV2Response.RequestAttributes["key2"]);

                MemoryStream ms = new MemoryStream();
                serializer.Serialize<LexV2Response>(lexV2Response, ms);
                ms.Position = 0;
                var json = new StreamReader(ms).ReadToEnd();

                var original = JObject.Parse(File.ReadAllText("lexv2-response.json"));
                var serialized = JObject.Parse(json);
                Assert.True(JToken.DeepEquals(serialized, original), "Serialized object is not the same as the original JSON");
            }
        }

        // Test is temporary disabled due to a bug in .NET 8 RC2
        // https://github.com/dotnet/runtime/issues/93903
#if !NET8_0
        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void KinesisFirehoseEvent(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-firehose-event.json"))
            {
                var kinesisEvent = serializer.Deserialize<KinesisFirehoseEvent>(fileStream);
                Assert.Equal("00540a87-5050-496a-84e4-e7d92bbaf5e2", kinesisEvent.InvocationId);
                Assert.Equal("arn:aws:firehose:us-east-1:AAAAAAAAAAAA:deliverystream/lambda-test", kinesisEvent.DeliveryStreamArn);
                Assert.Equal("us-east-1", kinesisEvent.Region);
                Assert.Equal(1, kinesisEvent.Records.Count);

                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisEvent.Records[0].RecordId);
                Assert.Equal("aGVsbG8gd29ybGQ=", kinesisEvent.Records[0].Base64EncodedData);
                Assert.Equal(1493276938812, kinesisEvent.Records[0].ApproximateArrivalEpoch);

            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void KinesisFirehoseResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-firehose-response.json"))
            {
                var kinesisResponse = serializer.Deserialize<KinesisFirehoseResponse>(fileStream);

                Assert.Equal(1, kinesisResponse.Records.Count);
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void KinesisAnalyticsOutputDeliveryResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-analytics-outputdelivery-response.json"))
            {
                var kinesisAnalyticsResponse = serializer.Deserialize<KinesisAnalyticsOutputDeliveryResponse>(fileStream);

                Assert.Equal(1, kinesisAnalyticsResponse.Records.Count);
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

        // Test is temporary disabled due to a bug in .NET 8 RC2
        // https://github.com/dotnet/runtime/issues/93903
#if !NET8_0
        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void KinesisAnalyticsInputProcessingEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-analytics-inputpreprocessing-event.json"))
            {
                var kinesisAnalyticsEvent = serializer.Deserialize<KinesisAnalyticsStreamsInputPreprocessingEvent>(fileStream);
                Assert.Equal("00540a87-5050-496a-84e4-e7d92bbaf5e2", kinesisAnalyticsEvent.InvocationId);
                Assert.Equal("arn:aws:kinesis:us-east-1:AAAAAAAAAAAA:stream/lambda-test", kinesisAnalyticsEvent.StreamArn);
                Assert.Equal("arn:aws:kinesisanalytics:us-east-1:12345678911:application/lambda-test", kinesisAnalyticsEvent.ApplicationArn);
                Assert.Equal(1, kinesisAnalyticsEvent.Records.Count);

                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisAnalyticsEvent.Records[0].RecordId);
                Assert.Equal("aGVsbG8gd29ybGQ=", kinesisAnalyticsEvent.Records[0].Base64EncodedData);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void KinesisAnalyticsInputProcessingResponseTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-analytics-inputpreprocessing-response.json"))
            {
                var kinesisAnalyticsResponse = serializer.Deserialize<KinesisAnalyticsInputPreprocessingResponse>(fileStream);

                Assert.Equal(1, kinesisAnalyticsResponse.Records.Count);
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

        // Test is temporary disabled due to a bug in .NET 8 RC2
        // https://github.com/dotnet/runtime/issues/93903
#if !NET8_0
        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void KinesisAnalyticsStreamsInputProcessingEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-analytics-streamsinputpreprocessing-event.json"))
            {
                var kinesisAnalyticsEvent = serializer.Deserialize<KinesisAnalyticsStreamsInputPreprocessingEvent>(fileStream);
                Assert.Equal("00540a87-5050-496a-84e4-e7d92bbaf5e2", kinesisAnalyticsEvent.InvocationId);
                Assert.Equal("arn:aws:kinesis:us-east-1:AAAAAAAAAAAA:stream/lambda-test", kinesisAnalyticsEvent.StreamArn);
                Assert.Equal("arn:aws:kinesisanalytics:us-east-1:12345678911:application/lambda-test", kinesisAnalyticsEvent.ApplicationArn);
                Assert.Equal(1, kinesisAnalyticsEvent.Records.Count);

                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisAnalyticsEvent.Records[0].RecordId);
                Assert.Equal("aGVsbG8gd29ybGQ=", kinesisAnalyticsEvent.Records[0].Base64EncodedData);

                Assert.NotNull(kinesisAnalyticsEvent.Records[0].RecordMetadata);
                Assert.Equal("shardId-000000000003", kinesisAnalyticsEvent.Records[0].RecordMetadata.ShardId);
                Assert.Equal("7400791606", kinesisAnalyticsEvent.Records[0].RecordMetadata.PartitionKey);
                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisAnalyticsEvent.Records[0].RecordMetadata.SequenceNumber);
                Assert.Equal(1520280173, kinesisAnalyticsEvent.Records[0].RecordMetadata.ApproximateArrivalEpoch);
            }
        }

        // Test is temporary disabled due to a bug in .NET 8 RC2
        // https://github.com/dotnet/runtime/issues/93903
#if !NET8_0
        [Theory]
        [InlineData(typeof(JsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void KinesisAnalyticsFirehoseInputProcessingEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kinesis-analytics-firehoseinputpreprocessing-event.json"))
            {
                var kinesisAnalyticsEvent = serializer.Deserialize<KinesisAnalyticsFirehoseInputPreprocessingEvent>(fileStream);
                Assert.Equal("00540a87-5050-496a-84e4-e7d92bbaf5e2", kinesisAnalyticsEvent.InvocationId);
                Assert.Equal("arn:aws:firehose:us-east-1:AAAAAAAAAAAA:deliverystream/lambda-test", kinesisAnalyticsEvent.StreamArn);
                Assert.Equal("arn:aws:kinesisanalytics:us-east-1:12345678911:application/lambda-test", kinesisAnalyticsEvent.ApplicationArn);
                Assert.Equal(1, kinesisAnalyticsEvent.Records.Count);

                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisAnalyticsEvent.Records[0].RecordId);
                Assert.Equal("aGVsbG8gd29ybGQ=", kinesisAnalyticsEvent.Records[0].Base64EncodedData);

                Assert.NotNull(kinesisAnalyticsEvent.Records[0].RecordMetadata);
                Assert.Equal(1520280173, kinesisAnalyticsEvent.Records[0].RecordMetadata.ApproximateArrivalEpoch);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void BatchJobStateChangeEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("batch-job-state-change-event.json"))
            {
                var jobStateChangeEvent = serializer.Deserialize<BatchJobStateChangeEvent>(fileStream);

                Assert.Equal(jobStateChangeEvent.Version, "0");
                Assert.Equal(jobStateChangeEvent.Id, "c8f9c4b5-76e5-d76a-f980-7011e206042b");
                Assert.Equal(jobStateChangeEvent.DetailType, "Batch Job State Change");
                Assert.Equal(jobStateChangeEvent.Source, "aws.batch");
                Assert.Equal(jobStateChangeEvent.Account, "aws_account_id");
                Assert.Equal(jobStateChangeEvent.Time.ToUniversalTime(), DateTime.Parse("2017-10-23T17:56:03Z").ToUniversalTime());
                Assert.Equal(jobStateChangeEvent.Region, "us-east-1");
                Assert.Equal(jobStateChangeEvent.Resources.Count, 1);
                Assert.Equal(jobStateChangeEvent.Resources[0], "arn:aws:batch:us-east-1:aws_account_id:job/4c7599ae-0a82-49aa-ba5a-4727fcce14a8");
                Assert.IsType(typeof(Job), jobStateChangeEvent.Detail);
                Assert.Equal(jobStateChangeEvent.Detail.JobName, "event-test");
                Assert.Equal(jobStateChangeEvent.Detail.JobId, "4c7599ae-0a82-49aa-ba5a-4727fcce14a8");
                Assert.Equal(jobStateChangeEvent.Detail.JobQueue, "arn:aws:batch:us-east-1:aws_account_id:job-queue/HighPriority");
                Assert.Equal(jobStateChangeEvent.Detail.Status, "RUNNABLE");
                Assert.Equal(jobStateChangeEvent.Detail.Attempts.Count, 0);
                Assert.Equal(jobStateChangeEvent.Detail.CreatedAt, 1508781340401);
                Assert.Equal(jobStateChangeEvent.Detail.RetryStrategy.Attempts, 1);
                Assert.Equal(jobStateChangeEvent.Detail.RetryStrategy.EvaluateOnExit.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.RetryStrategy.EvaluateOnExit[0].Action, "EXIT");
                Assert.Equal(jobStateChangeEvent.Detail.RetryStrategy.EvaluateOnExit[0].OnExitCode, "*");
                Assert.Equal(jobStateChangeEvent.Detail.RetryStrategy.EvaluateOnExit[0].OnReason, "*");
                Assert.Equal(jobStateChangeEvent.Detail.RetryStrategy.EvaluateOnExit[0].OnStatusReason, "*");
                Assert.Equal(jobStateChangeEvent.Detail.DependsOn.Count, 0);
                Assert.Equal(jobStateChangeEvent.Detail.JobDefinition, "arn:aws:batch:us-east-1:aws_account_id:job-definition/first-run-job-definition:1");
                Assert.Equal(jobStateChangeEvent.Detail.Parameters.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.Parameters["test"], "abc");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Image, "busybox");
                Assert.NotNull(jobStateChangeEvent.Detail.Container.ResourceRequirements);
                Assert.Equal(jobStateChangeEvent.Detail.Container.ResourceRequirements.Count, 2);
                Assert.Equal(jobStateChangeEvent.Detail.Container.ResourceRequirements[0].Type, "MEMORY");
                Assert.Equal(jobStateChangeEvent.Detail.Container.ResourceRequirements[0].Value, "2000");
                Assert.Equal(jobStateChangeEvent.Detail.Container.ResourceRequirements[1].Type, "VCPU");
                Assert.Equal(jobStateChangeEvent.Detail.Container.ResourceRequirements[1].Value, "2");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Vcpus, 2);
                Assert.Equal(jobStateChangeEvent.Detail.Container.Memory, 2000);
                Assert.Equal(jobStateChangeEvent.Detail.Container.Command.Count, 2);
                Assert.Equal(jobStateChangeEvent.Detail.Container.Command[0], "echo");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Command[1], "'hello world'");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Volumes.Count, 2);
                Assert.Equal(jobStateChangeEvent.Detail.Container.Volumes[0].Name, "myhostsource");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Volumes[0].Host.SourcePath, "/data");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Volumes[1].Name, "efs");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Volumes[1].EfsVolumeConfiguration.AuthorizationConfig.AccessPointId, "fsap-XXXXXXXXXXXXXXXXX");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Volumes[1].EfsVolumeConfiguration.AuthorizationConfig.Iam, "ENABLED");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Volumes[1].EfsVolumeConfiguration.FileSystemId, "fs-XXXXXXXXX");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Volumes[1].EfsVolumeConfiguration.RootDirectory, "/");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Volumes[1].EfsVolumeConfiguration.TransitEncryption, "ENABLED");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Volumes[1].EfsVolumeConfiguration.TransitEncryptionPort, 12345);
                Assert.NotNull(jobStateChangeEvent.Detail.Container.Environment);
                Assert.Equal(jobStateChangeEvent.Detail.Container.Environment.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.Container.Environment[0].Name, "MANAGED_BY_AWS");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Environment[0].Value, "STARTED_BY_STEP_FUNCTIONS");
                Assert.Equal(jobStateChangeEvent.Detail.Container.MountPoints.Count, 2);
                Assert.Equal(jobStateChangeEvent.Detail.Container.MountPoints[0].ContainerPath, "/data");
                Assert.Equal(jobStateChangeEvent.Detail.Container.MountPoints[0].ReadOnly, true);
                Assert.Equal(jobStateChangeEvent.Detail.Container.MountPoints[0].SourceVolume, "myhostsource");
                Assert.Equal(jobStateChangeEvent.Detail.Container.MountPoints[1].ContainerPath, "/mount/efs");
                Assert.Equal(jobStateChangeEvent.Detail.Container.MountPoints[1].SourceVolume, "efs");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Ulimits.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.Container.Ulimits[0].HardLimit, 2048);
                Assert.Equal(jobStateChangeEvent.Detail.Container.Ulimits[0].Name, "nofile");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Ulimits[0].SoftLimit, 2048);
                Assert.NotNull(jobStateChangeEvent.Detail.Container.LinuxParameters);
                Assert.Equal(jobStateChangeEvent.Detail.Container.LinuxParameters.Devices.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.Container.LinuxParameters.Devices[0].ContainerPath, "/dev/sda");
                Assert.Equal(jobStateChangeEvent.Detail.Container.LinuxParameters.Devices[0].HostPath, "/dev/xvdc");
                Assert.Equal(jobStateChangeEvent.Detail.Container.LinuxParameters.Devices[0].Permissions.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.Container.LinuxParameters.Devices[0].Permissions[0], "MKNOD");
                Assert.Equal(jobStateChangeEvent.Detail.Container.LinuxParameters.InitProcessEnabled, true);
                Assert.Equal(jobStateChangeEvent.Detail.Container.LinuxParameters.SharedMemorySize, 64);
                Assert.Equal(jobStateChangeEvent.Detail.Container.LinuxParameters.MaxSwap, 1024);
                Assert.Equal(jobStateChangeEvent.Detail.Container.LinuxParameters.Swappiness, 55);
                Assert.Equal(jobStateChangeEvent.Detail.Container.LinuxParameters.Tmpfs.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.Container.LinuxParameters.Tmpfs[0].ContainerPath, "/run");
                Assert.Equal(jobStateChangeEvent.Detail.Container.LinuxParameters.Tmpfs[0].Size, 65536);
                Assert.Equal(jobStateChangeEvent.Detail.Container.LinuxParameters.Tmpfs[0].MountOptions.Count, 2);
                Assert.Equal(jobStateChangeEvent.Detail.Container.LinuxParameters.Tmpfs[0].MountOptions[0], "noexec");
                Assert.Equal(jobStateChangeEvent.Detail.Container.LinuxParameters.Tmpfs[0].MountOptions[1], "nosuid");
                Assert.NotNull(jobStateChangeEvent.Detail.Container.LogConfiguration);
                Assert.Equal(jobStateChangeEvent.Detail.Container.LogConfiguration.LogDriver, "json-file");
                Assert.Equal(jobStateChangeEvent.Detail.Container.LogConfiguration.Options.Count, 2);
                Assert.Equal(jobStateChangeEvent.Detail.Container.LogConfiguration.Options["max-size"], "10m");
                Assert.Equal(jobStateChangeEvent.Detail.Container.LogConfiguration.Options["max-file"], "3");
                Assert.Equal(jobStateChangeEvent.Detail.Container.LogConfiguration.SecretOptions.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.Container.LogConfiguration.SecretOptions[0].Name, "apikey");
                Assert.Equal(jobStateChangeEvent.Detail.Container.LogConfiguration.SecretOptions[0].ValueFrom, "ddApiKey");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Secrets.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.Container.Secrets[0].Name, "DATABASE_PASSWORD");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Secrets[0].ValueFrom, "arn:aws:ssm:us-east-1:awsExampleAccountID:parameter/awsExampleParameter");
                Assert.NotNull(jobStateChangeEvent.Detail.Container.NetworkConfiguration);
                Assert.Equal(jobStateChangeEvent.Detail.Container.NetworkConfiguration.AssignPublicIp, "ENABLED");
                Assert.NotNull(jobStateChangeEvent.Detail.Container.FargatePlatformConfiguration);
                Assert.Equal(jobStateChangeEvent.Detail.Container.FargatePlatformConfiguration.PlatformVersion, "LATEST");
                Assert.NotNull(jobStateChangeEvent.Detail.NodeProperties);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.MainNode, 0);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NumNodes, 0);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].TargetNodes, "0:1");
                Assert.NotNull(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Image, "busybox");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.ResourceRequirements.Count, 2);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.ResourceRequirements[0].Type, "MEMORY");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.ResourceRequirements[0].Value, "2000");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.ResourceRequirements[1].Type, "VCPU");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.ResourceRequirements[1].Value, "2");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Vcpus, 2);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Memory, 2000);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Command.Count, 2);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Command[0], "echo");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Command[1], "'hello world'");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes.Count, 2);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[0].Name, "myhostsource");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[0].Host.SourcePath, "/data");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[1].Name, "efs");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[1].EfsVolumeConfiguration.AuthorizationConfig.AccessPointId, "fsap-XXXXXXXXXXXXXXXXX");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[1].EfsVolumeConfiguration.AuthorizationConfig.Iam, "ENABLED");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[1].EfsVolumeConfiguration.FileSystemId, "fs-XXXXXXXXX");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[1].EfsVolumeConfiguration.RootDirectory, "/");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[1].EfsVolumeConfiguration.TransitEncryption, "ENABLED");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Volumes[1].EfsVolumeConfiguration.TransitEncryptionPort, 12345);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Environment.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Environment[0].Name, "MANAGED_BY_AWS");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Environment[0].Value, "STARTED_BY_STEP_FUNCTIONS");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.MountPoints.Count, 2);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.MountPoints[0].ContainerPath, "/data");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.MountPoints[0].ReadOnly, true);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.MountPoints[0].SourceVolume, "myhostsource");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.MountPoints[1].ContainerPath, "/mount/efs");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.MountPoints[1].SourceVolume, "efs");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Ulimits.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Ulimits[0].HardLimit, 2048);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Ulimits[0].Name, "nofile");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Ulimits[0].SoftLimit, 2048);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.ExecutionRoleArn, "arn:aws:iam::awsExampleAccountID:role/awsExampleRoleName");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.InstanceType, "p3.2xlarge");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.User, "testuser");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.JobRoleArn, "arn:aws:iam::awsExampleAccountID:role/awsExampleRoleName");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Devices.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Devices[0].HostPath, "/dev/xvdc");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Devices[0].ContainerPath, "/dev/sda");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Devices[0].Permissions.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Devices[0].Permissions[0], "MKNOD");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.InitProcessEnabled, true);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.SharedMemorySize, 64);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.MaxSwap, 1024);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Swappiness, 55);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Tmpfs.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Tmpfs[0].ContainerPath, "/run");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Tmpfs[0].Size, 65536);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Tmpfs[0].MountOptions.Count, 2);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Tmpfs[0].MountOptions[0], "noexec");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LinuxParameters.Tmpfs[0].MountOptions[1], "nosuid");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LogConfiguration.LogDriver, "awslogs");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LogConfiguration.Options["awslogs-group"], "awslogs-wordpress");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LogConfiguration.Options["awslogs-stream-prefix"], "awslogs-example");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LogConfiguration.SecretOptions.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LogConfiguration.SecretOptions[0].Name, "apikey");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.LogConfiguration.SecretOptions[0].ValueFrom, "ddApiKey");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Secrets.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Secrets[0].Name, "DATABASE_PASSWORD");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.Secrets[0].ValueFrom, "arn:aws:ssm:us-east-1:awsExampleAccountID:parameter/awsExampleParameter");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.NetworkConfiguration.AssignPublicIp, "DISABLED");
                Assert.Equal(jobStateChangeEvent.Detail.NodeProperties.NodeRangeProperties[0].Container.FargatePlatformConfiguration.PlatformVersion, "LATEST");
                Assert.Equal(jobStateChangeEvent.Detail.PropagateTags, true);
                Assert.Equal(jobStateChangeEvent.Detail.Timeout.AttemptDurationSeconds, 90);
                Assert.Equal(jobStateChangeEvent.Detail.Tags.Count, 3);
                Assert.Equal(jobStateChangeEvent.Detail.Tags["Service"], "Batch");
                Assert.Equal(jobStateChangeEvent.Detail.Tags["Name"], "JobDefinitionTag");
                Assert.Equal(jobStateChangeEvent.Detail.Tags["Expected"], "MergeTag");
                Assert.Equal(jobStateChangeEvent.Detail.PlatformCapabilities.Count, 1);
                Assert.Equal(jobStateChangeEvent.Detail.PlatformCapabilities[0], "FARGATE");

                Handle(jobStateChangeEvent);
            }
        }

        private void Handle(BatchJobStateChangeEvent jobStateChangeEvent)
        {
            Console.WriteLine($"[{jobStateChangeEvent.Source} {jobStateChangeEvent.Time}] {jobStateChangeEvent.DetailType}");
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void ScheduledEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("scheduled-event.json"))
            {
                var scheduledEvent = serializer.Deserialize<ScheduledEvent>(fileStream);
                Assert.Equal(scheduledEvent.Version, "0");
                Assert.Equal(scheduledEvent.Id, "cdc73f9d-aea9-11e3-9d5a-835b769c0d9c");
                Assert.Equal(scheduledEvent.DetailType, "Scheduled Event");
                Assert.Equal(scheduledEvent.Source, "aws.events");
                Assert.Equal(scheduledEvent.Account, "123456789012");
                Assert.Equal(scheduledEvent.Time.ToUniversalTime(), DateTime.Parse("1970-01-01T00:00:00Z").ToUniversalTime());
                Assert.Equal(scheduledEvent.Region, "us-east-1");
                Assert.Equal(scheduledEvent.Resources.Count, 1);
                Assert.Equal(scheduledEvent.Resources[0], "arn:aws:events:us-east-1:123456789012:rule/my-schedule");
                Assert.IsType(typeof(Detail), scheduledEvent.Detail);

                Handle(scheduledEvent);
            }
        }

        private void Handle(ScheduledEvent scheduledEvent)
        {
            Console.WriteLine($"[{scheduledEvent.Source} {scheduledEvent.Time}] {scheduledEvent.DetailType}");
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void ECSContainerInstanceStateChangeEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("ecs-container-state-change-event.json"))
            {
                var ecsEvent = serializer.Deserialize<ECSContainerInstanceStateChangeEvent>(fileStream);

                Assert.Equal(ecsEvent.Version, "0");
                Assert.Equal(ecsEvent.Id, "8952ba83-7be2-4ab5-9c32-6687532d15a2");
                Assert.Equal(ecsEvent.DetailType, "ECS Container Instance State Change");
                Assert.Equal(ecsEvent.Source, "aws.ecs");
                Assert.Equal(ecsEvent.Account, "111122223333");
                Assert.Equal(ecsEvent.Time.ToUniversalTime(), DateTime.Parse("2016-12-06T16:41:06Z").ToUniversalTime());
                Assert.Equal(ecsEvent.Region, "us-east-1");
                Assert.Equal(ecsEvent.Resources.Count, 1);
                Assert.Equal(ecsEvent.Resources[0], "arn:aws:ecs:us-east-1:111122223333:container-instance/b54a2a04-046f-4331-9d74-3f6d7f6ca315");
                Assert.IsType(typeof(ContainerInstance), ecsEvent.Detail);
                Assert.Equal(ecsEvent.Detail.AgentConnected, true);
                Assert.Equal(ecsEvent.Detail.Attributes.Count, 14);
                Assert.Equal(ecsEvent.Detail.Attributes[0].Name, "com.amazonaws.ecs.capability.logging-driver.syslog");
                Assert.Equal(ecsEvent.Detail.ClusterArn, "arn:aws:ecs:us-east-1:111122223333:cluster/default");
                Assert.Equal(ecsEvent.Detail.ContainerInstanceArn, "arn:aws:ecs:us-east-1:111122223333:container-instance/b54a2a04-046f-4331-9d74-3f6d7f6ca315");
                Assert.Equal(ecsEvent.Detail.Ec2InstanceId, "i-f3a8506b");
                Assert.Equal(ecsEvent.Detail.RegisteredResources.Count, 4);
                Assert.Equal(ecsEvent.Detail.RegisteredResources[0].Name, "CPU");
                Assert.Equal(ecsEvent.Detail.RegisteredResources[0].Type, "INTEGER");
                Assert.Equal(ecsEvent.Detail.RegisteredResources[0].IntegerValue, 2048);
                Assert.Equal(ecsEvent.Detail.RegisteredResources[2].StringSetValue[0], "22");
                Assert.Equal(ecsEvent.Detail.RemainingResources.Count, 4);
                Assert.Equal(ecsEvent.Detail.RemainingResources[0].Name, "CPU");
                Assert.Equal(ecsEvent.Detail.RemainingResources[0].Type, "INTEGER");
                Assert.Equal(ecsEvent.Detail.RemainingResources[0].IntegerValue, 1988);
                Assert.Equal(ecsEvent.Detail.RemainingResources[2].StringSetValue[0], "22");
                Assert.Equal(ecsEvent.Detail.Status, "ACTIVE");
                Assert.Equal(ecsEvent.Detail.Version, 14801);
                Assert.Equal(ecsEvent.Detail.VersionInfo.AgentHash, "aebcbca");
                Assert.Equal(ecsEvent.Detail.VersionInfo.AgentVersion, "1.13.0");
                Assert.Equal(ecsEvent.Detail.VersionInfo.DockerVersion, "DockerVersion: 1.11.2");
                Assert.Equal(ecsEvent.Detail.UpdatedAt.ToUniversalTime(), DateTime.Parse("2016-12-06T16:41:06.991Z").ToUniversalTime());

                Handle(ecsEvent);
            }
        }


        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void ECSTaskStateChangeEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("ecs-task-state-change-event.json"))
            {
                var ecsEvent = serializer.Deserialize<ECSTaskStateChangeEvent>(fileStream);

                Assert.Equal(ecsEvent.Version, "0");
                Assert.Equal(ecsEvent.Id, "3317b2af-7005-947d-b652-f55e762e571a");
                Assert.Equal(ecsEvent.DetailType, "ECS Task State Change");
                Assert.Equal(ecsEvent.Source, "aws.ecs");
                Assert.Equal(ecsEvent.Account, "111122223333");
                Assert.Equal(ecsEvent.Time.ToUniversalTime(), DateTime.Parse("2020-01-23T17:57:58Z").ToUniversalTime());
                Assert.Equal(ecsEvent.Region, "us-west-2");
                Assert.NotNull(ecsEvent.Resources);
                Assert.Equal(ecsEvent.Resources.Count, 1);
                Assert.Equal(ecsEvent.Resources[0], "arn:aws:ecs:us-west-2:111122223333:task/FargateCluster/c13b4cb40f1f4fe4a2971f76ae5a47ad");
                Assert.NotNull(ecsEvent.Detail);
                Assert.IsType(typeof(Task), ecsEvent.Detail);

                Assert.NotNull(ecsEvent.Detail.Attachments);
                Assert.Equal(ecsEvent.Detail.Attachments.Count, 1);
                Assert.Equal(ecsEvent.Detail.Attachments[0].Id, "1789bcae-ddfb-4d10-8ebe-8ac87ddba5b8");
                Assert.Equal(ecsEvent.Detail.Attachments[0].Type, "eni");
                Assert.Equal(ecsEvent.Detail.Attachments[0].Status, "ATTACHED");
                Assert.NotNull(ecsEvent.Detail.Attachments[0].Details);
                Assert.Equal(ecsEvent.Detail.Attachments[0].Details.Count, 4);
                Assert.Equal(ecsEvent.Detail.Attachments[0].Details[0].Name, "subnetId");
                Assert.Equal(ecsEvent.Detail.Attachments[0].Details[0].Value, "subnet-abcd1234");
                Assert.Equal(ecsEvent.Detail.Attachments[0].Details[1].Name, "networkInterfaceId");
                Assert.Equal(ecsEvent.Detail.Attachments[0].Details[1].Value, "eni-abcd1234");
                Assert.Equal(ecsEvent.Detail.Attachments[0].Details[2].Name, "macAddress");
                Assert.Equal(ecsEvent.Detail.Attachments[0].Details[2].Value, "0a:98:eb:a7:29:ba");
                Assert.Equal(ecsEvent.Detail.Attachments[0].Details[3].Name, "privateIPv4Address");
                Assert.Equal(ecsEvent.Detail.Attachments[0].Details[3].Value, "10.0.0.139");

                Assert.Equal(ecsEvent.Detail.AvailabilityZone, "us-west-2c");
                Assert.Equal(ecsEvent.Detail.ClusterArn, "arn:aws:ecs:us-west-2:111122223333:cluster/FargateCluster");

                Assert.NotNull(ecsEvent.Detail.Containers);
                Assert.Equal(ecsEvent.Detail.Containers.Count, 1);
                Assert.Equal(ecsEvent.Detail.Containers[0].ContainerArn, "arn:aws:ecs:us-west-2:111122223333:container/cf159fd6-3e3f-4a9e-84f9-66cbe726af01");
                Assert.Equal(ecsEvent.Detail.Containers[0].ExitCode, 0);
                Assert.Equal(ecsEvent.Detail.Containers[0].LastStatus, "RUNNING");
                Assert.Equal(ecsEvent.Detail.Containers[0].Name, "FargateApp");
                Assert.Equal(ecsEvent.Detail.Containers[0].Image, "111122223333.dkr.ecr.us-west-2.amazonaws.com/hello-repository:latest");
                Assert.Equal(ecsEvent.Detail.Containers[0].ImageDigest, "sha256:74b2c688c700ec95a93e478cdb959737c148df3fbf5ea706abe0318726e885e6");
                Assert.Equal(ecsEvent.Detail.Containers[0].RuntimeId, "ad64cbc71c7fb31c55507ec24c9f77947132b03d48d9961115cf24f3b7307e1e");
                Assert.Equal(ecsEvent.Detail.Containers[0].TaskArn, "arn:aws:ecs:us-west-2:111122223333:task/FargateCluster/c13b4cb40f1f4fe4a2971f76ae5a47ad");
                Assert.NotNull(ecsEvent.Detail.Containers[0].NetworkInterfaces);
                Assert.Equal(ecsEvent.Detail.Containers[0].NetworkInterfaces.Count, 1);
                Assert.Equal(ecsEvent.Detail.Containers[0].NetworkInterfaces[0].AttachmentId, "1789bcae-ddfb-4d10-8ebe-8ac87ddba5b8");
                Assert.Equal(ecsEvent.Detail.Containers[0].NetworkInterfaces[0].PrivateIpv4Address, "10.0.0.139");
                Assert.Equal(ecsEvent.Detail.Containers[0].Cpu, "0");

                Assert.Equal(ecsEvent.Detail.CreatedAt.ToUniversalTime(), DateTime.Parse("2020-01-23T17:57:34.402Z").ToUniversalTime());
                Assert.Equal(ecsEvent.Detail.LaunchType, "FARGATE");
                Assert.Equal(ecsEvent.Detail.Cpu, "256");
                Assert.Equal(ecsEvent.Detail.Memory, "512");
                Assert.Equal(ecsEvent.Detail.DesiredStatus, "RUNNING");
                Assert.Equal(ecsEvent.Detail.Group, "family:sample-fargate");
                Assert.Equal(ecsEvent.Detail.LastStatus, "RUNNING");

                Assert.Equal(ecsEvent.Detail.Overrides.ContainerOverrides.Count, 1);
                Assert.Equal(ecsEvent.Detail.Overrides.ContainerOverrides[0].Name, "FargateApp");
                Assert.Equal(ecsEvent.Detail.Overrides.ContainerOverrides[0].Environment.Count, 1);
                Assert.Equal(ecsEvent.Detail.Overrides.ContainerOverrides[0].Environment[0].Name, "testname");
                Assert.Equal(ecsEvent.Detail.Overrides.ContainerOverrides[0].Environment[0].Value, "testvalue");

                Assert.Equal(ecsEvent.Detail.Connectivity, "CONNECTED");
                Assert.Equal(ecsEvent.Detail.ConnectivityAt.ToUniversalTime(), DateTime.Parse("2020-01-23T17:57:38.453Z").ToUniversalTime());
                Assert.Equal(ecsEvent.Detail.PullStartedAt.ToUniversalTime(), DateTime.Parse("2020-01-23T17:57:52.103Z").ToUniversalTime());
                Assert.Equal(ecsEvent.Detail.StartedAt.ToUniversalTime(), DateTime.Parse("2020-01-23T17:57:58.103Z").ToUniversalTime());
                Assert.Equal(ecsEvent.Detail.PullStoppedAt.ToUniversalTime(), DateTime.Parse("2020-01-23T17:57:55.103Z").ToUniversalTime());
                Assert.Equal(ecsEvent.Detail.UpdatedAt.ToUniversalTime(), DateTime.Parse("2020-01-23T17:57:58.103Z").ToUniversalTime());
                Assert.Equal(ecsEvent.Detail.TaskArn, "arn:aws:ecs:us-west-2:111122223333:task/FargateCluster/c13b4cb40f1f4fe4a2971f76ae5a47ad");
                Assert.Equal(ecsEvent.Detail.TaskDefinitionArn, "arn:aws:ecs:us-west-2:111122223333:task-definition/sample-fargate:1");
                Assert.Equal(ecsEvent.Detail.Version, 4);
                Assert.Equal(ecsEvent.Detail.PlatformVersion, "1.3.0");

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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void KafkaEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("kafka-event.json"))
            {
                var kafkaEvent = serializer.Deserialize<KafkaEvent>(fileStream);
                Assert.NotNull(kafkaEvent);
                Assert.Equal(kafkaEvent.EventSource, "aws:kafka");
                Assert.Equal(kafkaEvent.EventSourceArn, "arn:aws:kafka:us-east-1:123456789012:cluster/vpc-3432434/4834-3547-3455-9872-7929");
                Assert.Equal(kafkaEvent.BootstrapServers, "b-2.demo-cluster-1.a1bcde.c1.kafka.us-east-1.amazonaws.com:9092,b-1.demo-cluster-1.a1bcde.c1.kafka.us-east-1.amazonaws.com:9092");

                Assert.NotNull(kafkaEvent.Records);
                Assert.Equal(kafkaEvent.Records.Count, 1);

                var record = kafkaEvent.Records.FirstOrDefault();
                Assert.NotNull(record);
                Assert.Equal(record.Key, "mytopic-0");

                Assert.Equal(record.Value.Count, 1);
                var eventRecord = record.Value.FirstOrDefault();
                Assert.Equal(eventRecord.Topic, "mytopic");
                Assert.Equal(eventRecord.Partition, 12);
                Assert.Equal(eventRecord.Offset, 3043205);
                Assert.Equal(eventRecord.Timestamp, 1545084650987);
                Assert.Equal(eventRecord.TimestampType, "CREATE_TIME");

                Assert.Equal(new StreamReader(eventRecord.Value).ReadToEnd(), "Hello, this is a test.");

                Assert.Equal(eventRecord.Headers.Count, 8);
                var eventRecordHeader = eventRecord.Headers.FirstOrDefault();
                Assert.NotNull(eventRecordHeader);
                Assert.Equal(eventRecordHeader.Count, 1);
                var eventRecordHeaderValue = eventRecordHeader.FirstOrDefault();
                Assert.NotNull(eventRecordHeaderValue);
                Assert.Equal(eventRecordHeaderValue.Key, "headerKey");

                // Convert sbyte[] to byte[] array.
                var tempHeaderValueByteArray = new byte[eventRecordHeaderValue.Value.Length];
                Buffer.BlockCopy(eventRecordHeaderValue.Value, 0, tempHeaderValueByteArray, 0, tempHeaderValueByteArray.Length);

                Assert.Equal(Encoding.UTF8.GetString(tempHeaderValueByteArray), "headerValue");

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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
        public void RabbitMQEventTest(Type serializerType)
        {
            var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
            using (var fileStream = LoadJsonTestFile("amazonmq-rabbitmq.json"))
            {
                var rabbitmqEvent = serializer.Deserialize<RabbitMQEvent>(fileStream);
                Assert.NotNull(rabbitmqEvent);
                Assert.Equal("aws:rmq", rabbitmqEvent.EventSource);
                Assert.Equal("arn:aws:mq:us-west-2:112556298976:broker:pizzaBroker:b-9bcfa592-423a-4942-879d-eb284b418fc8", rabbitmqEvent.EventSourceArn);

                Assert.Equal(1, rabbitmqEvent.RmqMessagesByQueue.Count);
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
                Assert.Equal(0, secondMessage.BasicProperties.Headers.Count);
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
                            Action = new HashSet<string> { "execute-api:Invoke" },
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
        [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
#endif
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

            MemoryStream ms = new MemoryStream();
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
