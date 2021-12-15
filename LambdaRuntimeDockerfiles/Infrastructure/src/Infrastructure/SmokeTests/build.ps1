param (
    [Parameter(HelpMessage = "Source ECR containing source docker image.")]
    [string] $SourceEcr,

    [Parameter(HelpMessage = "Tag of the source image.")]
    [string]  $SourceImageTag,

    [Parameter(HelpMessage = "ECR repository name of source docker image.")]
    [string] $EcrRepositoryName,

    [Parameter(HelpMessage = "The .NET SDK docker build image.")]
    [string] $DotnetDockerBuildImage,

    [Parameter(HelpMessage = "The .NET SDK target framework.")]
    [string] $DotnetTargetFramework
)

$BASE_IMAGE = "${SourceEcr}/${EcrRepositoryName}:${SourceImageTag}"

& "./LambdaRuntimeDockerfiles/SmokeTests/test/ImageFunction.SmokeTests/build.ps1" -BaseImage $BASE_IMAGE -BuildImage "${DotnetDockerBuildImage}" -TargetFramework "${DotnetTargetFramework}"

if (!$?)
{
    throw "Smoke tests failed."
}