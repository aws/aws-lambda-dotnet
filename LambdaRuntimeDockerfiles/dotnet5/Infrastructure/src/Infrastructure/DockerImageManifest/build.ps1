param (
    [Parameter(HelpMessage = "ECR to push built image.")]
    [string] $StageEcr,

    [Parameter(HelpMessage = "ECR repository name to push final image.")]
    [string] $EcrRepositoryName,

    [Parameter(HelpMessage = "Final image tag to push.")]
    [string] $MultiArchImageTag,

    [Parameter(HelpMessage = "Image archtectures")]
    [string] $Arm64ImageTag,

    [Parameter(HelpMessage = "Image archtectures")]
    [string] $Amd64ImageTag
)

Import-Module $PSScriptRoot/../Common/manifest_push.psm1

# Change the ErrorActionPreference to 'Stop' to allow aborting script on error
$ErrorActionPreference = 'Stop'

# Login in the source ECR
$StageRegion = $StageEcr.Split(".")[3]
aws ecr get-login-password --region $StageRegion | docker login --username AWS --password-stdin $StageEcr
if (!$?)
{
    throw "Failed to login in ${StageEcr}"
}

# Push multi arch to stage ECR
$ManifestList = "${StageEcr}/${EcrRepositoryName}:${MultiArchImageTag}"
$Amd64Uri = "${StageEcr}/${EcrRepositoryName}:${Amd64ImageTag}"
$Arm64Uri = "${StageEcr}/${EcrRepositoryName}:${Arm64ImageTag}"
Push-MultiArchImageManifest -ManifestList $ManifestList -Amd64Manifest $Amd64Uri -Arm64Manifest $Arm64Uri