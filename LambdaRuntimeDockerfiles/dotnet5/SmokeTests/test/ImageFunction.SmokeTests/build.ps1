param (
    [Parameter()]
    [string] $Region = 'sa-east-1',

    [Parameter()]
    [string] $RepositoryName = 'image-function-tests',

    [Parameter()]
    [string] $AWSPowerShellVersion = '4.1.5.0',

    [Parameter()]
    [bool] $DeleteRepository = $false
)

# Load AWS Tools for PowerShell required docker image push
if (-not (Get-Module -ListAvailable -Name AWS.Tools.ECR | Where-Object { $_.Version -eq $AWSPowerShellVersion })) {
    Write-Host "Installing AWS.Tools.ECR $AWSPowerShellVersion"
    Install-Module -Name AWS.Tools.ECR -RequiredVersion $AWSPowerShellVersion -Force -SkipPublisherCheck
}
Import-Module -Name AWS.Tools.ECR -RequiredVersion $AWSPowerShellVersion

if (-not (Get-Module -ListAvailable -Name AWS.Tools.SecurityToken | Where-Object { $_.Version -eq $AWSPowerShellVersion })) {
    Write-Host "Installing AWS.Tools.SecurityToken $AWSPowerShellVersion"
    Install-Module -Name AWS.Tools.SecurityToken -RequiredVersion $AWSPowerShellVersion -Force -SkipPublisherCheck
}
Import-Module -Name AWS.Tools.SecurityToken -RequiredVersion $AWSPowerShellVersion

# Check whether the repository exists or not, if not create one
Get-ECRRepository -RepositoryName $RepositoryName -Region $Region
if (!$?) {
    New-ECRRepository -RepositoryName $RepositoryName -Region $Region
}

# Get login credential and login to docker
$LoginResponse = Get-ECRLoginCommand -Region $Region
Invoke-Expression $LoginResponse.Command

# Get AccountId required for building Image URI
$AccountId = (Get-STSCallerIdentity).Account

# Generate a random tag to make sure multiple test runs don't conflict
$Tag = New-Guid
$ImageUri = "$AccountId.dkr.ecr.$Region.amazonaws.com/${repositoryName}:${Tag}"

# Build and push Docker Image to ECR
try {
    Write-Host "Building and pushing ImageFunction to $RepositoryName ECR repository"
    Push-Location "$PSScriptRoot\..\ImageFunction"
    docker build -t ${RepositoryName} .
    docker tag "${RepositoryName}:latest" $ImageUri
    docker push $ImageUri
}
finally {
    Pop-Location
}

# Set environment variable to be consumed in tests
$env:AWS_LAMBDA_IMAGE_URI = $ImageUri

# Run Smoke Tests against pushed Image
try {
    Write-Host "Running Smoke Tests"
    Push-Location $PSScriptRoot
    dotnet test .\ImageFunction.SmokeTests.csproj -v n
}
finally {
    Pop-Location
}

If ($DeleteRepository) {
    Write-Host "Deleting $RepositoryName ECR repository"
    Remove-ECRRepository -RepositoryName $RepositoryName -IgnoreExistingImages $True -Force -Region $Region
}