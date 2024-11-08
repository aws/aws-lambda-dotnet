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
    public readonly string[] EcrRepositoryNames = Environment.GetEnvironmentVariable("AWS_LAMBDA_ECR_REPOSITORY_NAME")?.Split(";");
    public const string ProjectRoot = "LambdaRuntimeDockerfiles/Infrastructure/src/Infrastructure";
    public static readonly string ProjectName = $"aws-lambda-container-images{Environment.GetEnvironmentVariable("AWS_LAMBDA_PIPELINE_NAME_SUFFIX")}";
    public readonly string[] DockerArm64Images = new string[] { "net6", "net8", "net9" };
    // DotnetSdkVersions is used to specify a specific version of the .NET SDK to be installed on the CodeBuild image
    // The default behavior is to specify a channel and that installs the latest version in that channel
    // By specifying a specific .NET SDK version, you override the default channel behavior
    public readonly Dictionary<string, string> DotnetSdkVersions = new Dictionary<string, string> { };
    public readonly Dictionary<string, string> DockerBuildImages = new Dictionary<string, string> { {"net6", "6.0-bullseye-slim"}, {"net8", "8.0-bookworm-slim"}, {"net9", "9.0-bookworm-slim"} };
    public readonly Dictionary<string, string> BaseImageAmd64Tags = new Dictionary<string, string> { { "net6", "contributed-base-image-x86_64" }, { "net8", "contributed-base-image-x86_64" }, { "net9", "contributed-base-image-x86_64" } };
    public readonly Dictionary<string, string> BaseImageArm64Tags = new Dictionary<string, string> { { "net6", "contributed-base-image-arm64" }, { "net8", "contributed-base-image-arm64" }, { "net9", "contributed-base-image-arm64" } };
    public readonly string[] Frameworks = Environment.GetEnvironmentVariable("AWS_LAMBDA_DOTNET_FRAMEWORK_VERSION")?.Split(";");
    public readonly string[] Channels = Environment.GetEnvironmentVariable("AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL")?.Split(";");
}

internal class Ecrs
{
    public string Stage { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_STAGE_ECR");
    public string Beta { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_BETA_ECRS");
    public string Prod { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_PROD_ECRS");
}