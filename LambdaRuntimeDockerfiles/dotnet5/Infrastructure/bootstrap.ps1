param (
    [Parameter(Mandatory = $true, HelpMessage = "Source repository ARN.")]
    [string] $SourceRepositoryArn,

    [Parameter(Mandatory = $true, HelpMessage = "Source branch name.")]
    [string] $SourceBranchName,

    [Parameter(Mandatory = $false, HelpMessage = "Role ARN to allow cross AWS account CodeCommit access.")]
    [string] $SourceCrossAccountRoleArn,

    [Parameter(Mandatory = $false, HelpMessage = "ECR URI to store Stage images.")]
    [string] $StageEcr,

    [Parameter(Mandatory = $false, HelpMessage = "Semicolon seperated ECR URIs to store Beta images.")]
    [string] $BetaEcrs,

    [Parameter(Mandatory = $false, HelpMessage = "Semicolon seperated ECR URIs to store Prod images.")]
    [string] $ProdEcrs,

    [Parameter(Mandatory = $true, HelpMessage = "ECR repository name for Stage, Beta and Prod images.")]
    [string] $EcrRepositoryName,

    [Parameter(Mandatory = $true, HelpMessage = "AWS Profile used to created resources.")]
    [string] $Profile
)

$env:AWS_LAMBDA_SOURCE_REPOSITORY_ARN = $SourceRepositoryArn
$env:AWS_LAMBDA_SOURCE_BRANCH_NAME = $SourceBranchName
$env:AWS_LAMBDA_SOURCE_CROSS_ACCOUNT_ROLE_ARN = $SourceCrossAccountRoleArn

$env:AWS_LAMBDA_STAGE_ECR = $StageEcr
$env:AWS_LAMBDA_BETA_ECRS = $BetaEcrs
$env:AWS_LAMBDA_PROD_ECRS = $ProdEcrs

$env:AWS_LAMBDA_ECR_REPOSITORY_NAME = $EcrRepositoryName

npx cdk bootstrap --cloudformation-execution-policies arn:aws:iam::aws:policy/AdministratorAccess --profile $Profile
npx cdk deploy --require-approval never --profile $Profile
