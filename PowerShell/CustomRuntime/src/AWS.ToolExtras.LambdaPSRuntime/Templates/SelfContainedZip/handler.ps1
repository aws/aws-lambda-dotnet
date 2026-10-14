# PowerShell Lambda function handler (self-contained zip target).
# The bundled runtime invokes this script and passes the event and context.
# The deployed handler string is 'handler.ps1': the runtime runs a bare script file with
# $LambdaInput and $LambdaContext. The README lists the other handler forms.
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
