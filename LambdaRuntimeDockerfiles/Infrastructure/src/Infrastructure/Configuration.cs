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
        public Source Source { get; } = new Source();
        public Ecrs Ecrs { get; } = new Ecrs();
        public readonly string[] EcrRepositoryNames = Environment.GetEnvironmentVariable("AWS_LAMBDA_ECR_REPOSITORY_NAME")?.Split(";");
        public const string ProjectRoot = "LambdaRuntimeDockerfiles/Infrastructure/src/Infrastructure";
        public const string ProjectName = "aws-lambda-container-images";
        public readonly string[] DockerARM64Images = new string[] { "net6" };
        public readonly Dictionary<string, string> DockerBuildImages = new Dictionary<string, string> { {"net5", "5.0-buster-slim"}, {"net6", "6.0-bullseye-slim"} };
        public readonly Dictionary<string, string> BaseImageAMD64Tags = new Dictionary<string, string> { { "net5", "base-image-x86_64" }, { "net6", "base-image-x86_64" } };
        public readonly Dictionary<string, string> BaseImageARM64Tags = new Dictionary<string, string> { { "net5", "base-image-arm64" }, { "net6", "base-image-arm64" } };
        public readonly string[] Frameworks = Environment.GetEnvironmentVariable("AWS_LAMBDA_DOTNET_FRAMEWORK_VERSION")?.Split(";");
        public readonly string[] Channels = Environment.GetEnvironmentVariable("AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL")?.Split(";");
    }

    internal class Source
    {
        public string RepositoryArn { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_SOURCE_REPOSITORY_ARN");
        public string BranchName { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_SOURCE_BRANCH_NAME");
        public string CrossAccountRoleArn { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_SOURCE_CROSS_ACCOUNT_ROLE_ARN");
    }

    internal class Ecrs
    {
        public string Stage { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_STAGE_ECR");
        public string Beta { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_BETA_ECRS");
        public string Prod { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_PROD_ECRS");
    }
}