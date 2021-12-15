# Infrastructure for .NET 5 Lambda Runtime Dockerfile

Infrastructure project allows to create pipeline to build and push .NET 5 Lambda Runtime Dockerfile using CDK framework.

## Getting started
### Prerequisites
1. [AWS CLI](https://aws.amazon.com/cli/)
2. [AWS Account and User](https://portal.aws.amazon.com/billing/signup)
3. [Node.js](https://nodejs.org/)
4. [AWS CDK Toolkit](https://www.npmjs.com/package/aws-cdk)
5. [.NET Core 3.1 SDK or above](https://dotnet.microsoft.com/download)

### Bootstrap

#### Notes
 - AWS Profile used to execute `bootstrap.ps1` must have administrator access.
 - All resources used to bootstrap the pipeline must already exist.
 - `CdkAccountId` is AWS AccountId used for deploying CDK App.
 - `SourceAccountId` is AWS AccountId that contains source repository.
 
`bootstrap.ps1` provisions resources the AWS CDK needs to perform the deployment and deploys generated CloudFormation template.

```powershell
.\bootstrap.ps1 `
-SourceRepositoryArn "arn:aws:codecommit:us-west-2:CdkAccountId:aws-lambda-dotnet" `
-SourceBranchName "main" `
-StageEcr "AccountId.dkr.ecr.us-west-2.amazonaws.com" `
-BetaEcrs "AccountId.dkr.ecr.us-west-2.amazonaws.com;AccountId.dkr.ecr.us-west-2.amazonaws.com" `
-ProdEcrs "AccountId.dkr.ecr.us-west-2.amazonaws.com;AccountId.dkr.ecr.us-west-2.amazonaws.com" `
-EcrRepositoryName "awslambda/dotnet5.0-runtime;awslambda/dotnet6.0-runtime" `
-Profile "default" `
-DotnetFramework "net5;net6" `
-DotnetChannel "5.0;6.0"
```

If source repository exists in a separate AWS account, provide `SourceCrossAccountRoleArn` argument to bootstrap.
```powershell
.\bootstrap.ps1 `
-SourceRepositoryArn "arn:aws:codecommit:us-west-2:AccoundIdA:aws-lambda-dotnet" `
-SourceCrossAccountRoleArn "arn:aws:iam::SourceAccountId:role/aws-lambda-dotnet-source-account-role" `
-SourceBranchName "main" `
-StageEcr "AccountId.dkr.ecr.us-west-2.amazonaws.com" `
-BetaEcrs "AccountId.dkr.ecr.us-west-2.amazonaws.com;AccountId.dkr.ecr.us-west-2.amazonaws.com" `
-ProdEcrs "AccountId.dkr.ecr.us-west-2.amazonaws.com;AccountId.dkr.ecr.us-west-2.amazonaws.com" `
-EcrRepositoryName "awslambda/dotnet5.0-runtime;awslambda/dotnet6.0-runtime" `
-Profile "default" `
-TargetFramework "net5;net6" `
-DotnetChannel "5.0;6.0"
```

`arn:aws:iam::SourceAccountId:role/aws-lambda-dotnet-source-account-role` must have following permissions and trust relationships in `SourceAccountId`.

**Permissions**
```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "codecommit:UploadArchive",
                "kms:Decrypt",
                "s3:PutObject*",
                "codecommit:CancelUploadArchive",
                "s3:GetObject*",
                "kms:ReEncrypt*",
                "kms:GenerateDataKey*",
                "codecommit:GetCommit",
                "codecommit:GetUploadArchiveStatus",
                "s3:Abort*",
                "s3:List*",
                "kms:Encrypt",
                "codecommit:GetBranch",
                "kms:DescribeKey",
                "s3:GetBucket*",
                "s3:DeleteObject*"
            ],
            "Resource": [
                "arn:aws:kms:*:CdkAccountId:key/*",
                "arn:aws:codecommit:us-west-2:SourceAccountId:aws-lambda-dotnet",
                "arn:aws:s3:::aws-lambda-dotnet-5-cont-pipeline*"
            ]
        }
    ]
}
```

**Trust Relationships**
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "AWS": "arn:aws:iam::CdkAccountId:root" 
      },
      "Action": "sts:AssumeRole",
      "Condition": {}
    }
  ]
}
```

For consistency purpose, same ECR repository name is used across all ECRs.

## Useful commands
* `npx cdk bootstrap --cloudformation-execution-policies arn:aws:iam::aws:policy/AdministratorAccess` bootstrap this app
* `dotnet build` compile this app
* `cdk deploy`       deploy this stack to your default AWS account/region
* `cdk diff`         compare deployed stack with current state
* `cdk synth`        emits the synthesized CloudFormation template

