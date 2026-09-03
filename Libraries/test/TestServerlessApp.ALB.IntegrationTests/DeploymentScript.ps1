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
    $identifier = "test-alb-app-" + $suffix
    cd ..\TestServerlessApp.ALB

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
    $json = Get-Content .\aws-lambda-tools-defaults.json | Out-String | ConvertFrom-Json
    $region = $json.region

    # Install Amazon.Lambda.Tools idempotently. The integration test projects deploy in parallel,
    # so several DeploymentScript.ps1 processes may run "dotnet tool install -g" at the same time and
    # collide on the global tool store ("a file or directory with the same name already exists").
    # Skip if already present, and tolerate the concurrent-install race by treating an
    # already-installed/already-exists result as success, with a short retry for the transient case.
    if (dotnet tool list -g | Select-String -SimpleMatch 'amazon.lambda.tools')
    {
        Write-Host "Amazon.Lambda.Tools already installed."
    }
    else
    {
        for ($i = 1; $i -le 5; $i++)
        {
            $output = dotnet tool install -g Amazon.Lambda.Tools 2>&1 | Out-String
            Write-Host $output
            if ($LASTEXITCODE -eq 0 -or $output -match 'already installed' -or $output -match 'already exists')
            {
                break
            }
            if ($i -eq 5)
            {
                throw "Failed to install Amazon.Lambda.Tools after $i attempts."
            }
            Start-Sleep -Seconds ($i * 3)
        }
    }
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

    # Resolve this runner's public egress IP so the test ALB's security group only admits us.
    # The ALB stays internet-facing (the test client reaches it over public DNS), but locking
    # ingress to a single /32 means it is not reachable by DyePack's scanners, which avoids
    # EC2IPAuthentication findings on this short-lived integration ALB. Fail closed if we can't
    # determine the IP rather than falling back to 0.0.0.0/0.
    $myIp = $null
    for ($i = 1; $i -le 3; $i++)
    {
        try { $myIp = (Invoke-RestMethod -Uri 'https://checkip.amazonaws.com' -TimeoutSec 10).Trim(); break }
        catch { Write-Host "Attempt $i to resolve public IP failed: $_"; Start-Sleep -Seconds ($i * 2) }
    }
    if ([string]::IsNullOrEmpty($myIp) -or $myIp -notmatch '^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$')
    {
        throw "Could not resolve a valid public IP for the ALB security group; aborting to avoid opening the ALB to the internet."
    }
    Write-Host "Restricting test ALB ingress to runner IP $myIp/32"

    Write-Host "Creating CloudFormation Stack $identifier, Architecture $arch"
    dotnet lambda deploy-serverless --template-parameters "AllowedCidr=$myIp/32"
    if (!$?)
    {
        Write-Host "Deployment failed. Fetching CloudFormation stack events for debugging..."
        try {
            $events = aws cloudformation describe-stack-events --stack-name $identifier --query "StackEvents[?ResourceStatus=='CREATE_FAILED' || ResourceStatus=='UPDATE_FAILED']" --output json 2>&1
            if ($events) {
                Write-Host "CloudFormation failed events:"
                Write-Host $events
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
