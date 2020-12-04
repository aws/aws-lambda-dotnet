param (
    [Parameter(HelpMessage = "Soruce ECR containing source docker image.")]
    [string] $SourceEcr,

    [Parameter(HelpMessage = "Tag of the source image.")]
    [string]  $SourceImageTag,

    [Parameter(HelpMessage = "Semicolon seperated list of destincation ECR to push source image.")]
    [string] $DestinationEcrs,

    [Parameter(HelpMessage = "Tag of the destination image.")]
    [string] $DestinationImageTag,

    [Parameter(HelpMessage = "ECR repository name for both source and destination ECRs.")]
    [string] $EcrRepositoryName
)

# Change the ErrorActionPreference to 'Stop' to allow aborting script on error
$ErrorActionPreference = 'Stop'

# Login in the source ECR
$SourceRegion = $SourceEcr.Split(".")[3]
aws ecr get-login-password --region $SourceRegion | docker login --username AWS --password-stdin $SourceEcr
if (!$?)
{
    Write-Error "Failed to login in ${SourceEcr}"
}

# Pull image from source ECR
$SourceUri = "${SourceEcr}/${EcrRepositoryName}:${SourceImageTag}"
docker pull $SourceUri

# Push pulled image to desination ECRs
foreach ($DestinationEcr in $DestinationEcrs.Split(";"))
{
    # Login in the source ECR
    $DestinationRegion = $DestinationEcr.Split(".")[3]
    aws ecr get-login-password --region $DestinationRegion | docker login --username AWS --password-stdin $DestinationEcr
    if (!$?)
    {
        Write-Error "Failed to login in ${DestinationEcr}"
    }

    # Tag and push image to destination ECR
    $DesinationUri = "${DestinationEcr}/${EcrRepositoryName}:${DestinationImageTag}"
    docker tag $SourceUri $DesinationUri
    if (!$?)
    {
        Write-Error "Failed to tag. SourceTag: ${SourceUri}, TargetTag: ${DestinationUri}"
    }

    docker push $DesinationUri
    if (!$?)
    {
        Write-Error "Failed to push at ${DesinationUri}"
    }
}