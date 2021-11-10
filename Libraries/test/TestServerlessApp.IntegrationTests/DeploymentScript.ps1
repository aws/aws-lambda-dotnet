$ErrorActionPreference = 'Stop'
try
{
    Push-Location $PSScriptRoot
    $guid = New-Guid
    $suffix = $guid.ToString().Split('-') | Select-Object -First 1
    $identifier = "test-serverless-app-" + $suffix
    cd ..\TestServerlessApp

    # Replace bucket name in aws-lambda-tools-defaults.json
    $line = Get-Content .\aws-lambda-tools-defaults.json | Select-String s3-bucket | Select-Object -ExpandProperty Line
    $content = Get-Content .\aws-lambda-tools-defaults.json
    $content | ForEach-Object {$_ -replace $line, "`"s3-bucket`" : `"$identifier`","} | Set-Content .\aws-lambda-tools-defaults.json

    # Replace stack name in aws-lambda-tools-defaults.json
    $line = Get-Content .\aws-lambda-tools-defaults.json | Select-String stack-name | Select-Object -ExpandProperty Line
    $content = Get-Content .\aws-lambda-tools-defaults.json
    $content | ForEach-Object {$_ -replace $line, "`"stack-name`" : `"$identifier`""} | Set-Content .\aws-lambda-tools-defaults.json

    dotnet tool install -g Amazon.Lambda.Tools
    Write-Host "Creating S3 Bucket $identifier"
    aws s3 mb s3://$identifier
    if (!$?)
    {
        throw "Failed to create the following bucket: $identifier"
    }
    dotnet restore
    Write-Host "Creating CloudFormation Stack $identifier"
    dotnet lambda deploy-serverless
    if (!$?)
    {
        throw "Failed to create the following CloudFormation stack: $identifier"
    }
}
finally
{
    Pop-Location
}