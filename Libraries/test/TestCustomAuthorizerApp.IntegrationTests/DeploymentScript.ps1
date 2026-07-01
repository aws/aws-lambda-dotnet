$ErrorActionPreference = 'Stop'

function Get-Architecture {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    if ($arch -eq "Arm64" -or $arch -eq "Arm") {
        return "arm64"
    }

    if ($arch -eq "X64" -or $arch -eq "X86")  {
        return "x86_64"
    }

    throw "Unsupported architecture: $arch"
}

try
{
    Push-Location $PSScriptRoot
    $guid = New-Guid
    $suffix = $guid.ToString().Split('-') | Select-Object -First 1
    $identifier = "test-custom-authorizer-" + $suffix
    cd ..\TestCustomAuthorizerApp

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

    # Deploy with retries. The stack contains many Lambda functions that each reference
    # an IAM role created in the same stack. CloudFormation occasionally calls Lambda
    # CreateFunction before the role's trust policy has propagated through IAM, producing
    # "The role defined for the function cannot be assumed by Lambda" and rolling the whole
    # stack back. This is a transient eventual-consistency race, so retry the deployment.
    $maxAttempts = 3
    $deploySucceeded = $false
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++)
    {
        Write-Host "Creating CloudFormation Stack $identifier, Architecture $arch (attempt $attempt of $maxAttempts)"
        dotnet lambda deploy-serverless
        if ($?)
        {
            $deploySucceeded = $true
            break
        }

        Write-Host "Deployment attempt $attempt failed. Fetching CloudFormation stack events for debugging..."
        try {
            $events = aws cloudformation describe-stack-events --stack-name $identifier --query "StackEvents[?ResourceStatus=='CREATE_FAILED' || ResourceStatus=='UPDATE_FAILED' || ResourceStatus=='DELETE_FAILED']" --output json 2>&1
            if ($events) {
                Write-Host "CloudFormation failed events:"
                Write-Host $events
            }
        }
        catch {
            Write-Host "Could not fetch CloudFormation events: $_"
        }

        if ($attempt -lt $maxAttempts)
        {
            # A failed create leaves the stack in ROLLBACK_COMPLETE, which cannot be updated
            # or re-created. Delete it (and wait for the delete to finish) before retrying.
            Write-Host "Deleting rolled-back stack $identifier before retrying..."
            aws cloudformation delete-stack --stack-name $identifier
            aws cloudformation wait stack-delete-complete --stack-name $identifier
            # Brief pause to give IAM additional time to settle before the next attempt.
            Start-Sleep -Seconds 15
        }
    }

    if (!$deploySucceeded)
    {
        throw "Failed to create the following CloudFormation stack after $maxAttempts attempts: $identifier"
    }
}
finally
{
    Pop-Location
}
