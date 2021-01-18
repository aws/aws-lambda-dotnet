param (
    [Parameter(HelpMessage = "Soruce ECR containing source docker image.")]
    [string] $SourceEcr,

    [Parameter(HelpMessage = "Tag of the source image.")]
    [string]  $SourceImageTag,

    [Parameter(HelpMessage = "ECR repository name of source docker image.")]
    [string] $EcrRepositoryName
)

$BASE_IMAGE = "${SourceEcr}/${EcrRepositoryName}:${SourceImageTag}"

./LambdaRuntimeDockerfiles/dotnet5/SmokeTests/test/ImageFunction.SmokeTests/build.ps1 -BaseImage $BASE_IMAGE

if (!$?)
{
    Write-Error "Smoke tests failed."
}