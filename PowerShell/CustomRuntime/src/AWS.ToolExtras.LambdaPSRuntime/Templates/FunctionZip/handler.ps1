param($LambdaInput, $LambdaContext)

# Runs on each invocation. $LambdaInput is the parsed event; $LambdaContext holds
# request id, function name, remaining time, and so on. Whatever is left on the
# pipeline is returned as the response.

# To bundle a module, declare it with an exact version. Deploying stages it into the
# package with its dependencies, and downloads it if it is not installed:
# #Requires -Modules @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' }

$name = if ($LambdaInput.name) { $LambdaInput.name } else { 'world' }

[PSCustomObject]@{
    message   = "Hello, $name"
    function  = $LambdaContext.FunctionName
    requestId = $LambdaContext.AwsRequestId
}
