param (
    [Parameter(HelpMessage = "Source ECR containing source docker image.")]
    [string] $SourceEcr,

    [Parameter(HelpMessage = "Tag of the source image.")]
    [string]  $Amd64ImageTag,

    [Parameter(HelpMessage = "Tag of the source image.")]
    [string]  $Arm64ImageTag,

    [Parameter(HelpMessage = "Semicolon separated list of destincation ECR to push source image.")]
    [string] $DestinationEcrs,

    [Parameter(HelpMessage = "Tag of the destination image.")]
    [string] $MultiArchImageTag,

    [Parameter(HelpMessage = "ECR repository name for both source and destination ECRs.")]
    [string] $EcrRepositoryName,

    [Parameter(HelpMessage = "Indicates whether to include an Arm64 image in the manifest")]
    [string] $IncludeArm64
)

Import-Module $PSScriptRoot/../Common/manifest_push.psm1

# Change the ErrorActionPreference to 'Stop' to allow aborting script on error
$ErrorActionPreference = 'Stop'

# Login in the source ECR
$SourceRegion = $SourceEcr.Split(".")[3]
aws ecr get-login-password --region $SourceRegion | docker login --username AWS --password-stdin $SourceEcr
if (!$?)
{
    throw "Failed to login in ${SourceEcr}"
}

# Pull image from source ECR
$SourceAmd64Uri = "${SourceEcr}/${EcrRepositoryName}:${Amd64ImageTag}"
docker pull $SourceAmd64Uri

if ($IncludeArm64 -eq "True")
{
    $SourceArm64Uri = "${SourceEcr}/${EcrRepositoryName}:${Arm64ImageTag}"
    docker pull $SourceArm64Uri
}

# Push pulled image to desination ECRs
foreach ($DestinationEcr in $DestinationEcrs.Split(";"))
{
    # Login in the source ECR
    $DestinationRegion = $DestinationEcr.Split(".")[3]
    aws ecr get-login-password --region $DestinationRegion | docker login --username AWS --password-stdin $DestinationEcr
    if (!$?)
    {
        throw "Failed to login in ${DestinationEcr}"
    }

    # Tag and push Amd64 image to destination ECR
    $Amd64Uri = "${DestinationEcr}/${EcrRepositoryName}:${Amd64ImageTag}"
    docker tag $SourceAmd64Uri $Amd64Uri
    if (!$?)
    {
        throw "Failed to tag. SourceTag: ${SourceAmd64Uri}, TargetTag: ${Amd64Uri}"
    }

    docker push $Amd64Uri
    if (!$?)
    {
        throw "Failed to push at ${Amd64Uri}"
    }

    if ($IncludeArm64 -eq "True")
    {
        # Tag and push Arm64 image to destination ECR
        $Arm64Uri = "${DestinationEcr}/${EcrRepositoryName}:${Arm64ImageTag}"
        docker tag $SourceArm64Uri $Arm64Uri
        if (!$?)
        {
            throw "Failed to tag. SourceTag: ${SourceArm64Uri}, TargetTag: ${Arm64Uri}"
        }

        docker push $Arm64Uri
        if (!$?)
        {
            throw "Failed to push at ${Arm64Uri}"
        }
    }

    # Push multi arch to destination ECR
    $ManifestList = "${DestinationEcr}/${EcrRepositoryName}:${MultiArchImageTag}"
    if ($IncludeArm64 -eq "True")
    {
        Push-MultiArchImageManifest -ManifestList $ManifestList -Amd64Manifest $Amd64Uri -Arm64Manifest $Arm64Uri
    }
    else
    {
        Push-MultiArchImageManifest -ManifestList $ManifestList -Amd64Manifest $Amd64Uri
    }
}