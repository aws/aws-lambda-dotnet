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

using System.Collections.Generic;
using System.Linq;
using Amazon.CDK;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.CodeCommit;
using Amazon.CDK.AWS.CodePipeline;
using Amazon.CDK.AWS.CodePipeline.Actions;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.Pipelines;
using RepositoryProps = Amazon.CDK.AWS.ECR.RepositoryProps;

namespace Infrastructure
{
    public class PipelineStack : Stack
    {
        private const string PowershellArm64 = "7.1.3 powershell-7.1.3-linux-arm64.tar.gz";
        private const string PowershellAmd64 = "7.1.3 powershell-7.1.3-linux-x64.tar.gz";
        private const string BaseImageMultiArch = "base-image-multi-arch";

        internal PipelineStack(
            Construct scope, 
            string id, 
            string ecrRepositoryName, 
            string framework, 
            string channel,
            string dockerBuildImage,
            Configuration configuration, 
            IStackProps props = null) : base(scope, $"{id}-{framework}", props)
        {
            var repository = Repository.FromRepositoryArn(this, "Repository", configuration.Source.RepositoryArn);

            var sourceArtifact = new Artifact_();
            var outputArtifact = new Artifact_();
            var ecrPolicy = new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[] {"ecr:*"},
                Resources = new[] {"*"}
            });


            // Setup CodeCommit cross account role access policies if required
            IRole codeCommitRole = null;
            if (!string.IsNullOrWhiteSpace(configuration.Source.CrossAccountRoleArn))
            {
                codeCommitRole = Role.FromRoleArn(this, "CodeCommitRole", configuration.Source.CrossAccountRoleArn, new FromRoleArnOptions
                {
                    Mutable = false // Flag to indicate CDK to not modify the role
                });
            }

            // Strict ordering is required to make sure CloudFormation template doesn't result in false difference
            var environmentVariablesToCopy = System.Environment.GetEnvironmentVariables()
                .Keys.Cast<string>()
                .Where(variable => variable.StartsWith("AWS_LAMBDA_"))
                .OrderBy(variable => variable);

            // Self mutation
            var pipeline = new CdkPipeline(this, "Pipeline", new CdkPipelineProps
            {
                PipelineName = $"{id}-{framework}",
                CloudAssemblyArtifact = outputArtifact,

                SourceAction = new CodeCommitSourceAction(new CodeCommitSourceActionProps
                {
                    ActionName = "CodeCommit",
                    Output = sourceArtifact,
                    Repository = repository,
                    Branch = configuration.Source.BranchName,
                    Role = codeCommitRole,
                    Trigger = CodeCommitTrigger.POLL
                }),

                // It synthesizes CDK code to cdk.out directory which is picked by SelfMutate stage to mutate the pipeline
                SynthAction = new SimpleSynthAction(new SimpleSynthActionProps
                {
                    SourceArtifact = sourceArtifact,
                    CloudAssemblyArtifact = outputArtifact,
                    Subdirectory = "LambdaRuntimeDockerfiles/Infrastructure",
                    InstallCommands = new[]
                    {
                        "npm install -g aws-cdk",
                    },
                    BuildCommands = new[] {"dotnet build"},
                    SynthCommand = "cdk synth",
                    CopyEnvironmentVariables = environmentVariablesToCopy.ToArray()
                })
            });

            var stageEcr = GetStageEcr(this, ecrRepositoryName, configuration);

            var dockerBuildStage = pipeline.AddStage("Stage-DockerBuild");

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
                    {"AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL", new BuildEnvironmentVariable {Value = channel}}
                }
            });

            dockerBuildAmd64.AddToRolePolicy(ecrPolicy);
            dockerBuildStage.AddActions(new CodeBuildAction(new CodeBuildActionProps
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
                        BuildImage = LinuxBuildImage.AMAZON_LINUX_2_ARM,
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
                        {"AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL", new BuildEnvironmentVariable {Value = channel}}
                    }
                });

                dockerBuildArm64.AddToRolePolicy(ecrPolicy);
                dockerBuildStage.AddActions(new CodeBuildAction(new CodeBuildActionProps
                {
                    Input = sourceArtifact,
                    Project = dockerBuildArm64,
                    ActionName = "arm64"
                }));
            }

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

            var dockerImageManifestStage = pipeline.AddStage("DockerImageManifest");
            dockerImageManifestStage.AddActions(new CodeBuildAction(new CodeBuildActionProps
            {
                Input = sourceArtifact,
                Project = dockerImageManifest,
                ActionName = "DockerImageManifest"
            }));

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
                }
            });

            var smokeTestsPolicy = new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[]
                {
                    "sts:*",
                    "iam:*",
                    "ecr:*",
                    "lambda:*"
                },
                Resources = new[] {"*"}
            });

            amd64SmokeTests.AddToRolePolicy(smokeTestsPolicy);

            var smokeTestsStage = pipeline.AddStage("SmokeTests");
            smokeTestsStage.AddActions(new CodeBuildAction(new CodeBuildActionProps
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
                        BuildImage = LinuxBuildImage.AMAZON_LINUX_2_ARM,
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
                    }
                });

                arm64SmokeTests.AddToRolePolicy(smokeTestsPolicy);

                smokeTestsStage.AddActions(new CodeBuildAction(new CodeBuildActionProps
                {
                    Input = sourceArtifact,
                    Project = arm64SmokeTests,
                    ActionName = "arm64"
                }));
            }

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

                var betaDockerPushStage = pipeline.AddStage("Beta-DockerPush");
                betaDockerPushStage.AddActions(new CodeBuildAction(new CodeBuildActionProps
                {
                    Input = sourceArtifact,
                    Project = betaDockerPush,
                    ActionName = "DockerPush"
                }));
            }


            // Prod
            if (!string.IsNullOrWhiteSpace(configuration.Ecrs.Prod))
            {
                // Manual Approval
                var manualApprovalStage = pipeline.AddStage("Prod-ManualApproval");
                manualApprovalStage.AddActions(new ManualApprovalAction(new ManualApprovalActionProps
                {
                    ActionName = "ManualApproval"
                }));


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

                var prodDockerPushStage = pipeline.AddStage("Prod-DockerPush");
                prodDockerPushStage.AddActions(new CodeBuildAction(new CodeBuildActionProps
                {
                    Input = sourceArtifact,
                    Project = prodDockerPush,
                    ActionName = "DockerPush"
                }));
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