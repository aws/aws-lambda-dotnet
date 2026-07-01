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
    # Add TestQueue resource to serverless.template for integration testing
    # The source generator creates an Fn::GetAtt reference to TestQueue but doesn't define the resource itself
    $templatePath = ".\serverless.template"
    $template = Get-Content $templatePath | Out-String | ConvertFrom-Json
    if (-not $template.Resources.PSObject.Properties['TestQueue']) {
        $template.Resources | Add-Member -NotePropertyName "TestQueue" -NotePropertyValue @{ Type = "AWS::SQS::Queue" } -Force
        $template | ConvertTo-Json -Depth 100 | Set-Content $templatePath
        Write-Host "Added TestQueue resource to serverless.template"
    }

    # Add TestS3Bucket resource to serverless.template for S3 event integration testing
    # The source generator creates a Ref to TestS3Bucket but doesn't define the resource itself
    $template = Get-Content $templatePath | Out-String | ConvertFrom-Json
    if (-not $template.Resources.PSObject.Properties['TestS3Bucket']) {
        $template.Resources | Add-Member -NotePropertyName "TestS3Bucket" -NotePropertyValue @{ Type = "AWS::S3::Bucket" } -Force
        $template | ConvertTo-Json -Depth 100 | Set-Content $templatePath
        Write-Host "Added TestS3Bucket resource to serverless.template"
    }

    # Add TestTopic resource to serverless.template for SNS event integration testing
    # The source generator creates a Ref to TestTopic but doesn't define the resource itself
    $template = Get-Content $templatePath | Out-String | ConvertFrom-Json
    if (-not $template.Resources.PSObject.Properties['TestTopic']) {
        $template.Resources | Add-Member -NotePropertyName "TestTopic" -NotePropertyValue @{ Type = "AWS::SNS::Topic" } -Force
        $template | ConvertTo-Json -Depth 100 | Set-Content $templatePath
        Write-Host "Added TestTopic resource to serverless.template"
    }

    # Add TestTable resource to serverless.template for DynamoDB event integration testing
    # The source generator creates a Fn::GetAtt reference to TestTable for StreamArn but doesn't define the resource itself
    $template = Get-Content $templatePath | Out-String | ConvertFrom-Json
    if (-not $template.Resources.PSObject.Properties['TestTable']) {
        $testTableResource = @{
            Type = "AWS::DynamoDB::Table"
            Properties = @{
                BillingMode = "PAY_PER_REQUEST"
                AttributeDefinitions = @(
                    @{
                        AttributeName = "Id"
                        AttributeType = "S"
                    }
                )
                KeySchema = @(
                    @{
                        AttributeName = "Id"
                        KeyType = "HASH"
                    }
                )
                StreamSpecification = @{
                    StreamViewType = "NEW_AND_OLD_IMAGES"
                }
            }
        }
        $template.Resources | Add-Member -NotePropertyName "TestTable" -NotePropertyValue $testTableResource -Force
        $template | ConvertTo-Json -Depth 100 | Set-Content $templatePath
        Write-Host "Added TestTable resource to serverless.template"
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
