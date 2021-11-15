$guid = New-Guid
$suffix = $guid.ToString().Split('-') | Select-Object -First 1
$stackName = "test-serverless-app-" + $suffix
$bucketName = "devex-test-serverless-app-" + $suffix
& cd ..\TestServerlessApp
& dotnet tool install -g Amazon.Lambda.Tools
& aws s3 mb s3://$bucketName
& dotnet restore
& dotnet lambda deploy-serverless $stackName --s3-bucket $bucketName
& cd ..\TestServerlessApp.IntegrationTests
New-Item -Path . -Name "parameters.txt" -ItemType "file" -Value "stackName=$stackName`nbucketName=$bucketName" -Force