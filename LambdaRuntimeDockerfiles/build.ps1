param(
    [ValidateSet('amd64','arm64')]
    [string]$Architecture = "amd64",

    [ValidateSet('net5','net6')]
    [string]$TargetFramework = "net5"
)

function Write-Status($string)
{    
    Write-Host
    Write-Host ******************************** -ForegroundColor Gray
    Write-Host $string -ForegroundColor Gray
    Write-Host ******************************** -ForegroundColor Gray   
    Write-Host
}

# docker layout and dependencies based on this repo: https://github.com/dotnet/dotnet-docker

try
{
    # runtime docker image need to be built from the root of the reposiotory
    # so it can include the Amazon.Lambda.RuntimeSupport project in its Docker Build Context
    Push-Location $PSScriptRoot\..   

    if (Test-Path -Path (Join-Path $PWD '.\LambdaRuntimeDockerfiles\Images\' $TargetFramework $Architecture 'Dockerfile') -PathType Leaf)
    {
        $Tag = "dot$TargetFramework.0-runtime:base-image-$Architecture"

        Write-Status "Building $TargetFramework base image: $Tag"
        docker build -f (Join-Path $PWD '.\LambdaRuntimeDockerfiles\Images\' $TargetFramework $Architecture 'Dockerfile') -t $Tag .
    }
}
finally
{
    Pop-Location
}