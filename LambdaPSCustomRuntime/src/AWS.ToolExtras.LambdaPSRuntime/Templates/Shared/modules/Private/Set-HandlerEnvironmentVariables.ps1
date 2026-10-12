# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

function private:Set-HandlerEnvironmentVariables {
    <#
        .SYNOPSIS
            Set default and AWS Lambda specific environment variables for each invocation.

        .DESCRIPTION
            Set default and AWS Lambda specific environment variables for each invocation.

        .Notes

    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] $Headers
    )

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Write-Host '[RUNTIME-Set-HandlerEnvironmentVariables]Start: Set-HandlerEnvironmentVariables' }
    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Write-Host "[RUNTIME-Set-HandlerEnvironmentVariables]Received headers: $($Headers.Keys -join ', ')" }

    # Set default TEMP environment variables for each invocation to ensure they're "clean" for each handler invocation
    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Write-Host '[RUNTIME-Set-HandlerEnvironmentVariables]Set default TEMP environment variables' }
    $env:TEMP = '/tmp'
    $env:TMP = '/tmp'
    $env:TMPDIR = '/tmp'

    # Set AWS Lambda specific environment variables
    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Write-Host '[RUNTIME-Set-HandlerEnvironmentVariables]Set AWS Lambda specific environment variables' }
    $env:AWS_LAMBDA_RUNTIME_AWS_REQUEST_ID = $Headers['Lambda-Runtime-Aws-Request-Id']
    $env:AWS_LAMBDA_RUNTIME_CLIENT_CONTEXT = $Headers['Lambda-Runtime-Client-Context']
    $env:AWS_LAMBDA_RUNTIME_COGNITO_IDENTITY = $Headers['Lambda-Runtime-Cognito-Identity']
    $env:AWS_LAMBDA_RUNTIME_DEADLINE_MS = $Headers['Lambda-Runtime-Deadline-Ms']
    $env:AWS_LAMBDA_RUNTIME_INVOKED_FUNCTION_ARN = $Headers['Lambda-Runtime-Invoked-Function-Arn']
    $env:_X_AMZN_TRACE_ID = $Headers['Lambda-Runtime-Trace-Id']
}
