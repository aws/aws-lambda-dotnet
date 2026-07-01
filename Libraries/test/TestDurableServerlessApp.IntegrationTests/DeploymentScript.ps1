$ErrorActionPreference = 'Stop'

try
{
    Push-Location $PSScriptRoot
    $guid = New-Guid
    $suffix = $guid.ToString().Split('-') | Select-Object -First 1
    $identifier = "test-durable-serverless-app-" + $suffix
    cd ..\TestDurableServerlessApp

    # Replace bucket name in aws-lambda-tools-defaults.json
    $line = Get-Content .\aws-lambda-tools-defaults.json | Select-String s3-bucket | Select-Object -ExpandProperty Line
    $content = Get-Content .\aws-lambda-tools-defaults.json
    $content | ForEach-Object {$_ -replace $line, "`"s3-bucket`" : `"$identifier`","} | Set-Content .\aws-lambda-tools-defaults.json

    # Replace stack name in aws-lambda-tools-defaults.json
    $line = Get-Content .\aws-lambda-tools-defaults.json | Select-String stack-name | Select-Object -ExpandProperty Line
    $content = Get-Content .\aws-lambda-tools-defaults.json
    $content | ForEach-Object {$_ -replace $line, "`"stack-name`" : `"$identifier`""} | Set-Content .\aws-lambda-tools-defaults.json

    # Extract region
    $json =  Get-Content .\aws-lambda-tools-defaults.json | Out-String | ConvertFrom-Json
    $region = $json.region

    # Install Amazon.Lambda.Tools idempotently (parallel integ deploys may race on the global tool store).
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
    Write-Host "Creating CloudFormation Stack $identifier"
    dotnet lambda deploy-serverless
    if (!$?)
    {
        Write-Host "Deployment failed. Fetching CloudFormation stack events for debugging..."
        try {
            $events = aws cloudformation describe-stack-events --stack-name $identifier --query "StackEvents[?ResourceStatus=='CREATE_FAILED' || ResourceStatus=='UPDATE_FAILED' || ResourceStatus=='DELETE_FAILED']" --output json 2>&1
            if ($events) {
                Write-Host "CloudFormation failed events:"
                Write-Host $events
            }
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
