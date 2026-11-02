# PowerShell Lambda function handler (container-per-function target).
# Baked into the image at /var/task/handler.ps1; the bundled runtime invokes it and passes
# the event and context. The handler string is 'handler.ps1' (set via the Dockerfile CMD).
param(
    $LambdaInput,
    $LambdaContext
)

Write-Information "Handling request $($LambdaContext.AwsRequestId)" -InformationAction Continue

# Return value becomes the function response.
[PSCustomObject]@{
    Message = 'Hello from PowerShell on Lambda (container-per-function)'
    Echo    = $LambdaInput
}
