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
    using Amazon.Lambda.LexEvents;
    using Amazon.Lambda.KinesisFirehoseEvents;
    using Amazon.Lambda.KinesisAnalyticsEvents;
    using Amazon.Lambda.CloudWatchLogsEvents;
    using Amazon.Lambda.CloudWatchEvents.ECSEvents;

    using Newtonsoft.Json.Linq;

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Xunit;
    using System.Linq;
    using Newtonsoft.Json;

    using JsonSerializer = Amazon.Lambda.Serialization.Json.JsonSerializer;


    public class EventTest
    {
        [Fact]
        public void S3PutTest()
        {
            using (var fileStream = File.OpenRead("s3-event.json"))
            {
                var serializer = new JsonSerializer();
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

        [Fact]
        public void CognitoTest()
        {
            using (var fileStream = File.OpenRead("cognito-event.json"))
            {
                var serializer = new JsonSerializer();
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

        String ConfigInvokingEvent = "{\"configSnapshotId\":\"00000000-0000-0000-0000-000000000000\",\"s3ObjectKey\":\"AWSLogs/000000000000/Config/us-east-1/2016/2/24/ConfigSnapshot/000000000000_Config_us-east-1_ConfigSnapshot_20160224T182319Z_00000000-0000-0000-0000-000000000000.json.gz\",\"s3Bucket\":\"config-bucket\",\"notificationCreationTime\":\"2016-02-24T18:23:20.328Z\",\"messageType\":\"ConfigurationSnapshotDeliveryCompleted\",\"recordVersion\":\"1.1\"}";

        [Fact]
        public void ConfigTest()
        {
            using (var fileStream = File.OpenRead("config-event.json"))
            {
                var serializer = new JsonSerializer();
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

        [Fact]
        public void SimpleEmailTest()
        {
            using (var fileStream = File.OpenRead("simple-email-event.json"))
            {
                var serializer = new JsonSerializer();
                var sesEvent = serializer.Deserialize<SimpleEmailEvent>(fileStream);

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
                Assert.Equal(record.Ses.Receipt.ProcessingTimeMillis, 574);
                Assert.Equal(record.Ses.Receipt.Action.Type, "Lambda");
                Assert.Equal(record.Ses.Receipt.Action.InvocationType, "Event");
                Assert.Equal(record.Ses.Receipt.Action.FunctionArn, "arn:aws:lambda:us-east-1:000000000000:function:my-ses-lambda-function");

                Handle(sesEvent);
            }
        }
        private static void Handle(SimpleEmailEvent sesEvent)
        {
            foreach (var record in sesEvent.Records)
            {
                var sesRecord = record.Ses;
                Console.WriteLine($"[{record.EventSource} {sesRecord.Mail.Timestamp}] Subject = {sesRecord.Mail.CommonHeaders.Subject}");
            }
        }

        [Fact]
        public void SNSTest()
        {
            using (var fileStream = File.OpenRead("sns-event.json"))
            {
                var serializer = new JsonSerializer();
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

        [Fact]
        public void SQSTest()
        {
            using (var fileStream = File.OpenRead("sqs-event.json"))
            {
                var serializer = new JsonSerializer();
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

        [Fact]
        public void APIGatewayProxyRequestTest()
        {
            using (var fileStream = File.OpenRead("proxy-event.json"))
            {
                var serializer = new JsonSerializer();
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

        [Fact]
        public void APIGatewayAuthorizerResponseTest()
        {
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
                var serializer = new JsonSerializer();
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


        [Fact]
        public void LexEvent()
        {
            using (var fileStream = File.OpenRead("lex-event.json"))
            {
                var serializer = new JsonSerializer();
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

        [Fact]
        public void LexResponse()
        {
            using (var fileStream = File.OpenRead("lex-response.json"))
            {
                var serializer = new JsonSerializer();
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

        [Fact]
        public void KinesisFirehoseEvent()
        {
            using (var fileStream = File.OpenRead("kinesis-firehose-event.json"))
            {
                var serializer = new JsonSerializer();
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

        [Fact]
        public void KinesisFirehoseResponseTest()
        {
            using (var fileStream = File.OpenRead("kinesis-firehose-response.json"))
            {
                var serializer = new JsonSerializer();
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

        [Fact]
        public void KinesisAnalyticsOutputDeliveryEvent()
        {
            using (var fileStream = File.OpenRead("kinesis-analytics-outputdelivery-event.json"))
            {
                var serializer = new JsonSerializer();
                var kinesisAnalyticsEvent = serializer.Deserialize<KinesisAnalyticsOutputDeliveryEvent>(fileStream);
                Assert.Equal("00540a87-5050-496a-84e4-e7d92bbaf5e2", kinesisAnalyticsEvent.InvocationId);
                Assert.Equal("arn:aws:kinesisanalytics:us-east-1:12345678911:application/lambda-test", kinesisAnalyticsEvent.ApplicationArn);

                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisAnalyticsEvent.Records[0].RecordId);
                Assert.Equal("aGVsbG8gd29ybGQ=", kinesisAnalyticsEvent.Records[0].Base64EncodedData);
            }
        }

        [Fact]
        public void KinesisAnalyticsOutputDeliveryResponseTest()
        {
            using (var fileStream = File.OpenRead("kinesis-analytics-outputdelivery-response.json"))
            {
                var serializer = new JsonSerializer();
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

        [Fact]
        public void KinesisAnalyticsInputProcessingEventTest()
        {
            using (var fileStream = File.OpenRead("kinesis-analytics-inputpreprocessing-event.json"))
            {
                var serializer = new JsonSerializer();
                var kinesisAnalyticsEvent = serializer.Deserialize<KinesisAnalyticsStreamsInputPreprocessingEvent>(fileStream);
                Assert.Equal("00540a87-5050-496a-84e4-e7d92bbaf5e2", kinesisAnalyticsEvent.InvocationId);
                Assert.Equal("arn:aws:kinesis:us-east-1:AAAAAAAAAAAA:stream/lambda-test", kinesisAnalyticsEvent.StreamArn);
                Assert.Equal("arn:aws:kinesisanalytics:us-east-1:12345678911:application/lambda-test", kinesisAnalyticsEvent.ApplicationArn);
                Assert.Equal(1, kinesisAnalyticsEvent.Records.Count);

                Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisAnalyticsEvent.Records[0].RecordId);
                Assert.Equal("aGVsbG8gd29ybGQ=", kinesisAnalyticsEvent.Records[0].Base64EncodedData);
            }
        }

        [Fact]
        public void KinesisAnalyticsInputProcessingResponseTest()
        {
            using (var fileStream = File.OpenRead("kinesis-analytics-inputpreprocessing-response.json"))
            {
                var serializer = new JsonSerializer();
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

        [Fact]
        public void CloudWatchLogEvent()
        {
            using (var fileStream = File.OpenRead("logs-event.json"))
            {
                var serializer = new JsonSerializer();
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

        [Fact]
        public void ECSContainerStateChangeEventTest()
        {
            using (var fileStream = File.OpenRead("ecs-container-state-change-event.json"))
            {
                var serializer = new JsonSerializer();
                var ecsEvent = serializer.Deserialize<ECSEvent>(fileStream);

                Assert.Equal(ecsEvent.Version, "0");
                Assert.Equal(ecsEvent.Id, "8952ba83-7be2-4ab5-9c32-6687532d15a2");
                Assert.Equal(ecsEvent.DetailType, "ECS Container Instance State Change");
                Assert.Equal(ecsEvent.Source, "aws.ecs");
                Assert.Equal(ecsEvent.Account, "111122223333");
                Assert.Equal(ecsEvent.Time.ToUniversalTime(), DateTime.Parse("2016-12-06T16:41:06Z").ToUniversalTime());
                Assert.Equal(ecsEvent.Region, "us-east-1");
                Assert.Equal(ecsEvent.Resources.Count, 1);
                Assert.Equal(ecsEvent.Resources[0], "arn:aws:ecs:us-east-1:111122223333:container-instance/b54a2a04-046f-4331-9d74-3f6d7f6ca315");
                Assert.IsType(typeof(CloudWatchEvents.ECSEvents.Detail), ecsEvent.Detail);
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

        [Fact]
        public void ECSTaskStateChangeEventTest()
        {
            using (var fileStream = File.OpenRead("ecs-task-state-change-event.json"))
            {
                var serializer = new JsonSerializer();
                var ecsEvent = serializer.Deserialize<ECSEvent>(fileStream);

                Assert.Equal(ecsEvent.Version, "0");
                Assert.Equal(ecsEvent.Id, "9bcdac79-b31f-4d3d-9410-fbd727c29fab");
                Assert.Equal(ecsEvent.DetailType, "ECS Task State Change");
                Assert.Equal(ecsEvent.Source, "aws.ecs");
                Assert.Equal(ecsEvent.Account, "111122223333");
                Assert.Equal(ecsEvent.Time.ToUniversalTime(), DateTime.Parse("2016-12-06T16:41:06Z").ToUniversalTime());
                Assert.Equal(ecsEvent.Region, "us-east-1");
                Assert.Equal(ecsEvent.Resources.Count, 1);
                Assert.Equal(ecsEvent.Resources[0], "arn:aws:ecs:us-east-1:111122223333:task/b99d40b3-5176-4f71-9a52-9dbd6f1cebef");
                Assert.IsType(typeof(CloudWatchEvents.ECSEvents.Detail), ecsEvent.Detail);
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

        private void Handle(ECSEvent ecsEvent)
        {
            Console.WriteLine(ecsEvent.DetailType);
            Console.WriteLine(ecsEvent.Time);
        }

    }
}
