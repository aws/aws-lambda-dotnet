# Sample
This sample demonstrates deployment process of a C# Console application as container image using a local .NET 5 image. 

# Prerequisite
- **Dockerfile** uses local version of base image, therefore, it is required to build AWS Lambda .NET 5 base image.
```
..\..\build.ps1
```
# Build & deploy
```
cd Sample
dotnet lambda deploy-function
```

# Testing
- Login to AWS Console and navigate to deployed function **Sample** in Lambda Service.
- Create a new test event
  - Event name: `TestSample`
  - Payload: `hello world`
- Test lambda function with TestSample event.
