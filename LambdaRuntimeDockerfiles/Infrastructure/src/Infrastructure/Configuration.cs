/*
 * Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 *
 *  http://aws.amazon.com/apache2.0
 *
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */

using System;
using System.Collections.Generic;

namespace Infrastructure
{
    internal class Configuration
    {
        public string AccountId { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_PIPELINE_ACCOUNT_ID");
        public string CodeCommitAccountId { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_PIPELINE_CODECOMMIT_ACCOUNT_ID");
        public string Region { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_PIPELINE_REGION");

        public Source Source { get; } = new Source();
        public Ecrs Ecrs { get; } = new Ecrs();
        public readonly string[] EcrRepositoryNames = Environment.GetEnvironmentVariable("AWS_LAMBDA_ECR_REPOSITORY_NAME")?.Split(";");
        public const string ProjectRoot = "LambdaRuntimeDockerfiles/Infrastructure/src/Infrastructure";
        public const string ProjectName = "aws-lambda-container-images";
        public readonly string[] DockerARM64Images = new string[] { "net6" };
        // DotnetSdkVersions is used to specify a specific version of the .NET SDK to be installed on the CodeBuild image
        // The default behavior is to specify a channel and that installs the latest version in that channel
        // By specifying a specific .NET SDK version, you override the default channel behavior
        public readonly Dictionary<string, string> DotnetSdkVersions = new Dictionary<string, string> { };
        public readonly Dictionary<string, string> DockerBuildImages = new Dictionary<string, string> { {"net5", "5.0-buster-slim"}, {"net6", "6.0-bullseye-slim"}, {"net7", "7.0-bullseye-slim"} };
        public readonly Dictionary<string, string> BaseImageAMD64Tags = new Dictionary<string, string> { { "net5", "base-image-x86_64" }, { "net6", "contributed-base-image-x86_64" }, { "net7", "contributed-base-image-x86_64" } };
        public readonly Dictionary<string, string> BaseImageARM64Tags = new Dictionary<string, string> { { "net5", "base-image-arm64" }, { "net6", "contributed-base-image-arm64" }, { "net7", "contributed-base-image-arm64" } };
        public readonly string[] Frameworks = Environment.GetEnvironmentVariable("AWS_LAMBDA_DOTNET_FRAMEWORK_VERSION")?.Split(";");
        public readonly string[] Channels = Environment.GetEnvironmentVariable("AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL")?.Split(";");
    }

    internal class Source
    {
        public string RepositoryArn { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_SOURCE_REPOSITORY_ARN");
        public string BranchName { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_SOURCE_BRANCH_NAME");
    }

    internal class Ecrs
    {
        public string Stage { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_STAGE_ECR");
        public string Beta { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_BETA_ECRS");
        public string Prod { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_PROD_ECRS");
    }
}