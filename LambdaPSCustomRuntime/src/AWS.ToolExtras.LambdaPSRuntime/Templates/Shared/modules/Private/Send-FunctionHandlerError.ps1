# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

function private:Send-FunctionHandlerError {
    <#
        .SYNOPSIS
            POST function invocation error back to Runtime API.

        .DESCRIPTION
            POST function invocation error back to Runtime API.

        .PARAMETER Uri
            Where to post the error. Defaults to the current invocation's /error; the runtime passes
            /runtime/init/error for a failure before the first event.

        .PARAMETER NoStackTrace
            Send an empty stackTrace, for an error the runtime raised itself: its frames are the runtime's
            own and tell the customer nothing. The function log still shows them.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Position=0)]
        [System.Net.Http.HttpClient]$private:HttpClient,

        [Parameter(Mandatory, Position=1)]
        $private:Exception,

        [string]$Uri = "http://$env:AWS_LAMBDA_RUNTIME_API/2018-06-01/runtime/invocation/$env:AWS_LAMBDA_RUNTIME_AWS_REQUEST_ID/error",

        [switch]$NoStackTrace
    )

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host '[RUNTIME-Send-FunctionHandlerError]Start: Send-FunctionHandlerError' }
    # The message alone does not say where the handler failed, so log the script position and stack.
    # Lambda makes each stdout line its own log event, so the error is written as one line with its
    # newlines turned into carriage returns, as the Node.js and Python runtimes do: one failure is one
    # event, and text in the exception cannot add lines such as 'END RequestId: ...' to the log.
    $private:logText = @(
        [string]$private:Exception
        if ($private:Exception.InvocationInfo) { $private:Exception.InvocationInfo.PositionMessage }
        if ($private:Exception.ScriptStackTrace) { $private:Exception.ScriptStackTrace }
    ) -join "`n"
    Microsoft.PowerShell.Utility\Write-Host ($private:logText -replace '\r?\n', "`r")

    $private:body = Microsoft.PowerShell.Utility\ConvertTo-Json -Compress -InputObject @{
        errorMessage = $private:Exception.Exception.Message
        errorType    = $private:Exception.CategoryInfo.Reason
        # One frame per entry, as the other Lambda runtimes send it; the invoke result shows it.
        stackTrace   = @(if (-not $NoStackTrace) { [string]$private:Exception.ScriptStackTrace -split '\r?\n' | Microsoft.PowerShell.Core\Where-Object { $_ } })
    }
    $private:headers = @(@{
        Key = 'Lambda-Runtime-Function-Error-Type'
        Value = '{0}.{1}' -f $private:Exception.CategoryInfo.Category, $private:Exception.CategoryInfo.Reason
    })
    # If the error report itself is rejected there is nothing further to tell Lambda; log why (so the
    # resulting timeout has a cause in the function log) rather than let the event loop die.
    try {
        & $script:SendRuntimeApiRequest $private:HttpClient $Uri $private:body $private:headers
    }
    catch {
        Microsoft.PowerShell.Utility\Write-Host "[RUNTIME-Send-FunctionHandlerError]Failed to report the error: $($_.Exception.Message)"
    }
}
