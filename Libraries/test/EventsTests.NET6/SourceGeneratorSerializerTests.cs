using System.IO;
using System.Text.Json.Serialization;

using Xunit;

using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.APIGatewayEvents;
using System;
using Amazon.Lambda.Core;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.S3Events;
using Amazon.Lambda.CloudWatchEvents.BatchEvents;

namespace EventsTests.NET6
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
    [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
    internal partial class HttpApiJsonSerializationContext : JsonSerializerContext
    {

    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(S3ObjectLambdaEvent))]
    internal partial class S3ObjectLambdaSerializationContext : JsonSerializerContext
    {

    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(BatchJobStateChangeEvent))]
    internal partial class BatchJobStateChangeEventSerializationContext : JsonSerializerContext
    {

    }

    public class SourceGeneratorSerializerTests
    {
        [Theory]
        [InlineData(typeof(SourceGeneratorLambdaJsonSerializer<HttpApiJsonSerializationContext>))]
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
        [InlineData(typeof(SourceGeneratorLambdaJsonSerializer<S3ObjectLambdaSerializationContext>))]
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
        [InlineData(typeof(SourceGeneratorLambdaJsonSerializer<BatchJobStateChangeEventSerializationContext>))]

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
            }
        }


        public MemoryStream LoadJsonTestFile(string filename)
        {
            var json = File.ReadAllText(filename);
            return new MemoryStream(UTF8Encoding.UTF8.GetBytes(json));
        }
    }
}
