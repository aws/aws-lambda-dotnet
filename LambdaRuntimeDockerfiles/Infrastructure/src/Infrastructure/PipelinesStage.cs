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
        for (var i = 0; i < configuration.Frameworks.Length; i++)
        {
            new PipelineStack(this,
                configuration.Frameworks[i].Framework,
                configuration,
                configuration.Frameworks[i],
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
