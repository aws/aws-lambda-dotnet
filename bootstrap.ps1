param (
    [Parameter(Mandatory = $true, HelpMessage = "Pipeline Account ID.")]
    [string] $PipelineAccountId,

    [Parameter(Mandatory = $false, HelpMessage = "Pipeline name suffix.")]
    [string] $PipelineNameSuffix,

    [Parameter(Mandatory = $true, HelpMessage = "Region.")]
    [string] $Region,

    [Parameter(Mandatory = $true, HelpMessage = "The name of the secret in Secret Manager that contains the GitHub Access Token.")]
    [string] $GitHubTokenSecretName,

    [Parameter(Mandatory = $true, HelpMessage = "The secret key in Secret Manager that contains the GitHub Access Token.")]
    [string] $GitHubTokenSecretKey,

    [Parameter(Mandatory = $true, HelpMessage = "GitHub repository owner name.")]
    [string] $GitHubRepoOwner,

    [Parameter(Mandatory = $true, HelpMessage = "GitHub repository name.")]
    [string] $GitHubRepoName,

    [Parameter(Mandatory = $true, HelpMessage = "GitHub repository branch name.")]
    [string] $GitHubRepoBranch,

    [Parameter(Mandatory = $false, HelpMessage = "ECR URI to store Stage images.")]
    [string] $StageEcr,

    [Parameter(Mandatory = $false, HelpMessage = "Semicolon separated ECR URIs to store Beta images.")]
    [string] $BetaEcrs,

    [Parameter(Mandatory = $false, HelpMessage = "Semicolon separated ECR URIs to store Prod images.")]
    [string] $ProdEcrs,

    [Parameter(Mandatory = $true, HelpMessage = "ECR repository name for Stage, Beta and Prod images.")]
    [string] $EcrRepositoryName,

    [Parameter(Mandatory = $true, HelpMessage = "The target .NET framework to create a pipeline for.")]
    [string] $TargetFramework,

    [Parameter(Mandatory = $true, HelpMessage = "The .NET channel corresponding to the Target Framework.")]
    [string] $DotnetChannel
)

$env:AWS_LAMBDA_PIPELINE_ACCOUNT_ID = $PipelineAccountId
$env:AWS_LAMBDA_PIPELINE_REGION = $Region
$env:AWS_LAMBDA_PIPELINE_NAME_SUFFIX = $PipelineNameSuffix

$env:AWS_LAMBDA_GITHUB_TOKEN_SECRET_NAME = $GitHubTokenSecretName
$env:AWS_LAMBDA_GITHUB_TOKEN_SECRET_KEY = $GitHubTokenSecretKey

$env:AWS_LAMBDA_GITHUB_REPO_OWNER = $GitHubRepoOwner
$env:AWS_LAMBDA_GITHUB_REPO_NAME = $GitHubRepoName
$env:AWS_LAMBDA_GITHUB_REPO_BRANCH = $GitHubRepoBranch

$env:AWS_LAMBDA_STAGE_ECR = $StageEcr
$env:AWS_LAMBDA_BETA_ECRS = $BetaEcrs
$env:AWS_LAMBDA_PROD_ECRS = $ProdEcrs

$env:AWS_LAMBDA_ECR_REPOSITORY_NAME = $EcrRepositoryName

$env:AWS_LAMBDA_DOTNET_FRAMEWORK_VERSION = $TargetFramework
$env:AWS_LAMBDA_DOTNET_FRAMEWORK_CHANNEL = $DotnetChannel

npx cdk bootstrap --cloudformation-execution-policies arn:aws:iam::aws:policy/AdministratorAccess aws://$PipelineAccountId/$Region
npx cdk deploy --require-approval never --all