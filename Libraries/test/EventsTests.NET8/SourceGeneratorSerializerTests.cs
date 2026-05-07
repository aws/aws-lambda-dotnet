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

namespace EventsTests.NET8
{
    [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
    [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
    internal partial class HttpApiJsonSerializationContext : JsonSerializerContext
    {

    }

    [JsonSerializable(typeof(S3ObjectLambdaEvent))]
    internal partial class S3ObjectLambdaSerializationContext : JsonSerializerContext
    {

    }

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
            }
        }


        public MemoryStream LoadJsonTestFile(string filename)
        {
            var json = File.ReadAllText(filename);
            return new MemoryStream(UTF8Encoding.UTF8.GetBytes(json));
        }
    }
}
