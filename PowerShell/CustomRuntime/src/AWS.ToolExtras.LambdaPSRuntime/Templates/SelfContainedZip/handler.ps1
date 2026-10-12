# PowerShell Lambda function handler (self-contained zip target).
# The bundled runtime invokes this script and passes the event and context.
# The deployed handler string is 'handler.ps1' (the runtime's Get-Handler treats a bare
# script file as the Script handler form and runs it with $LambdaInput / $LambdaContext).
param(
    $LambdaInput,
    $LambdaContext
)

Write-Information "Handling request $($LambdaContext.AwsRequestId)" -InformationAction Continue

# Return value becomes the function response.
[PSCustomObject]@{
    Message = 'Hello from PowerShell on Lambda (self-contained zip)'
    Echo    = $LambdaInput
}
