param (
    [Parameter(HelpMessage = "ECR to push built image.")]
    [string] $StageEcr,

    [Parameter(HelpMessage = "ECR repository name to push final image.")]
    [string] $EcrRepositoryName,

    [Parameter(HelpMessage = "Final image tag to push.")]
    [string] $MultiArchImageTag,

    [Parameter(HelpMessage = "Image architectures")]
    [string] $Arm64ImageTag,

    [Parameter(HelpMessage = "Image architectures")]
    [string] $Amd64ImageTag,

    [Parameter(HelpMessage = "Indicates whether to include an Arm64 image in the manifest")]
    [string] $IncludeArm64
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
if ($IncludeArm64 -eq "True")
{
    Push-MultiArchImageManifest -ManifestList $ManifestList -Amd64Manifest $Amd64Uri -Arm64Manifest $Arm64Uri
}
else
{
    Push-MultiArchImageManifest -ManifestList $ManifestList -Amd64Manifest $Amd64Uri
}