// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.CodePipeline;
using Amazon.CDK.Pipelines;
using Constructs;

namespace Infrastructure;

public class SelfMutatingPipelineStack : Stack
{
    private const string CDK_CLI_VERSION = "2.1018.0";

    internal SelfMutatingPipelineStack(
        Construct scope,
        string id,
        Configuration configuration,
        IStackProps props = null) : base(scope, id, props)
    {
        var environmentVariables =
            new Dictionary<string, IBuildEnvironmentVariable>
            {
                { "AWS_LAMBDA_PIPELINE_ACCOUNT_ID",
                    new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                    System.Environment.GetEnvironmentVariable("AWS_LAMBDA_PIPELINE_ACCOUNT_ID") ?? string.Empty } },
                { "AWS_LAMBDA_PIPELINE_NAME_SUFFIX",
                    new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                        System.Environment.GetEnvironmentVariable("AWS_LAMBDA_PIPELINE_NAME_SUFFIX") ?? string.Empty } },
                { "AWS_LAMBDA_PIPELINE_REGION",
                    new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                    System.Environment.GetEnvironmentVariable("AWS_LAMBDA_PIPELINE_REGION") ?? string.Empty } },
                { "AWS_LAMBDA_GITHUB_TOKEN_SECRET_NAME",
                    new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                    System.Environment.GetEnvironmentVariable("AWS_LAMBDA_GITHUB_TOKEN_SECRET_NAME") ?? string.Empty } },
                { "AWS_LAMBDA_GITHUB_TOKEN_SECRET_KEY",
                    new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                    System.Environment.GetEnvironmentVariable("AWS_LAMBDA_GITHUB_TOKEN_SECRET_KEY") ?? string.Empty } },
                { "AWS_LAMBDA_GITHUB_REPO_OWNER",
                    new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                    System.Environment.GetEnvironmentVariable("AWS_LAMBDA_GITHUB_REPO_OWNER") ?? string.Empty } },
                { "AWS_LAMBDA_GITHUB_REPO_NAME",
                    new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                    System.Environment.GetEnvironmentVariable("AWS_LAMBDA_GITHUB_REPO_NAME") ?? string.Empty } },
                { "AWS_LAMBDA_GITHUB_REPO_BRANCH",
                    new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                    System.Environment.GetEnvironmentVariable("AWS_LAMBDA_GITHUB_REPO_BRANCH") ?? string.Empty } },
                { "AWS_LAMBDA_STAGE_ECR",
                    new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                    System.Environment.GetEnvironmentVariable("AWS_LAMBDA_STAGE_ECR") ?? string.Empty } },
                { "AWS_LAMBDA_BETA_ECRS",
                    new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                    System.Environment.GetEnvironmentVariable("AWS_LAMBDA_BETA_ECRS") ?? string.Empty } },
                { "AWS_LAMBDA_PROD_ECRS",
                    new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                    System.Environment.GetEnvironmentVariable("AWS_LAMBDA_PROD_ECRS") ?? string.Empty } },
                { "AWS_LAMBDA_ECR_REPOSITORY_NAME",
                    new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                    System.Environment.GetEnvironmentVariable("AWS_LAMBDA_ECR_REPOSITORY_NAME") ?? string.Empty } },
                { "AWS_LAMBDA_DOTNET_FRAMEWORK_VERSION",
                    new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                    System.Environment.GetEnvironmentVariable("AWS_LAMBDA_DOTNET_FRAMEWORK_VERSION") ?? string.Empty } },
                { "AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL",
                    new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                    System.Environment.GetEnvironmentVariable("AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL") ?? string.Empty } },
            };

        // Self mutation
        var pipeline = new CodePipeline(this, "SelfMutatingPipeline", new CodePipelineProps
        {
            PipelineName = id,
            PipelineType = PipelineType.V2,
            // It synthesizes CDK code to cdk.out directory which is picked by SelfMutate stage to mutate the pipeline
            Synth = new ShellStep("Synth", new ShellStepProps
            {
                Input = CodePipelineSource.GitHub(
                    $"{configuration.GitHubOwner}/{configuration.GitHubRepository}",
                    configuration.GitHubBranch,
                    new GitHubSourceOptions
                    {
                        Authentication = SecretValue.SecretsManager(configuration.GitHubTokenSecretName, new SecretsManagerSecretOptions
                        {
                            JsonField = configuration.GitHubTokenSecretKey
                        })
                    }),
                InstallCommands = new[] { $"npm install -g aws-cdk@{CDK_CLI_VERSION}" },
                Commands = new[] { $"dotnet build {Configuration.ProjectRoot}", "cdk synth" }
            }),
            CodeBuildDefaults = new CodeBuildOptions
            {
                BuildEnvironment = new BuildEnvironment
                {
                    EnvironmentVariables = environmentVariables
                },
                PartialBuildSpec = BuildSpec.FromObject(new Dictionary<string, object>
                {
                    { "phases", new Dictionary<string, object>
                        {
                            { "install", new Dictionary<string, object>
                                {
                                    { "runtime-versions", new Dictionary<string, object>
                                        {
                                            { "dotnet", "8.x"}
                                        }
                                    }
                                }
                            }
                        }
                    }
                })
            }
        });

        // Add a stage in the pipeline to deploy the Lambda container pipelines
        pipeline.AddStage(new PipelinesStage(this, Configuration.ProjectName, configuration));
    }
}
