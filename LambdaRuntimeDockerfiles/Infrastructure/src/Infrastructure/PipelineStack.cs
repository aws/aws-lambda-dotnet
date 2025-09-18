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
using Amazon.CDK;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.CodePipeline;
using Amazon.CDK.AWS.CodePipeline.Actions;
using Amazon.CDK.AWS.IAM;
using Constructs;

namespace Infrastructure;

public class PipelineStack : Stack
{
    private const string PowershellVersion = "7.4.5";
    private const string BaseImageMultiArch = "contributed-base-image-multi-arch";

    internal PipelineStack(
        Construct scope,
        string id,
        Configuration configuration,
        FrameworkConfiguration frameworkConfiguration,
        string gitHubOwner,
        string gitHubRepository,
        string gitHubBranch,
        IStackProps props = null) : base(scope, id, props)
    {
        var sourceArtifact = new Artifact_();

        var ecrPolicy = new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[] { "ecr:*" },
            Resources = new[] { "*" }
        });

        var sourceAction = new GitHubSourceAction(new GitHubSourceActionProps
        {
            ActionName = gitHubRepository,
            Output = sourceArtifact,
            Owner = gitHubOwner,
            Repo = gitHubRepository,
            Branch = gitHubBranch,
            Trigger = GitHubTrigger.WEBHOOK,
            OauthToken = SecretValue.SecretsManager(configuration.GitHubTokenSecretName, new SecretsManagerSecretOptions
            {
                JsonField = configuration.GitHubTokenSecretKey
            })
        });

        var pipeline = new Pipeline(this, "CodePipeline", new PipelineProps
        {
            PipelineType = PipelineType.V2,
            PipelineName = id,
            RestartExecutionOnUpdate = true,
            Stages =
            [
                new StageOptions
                {
                    StageName = "Source",
                    Actions = [sourceAction]
                }
            ]
        });

        var stageEcr = GetStageEcr(this, frameworkConfiguration.EcrRepositoryName, configuration);

        var dockerBuildActions = new List<IAction>();

        // Stage
        // Build AMD64 image
        var dockerBuildAmd64 = new Project(this, "DockerBuild-amd64", new ProjectProps
        {
            BuildSpec = BuildSpec.FromSourceFilename($"{Configuration.ProjectRoot}/DockerBuild/buildspec.yml"),
            Description = $"Builds and pushes image to {stageEcr}",
            Environment = new BuildEnvironment
            {
                BuildImage = LinuxBuildImage.AMAZON_LINUX_2_5,
                Privileged = true
            },
            Source = Source.GitHub(new GitHubSourceProps
            {
                Owner = gitHubOwner,
                Repo = gitHubRepository,
                BranchOrRef = gitHubBranch
            }),
            EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
            {
                {"AWS_LAMBDA_STAGE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = frameworkConfiguration.EcrRepositoryName}},
                {"AWS_LAMBDA_ARCHITECTURE", new BuildEnvironmentVariable {Value = "amd64"}},
                {"AWS_LAMBDA_POWERSHELL_VERSION", new BuildEnvironmentVariable {Value = PowershellVersion}},
                {"AWS_LAMBDA_IMAGE_TAG", new BuildEnvironmentVariable {Value = frameworkConfiguration.BaseImageAmd64Tag}},
                {"AWS_LAMBDA_DOTNET_FRAMEWORK_VERSION", new BuildEnvironmentVariable {Value = frameworkConfiguration.Framework}},
                {"AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL", new BuildEnvironmentVariable {Value = frameworkConfiguration.Channel}},
                {"AWS_LAMBDA_DOTNET_SDK_VERSION", new BuildEnvironmentVariable {Value = frameworkConfiguration.SpecificSdkVersion }}
            }
        });

        dockerBuildAmd64.AddToRolePolicy(ecrPolicy);
        dockerBuildActions.Add(new CodeBuildAction(new CodeBuildActionProps
        {
            Input = sourceArtifact,
            Project = dockerBuildAmd64,
            ActionName = "amd64"
        }));

        if (frameworkConfiguration.HasArm64Image)
        {
            // Build ARM64 image
            var dockerBuildArm64 = new Project(this, "DockerBuild-arm64", new ProjectProps
            {
                BuildSpec = BuildSpec.FromSourceFilename($"{Configuration.ProjectRoot}/DockerBuild/buildspec.yml"),
                Description = $"Builds and pushes image to {stageEcr}",
                Environment = new BuildEnvironment
                {
                    BuildImage = LinuxArmBuildImage.AMAZON_LINUX_2_STANDARD_3_0,
                    Privileged = true
                },
                Source = Source.GitHub(new GitHubSourceProps
                {
                    Owner = gitHubOwner,
                    Repo = gitHubRepository,
                    BranchOrRef = gitHubBranch
                }),
                EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                {
                    {"AWS_LAMBDA_STAGE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                    {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = frameworkConfiguration.EcrRepositoryName}},
                    {"AWS_LAMBDA_ARCHITECTURE", new BuildEnvironmentVariable {Value = "arm64"}},
                    {"AWS_LAMBDA_POWERSHELL_VERSION", new BuildEnvironmentVariable {Value = PowershellVersion}},
                    {"AWS_LAMBDA_IMAGE_TAG", new BuildEnvironmentVariable {Value = frameworkConfiguration.BaseImageArm64Tag}},
                    {"AWS_LAMBDA_DOTNET_FRAMEWORK_VERSION", new BuildEnvironmentVariable {Value = frameworkConfiguration.Framework}},
                    {"AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL", new BuildEnvironmentVariable {Value = frameworkConfiguration.Channel}},
                    {"AWS_LAMBDA_DOTNET_SDK_VERSION", new BuildEnvironmentVariable {Value = frameworkConfiguration.SpecificSdkVersion }}
                }
            });

            dockerBuildArm64.AddToRolePolicy(ecrPolicy);
            dockerBuildActions.Add(new CodeBuildAction(new CodeBuildActionProps
            {
                Input = sourceArtifact,
                Project = dockerBuildArm64,
                ActionName = "arm64"
            }));
        }

        pipeline.AddStage(new StageOptions
        {
            StageName = "DockerBuild",
            Actions = dockerBuildActions.ToArray()
        });

        // Create multi arch image manifest
        var dockerImageManifest = new Project(this, "DockerImageManifest", new ProjectProps
        {
            BuildSpec = BuildSpec.FromSourceFilename($"{Configuration.ProjectRoot}/DockerImageManifest/buildspec.yml"),
            Description = $"Creates image manifest and pushes to {stageEcr}",
            Environment = new BuildEnvironment
            {
                BuildImage = LinuxBuildImage.AMAZON_LINUX_2_5,
                Privileged = true
            },
            Source = Source.GitHub(new GitHubSourceProps
            {
                Owner = gitHubOwner,
                Repo = gitHubRepository,
                BranchOrRef = gitHubBranch
            }),
            EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
            {
                {"AWS_LAMBDA_STAGE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = frameworkConfiguration.EcrRepositoryName}},
                {"AWS_LAMBDA_MULTI_ARCH_IMAGE_TAG", new BuildEnvironmentVariable {Value = BaseImageMultiArch}},
                {"AWS_LAMBDA_AMD64_IMAGE_TAG", new BuildEnvironmentVariable {Value = frameworkConfiguration.BaseImageAmd64Tag}},
                {"AWS_LAMBDA_ARM64_IMAGE_TAG", new BuildEnvironmentVariable {Value = frameworkConfiguration.BaseImageArm64Tag}},
                {"AWS_LAMBDA_INCLUDE_ARM64", new BuildEnvironmentVariable {Value = frameworkConfiguration.HasArm64Image.ToString()}},
            }
        });

        dockerImageManifest.AddToRolePolicy(ecrPolicy);

        pipeline.AddStage(new StageOptions
        {
            StageName = "DockerImageManifest",
            Actions =
            [
                new CodeBuildAction(new CodeBuildActionProps
                {
                    Input = sourceArtifact,
                    Project = dockerImageManifest,
                    ActionName = "DockerImageManifest"
                })
            ]
        });

        var smokeTestsLambdaFunctionRole = new Role(this, "SmokeTestsLambdaFunctionRole", new RoleProps
        {
            RoleName = $"image-function-tests-{Guid.NewGuid()}",
            ManagedPolicies = [ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")],
            AssumedBy = new ServicePrincipal("lambda.amazonaws.com")
        });

        // Smoke test AMD64 image
        var amd64SmokeTests = new Project(this, "SmokeTests-amd64", new ProjectProps
        {
            BuildSpec = BuildSpec.FromSourceFilename($"{Configuration.ProjectRoot}/SmokeTests/buildspec.yml"),
            Description = "Runs smoke tests on the built image.",
            Environment = new BuildEnvironment
            {
                BuildImage = LinuxBuildImage.AMAZON_LINUX_2_5,
                Privileged = true
            },
            Source = Source.GitHub(new GitHubSourceProps
            {
                Owner = gitHubOwner,
                Repo = gitHubRepository,
                BranchOrRef = gitHubBranch
            }),
            EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
            {
                {"AWS_LAMBDA_SOURCE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = frameworkConfiguration.EcrRepositoryName}},
                {"AWS_LAMBDA_SOURCE_IMAGE_TAG", new BuildEnvironmentVariable {Value = BaseImageMultiArch}},
                {"AWS_LAMBDA_POWERSHELL_VERSION", new BuildEnvironmentVariable {Value = PowershellVersion}},
                {"AWS_LAMBDA_DOTNET_FRAMEWORK_VERSION", new BuildEnvironmentVariable {Value = frameworkConfiguration.Framework}},
                {"AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL", new BuildEnvironmentVariable {Value = frameworkConfiguration.Channel}},
                {"AWS_LAMBDA_DOTNET_BUILD_IMAGE", new BuildEnvironmentVariable {Value = frameworkConfiguration.DockerBuildImage}},
                {"AWS_LAMBDA_DOTNET_SDK_VERSION", new BuildEnvironmentVariable {Value = frameworkConfiguration.SpecificSdkVersion }},
                {"AWS_LAMBDA_SMOKETESTS_LAMBDA_ROLE", new BuildEnvironmentVariable {Value = smokeTestsLambdaFunctionRole.RoleArn}}
            },
        });

        var smokeTestsPolicies = new List<PolicyStatement>();

        // ECR Policies
        smokeTestsPolicies.Add(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions =
            [
                "ecr:BatchCheckLayerAvailability",
                "ecr:BatchDeleteImage",
                "ecr:BatchGetImage",
                "ecr:CompleteLayerUpload",
                "ecr:CreateRepository",
                "ecr:DescribeRepositories",
                "ecr:GetAuthorizationToken",
                "ecr:GetDownloadUrlForLayer",
                "ecr:InitiateLayerUpload",
                "ecr:PutImage",
                "ecr:UploadLayerPart"
            ],
            Resources =
            [
                $"arn:aws:ecr:{configuration.Region}:{configuration.AccountId}:repository/image-function-tests",
                $"arn:aws:ecr:{configuration.Region}:{configuration.AccountId}:repository/{frameworkConfiguration.EcrRepositoryName}"
            ]
        }));

        // The following ECR policy needs to specify * as the resource since that is what is explicitly stated by the following error:
        // An error occurred (AccessDeniedException) when calling the GetAuthorizationToken operation:
        // User: *** is not authorized to perform: ecr:GetAuthorizationToken on resource: * because no identity-based policy
        // allows the ecr:GetAuthorizationToken action
        smokeTestsPolicies.Add(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions =
            [
                "ecr:GetAuthorizationToken"
            ],
            Resources = ["*"]
        }));

        // IAM Policies
        smokeTestsPolicies.Add(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions =
            [
                "iam:PassRole"
            ],
            Resources = [smokeTestsLambdaFunctionRole.RoleArn]
        }));

        // Lambda Policies
        smokeTestsPolicies.Add(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions =
            [
                "lambda:CreateFunction",
                "lambda:DeleteFunction",
                "lambda:GetFunction",
                "lambda:GetFunctionConfiguration",
                "lambda:InvokeFunction",
                "lambda:UpdateFunctionConfiguration"
            ],
            Resources =
            [
                $"arn:aws:lambda:{configuration.Region}:{configuration.AccountId}:function:image-function-tests-*"
            ]
        }));

        foreach (var policy in smokeTestsPolicies)
            amd64SmokeTests.AddToRolePolicy(policy);

        var smokeTestsActions = new List<IAction>();
        smokeTestsActions.Add(new CodeBuildAction(new CodeBuildActionProps
        {
            Input = sourceArtifact,
            Project = amd64SmokeTests,
            ActionName = "amd64"
        }));

        if (frameworkConfiguration.HasArm64Image)
        {
            // Smoke test ARM64 image
            var arm64SmokeTests = new Project(this, "SmokeTests-arm64", new ProjectProps
            {
                BuildSpec = BuildSpec.FromSourceFilename($"{Configuration.ProjectRoot}/SmokeTests/buildspec.yml"),
                Description = "Runs smoke tests on the built image.",
                Environment = new BuildEnvironment
                {
                    BuildImage = LinuxArmBuildImage.AMAZON_LINUX_2_STANDARD_3_0,
                    Privileged = true
                },
                Source = Source.GitHub(new GitHubSourceProps
                {
                    Owner = gitHubOwner,
                    Repo = gitHubRepository,
                    BranchOrRef = gitHubBranch
                }),
                EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                {
                    {"AWS_LAMBDA_SOURCE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                    {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = frameworkConfiguration.EcrRepositoryName}},
                    {"AWS_LAMBDA_SOURCE_IMAGE_TAG", new BuildEnvironmentVariable {Value = BaseImageMultiArch}},
                    {"AWS_LAMBDA_POWERSHELL_VERSION", new BuildEnvironmentVariable {Value = PowershellVersion}},
                    {"AWS_LAMBDA_DOTNET_FRAMEWORK_VERSION", new BuildEnvironmentVariable {Value = frameworkConfiguration.Framework}},
                    {"AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL", new BuildEnvironmentVariable {Value = frameworkConfiguration.Channel}},
                    {"AWS_LAMBDA_DOTNET_BUILD_IMAGE", new BuildEnvironmentVariable {Value = frameworkConfiguration.DockerBuildImage}},
                    {"AWS_LAMBDA_DOTNET_SDK_VERSION", new BuildEnvironmentVariable {Value = frameworkConfiguration.SpecificSdkVersion }},
                    {"AWS_LAMBDA_SMOKETESTS_LAMBDA_ROLE", new BuildEnvironmentVariable {Value = smokeTestsLambdaFunctionRole.RoleArn}}
                }
            });

            foreach (var policy in smokeTestsPolicies)
                arm64SmokeTests.AddToRolePolicy(policy);

            smokeTestsActions.Add(new CodeBuildAction(new CodeBuildActionProps
            {
                Input = sourceArtifact,
                Project = arm64SmokeTests,
                ActionName = "arm64"
            }));
        }

        pipeline.AddStage(new StageOptions
        {
            StageName = "SmokeTests",
            Actions = smokeTestsActions.ToArray()
        });

        // Beta
        if (!string.IsNullOrWhiteSpace(configuration.Ecrs.Beta))
        {
            var betaDockerPush = new Project(this, "Beta-DockerPush", new ProjectProps
            {
                BuildSpec = BuildSpec.FromSourceFilename($"{Configuration.ProjectRoot}/DockerPush/buildspec.yml"),
                Description = $"Pushes staged image to {configuration.Ecrs.Beta}",
                Environment = new BuildEnvironment
                {
                    BuildImage = LinuxBuildImage.AMAZON_LINUX_2_5,
                    Privileged = true
                },
                Source = Source.GitHub(new GitHubSourceProps
                {
                    Owner = gitHubOwner,
                    Repo = gitHubRepository,
                    BranchOrRef = gitHubBranch
                }),
                EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                {
                    {"AWS_LAMBDA_SOURCE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                    {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = frameworkConfiguration.EcrRepositoryName}},
                    {"AWS_LAMBDA_DESTINATION_ECRS", new BuildEnvironmentVariable {Value = configuration.Ecrs.Beta}},
                    {"AWS_LAMBDA_MULTI_ARCH_IMAGE_TAG", new BuildEnvironmentVariable {Value = BaseImageMultiArch}},
                    {"AWS_LAMBDA_AMD64_IMAGE_TAG", new BuildEnvironmentVariable {Value = frameworkConfiguration.BaseImageAmd64Tag}},
                    {"AWS_LAMBDA_ARM64_IMAGE_TAG", new BuildEnvironmentVariable {Value = frameworkConfiguration.BaseImageArm64Tag}},
                    {"AWS_LAMBDA_INCLUDE_ARM64", new BuildEnvironmentVariable {Value = frameworkConfiguration.HasArm64Image.ToString()}},
                }
            });

            betaDockerPush.AddToRolePolicy(ecrPolicy);

            pipeline.AddStage(new StageOptions
            {
                StageName = "Beta-DockerPush",
                Actions =
                [
                    new CodeBuildAction(new CodeBuildActionProps
                    {
                        Input = sourceArtifact,
                        Project = betaDockerPush,
                        ActionName = "DockerPush"
                    })
                ]
            });
        }

        // Prod
        if (!string.IsNullOrWhiteSpace(configuration.Ecrs.Prod))
        {
            // Manual Approval
            pipeline.AddStage(new StageOptions
            {
                StageName = "Prod-ManualApproval",
                Actions =
                [
                    new ManualApprovalAction(new ManualApprovalActionProps
                    {
                        ActionName = "ManualApproval"
                    })
                ]
            });

            var prodDockerPush = new Project(this, "Prod-DockerPush", new ProjectProps
            {
                BuildSpec = BuildSpec.FromSourceFilename($"{Configuration.ProjectRoot}/DockerPush/buildspec.yml"),
                Description = $"Pushes staged image to {configuration.Ecrs.Prod}",
                Environment = new BuildEnvironment
                {
                    BuildImage = LinuxBuildImage.AMAZON_LINUX_2_5,
                    Privileged = true
                },
                Source = Source.GitHub(new GitHubSourceProps
                {
                    Owner = gitHubOwner,
                    Repo = gitHubRepository,
                    BranchOrRef = gitHubBranch
                }),
                EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                {
                    {"AWS_LAMBDA_SOURCE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                    {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = frameworkConfiguration.EcrRepositoryName}},
                    {"AWS_LAMBDA_DESTINATION_ECRS", new BuildEnvironmentVariable {Value = configuration.Ecrs.Prod}},
                    {"AWS_LAMBDA_MULTI_ARCH_IMAGE_TAG", new BuildEnvironmentVariable {Value = BaseImageMultiArch}},
                    {"AWS_LAMBDA_AMD64_IMAGE_TAG", new BuildEnvironmentVariable {Value = frameworkConfiguration.BaseImageAmd64Tag}},
                    {"AWS_LAMBDA_ARM64_IMAGE_TAG", new BuildEnvironmentVariable {Value = frameworkConfiguration.BaseImageArm64Tag}},
                    {"AWS_LAMBDA_INCLUDE_ARM64", new BuildEnvironmentVariable {Value = frameworkConfiguration.HasArm64Image.ToString()}},
                }
            });

            prodDockerPush.AddToRolePolicy(ecrPolicy);

            pipeline.AddStage(new StageOptions
            {
                StageName = "Prod-DockerPush",
                Actions =
                [
                    new CodeBuildAction(new CodeBuildActionProps
                    {
                        Input = sourceArtifact,
                        Project = prodDockerPush,
                        ActionName = "DockerPush"
                    })
                ]
            });
        }
    }

    private string GetStageEcr(Construct scope, string ecrRepositoryName, Configuration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Ecrs.Stage))
        {
            var repository = new Amazon.CDK.AWS.ECR.Repository(scope, "StageEcr", new Amazon.CDK.AWS.ECR.RepositoryProps
            {
                RepositoryName = ecrRepositoryName
            });
            return GetEcr(repository.RepositoryUri);
        }

        return configuration.Ecrs.Stage;
    }

    private static string GetEcr(string ecrRepositoryUri)
    {
        return ecrRepositoryUri.Split('/')[0];
    }
}
