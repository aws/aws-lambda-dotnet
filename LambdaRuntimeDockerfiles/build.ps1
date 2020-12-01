param(
    [string]$Tag = "local"
)

function Write-Status($string)
{    
    Write-Host
    Write-Host ******************************** -ForegroundColor Gray
    Write-Host $string -ForegroundColor Gray
    Write-Host ******************************** -ForegroundColor Gray   
    Write-Host
}

# docker layout and dependcies based on this repo: https://github.com/dotnet/dotnet-docker

try
{
    # runtime docker image need to be built from the root of the reposiotory
    # so it can include the Amazon.Lambda.RuntimeSupport project in its Docker Build Context
    Push-Location $PSScriptRoot\..   

    Write-Status "Building .NET 5 base image: $Tag"
    docker build -f (Join-Path $PWD '.\LambdaRuntimeDockerfiles\dotnet5\Dockerfile') -t aws-lambda-dotnet:$Tag .
}
finally
{
    Pop-Location
}