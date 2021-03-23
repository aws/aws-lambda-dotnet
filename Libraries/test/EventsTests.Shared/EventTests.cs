using Amazon.Lambda.Core;

#pragma warning disable 618
namespace Amazon.Lambda.Tests
{
    using Amazon.Lambda;
    using Amazon.Lambda.Serialization.Json;
    using Amazon.Lambda.S3Events;
    using Amazon.Lambda.KinesisEvents;
    using Amazon.Lambda.DynamoDBEvents;
    using Amazon.Lambda.CognitoEvents;
    using Amazon.Lambda.ConfigEvents;
    using Amazon.Lambda.SimpleEmailEvents;
    using Amazon.Lambda.SNSEvents;
    using Amazon.Lambda.SQSEvents;
    using Amazon.Lambda.APIGatewayEvents;
    using Amazon.Lambda.ApplicationLoadBalancerEvents;
    using Amazon.Lambda.LexEvents;
    using Amazon.Lambda.KinesisFirehoseEvents;
    using Amazon.Lambda.KinesisAnalyticsEvents;

    using Amazon.Lambda.CloudWatchLogsEvents;

    using Amazon.Lambda.CloudWatchEvents.ECSEvents;
	using Amazon.Lambda.CloudWatchEvents.BatchEvents;
    using Amazon.Lambda.CloudWatchEvents.ScheduledEvents;
    using Newtonsoft.Json.Linq;

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Xunit;
    using System.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    using JsonSerializer = Amazon.Lambda.Serialization.Json.JsonSerializer;

    public class EventTest
    {
        
        // This utility method takes care of removing the BOM that System.Text.Json doesn't like.
        public MemoryStream LoadJsonTestFile(string filename)
        {
            var json = File.ReadAllText(filename);
            return new MemoryStream(UTF8Encoding.UTF8.GetBytes(json));
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP_3_1
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
#if NETCOREAPP_3_1
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
#if NETCOREAPP_3_1
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
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP_3_1        
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

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP_3_1        
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
            foreach(var datasetKVP in cognitoEvent.DatasetRecords)
            {
                var datasetName = datasetKVP.Key;
                var datasetRecord = datasetKVP.Value;

                Console.WriteLine($"[{cognitoEvent.EventType}-{datasetName}] {datasetRecord.OldValue} -> {datasetRecord.Op} -> {datasetRecord.NewValue}");
            }
        }

        String ConfigInvokingEvent = "{\"configSnapshotId\":\"00000000-0000-0000-0000-000000000000\",\"s3ObjectKey\":\"AWSLogs/000000000000/Config/us-east-1/2016/2/24/ConfigSnapshot/000000000000_Config_us-east-1_ConfigSnapshot_20160224T182319Z_00000000-0000-0000-0000-000000000000.json.gz\",\"s3Bucket\":\"config-bucket\",\"notificationCreationTime\":\"2016-02-24T18:23:20.328Z\",\"messageType\":\"ConfigurationSnapshotDeliveryCompleted\",\"recordVersion\":\"1.1\"}";

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
                Assert.Equal("fce0ea8dd236ccb3ed9b37dae260836f", record.Md5OfBody );
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
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
        }
        
        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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

                Assert.Equal(2, lexEvent.RequestAttributes.Count);
                Assert.Equal("value1", lexEvent.RequestAttributes["key1"]);
                Assert.Equal("value2", lexEvent.RequestAttributes["key2"]);

                Assert.Equal(2, lexEvent.CurrentIntent.SlotDetails.Count);
                Assert.Equal("resolved value1", lexEvent.CurrentIntent.SlotDetails["slot name1"].Resolutions[0]["value1"]);
                Assert.Equal("resolved value2", lexEvent.CurrentIntent.SlotDetails["slot name1"].Resolutions[1]["value2"]);

