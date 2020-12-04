param (
    [Parameter(Mandatory = $true, HelpMessage = "Source repository ARN.")]
    [string] $SourceRepositoryArn,

    [Parameter(Mandatory = $true, HelpMessage = "Source branch name.")]
    [string] $SourceBranchName,

    [Parameter(HelpMessage = "Role ARN to allow cross AWS account CodeCommit access.")]
    [string] $SourceCrossAccountRoleArn,

    [Parameter(Mandatory = $true, HelpMessage = "Semicolon seperated ECR URIs to pull base images.")]
    [string] $BaseEcrs,

    [Parameter(Mandatory = $true, HelpMessage = "ECR URI to store Stage images.")]
    [string] $StageEcr,

    [Parameter(Mandatory = $true, HelpMessage = "Semicolon seperated ECR URIs to store Beta images.")]
    [string] $BetaEcrs,

    [Parameter(Mandatory = $true, HelpMessage = "Semicolon seperated ECR URIs to store Prod images.")]
    [string] $ProdEcrs,

    [Parameter(HelpMessage = "ECR repository name for Stage, Beta and Prod images.")]
    [string] $EcrRepositoryName,

    [Parameter(Mandatory = $true, HelpMessage = "AWS Profile used to created resources.")]
    [string] $Profile
)

$env:SOURCE_REPOSITORY_ARN = $SourceRepositoryArn
$env:SOURCE_BRANCH_NAME = $SourceBranchName
$env:SOURCE_CROSS_ACCOUNT_ROLE_ARN = $SourceCrossAccountRoleArn

$env:BASE_ECRS = $BaseEcrs
$env:STAGE_ECR = $StageEcr
$env:BETA_ECRS = $BetaEcrs
$env:PROD_ECRS = $ProdEcrs

$env:ECR_REPOSITORY_NAME = $EcrRepositoryName

npx cdk bootstrap --cloudformation-execution-policies arn:aws:iam::aws:policy/AdministratorAccess --profile $Profile
npx cdk deploy --require-approval never --profile $Profile
