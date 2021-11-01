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

    if (Test-Path -Path (Join-Path $PWD -ChildPath '.\LambdaRuntimeDockerfiles\Images\' | Join-Path -ChildPath $TargetFramework | Join-Path -ChildPath  $Architecture | Join-Path -ChildPath 'Dockerfile') -PathType Leaf)
    {
		if ($TargetFramework -eq "net6")
		{
			$Tag = "dotnet6-runtime:base-image-$Architecture"
		}
		elseif($TargetFramework -eq "net5")
		{
			$Tag = "dotnet5.0-runtime:base-image-$Architecture"
		}
		else
		{
			throw "Unable to determine tag for target framework $TargetFramework" 
		}

        Write-Status "Building $TargetFramework base image: $Tag"
        docker build -f (Join-Path $PWD -ChildPath '.\LambdaRuntimeDockerfiles\Images\' | Join-Path -ChildPath $TargetFramework | Join-Path -ChildPath  $Architecture | Join-Path -ChildPath 'Dockerfile') -t $Tag .
    }
}
finally
{
    Pop-Location
}