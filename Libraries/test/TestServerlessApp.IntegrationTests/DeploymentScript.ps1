$ErrorActionPreference = 'Stop'

function Get-Architecture {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    if ($arch -eq "Arm64" || $arch -eq "Arm") {
        return "arm64"
    }

    if ($arch -eq "X64" || $arch -eq "X86")  {
        return "x86_64"
    }

    throw "Unsupported architecture: $arch"
}

try
{
    Push-Location $PSScriptRoot
    $guid = New-Guid
    $suffix = $guid.ToString().Split('-') | Select-Object -First 1
    $identifier = "test-serverless-app-" + $suffix
    cd ..\TestServerlessApp

    $arch = Get-Architecture

    # Replace bucket name in aws-lambda-tools-defaults.json
    $line = Get-Content .\aws-lambda-tools-defaults.json | Select-String s3-bucket | Select-Object -ExpandProperty Line
    $content = Get-Content .\aws-lambda-tools-defaults.json
    $content | ForEach-Object {$_ -replace $line, "`"s3-bucket`" : `"$identifier`","} | Set-Content .\aws-lambda-tools-defaults.json

    # Replace stack name in aws-lambda-tools-defaults.json
    $line = Get-Content .\aws-lambda-tools-defaults.json | Select-String stack-name | Select-Object -ExpandProperty Line
    $content = Get-Content .\aws-lambda-tools-defaults.json
    $content | ForEach-Object {$_ -replace $line, "`"stack-name`" : `"$identifier`","} | Set-Content .\aws-lambda-tools-defaults.json

    # Replace function-architecture in aws-lambda-tools-defaults.json
    $line = Get-Content .\aws-lambda-tools-defaults.json | Select-String function-architecture | Select-Object -ExpandProperty Line
    $content = Get-Content .\aws-lambda-tools-defaults.json
    $content | ForEach-Object {$_ -replace $line, "`"function-architecture`" : `"$arch`""} | Set-Content .\aws-lambda-tools-defaults.json

    # Extract region
    $json =  Get-Content .\aws-lambda-tools-defaults.json | Out-String | ConvertFrom-Json
    $region = $json.region

    dotnet tool install -g Amazon.Lambda.Tools
    Write-Host "Creating S3 Bucket $identifier"

    if(![string]::IsNullOrEmpty($region))
    {
        aws s3 mb s3://$identifier --region $region
    }
    else
    {
        aws s3 mb s3://$identifier
    }
    
    if (!$?)
    {
        throw "Failed to create the following bucket: $identifier"
    }
    dotnet restore
    Write-Host "Creating CloudFormation Stack $identifier, Architecture $arch, Runtime $runtime"
    dotnet lambda deploy-serverless --template-parameters "ArchitectureTypeParameter=$arch"
    if (!$?)
    {
        # Try to get detailed CloudFormation events for debugging
        Write-Host "Deployment failed. Fetching CloudFormation stack events for debugging..."
        try {
            $events = aws cloudformation describe-stack-events --stack-name $identifier --query "StackEvents[?ResourceStatus=='CREATE_FAILED' || ResourceStatus=='UPDATE_FAILED' || ResourceStatus=='DELETE_FAILED']" --output json 2>&1
            if ($events) {
                Write-Host "CloudFormation failed events:"
                Write-Host $events
            }
            
            # Also try to get change set details if available
            $changeSets = aws cloudformation list-change-sets --stack-name $identifier --output json 2>&1
            if ($changeSets) {
                Write-Host "CloudFormation change sets:"
                Write-Host $changeSets
            }
        }
        catch {
            Write-Host "Could not fetch CloudFormation events: $_"
        }
        
        throw "Failed to create the following CloudFormation stack: $identifier"
    }
}
finally
{
    Pop-Location
}
