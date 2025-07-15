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

namespace Infrastructure;

internal class Configuration
{
    public string AccountId { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_PIPELINE_ACCOUNT_ID");
    public string Region { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_PIPELINE_REGION");

    public string GitHubTokenSecretName { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_GITHUB_TOKEN_SECRET_NAME");

    public string GitHubTokenSecretKey { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_GITHUB_TOKEN_SECRET_KEY");
    public string GitHubOwner { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_GITHUB_REPO_OWNER");
    public string GitHubRepository { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_GITHUB_REPO_NAME");
    public string GitHubBranch { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_GITHUB_REPO_BRANCH");
    public Ecrs Ecrs { get; } = new Ecrs();
    public const string ProjectRoot = "LambdaRuntimeDockerfiles/Infrastructure/src/Infrastructure";
    public static readonly string ProjectName = "aws-lambda-container-images";

    public readonly FrameworkConfiguration[] Frameworks = new[]
    {
        new FrameworkConfiguration
        {
            Framework = "net8",
            Channel = "8.0",
            EcrRepositoryName = "awslambda/dotnet8-runtime",
            DockerBuildImage = "8.0-bookworm-slim",
            BaseImageAmd64Tag = "contributed-base-image-x86_64",
            BaseImageArm64Tag = "contributed-base-image-arm64",
            HasArm64Image = true
        },
        new FrameworkConfiguration
        {
            Framework = "net9",
            Channel = "9.0",
            EcrRepositoryName = "awslambda/dotnet9-runtime",
            DockerBuildImage = "9.0-bookworm-slim",
            BaseImageAmd64Tag = "contributed-base-image-x86_64",
            BaseImageArm64Tag = "contributed-base-image-arm64",
            HasArm64Image = true
        },
        new FrameworkConfiguration
        {
            Framework = "net10",
            Channel = "10.0",
            EcrRepositoryName = "awslambda/dotnet10-runtime",
            DockerBuildImage = "10.0-preview-trixie-slim",
            BaseImageAmd64Tag = "contributed-base-image-x86_64",
            BaseImageArm64Tag = "contributed-base-image-arm64",
            HasArm64Image = true
        }
    };
}

internal class Ecrs
{
    public string Stage { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_STAGE_ECR");
    public string Beta { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_BETA_ECRS");
    public string Prod { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_PROD_ECRS");
}

internal class FrameworkConfiguration
{
    public string Framework { get; set; }

    public string Channel { get; set; }

    public string EcrRepositoryName { get; set; }

    public string DockerBuildImage { get; set; }

    public string BaseImageAmd64Tag { get; set; }

    public string BaseImageArm64Tag { get; set; }

    public bool HasArm64Image { get; set; }

    // DotnetSdkVersions is used to specify a specific version of the .NET SDK to be installed on the CodeBuild image
    // The default behavior is to specify a channel and that installs the latest version in that channel
    // By specifying a specific .NET SDK version, you override the default channel behavior
    public string SpecificSdkVersion { get; set; } = string.Empty;
}
