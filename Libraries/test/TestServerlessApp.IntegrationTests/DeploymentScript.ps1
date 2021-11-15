$ErrorActionPreference = 'Stop'
try
{
    Push-Location $PSScriptRoot
    $guid = New-Guid
    $suffix = $guid.ToString().Split('-') | Select-Object -First 1
    $identifier = "test-serverless-app-" + $suffix
    & cd ..\TestServerlessApp
    if (!$?)
    {
        throw "Failed to tag. SourceTag: ${SourceAmd64Uri}, TargetTag: ${Amd64Uri}"
    }
    dotnet tool install -g Amazon.Lambda.Tools
    Write-Host "Creating Bucket $identifier..."
    aws s3 mb s3://$identifier
    if (!$?)
    {
        throw "Failed to tag. SourceTag: ${SourceAmd64Uri}, TargetTag: ${Amd64Uri}"
    }
    dotnet restore
    Write-Host "Creating CloudFormation Stack $identifier..."
    dotnet lambda deploy-serverless $identifier --s3-bucket $identifier
    if (!$?)
    {
        throw "Failed to tag. SourceTag: ${SourceAmd64Uri}, TargetTag: ${Amd64Uri}"
    }
    cd ..\TestServerlessApp.IntegrationTests
    if (!$?)
    {
        throw "Failed to tag. SourceTag: ${SourceAmd64Uri}, TargetTag: ${Amd64Uri}"
    }
    New-Item -Path . -Name "parameters.txt" -ItemType "file" -Value "stackName=$identifier`nbucketName=$identifier" -Force
}
finally
{
    Pop-Location
}