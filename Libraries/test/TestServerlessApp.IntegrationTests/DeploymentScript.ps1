$guid = New-Guid
$suffix = $guid.ToString().Split('-') | Select-Object -First 1
$stackName = "test-serverless-app-" + $suffix
$bucketName = "devex-test-serverless-app-" + $suffix
& cd ..\TestServerlessApp
& dotnet tool install -g Amazon.Lambda.Tools
& aws s3api create-bucket --bucket $bucketName --create-bucket-configuration LocationConstraint=us-west-2
& dotnet restore
& dotnet lambda deploy-serverless $stackName --s3-bucket $bucketName
& cd ..\TestServerlessApp.IntegrationTests
New-Item -Path . -Name "parameters.txt" -ItemType "file" -Value "stackName=$stackName`nbucketName=$bucketName" -Force