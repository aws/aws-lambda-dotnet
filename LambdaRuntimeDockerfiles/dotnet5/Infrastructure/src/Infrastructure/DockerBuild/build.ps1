param (
    [Parameter(HelpMessage = "ECR to push built image.")]
    [string] $StageEcr,

    [Parameter(HelpMessage = "ECR repository name to push final image.")]
    [string] $EcrRepositoryName,

    [Parameter(HelpMessage = "Final image tag to push.")]
    [string] $ImageTag
)

# Change the ErrorActionPreference to 'Stop' to allow aborting script on error
$ErrorActionPreference = 'Stop'

# Login in the stage ECR
$StageRegion = $StageEcr.Split(".")[3]
aws ecr get-login-password --region $StageRegion | docker login --username AWS --password-stdin $StageEcr
if (!$?)
{
    Write-Error "Failed to login in ${StageEcr}"
}

$SourceNameTagPair = "aws-lambda-dotnet-5:latest"

# Build runtime docker image
try
{
    # runtime docker image need to be built from the root of the reposiotory
    # so it can include the Amazon.Lambda.RuntimeSupport project in its Docker Build Context
    Push-Location $PSScriptRoot\..\..\..\..\..\..
    docker build -f (Join-Path $PWD '.\LambdaRuntimeDockerfiles\dotnet5\Dockerfile') -t $SourceNameTagPair .
}
finally
{
    Pop-Location
}

# Push built image
$DestinationUris = @(
    "$StageEcr/${EcrRepositoryName}:latest",
    "$StageEcr/${EcrRepositoryName}:${ImageTag}"
)

foreach ($DestinationUri in $DestinationUris)
{
    docker tag $SourceNameTagPair $DestinationUri
    if (!$?)
    {
        Write-Error "Failed to tag. SourceTag: ${SourceNameTagPair}, TargetTag: ${DestinationUri}"
    }

    docker push $DestinationUri
    if (!$?)
    {
        Write-Error "Failed to push at ${DestinationUri}"
    }
}