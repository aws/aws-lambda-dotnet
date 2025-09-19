// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.CDK;
using Constructs;

namespace Infrastructure;

internal class PipelinesStage : Stage
{
    public PipelinesStage(
        Construct scope,
        string id,
        Configuration configuration,
        IStageProps props = null) : base(scope, id, props)
    {
        // Create pipelines for main repository
        CreatePipelinesForRepository(configuration, 
            string.Empty, // cloudformation already prepends the parent stack name which is configuration.ProjectName so we don't need to add it again
            configuration.GitHubOwner, 
            configuration.GitHubRepository, 
            configuration.GitHubBranch);
        
        // Create pipelines for staging repository
        CreatePipelinesForRepository(configuration,
            "staging", // cloudformation already prepends the parent stack name which is configuration.ProjectName so we just add staging prefix only
            configuration.GitHubOwnerStaging,
            configuration.GitHubRepositoryStaging,
            configuration.GitHubBranchStaging);
    }

    private void CreatePipelinesForRepository(
        Configuration configuration, 
        string pipelinePrefix,
        string gitHubOwner, 
        string gitHubRepository, 
        string gitHubBranch)
    {
        for (var i = 0; i < configuration.Frameworks.Length; i++)
        {
            new PipelineStack(this,
                $"{pipelinePrefix}-{configuration.Frameworks[i].Framework}",
                configuration,
                configuration.Frameworks[i],
                gitHubOwner,
                gitHubRepository,
                gitHubBranch,
                new StackProps
                {
                    TerminationProtection = true,
                    Env = new Amazon.CDK.Environment
                    {
                        Account = configuration.AccountId,
                        Region = configuration.Region
                    }
                }
            );
        }
    }
}
