# Infrastructure for .NET Lambda Runtime Dockerfiles

Infrastructure project allows to create pipeline to build and push .NET Lambda Runtime Dockerfiles using CDK framework.

## Getting started
### Prerequisites
1. [AWS CLI](https://aws.amazon.com/cli/)
2. [AWS Account and User](https://portal.aws.amazon.com/billing/signup)
3. [Node.js](https://nodejs.org/)
4. [AWS CDK Toolkit](https://www.npmjs.com/package/aws-cdk)
5. [.NET 8 SDK or above](https://dotnet.microsoft.com/download)

### Bootstrap

`bootstrap.ps1` provisions resources the AWS CDK needs to perform the deployment and deploys generated CloudFormation template.

```powershell
.\bootstrap.ps1 `
-PipelineAccountId "AccountId" `
-Region "AwsRegion" `
-GitHubTokenSecretName "SecretName" `
-GitHubTokenSecretKey "Key" `
-GitHubRepoOwner "GitHubOwner" `
-GitHubRepoName "GitHubRepo" `
-GitHubRepoBranch "GitHubBranch" `
-StageEcr "AccountId.dkr.ecr.us-west-2.amazonaws.com" `
-BetaEcrs "AccountId.dkr.ecr.us-west-2.amazonaws.com;AccountId.dkr.ecr.us-west-2.amazonaws.com" `
-ProdEcrs "AccountId.dkr.ecr.us-west-2.amazonaws.com;AccountId.dkr.ecr.us-west-2.amazonaws.com" `
-EcrRepositoryName "awslambda/dotnet6.0-runtime;awslambda/dotnet8-runtime;awslambda/dotnet9-runtime" `
-TargetFramework "net6;net8;net9" `
-DotnetChannel "6.0;8.0;9.0"
```

#### Notes
 - AWS Profiles used to execute `bootstrap.ps1` must have administrator access.
 - All resources used to bootstrap the pipeline must already exist.
 - `AccountId` is AWS AccountId used for deploying CDK App.

## Useful commands
* `npx cdk bootstrap --cloudformation-execution-policies arn:aws:iam::aws:policy/AdministratorAccess` bootstrap this app
* `dotnet build` compile this app
* `cdk deploy`       deploy this stack to your default AWS account/region
* `cdk diff`         compare deployed stack with current state
* `cdk synth`        emits the synthesized CloudFormation template
