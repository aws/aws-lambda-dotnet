# Infrastructure for .NET Lambda Runtime Dockerfiles

Infrastructure project allows to create pipeline to build and push .NET Lambda Runtime Dockerfiles using CDK framework.

## Getting started
### Prerequisites
1. [AWS CLI](https://aws.amazon.com/cli/)
2. [AWS Account and User](https://portal.aws.amazon.com/billing/signup)
3. [Node.js](https://nodejs.org/)
4. [AWS CDK Toolkit](https://www.npmjs.com/package/aws-cdk)
5. [.NET Core 3.1 SDK or above](https://dotnet.microsoft.com/download)

### Bootstrap

`bootstrap.ps1` provisions resources the AWS CDK needs to perform the deployment and deploys generated CloudFormation template.

```powershell
.\bootstrap.ps1 `
-PipelineAccountId "AccountId" `
-CodeCommitAccountId "CodeCommitAccountId" `
-Profile "AccountProfile" `
-CodeCommitAccountProfile "CodeCommitAccountProfile" `
-Region "AwsRegion" `
-SourceRepositoryArn "arn:aws:codecommit:us-west-2:CodeCommitAccountId:aws-lambda-dotnet" `
-SourceBranchName "main" `
-StageEcr "AccountId.dkr.ecr.us-west-2.amazonaws.com" `
-BetaEcrs "AccountId.dkr.ecr.us-west-2.amazonaws.com;AccountId.dkr.ecr.us-west-2.amazonaws.com" `
-ProdEcrs "AccountId.dkr.ecr.us-west-2.amazonaws.com;AccountId.dkr.ecr.us-west-2.amazonaws.com" `
-EcrRepositoryName "awslambda/dotnet5.0-runtime;awslambda/dotnet6.0-runtime" `
-TargetFramework "net5;net6" `
-DotnetChannel "5.0;6.0"
```

#### Notes
 - AWS Profiles used to execute `bootstrap.ps1` must have administrator access.
 - All resources used to bootstrap the pipeline must already exist.
 - `AccountId` is AWS AccountId used for deploying CDK App.
 - `CodeCommitAccountId` is AWS AccountId that contains source repository.
 - If the CodeCommit repository is in the same account, use the same account Id for `PipelineAccountId` and `CodeCommitAccountId`.
 - When doing a cross-account deployment, you need to have AWS Profiles for both accounts.
 - `bootstrap.ps1` will run 2 `cdk bootstrap` commands for the cross account deployments to establish a trust relationships between the accounts. This way, we do not require a separate IAM role to be created manually.

## Useful commands
* `npx cdk bootstrap --cloudformation-execution-policies arn:aws:iam::aws:policy/AdministratorAccess` bootstrap this app
* `dotnet build` compile this app
* `cdk deploy`       deploy this stack to your default AWS account/region
* `cdk diff`         compare deployed stack with current state
* `cdk synth`        emits the synthesized CloudFormation template

