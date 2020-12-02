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

using Amazon.CDK;
using Amazon.CDK.AWS.CodePipeline;
using Amazon.CDK.AWS.CodePipeline.Actions;
using Amazon.CDK.Pipelines;
using System.Collections.Generic;
using System.Linq;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.IAM;
using Repository = Amazon.CDK.AWS.CodeCommit.Repository;
using RepositoryProps = Amazon.CDK.AWS.ECR.RepositoryProps;


namespace Infrastructure
{
    public class PipelineStack : Stack
    {
        internal PipelineStack(Construct scope, string id, Configuration configuration, IStackProps props = null) : base(scope, id, props)
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
                codeCommitRole = Role.FromRoleArn(this, "CodeCommitRole", configuration.Source.CrossAccountRoleArn, new FromRoleArnOptions()
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
                PipelineName = Configuration.ProjectName,
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
                    Subdirectory = "LambdaRuntimeDockerfiles/dotnet5/Infrastructure",
                    InstallCommands = new[]
                    {
                        "npm install -g aws-cdk",
                    },
                    BuildCommands = new[] {"dotnet build"},
                    SynthCommand = "cdk synth",
                    CopyEnvironmentVariables = environmentVariablesToCopy.ToArray()
                })
            });

            var stageEcr = GetStageEcr(this, configuration);

            // Stage
            var dockerBuild = new Project(this, "DockerBuild", new ProjectProps()
            {
                BuildSpec = BuildSpec.FromSourceFilename($"{Configuration.ProjectRoot}/DockerBuild/buildspec.yml"),
                Description = $"Builds and pushes image to {stageEcr}",
                Environment = new BuildEnvironment()
                {
                    BuildImage = LinuxBuildImage.AMAZON_LINUX_2_3,
                    Privileged = true
                },
                Source = Amazon.CDK.AWS.CodeBuild.Source.CodeCommit(new CodeCommitSourceProps()
                {
                    Repository = repository,
                    BranchOrRef = configuration.Source.BranchName
                }),
                EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                {
                    {"AWS_LAMBDA_STAGE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                    {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = configuration.EcrRepositoryName}}
                }
            });

            dockerBuild.AddToRolePolicy(ecrPolicy);

            var dockerBuildStage = pipeline.AddStage("Stage-DockerBuild");
            dockerBuildStage.AddActions(new CodeBuildAction(new CodeBuildActionProps()
            {
                Input = sourceArtifact,
                Project = dockerBuild,
                ActionName = "DockerBuild"
            }));


            // Beta
            if (!string.IsNullOrWhiteSpace(configuration.Ecrs.Beta))
            {
                var betaDockerPush = new Project(this, "Beta-DockerPush", new ProjectProps()
                {
                    BuildSpec = BuildSpec.FromSourceFilename($"{Configuration.ProjectRoot}/DockerPush/buildspec.yml"),
                    Description = $"Pushes staged image to {configuration.Ecrs.Beta}",
                    Environment = new BuildEnvironment()
                    {
                        BuildImage = LinuxBuildImage.AMAZON_LINUX_2_3,
                        Privileged = true
                    },
                    Source = Amazon.CDK.AWS.CodeBuild.Source.CodeCommit(new CodeCommitSourceProps()
                    {
                        Repository = repository,
                        BranchOrRef = configuration.Source.BranchName
                    }),
                    EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                    {
                        {"AWS_LAMBDA_SOURCE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                        {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = configuration.EcrRepositoryName}},
                        {"AWS_LAMBDA_DESTINATION_ECRS", new BuildEnvironmentVariable {Value = configuration.Ecrs.Beta}},
                        {"AWS_LAMBDA_DESTINATION_IMAGE_TAG", new BuildEnvironmentVariable {Value = "beta"}},
                    }
                });

                betaDockerPush.AddToRolePolicy(ecrPolicy);

                var betaDockerPushStage = pipeline.AddStage("Beta-DockerPush");
                betaDockerPushStage.AddActions(new CodeBuildAction(new CodeBuildActionProps()
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
                manualApprovalStage.AddActions(new ManualApprovalAction(new ManualApprovalActionProps()
                {
                    ActionName = "ManualApproval"
                }));


                var prodDockerPush = new Project(this, "Prod-DockerPush", new ProjectProps()
                {
                    BuildSpec = BuildSpec.FromSourceFilename($"{Configuration.ProjectRoot}/DockerPush/buildspec.yml"),
                    Description = $"Pushes staged image to {configuration.Ecrs.Prod}",
                    Environment = new BuildEnvironment()
                    {
                        BuildImage = LinuxBuildImage.AMAZON_LINUX_2_3,
                        Privileged = true
                    },
                    Source = Amazon.CDK.AWS.CodeBuild.Source.CodeCommit(new CodeCommitSourceProps()
                    {
                        Repository = repository,
                        BranchOrRef = "dotnet5/cdk"
                    }),
                    EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                    {
                        {"AWS_LAMBDA_SOURCE_ECR", new BuildEnvironmentVariable {Value = stageEcr}},
                        {"AWS_LAMBDA_ECR_REPOSITORY_NAME", new BuildEnvironmentVariable {Value = configuration.EcrRepositoryName}},
                        {"AWS_LAMBDA_DESTINATION_ECRS", new BuildEnvironmentVariable {Value = configuration.Ecrs.Prod}},
                        {"AWS_LAMBDA_DESTINATION_IMAGE_TAG", new BuildEnvironmentVariable {Value = "beta"}}, // Prod images are also tagged as beta
                    }
                });

                prodDockerPush.AddToRolePolicy(ecrPolicy);

                var prodDockerPushStage = pipeline.AddStage("Prod-DockerPush");
                prodDockerPushStage.AddActions(new CodeBuildAction(new CodeBuildActionProps()
                {
                    Input = sourceArtifact,
                    Project = prodDockerPush,
                    ActionName = "DockerPush"
                }));
            }
        }

        private string GetStageEcr(Construct scope, Configuration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration.Ecrs.Stage))
            {
                var repository = new Amazon.CDK.AWS.ECR.Repository(scope, "StageEcr", new RepositoryProps
                {
                    RepositoryName = configuration.EcrRepositoryName
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