                Assert.Equal("resolved value1", lexEvent.CurrentIntent.SlotDetails["slot name2"].Resolutions[0]["value1"]);
                Assert.Equal("resolved value2", lexEvent.CurrentIntent.SlotDetails["slot name2"].Resolutions[1]["value2"]);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
                Assert.Equal(jobStateChangeEvent.Detail.DependsOn.Count, 0);
                Assert.Equal(jobStateChangeEvent.Detail.JobDefinition, "arn:aws:batch:us-east-1:aws_account_id:job-definition/first-run-job-definition:1");
                Assert.Equal(jobStateChangeEvent.Detail.Parameters.Count, 0);
                Assert.Equal(jobStateChangeEvent.Detail.Container.Image, "busybox");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Vcpus, 2);
                Assert.Equal(jobStateChangeEvent.Detail.Container.Memory, 2000);
                Assert.Equal(jobStateChangeEvent.Detail.Container.Command.Count, 2);
                Assert.Equal(jobStateChangeEvent.Detail.Container.Command[0], "echo");
                Assert.Equal(jobStateChangeEvent.Detail.Container.Volumes.Count, 0);
                Assert.Equal(jobStateChangeEvent.Detail.Container.Environment.Count, 0);
                Assert.Equal(jobStateChangeEvent.Detail.Container.MountPoints.Count, 0);
                Assert.Equal(jobStateChangeEvent.Detail.Container.Ulimits.Count, 0);

                Handle(jobStateChangeEvent);
            }
        }

        private void Handle(BatchJobStateChangeEvent jobStateChangeEvent)
        {
            Console.WriteLine($"[{jobStateChangeEvent.Source} {jobStateChangeEvent.Time}] {jobStateChangeEvent.DetailType}");
        }

        [Theory]
        [InlineData(typeof(JsonSerializer))]
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
#if NETCOREAPP_3_1        
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
                Assert.Equal(ecsEvent.Id, "9bcdac79-b31f-4d3d-9410-fbd727c29fab");
                Assert.Equal(ecsEvent.DetailType, "ECS Task State Change");
                Assert.Equal(ecsEvent.Source, "aws.ecs");
                Assert.Equal(ecsEvent.Account, "111122223333");
                Assert.Equal(ecsEvent.Time.ToUniversalTime(), DateTime.Parse("2016-12-06T16:41:06Z").ToUniversalTime());
                Assert.Equal(ecsEvent.Region, "us-east-1");
                Assert.Equal(ecsEvent.Resources.Count, 1);
                Assert.Equal(ecsEvent.Resources[0], "arn:aws:ecs:us-east-1:111122223333:task/b99d40b3-5176-4f71-9a52-9dbd6f1cebef");
                Assert.IsType(typeof(Task), ecsEvent.Detail);
                Assert.Equal(ecsEvent.Detail.ClusterArn, "arn:aws:ecs:us-east-1:111122223333:cluster/default");
                Assert.Equal(ecsEvent.Detail.ContainerInstanceArn, "arn:aws:ecs:us-east-1:111122223333:container-instance/b54a2a04-046f-4331-9d74-3f6d7f6ca315");
                Assert.Equal(ecsEvent.Detail.Containers.Count, 1);
                Assert.Equal(ecsEvent.Detail.Containers[0].ContainerArn, "arn:aws:ecs:us-east-1:111122223333:container/3305bea1-bd16-4217-803d-3e0482170a17");
                Assert.Equal(ecsEvent.Detail.Containers[0].ExitCode, 0);
                Assert.Equal(ecsEvent.Detail.Containers[0].LastStatus, "STOPPED");
                Assert.Equal(ecsEvent.Detail.Containers[0].Name, "xray");
                Assert.Equal(ecsEvent.Detail.Containers[0].TaskArn, "arn:aws:ecs:us-east-1:111122223333:task/b99d40b3-5176-4f71-9a52-9dbd6f1cebef");
                Assert.Equal(ecsEvent.Detail.CreatedAt.ToUniversalTime(), DateTime.Parse("2016-12-06T16:41:05.702Z").ToUniversalTime());
                Assert.Equal(ecsEvent.Detail.DesiredStatus, "RUNNING");
                Assert.Equal(ecsEvent.Detail.Group, "task-group");
                Assert.Equal(ecsEvent.Detail.LastStatus, "RUNNING");
                Assert.Equal(ecsEvent.Detail.Overrides.ContainerOverrides.Count, 1);
                Assert.Equal(ecsEvent.Detail.Overrides.ContainerOverrides[0].Name, "xray");
                Assert.Equal(ecsEvent.Detail.StartedAt.ToUniversalTime(), DateTime.Parse("2016-12-06T16:41:06.8Z").ToUniversalTime());
                Assert.Equal(ecsEvent.Detail.StartedBy, "ecs-svc/9223370556150183303");
                Assert.Equal(ecsEvent.Detail.UpdatedAt.ToUniversalTime(), DateTime.Parse("2016-12-06T16:41:06.975Z").ToUniversalTime());
                Assert.Equal(ecsEvent.Detail.TaskArn, "arn:aws:ecs:us-east-1:111122223333:task/b99d40b3-5176-4f71-9a52-9dbd6f1cebef");
                Assert.Equal(ecsEvent.Detail.TaskDefinitionArn, "arn:aws:ecs:us-east-1:111122223333:task-definition/xray:2");
                Assert.Equal(ecsEvent.Detail.Version, 4);

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

            var camelCaseString  = @"{""someValue"":12,""someOtherValue"":""abcd""}";
            var pascalCaseString = @"{""SomeValue"":12,""SomeOtherValue"":""abcd""}";

            var camelCaseObject  = serializer.Deserialize<ClassUsingPascalCase>(new MemoryStream(Encoding.ASCII.GetBytes(camelCaseString)));
            var pascalCaseObject = serializer.Deserialize<ClassUsingPascalCase>(new MemoryStream(Encoding.ASCII.GetBytes(pascalCaseString)));

            Assert.Equal(12, camelCaseObject.SomeValue);
            Assert.Equal(12, pascalCaseObject.SomeValue);
            Assert.Equal("abcd", camelCaseObject.SomeOtherValue);
            Assert.Equal("abcd", pascalCaseObject.SomeOtherValue);
        }

        class ClassUsingPascalCase
        {
            public int SomeValue { get; set; }
            public string SomeOtherValue { get; set; }
        }
    }
}
#pragma warning restore 618