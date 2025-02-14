param(
    [ValidateSet('amd64','arm64')]
    [string]$Architecture = "amd64",

    [ValidateSet('net6', 'net8', 'net9')]
    [string]$TargetFramework = "net6"
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

    if (Test-Path -Path (Join-Path $PWD -ChildPath '.\LambdaRuntimeDockerfiles\Images\' | Join-Path -ChildPath $TargetFramework | Join-Path -ChildPath  $Architecture | Join-Path -ChildPath 'Dockerfile') -PathType Leaf)
    {
		$Tag = "dot$TargetFramework-runtime:base-image-$Architecture"

        Write-Status "Building $TargetFramework base image: $Tag"
        docker build -f (Join-Path $PWD -ChildPath '.\LambdaRuntimeDockerfiles\Images\' | Join-Path -ChildPath $TargetFramework | Join-Path -ChildPath  $Architecture | Join-Path -ChildPath 'Dockerfile') -t $Tag .
    }
}
finally
{
    Pop-Location
}