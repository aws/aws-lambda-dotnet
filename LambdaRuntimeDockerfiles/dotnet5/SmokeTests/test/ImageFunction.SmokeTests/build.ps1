param (
    [Parameter()]
    [string] $Region = "us-west-2",

    [Parameter()]
    [string] $RepositoryName = 'image-function-tests',

    [Parameter()]
    [string] $AWSPowerShellVersion = '4.1.5.0',

    [Parameter()]
    [bool] $DeleteRepository = $false,

    [Parameter()]
    [string] $BaseImage = "aws-lambda-dotnet:local"
)

# Check whether the repository exists or not, if not create one
aws ecr describe-repositories --repository-names ${RepositoryName}
if (!$?) {
    aws ecr create-repository --repository-name ${RepositoryName}
}

# Get AccountId required for building Image URI
$AccountId = aws sts get-caller-identity --query Account --output text

$Ecr = "$AccountId.dkr.ecr.$Region.amazonaws.com"

# Get login credential and login to docker
aws ecr get-login-password | docker login --username AWS --password-stdin $Ecr
if (!$?)
{
    Write-Error "Failed to login in $Ecr"
}

# Generate a random tag to make sure multiple test runs don't conflict
$Tag = New-Guid
$SourceNameTagPair = "${RepositoryName}:latest"
$DestinationUri = "${Ecr}/${repositoryName}:${Tag}"

# Build and push Docker Image to ECR
try {
    Write-Host "Building and pushing ImageFunction to $RepositoryName ECR repository"
    Push-Location "$PSScriptRoot\..\ImageFunction"

    docker build -t ${SourceNameTagPair} --build-arg BASE_IMAGE=$BaseImage .

    docker tag "${SourceNameTagPair}" $DestinationUri
    if (!$?)
    {
        Write-Error "Failed to tag. SourceTag: ${SourceNameTagPair}, TargetTag: ${DestinationUri}"
    }

    docker push $DestinationUri
    if (!$?)
    {
        Write-Error "Failed to push at ${DestinationUri}"
    }
}
finally {
    Pop-Location
}

# Set environment variable to be consumed in tests
$env:AWS_LAMBDA_IMAGE_URI = $DestinationUri

# Run Smoke Tests against pushed Image
try {
    Write-Host "Running Smoke Tests"
    Push-Location $PSScriptRoot
    dotnet test .\ImageFunction.SmokeTests.csproj -v n

    if (!$?)
    {
        Write-Error "Smoke tests failed."
    }
}
finally {
    Pop-Location

    # Delete image pushed for testing
    aws ecr batch-delete-image --repository-name $RepositoryName --image-ids imageTag=$Tag
}

If ($DeleteRepository) {
    Write-Host "Deleting $RepositoryName ECR repository"
    Remove-ECRRepository -RepositoryName $RepositoryName -IgnoreExistingImages $True -Force -Region $Region
}