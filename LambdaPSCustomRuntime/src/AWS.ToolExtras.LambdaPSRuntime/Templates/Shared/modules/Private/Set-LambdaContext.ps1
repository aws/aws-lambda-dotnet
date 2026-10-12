# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

function Private:Set-LambdaContext {
    <#
        .SYNOPSIS
            Creates a Lambda context object from environment variables.

        .DESCRIPTION
            Creates a Lambda context object populated with AWS Lambda runtime environment variables.
            The PowerShellLambdaContext C# class must be loaded before calling this function.
            In the Lambda runtime, runtime.ps1 loads it at start-up.
    #>

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Write-Host '[RUNTIME-Set-LambdaContext]Start: Set-LambdaContext' }

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Write-Host '[RUNTIME-Set-LambdaContext]Creating LambdaContext' }
    $private:LambdaContext = [Amazon.Lambda.PowerShell.Internal.LambdaContext]::new(
        $env:AWS_LAMBDA_FUNCTION_NAME,
        $env:AWS_LAMBDA_FUNCTION_VERSION,
        $env:AWS_LAMBDA_RUNTIME_INVOKED_FUNCTION_ARN,
        [int]$env:AWS_LAMBDA_FUNCTION_MEMORY_SIZE,
        $env:AWS_LAMBDA_RUNTIME_AWS_REQUEST_ID,
        $env:AWS_LAMBDA_LOG_GROUP_NAME,
        $env:AWS_LAMBDA_LOG_STREAM_NAME,
        $env:AWS_LAMBDA_RUNTIME_COGNITO_IDENTITY,
        $env:AWS_LAMBDA_RUNTIME_CLIENT_CONTEXT,
        [double]$env:AWS_LAMBDA_RUNTIME_DEADLINE_MS
    )
    # ClientContext and Identity are the caller's data, so they stay out of the log.
    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Write-Host "[RUNTIME-Set-LambdaContext]Return LambdaContext: $($private:LambdaContext | Select-Object -Property * -ExcludeProperty ClientContext, Identity | ConvertTo-Json -Compress)" }
    return $private:LambdaContext
}
