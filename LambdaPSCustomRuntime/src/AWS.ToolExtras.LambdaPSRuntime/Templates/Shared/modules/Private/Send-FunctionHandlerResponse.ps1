# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

function private:Send-FunctionHandlerResponse {
    <#
        .SYNOPSIS
            POST function response back to Runtime API.

        .DESCRIPTION
            POST function response back to Runtime API.

        .Notes

    #>
    [CmdletBinding()]
    param (
        [Parameter(Position=0)]
        [System.Net.Http.HttpClient]$private:HttpClient,

        [Parameter(Position=1)]
        $private:InvocationResponse
    )

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Write-Host '[RUNTIME-Send-FunctionHandlerResponse]Start: Send-FunctionHandlerResponse' }
    $private:uri = "http://$env:AWS_LAMBDA_RUNTIME_API/2018-06-01/runtime/invocation/$env:AWS_LAMBDA_RUNTIME_AWS_REQUEST_ID/response"

    _SendRuntimeApiRequest $private:HttpClient $private:uri $private:InvocationResponse
}
