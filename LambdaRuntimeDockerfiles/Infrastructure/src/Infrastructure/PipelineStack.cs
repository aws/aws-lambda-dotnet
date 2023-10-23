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
using System.Linq;
using Amazon.CDK;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.CodeCommit;
using Amazon.CDK.AWS.CodePipeline;
using Amazon.CDK.AWS.CodePipeline.Actions;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.KMS;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.Pipelines;
using Constructs;
using Action = Amazon.CDK.AWS.CodePipeline.Actions.Action;
using RepositoryProps = Amazon.CDK.AWS.ECR.RepositoryProps;

namespace Infrastructure
{
    public class PipelineStack : Stack
    {
        private const string cdkCliVersion = "2.44.0";
        private const string PowershellArm64 = "7.1.3 powershell-7.1.3-linux-arm64.tar.gz";
        private const string PowershellAmd64 = "7.1.3 powershell-7.1.3-linux-x64.tar.gz";
        private const string BaseImageMultiArch = "contributed-base-image-multi-arch";

        internal PipelineStack(
            Construct scope,
            string id,
            string ecrRepositoryName,
            string framework,
            string channel,
            string dockerBuildImage,
            Configuration configuration,
            IStackProps props = null) : base(scope, id, props)
        {
            var repository = Repository.FromRepositoryArn(this, "Repository", configuration.Source.RepositoryArn);

            var artifactBucket = new Bucket(this, "ArtifactBucket", new BucketProps
            {
                AutoDeleteObjects = true,
                BucketName = $"{id}-{configuration.AccountId}",
                EncryptionKey = new Key(this, $"{id}-crossaccountaccess-encryptionkey"),
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            var sourceArtifact = new Artifact_();

            var ecrPolicy = new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[] { "ecr:*" },
                Resources = new[] { "*" }
            });

            var sourceAction = new CodeCommitSourceAction(new CodeCommitSourceActionProps
            {
                ActionName = repository.RepositoryName,
                Output = sourceArtifact,
                Repository = repository,
                Branch = configuration.Source.BranchName,
                Trigger = CodeCommitTrigger.POLL
            });

            var basePipeline = new Pipeline(this, "CodePipeline", new PipelineProps
            {
                PipelineName = id,
                RestartExecutionOnUpdate = true,
                ArtifactBucket = artifactBucket,
                Stages = new StageOptions[] {
                    new StageOptions
                    {
                        StageName = "Source",
                        Actions = new Action[] { sourceAction }
                    }
                }
            });

            var environmentVariables =
                new Dictionary<string, IBuildEnvironmentVariable>
                {
                    { "AWS_LAMBDA_PIPELINE_ACCOUNT_ID",
                        new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                        System.Environment.GetEnvironmentVariable("AWS_LAMBDA_PIPELINE_ACCOUNT_ID") ?? string.Empty } },
                    { "AWS_LAMBDA_PIPELINE_CODECOMMIT_ACCOUNT_ID",
                        new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                        System.Environment.GetEnvironmentVariable("AWS_LAMBDA_PIPELINE_CODECOMMIT_ACCOUNT_ID") ?? string.Empty } },
                    { "AWS_LAMBDA_PIPELINE_REGION",
                        new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                        System.Environment.GetEnvironmentVariable("AWS_LAMBDA_PIPELINE_REGION") ?? string.Empty } },
                    { "AWS_LAMBDA_SOURCE_REPOSITORY_ARN",
                        new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                        System.Environment.GetEnvironmentVariable("AWS_LAMBDA_SOURCE_REPOSITORY_ARN") ?? string.Empty } },
                    { "AWS_LAMBDA_SOURCE_BRANCH_NAME",
                        new BuildEnvironmentVariable { Type = BuildEnvironmentVariableType.PLAINTEXT, Value =
                        System.Environment.GetEnvironmentVariable("AWS_LAMBDA_SOURCE_BRANCH_NAME") ?? string.Empty } },
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
            var pipeline = new CodePipeline(this, "Pipeline", new CodePipelineProps
            {
                CodePipeline = basePipeline,

                // It synthesizes CDK code to cdk.out directory which is picked by SelfMutate stage to mutate the pipeline
                Synth = new ShellStep("Synth", new ShellStepProps
                {
                    Input = CodePipelineFileSet.FromArtifact(sourceArtifact),
                    InstallCommands = new[] { $"npm install -g aws-cdk@{cdkCliVersion}" },
                    Commands = new[] { $"dotnet build {Configuration.ProjectRoot}", "cdk synth" }
                }),
                CodeBuildDefaults = new CodeBuildOptions
                {
                    BuildEnvironment = new BuildEnvironment
                    {
                        EnvironmentVariables = environmentVariables
                    }
                },
                SelfMutationCodeBuildDefaults = new CodeBuildOptions
                {
                    RolePolicy = new PolicyStatement[]
                    {
                        new PolicyStatement(new PolicyStatementProps
                        {
                            Effect = Effect.ALLOW,
                            Actions = new[] { "sts:AssumeRole" },
                            Resources = new[] { $"arn:aws:iam::{configuration.CodeCommitAccountId}:role/*" }
                        })
                    }
                }
            });

            pipeline.BuildPipeline();

            var stageEcr = GetStageEcr(this, ecrRepositoryName, configuration);

            var dockerBuildActions = new List<Action>();

            // Stage
            // Build AMD64 image
            var dockerBuildAmd64 = new Project(this, "DockerBuild-amd64", new ProjectProps
            {
                BuildSpec = BuildSpec.FromSourceFilename($"{Configuration.ProjectRoot}/DockerBuild/buildspec.yml"),
                Description = $"Builds and pushes image to {stageEcr}",
                Environment = new BuildEnvironment
                {
                    BuildImage = LinuxBuildImage.AMAZON_LINUX_2_3,
                    Privileged = true
                },
                Source = Amazon.CDK.AWS.CodeBuild.Source.CodeCommit(new CodeCommitSourceProps
                {
                    Repository = repository,
                    BranchOrRef = configuration.Source.BranchName
                }),
                EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                {
                    {"AWS_LAMBDA_STAGE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                    {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = ecrRepositoryName}},
                    {"AWS_LAMBDA_ARCHITECTURE", new BuildEnvironmentVariable {Value = "amd64"}},
                    {"AWS_LAMBDA_POWERSHELL_VERSION", new BuildEnvironmentVariable {Value = PowershellAmd64}},
                    {"AWS_LAMBDA_IMAGE_TAG", new BuildEnvironmentVariable {Value = configuration.BaseImageAMD64Tags[framework]}},
                    {"AWS_LAMBDA_DOTNET_FRAMEWORK_VERSION", new BuildEnvironmentVariable {Value = framework}},
                    {"AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL", new BuildEnvironmentVariable {Value = channel}},
                    {"AWS_LAMBDA_DOTNET_SDK_VERSION", new BuildEnvironmentVariable {Value = configuration.DotnetSdkVersions.ContainsKey(framework) ? configuration.DotnetSdkVersions[framework] : string.Empty }}
                }
            });

            dockerBuildAmd64.AddToRolePolicy(ecrPolicy);
            dockerBuildActions.Add(new CodeBuildAction(new CodeBuildActionProps
            {
                Input = sourceArtifact,
                Project = dockerBuildAmd64,
                ActionName = "amd64"
            }));

            if (configuration.DockerARM64Images.Contains(framework))
            {
                // Build ARM64 image
                var dockerBuildArm64 = new Project(this, "DockerBuild-arm64", new ProjectProps
                {
                    BuildSpec = BuildSpec.FromSourceFilename($"{Configuration.ProjectRoot}/DockerBuild/buildspec.yml"),
                    Description = $"Builds and pushes image to {stageEcr}",
                    Environment = new BuildEnvironment
                    {
                        BuildImage = LinuxArmBuildImage.AMAZON_LINUX_2_STANDARD_1_0,
                        Privileged = true
                    },
                    Source = Amazon.CDK.AWS.CodeBuild.Source.CodeCommit(new CodeCommitSourceProps
                    {
                        Repository = repository,
                        BranchOrRef = configuration.Source.BranchName
                    }),
                    EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                    {
                        {"AWS_LAMBDA_STAGE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                        {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = ecrRepositoryName}},
                        {"AWS_LAMBDA_ARCHITECTURE", new BuildEnvironmentVariable {Value = "arm64"}},
                        {"AWS_LAMBDA_POWERSHELL_VERSION", new BuildEnvironmentVariable {Value = PowershellArm64}},
                        {"AWS_LAMBDA_IMAGE_TAG", new BuildEnvironmentVariable {Value = configuration.BaseImageARM64Tags[framework]}},
                        {"AWS_LAMBDA_DOTNET_FRAMEWORK_VERSION", new BuildEnvironmentVariable {Value = framework}},
                        {"AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL", new BuildEnvironmentVariable {Value = channel}},
                        {"AWS_LAMBDA_DOTNET_SDK_VERSION", new BuildEnvironmentVariable {Value = configuration.DotnetSdkVersions.ContainsKey(framework) ? configuration.DotnetSdkVersions[framework] : string.Empty }}
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

            basePipeline.AddStage(new StageOptions
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
                    BuildImage = LinuxBuildImage.AMAZON_LINUX_2_3,
                    Privileged = true
                },
                Source = Amazon.CDK.AWS.CodeBuild.Source.CodeCommit(new CodeCommitSourceProps
                {
                    Repository = repository,
                    BranchOrRef = configuration.Source.BranchName
                }),
                EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                {
                    {"AWS_LAMBDA_STAGE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                    {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = ecrRepositoryName}},
                    {"AWS_LAMBDA_MULTI_ARCH_IMAGE_TAG", new BuildEnvironmentVariable {Value = BaseImageMultiArch}},
                    {"AWS_LAMBDA_AMD64_IMAGE_TAG", new BuildEnvironmentVariable {Value = configuration.BaseImageAMD64Tags[framework]}},
                    {"AWS_LAMBDA_ARM64_IMAGE_TAG", new BuildEnvironmentVariable {Value = configuration.BaseImageARM64Tags[framework]}},
                    {"AWS_LAMBDA_INCLUDE_ARM64", new BuildEnvironmentVariable {Value = configuration.DockerARM64Images.Contains(framework).ToString()}},
                }
            });

            dockerImageManifest.AddToRolePolicy(ecrPolicy);

            basePipeline.AddStage(new StageOptions
            {
                StageName = "DockerImageManifest",
                Actions = new Action[] {
                    new CodeBuildAction(new CodeBuildActionProps
                    {
                        Input = sourceArtifact,
                        Project = dockerImageManifest,
                        ActionName = "DockerImageManifest"
                    })
                }
            });

            var smokeTestsLambdaFunctionRole = new Role(this, "SmokeTestsLambdaFunctionRole", new RoleProps
            {
                RoleName = $"image-function-tests-{Guid.NewGuid()}",
                ManagedPolicies = new IManagedPolicy[] { ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole") },
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com")
            });

            // Smoke test AMD64 image
            var amd64SmokeTests = new Project(this, "SmokeTests-amd64", new ProjectProps
            {
                BuildSpec = BuildSpec.FromSourceFilename($"{Configuration.ProjectRoot}/SmokeTests/buildspec.yml"),
                Description = "Runs smoke tests on the built image.",
                Environment = new BuildEnvironment
                {
                    BuildImage = LinuxBuildImage.AMAZON_LINUX_2_3,
                    Privileged = true
                },
                Source = Amazon.CDK.AWS.CodeBuild.Source.CodeCommit(new CodeCommitSourceProps
                {
                    Repository = repository,
                    BranchOrRef = configuration.Source.BranchName
                }),
                EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                {
                    {"AWS_LAMBDA_SOURCE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                    {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = ecrRepositoryName}},
                    {"AWS_LAMBDA_SOURCE_IMAGE_TAG", new BuildEnvironmentVariable {Value = BaseImageMultiArch}},
                    {"AWS_LAMBDA_POWERSHELL_VERSION", new BuildEnvironmentVariable {Value = PowershellAmd64}},
                    {"AWS_LAMBDA_DOTNET_FRAMEWORK_VERSION", new BuildEnvironmentVariable {Value = framework}},
                    {"AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL", new BuildEnvironmentVariable {Value = channel}},
                    {"AWS_LAMBDA_DOTNET_BUILD_IMAGE", new BuildEnvironmentVariable {Value = dockerBuildImage}},
                    {"AWS_LAMBDA_DOTNET_SDK_VERSION", new BuildEnvironmentVariable {Value = configuration.DotnetSdkVersions.ContainsKey(framework) ? configuration.DotnetSdkVersions[framework] : string.Empty }},
                    {"AWS_LAMBDA_SMOKETESTS_LAMBDA_ROLE", new BuildEnvironmentVariable {Value = smokeTestsLambdaFunctionRole.RoleArn}}
                },
            });

            var smokeTestsPolicies = new List<PolicyStatement>();

            // ECR Policies
            smokeTestsPolicies.Add(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[]
                {
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
                },
                Resources = new[] { 
                    $"arn:aws:ecr:{configuration.Region}:{configuration.AccountId}:repository/image-function-tests",
                    $"arn:aws:ecr:{configuration.Region}:{configuration.AccountId}:repository/{ecrRepositoryName}"
                }
            }));

            // The following ECR policy needs to specify * as the resource since that is what is explicitly stated by the following error:
            // An error occurred (AccessDeniedException) when calling the GetAuthorizationToken operation:
            // User: *** is not authorized to perform: ecr:GetAuthorizationToken on resource: * because no identity-based policy
            // allows the ecr:GetAuthorizationToken action
            smokeTestsPolicies.Add(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[]
                {
                    "ecr:GetAuthorizationToken"
                },
                Resources = new[] { "*" }
            }));

            // IAM Policies
            smokeTestsPolicies.Add(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[]
                {
                    "iam:PassRole"
                },
                Resources = new[] { smokeTestsLambdaFunctionRole.RoleArn }
            }));

            // Lambda Policies
            smokeTestsPolicies.Add(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[]
                {
                    "lambda:CreateFunction",
                    "lambda:DeleteFunction",
                    "lambda:GetFunction",
                    "lambda:GetFunctionConfiguration",
                    "lambda:InvokeFunction",
                    "lambda:UpdateFunctionConfiguration"
                },
                Resources = new[] {
                    $"arn:aws:lambda:{configuration.Region}:{configuration.AccountId}:function:image-function-tests-*"
                }
            }));

            foreach (var policy in smokeTestsPolicies)
                amd64SmokeTests.AddToRolePolicy(policy);

            var smokeTestsActions = new List<Action>();
            smokeTestsActions.Add(new CodeBuildAction(new CodeBuildActionProps
            {
                Input = sourceArtifact,
                Project = amd64SmokeTests,
                ActionName = "amd64"
            }));

            if (configuration.DockerARM64Images.Contains(framework))
            {
                // Smoke test ARM64 image
                var arm64SmokeTests = new Project(this, "SmokeTests-arm64", new ProjectProps
                {
                    BuildSpec = BuildSpec.FromSourceFilename($"{Configuration.ProjectRoot}/SmokeTests/buildspec.yml"),
                    Description = "Runs smoke tests on the built image.",
                    Environment = new BuildEnvironment
                    {
                        BuildImage = LinuxArmBuildImage.AMAZON_LINUX_2_STANDARD_1_0,
                        Privileged = true
                    },
                    Source = Amazon.CDK.AWS.CodeBuild.Source.CodeCommit(new CodeCommitSourceProps
                    {
                        Repository = repository,
                        BranchOrRef = configuration.Source.BranchName
                    }),
                    EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                    {
                        {"AWS_LAMBDA_SOURCE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                        {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = ecrRepositoryName}},
                        {"AWS_LAMBDA_SOURCE_IMAGE_TAG", new BuildEnvironmentVariable {Value = BaseImageMultiArch}},
                        {"AWS_LAMBDA_POWERSHELL_VERSION", new BuildEnvironmentVariable {Value = PowershellArm64}},
                        {"AWS_LAMBDA_DOTNET_FRAMEWORK_VERSION", new BuildEnvironmentVariable {Value = framework}},
                        {"AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL", new BuildEnvironmentVariable {Value = channel}},
                        {"AWS_LAMBDA_DOTNET_BUILD_IMAGE", new BuildEnvironmentVariable {Value = dockerBuildImage}},
                        {"AWS_LAMBDA_DOTNET_SDK_VERSION", new BuildEnvironmentVariable {Value = configuration.DotnetSdkVersions.ContainsKey(framework) ? configuration.DotnetSdkVersions[framework] : string.Empty }},
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

            basePipeline.AddStage(new StageOptions
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
                        BuildImage = LinuxBuildImage.AMAZON_LINUX_2_3,
                        Privileged = true
                    },
                    Source = Amazon.CDK.AWS.CodeBuild.Source.CodeCommit(new CodeCommitSourceProps
                    {
                        Repository = repository,
                        BranchOrRef = configuration.Source.BranchName
                    }),
                    EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                    {
                        {"AWS_LAMBDA_SOURCE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                        {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = ecrRepositoryName}},
                        {"AWS_LAMBDA_DESTINATION_ECRS", new BuildEnvironmentVariable {Value = configuration.Ecrs.Beta}},
                        {"AWS_LAMBDA_MULTI_ARCH_IMAGE_TAG", new BuildEnvironmentVariable {Value = BaseImageMultiArch}},
                        {"AWS_LAMBDA_AMD64_IMAGE_TAG", new BuildEnvironmentVariable {Value = configuration.BaseImageAMD64Tags[framework]}},
                        {"AWS_LAMBDA_ARM64_IMAGE_TAG", new BuildEnvironmentVariable {Value = configuration.BaseImageARM64Tags[framework]}},
                        {"AWS_LAMBDA_INCLUDE_ARM64", new BuildEnvironmentVariable {Value = configuration.DockerARM64Images.Contains(framework).ToString()}},
                    }
                });

                betaDockerPush.AddToRolePolicy(ecrPolicy);

                basePipeline.AddStage(new StageOptions
                {
                    StageName = "Beta-DockerPush",
                    Actions = new Action[]
                    {
                        new CodeBuildAction(new CodeBuildActionProps
                        {
                            Input = sourceArtifact,
                            Project = betaDockerPush,
                            ActionName = "DockerPush"
                        })
                    }
                });
            }

            // Prod
            if (!string.IsNullOrWhiteSpace(configuration.Ecrs.Prod))
            {
                // Manual Approval
                basePipeline.AddStage(new StageOptions
                {
                    StageName = "Prod-ManualApproval",
                    Actions = new Action[]
                    {
                        new ManualApprovalAction(new ManualApprovalActionProps
                        {
                            ActionName = "ManualApproval"
                        })
                    }
                });

                var prodDockerPush = new Project(this, "Prod-DockerPush", new ProjectProps
                {
                    BuildSpec = BuildSpec.FromSourceFilename($"{Configuration.ProjectRoot}/DockerPush/buildspec.yml"),
                    Description = $"Pushes staged image to {configuration.Ecrs.Prod}",
                    Environment = new BuildEnvironment
                    {
                        BuildImage = LinuxBuildImage.AMAZON_LINUX_2_3,
                        Privileged = true
                    },
                    Source = Amazon.CDK.AWS.CodeBuild.Source.CodeCommit(new CodeCommitSourceProps
                    {
                        Repository = repository,
                        BranchOrRef = configuration.Source.BranchName
                    }),
                    EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                    {
                        {"AWS_LAMBDA_SOURCE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                        {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = ecrRepositoryName}},
                        {"AWS_LAMBDA_DESTINATION_ECRS", new BuildEnvironmentVariable {Value = configuration.Ecrs.Prod}},
                        {"AWS_LAMBDA_MULTI_ARCH_IMAGE_TAG", new BuildEnvironmentVariable {Value = BaseImageMultiArch}},
                        {"AWS_LAMBDA_AMD64_IMAGE_TAG", new BuildEnvironmentVariable {Value = configuration.BaseImageAMD64Tags[framework]}},
                        {"AWS_LAMBDA_ARM64_IMAGE_TAG", new BuildEnvironmentVariable {Value = configuration.BaseImageARM64Tags[framework]}},
                        {"AWS_LAMBDA_INCLUDE_ARM64", new BuildEnvironmentVariable {Value = configuration.DockerARM64Images.Contains(framework).ToString()}},
                    }
                });

                prodDockerPush.AddToRolePolicy(ecrPolicy);

                basePipeline.AddStage(new StageOptions
                {
                    StageName = "Prod-DockerPush",
                    Actions = new Action[]
                    {
                        new CodeBuildAction(new CodeBuildActionProps
                        {
                            Input = sourceArtifact,
                            Project = prodDockerPush,
                            ActionName = "DockerPush"
                        })
                    }
                });
            }
        }

        private string GetStageEcr(Construct scope, string ecrRepositoryName, Configuration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration.Ecrs.Stage))
            {
                var repository = new Amazon.CDK.AWS.ECR.Repository(scope, "StageEcr", new RepositoryProps
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
}