# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

function private:Set-LambdaContext {
    <#
        .SYNOPSIS
            Creates a Lambda context object from environment variables.

        .DESCRIPTION
            Creates a Lambda context object populated with AWS Lambda runtime environment variables.
            The PowerShellLambdaContext C# class must be loaded before calling this function.
            In the Lambda runtime, runtime.ps1 loads it at start-up.
    #>

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Set-LambdaContext]Start: Set-LambdaContext' }

    # Without a deadline the context would count from 1970, so RemainingTime would be about -56 years
    # and a handler pacing its work on it would stop at once. Lambda always sends the header; fail the
    # invocation, saying so, if it is missing or not a positive number of milliseconds.
    $private:deadlineMs = 0.0
    if (-not [double]::TryParse($env:AWS_LAMBDA_RUNTIME_DEADLINE_MS, [System.Globalization.NumberStyles]::Float,
            [System.Globalization.CultureInfo]::InvariantCulture, [ref]$private:deadlineMs) -or $private:deadlineMs -le 0) {
        throw [System.InvalidOperationException]::new(
            "The Lambda Runtime API sent no usable Lambda-Runtime-Deadline-Ms header ('$env:AWS_LAMBDA_RUNTIME_DEADLINE_MS'), so the handler was not called: its `$LambdaContext.RemainingTime would be wrong.")
    }

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Set-LambdaContext]Creating LambdaContext' }
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
        $private:deadlineMs,
        # [NullString], since PowerShell passes $null to a string parameter as ''.
        $(if ($script:__LambdaPSRuntimeTenantId) { $script:__LambdaPSRuntimeTenantId } else { [NullString]::Value })
    )
    # ClientContext and Identity are the caller's data, so they stay out of the log.
    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue "[RUNTIME-Set-LambdaContext]Return LambdaContext: $($private:LambdaContext | Microsoft.PowerShell.Utility\Select-Object -Property * -ExcludeProperty ClientContext, Identity | Microsoft.PowerShell.Utility\ConvertTo-Json -Compress)" }
    return $private:LambdaContext
}
