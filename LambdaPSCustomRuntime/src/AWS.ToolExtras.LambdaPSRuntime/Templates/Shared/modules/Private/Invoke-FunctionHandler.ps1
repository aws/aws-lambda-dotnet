# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

function private:Invoke-FunctionHandler {
    <#
        .SYNOPSIS
            Invoke Lambda handler function.

        .DESCRIPTION
            Invoke Lambda handler function.

        .Notes
            Runs the entire script if the handler is the script, or function from script/module.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] $private:runtimeNextInvocationResponse,
        [Parameter(Mandatory)] $private:HandlerArray,
        [Parameter(Mandatory)] $private:LambdaContext
    )

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host '[RUNTIME-Invoke-FunctionHandler]Start: Invoke-FunctionHandler' }

    # -NoEnumerate keeps a JSON array an array: [] is an empty array and [x] a one-element array,
    # rather than $null and x.
    # ConvertFrom-Json's own message for an event it cannot convert (for example two keys that differ only
    # in case) suggests a switch the customer cannot pass, so the runtime words the error itself.
    try {
        $private:LambdaInput = Microsoft.PowerShell.Utility\ConvertFrom-Json -InputObject $private:runtimeNextInvocationResponse.incomingEvent -NoEnumerate -ErrorAction Stop
    }
    catch {
        $private:jsonError = $_.Exception.Message -replace '\s*Please use the -AsHashTable switch instead\.', ''
        throw [System.ArgumentException]::new(
            "The event could not be converted from JSON, so the handler was not called: $private:jsonError")
    }
    $private:handlerCommand = if ($private:HandlerArray.handlerType -eq 'Script') { $private:HandlerArray.scriptFilePath } else { $private:HandlerArray.functionName }

    # A break or continue in the handler that is not inside a loop of its own goes up the call stack to
    # the nearest enclosing loop. For an unlabelled one this is that loop, so the call is cut short here
    # and reported as an error, rather than ending some loop of the runtime's and losing the handler's
    # result unreported. A labelled one whose label matches no loop, like exit in a handler function,
    # goes past this loop and ends the runtime; the finally in runtime.ps1's event loop reports that on
    # /error and exits 1. (exit in a <script>.ps1 handler ends only that script.)
    $private:handlerCompleted = $false
    do {
        $private:functionInvocationResponse = & $private:handlerCommand $private:LambdaInput $private:LambdaContext
        $private:handlerCompleted = $true
    } while ($false)
    if (-not $private:handlerCompleted) {
        throw [System.InvalidOperationException]::new(
            'The handler used break or continue outside a loop of its own, so it stopped before returning. Use return to leave the handler early.')
    }

    # The serialization depth: POWERSHELL_RUNTIME_JSON_DEPTH (1-100) lowers it from 100. A value that is
    # set but not a whole number in that range falls back to 100, with one warning in the function log
    # for each value (the setting is the function's, so it holds for every invocation in this execution
    # environment). An unset variable is silent. [Environment] tells set-but-empty from unset.
    $private:depth = 100
    $private:depthValue = [System.Environment]::GetEnvironmentVariable('POWERSHELL_RUNTIME_JSON_DEPTH')
    if ($null -ne $private:depthValue) {
        $private:depthSetting = 0
        if ([int]::TryParse($private:depthValue, [ref]$private:depthSetting) -and
            $private:depthSetting -ge 1 -and $private:depthSetting -le 100) {
            $private:depth = $private:depthSetting
        }
        elseif ($script:LambdaPSRuntimeDepthWarnedFor -cne $private:depthValue) {
            $script:LambdaPSRuntimeDepthWarnedFor = $private:depthValue
            Microsoft.PowerShell.Utility\Write-Host "[RUNTIME-Invoke-FunctionHandler]POWERSHELL_RUNTIME_JSON_DEPTH is '$private:depthValue', which is not a whole number from 1 to 100; using 100."
        }
    }

    # Convert function response to a string if needed
    # $null and strings go as they are; anything else, including $false and 0, is serialized, so a
    # handler returning $false sends the JSON 'false' rather than the text 'False'.
    if ($null -ne $private:functionInvocationResponse -and $private:functionInvocationResponse -isnot [string]) {
        # -Depth 100 (the maximum) by default, so nested data is never truncated; the default depth of
        # 2 would send @{a=@{b=@{c=@{d=1}}}} as {"a":{"b":{"c":"System.Collections.Hashtable"}}}.
        # Return plain data (hashtables, PSCustomObjects, arrays, strings, numbers): a rich .NET object
        # such as Get-Item's output references other objects without end, and serializing it that deep
        # takes minutes and exhausts memory. POWERSHELL_RUNTIME_JSON_DEPTH (1-100) lowers the depth.
        $private:responseString = Microsoft.PowerShell.Utility\ConvertTo-Json -InputObject $private:functionInvocationResponse -Compress -Depth $private:depth
    }
    else {
        $private:responseString = $private:functionInvocationResponse
    }

    return $private:responseString
}
