# TestCustomAuthorizerApp

This test application demonstrates the `[FromCustomAuthorizer]` attribute functionality in AWS Lambda Annotations.

## What This Demonstrates

- **Custom Lambda Authorizer**: A Lambda function that authenticates requests and returns context values
- **Protected Lambda Functions**: Functions that use `[FromCustomAuthorizer]` to extract values set by the authorizer

## Prerequisites

1. AWS CLI configured with credentials
2. .NET 8 SDK installed
3. Amazon.Lambda.Tools installed:
   ```bash
   dotnet tool install -g Amazon.Lambda.Tools
   ```
4. An S3 bucket for deployment artifacts

## Project Structure

```
TestCustomAuthorizerApp/
├── AuthorizerFunction.cs       # Custom Lambda authorizer
├── ProtectedFunction.cs        # Functions using [FromCustomAuthorizer]
├── serverless.template         # CloudFormation template with API Gateway
├── aws-lambda-tools-defaults.json
└── README.md
```

## How It Works

### 1. Custom Authorizer (`AuthorizerFunction.cs`)

The authorizer Lambda:
- Receives the API Gateway request
- Validates the request (in this demo, it always authorizes unless `Authorization: deny`)
- Returns context values that are passed to the downstream Lambda:
  ```csharp
  Context = new Dictionary<string, object>
  {
      { "userId", "user-12345" },
      { "tenantId", 42 },
      { "userRole", "admin" },
      { "email", "test@example.com" }
  }
  ```

### 2. Protected Functions (`ProtectedFunction.cs`)

The protected functions use `[FromCustomAuthorizer]` to extract these values:
```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/api/protected")]
public string GetProtectedData(
    [FromCustomAuthorizer(Name = "userId")] string userId,
    [FromCustomAuthorizer(Name = "tenantId")] int tenantId,
    [FromCustomAuthorizer(Name = "userRole")] string userRole,
    ILambdaContext context)
{
    return $"Hello {userId}! You are a {userRole} in tenant {tenantId}.";
}
```

## Deployment

### Step 1: Build the project

```bash
cd Libraries/test/TestCustomAuthorizerApp
dotnet build
```

### Step 2: Deploy to AWS

Replace `YOUR_S3_BUCKET` with your bucket name:

```bash
dotnet lambda deploy-serverless --s3-bucket YOUR_S3_BUCKET
```

Or specify all parameters:

```bash
dotnet lambda deploy-serverless \
    --stack-name test-custom-authorizer \
    --s3-bucket YOUR_S3_BUCKET \
    --region us-east-1
```

### Step 3: Get the API URL

After deployment, the output will show the API URL:

```
Outputs:
  ApiUrl: https://xxxxxxxxxx.execute-api.us-east-1.amazonaws.com
  ProtectedEndpointUrl: https://xxxxxxxxxx.execute-api.us-east-1.amazonaws.com/api/protected
  UserInfoUrl: https://xxxxxxxxxx.execute-api.us-east-1.amazonaws.com/api/user-info
  HealthCheckUrl: https://xxxxxxxxxx.execute-api.us-east-1.amazonaws.com/api/health
```

## Testing

### Test Health Check (No Auth Required)

```bash
curl https://YOUR_API_URL/api/health
# Response: "OK"
```

### Test Protected Endpoint (With Authorization)

```bash
curl -H "Authorization: my-token" https://YOUR_API_URL/api/protected
# Response: "Hello user-12345! You are a admin in tenant 42."
```

### Test User Info Endpoint

```bash
curl -H "Authorization: any-value" https://YOUR_API_URL/api/user-info
# Response: {"userId":"user-12345","email":"test@example.com","tenantId":42,"message":"This data came from the custom authorizer context!"}
```

### Test Authorization Denial

```bash
curl -H "Authorization: deny" https://YOUR_API_URL/api/protected
# Response: 403 Forbidden (authorizer denies requests with "deny" token)
```

### Test Missing Authorization Header

```bash
curl https://YOUR_API_URL/api/protected
# Response: 401 Unauthorized (missing Authorization header)
```

## Cleanup

To delete the deployed resources:

```bash
dotnet lambda delete-serverless --stack-name test-custom-authorizer
```

Or via AWS CLI:

```bash
aws cloudformation delete-stack --stack-name test-custom-authorizer
```

## Troubleshooting

### "Handler not found" error
Make sure you built the project in Release mode:
```bash
dotnet build -c Release
```

### Authorizer returns 500 error
Check the authorizer Lambda logs in CloudWatch for detailed error messages.

### 401 Unauthorized even with Authorization header
The HTTP API authorizer expects the header specified in `IdentitySource`. Make sure you're using the `Authorization` header.

## Key Points About `[FromCustomAuthorizer]`

1. **HTTP API vs REST API**: The authorizer context location differs:
   - HTTP API: `RequestContext.Authorizer.Lambda["key"]`
   - REST API: `RequestContext.Authorizer["key"]`

2. **Automatic 401 Response**: If the specified key is not found in the authorizer context, the generated code automatically returns HTTP 401 Unauthorized.

3. **Type Conversion**: The attribute handles type conversion (e.g., string to int for `tenantId`).

4. **Name Property**: Use `Name = "key"` when the authorizer context key differs from your parameter name.
