param (
    [Parameter(Mandatory = $true, HelpMessage = "Pipeline Account ID.")]
    [string] $PipelineAccountId,

    [Parameter(Mandatory = $true, HelpMessage = "Code Commit Account ID.")]
    [string] $CodeCommitAccountId,

    [Parameter(Mandatory = $true, HelpMessage = "Region.")]
    [string] $Region,

    [Parameter(Mandatory = $true, HelpMessage = "Source repository ARN.")]
    [string] $SourceRepositoryArn,

    [Parameter(Mandatory = $true, HelpMessage = "Source branch name.")]
    [string] $SourceBranchName,

    [Parameter(Mandatory = $false, HelpMessage = "ECR URI to store Stage images.")]
    [string] $StageEcr,

    [Parameter(Mandatory = $false, HelpMessage = "Semicolon separated ECR URIs to store Beta images.")]
    [string] $BetaEcrs,

    [Parameter(Mandatory = $false, HelpMessage = "Semicolon separated ECR URIs to store Prod images.")]
    [string] $ProdEcrs,

    [Parameter(Mandatory = $true, HelpMessage = "ECR repository name for Stage, Beta and Prod images.")]
    [string] $EcrRepositoryName,

    [Parameter(Mandatory = $true, HelpMessage = "AWS Profile used to created resources.")]
    [string] $Profile,

    [Parameter(Mandatory = $true, HelpMessage = "AWS Profile for the CodeCommmit AWS account.")]
    [string] $CodeCommitAccountProfile,

    [Parameter(Mandatory = $true, HelpMessage = "The target .NET framework to create a pipeline for.")]
    [string] $TargetFramework,

    [Parameter(Mandatory = $true, HelpMessage = "The .NET channel corresponding to the Target Framework.")]
    [string] $DotnetChannel
)

$env:AWS_LAMBDA_PIPELINE_ACCOUNT_ID = $PipelineAccountId
$env:AWS_LAMBDA_PIPELINE_CODECOMMIT_ACCOUNT_ID = $CodeCommitAccountId
$env:AWS_LAMBDA_PIPELINE_REGION = $Region

$env:AWS_LAMBDA_SOURCE_REPOSITORY_ARN = $SourceRepositoryArn
$env:AWS_LAMBDA_SOURCE_BRANCH_NAME = $SourceBranchName

$env:AWS_LAMBDA_STAGE_ECR = $StageEcr
$env:AWS_LAMBDA_BETA_ECRS = $BetaEcrs
$env:AWS_LAMBDA_PROD_ECRS = $ProdEcrs

$env:AWS_LAMBDA_ECR_REPOSITORY_NAME = $EcrRepositoryName

$env:AWS_LAMBDA_DOTNET_FRAMEWORK_VERSION = $TargetFramework
$env:AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL = $DotnetChannel

npx cdk bootstrap --cloudformation-execution-policies arn:aws:iam::aws:policy/AdministratorAccess aws://$PipelineAccountId/$Region --profile $Profile
npx cdk bootstrap --cloudformation-execution-policies arn:aws:iam::aws:policy/AdministratorAccess --trust $PipelineAccountId aws://$CodeCommitAccountId/$Region --profile $CodeCommitAccountProfile
npx cdk deploy --require-approval never --all --profile $Profile
