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

namespace Infrastructure
{
    internal class Configuration
    {
        public Source Source { get; } = new Source();
        public Ecrs Ecrs { get; } = new Ecrs();
        public string EcrRepositoryName { get; } = Environment.GetEnvironmentVariable("AWS_LAMBDA_ECR_REPOSITORY_NAME");
        public const string ProjectRoot = "LambdaRuntimeDockerfiles/dotnet5/Infrastructure/src/Infrastructure";
        public const string ProjectName = "aws-lambda-dotnet-5-container-image";
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