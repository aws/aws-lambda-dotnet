# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

function private:Invoke-FunctionHandler {
    <#
        .SYNOPSIS
            Invoke Lambda handler function.

        .DESCRIPTION
            Converts the event from JSON, runs the handler with the event and the context, and returns its
            output: a string as it is, anything else as JSON.

        .Notes
            Runs the entire script if the handler is the script, or function from script/module.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] $private:runtimeNextInvocationResponse,
        [Parameter(Mandatory)] $private:HandlerArray,
        [Parameter(Mandatory)] $private:context
    )

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Invoke-FunctionHandler]Start: Invoke-FunctionHandler' }

    # -NoEnumerate keeps a JSON array an array: [] is an empty array and [x] a one-element array,
    # rather than $null and x.
    # ConvertFrom-Json's own message for an event it cannot convert (for example two keys that differ only
    # in case) suggests a switch the customer cannot pass, so the runtime words the error itself.
    try {
        $private:inputObject = Microsoft.PowerShell.Utility\ConvertFrom-Json -InputObject $private:runtimeNextInvocationResponse.incomingEvent -NoEnumerate -ErrorAction Stop
    }
    catch {
        $private:jsonError = $_.Exception.Message -replace '\s*Please use the -AsHashTable switch instead\.', ''
        throw [System.ArgumentException]::new(
            "The event could not be converted from JSON, so the handler was not called: $private:jsonError")
    }
    # The runtime passes the command its start-up check resolved (in the scope the handler shares), so
    # that command runs here, not a module command that happens to have the handler's name.
    $private:handlerCommand = if ($private:HandlerArray.handlerType -eq 'Script') { $private:HandlerArray.scriptFilePath }
    elseif ($private:HandlerArray.PSObject.Properties['command']) { $private:HandlerArray.command }
    else { $private:HandlerArray.functionName }

    # The event and context are $LambdaInput and $LambdaContext in this scope, not private ones: a
    # <script>.ps1 handler runs in a child of this scope, so it reads them whether or not it declares
    # param($LambdaInput, $LambdaContext), as AWSLambdaPSCore scripts expect. A handler function runs in
    # the scope where it was defined, does not see them, and takes both as its parameters.
    $LambdaInput = $private:inputObject
    $LambdaContext = $private:context

    # A break or continue in the handler that is not inside a loop of its own goes up the call stack to
    # the nearest enclosing loop. For an unlabelled one this is that loop, so the call is cut short here
    # and reported as an error, rather than ending some loop of the runtime's and losing the handler's
    # result unreported. A labelled one whose label matches no loop, like exit in a handler function,
    # goes past this loop and ends the runtime; the finally in runtime.ps1's event loop reports that on
    # /error and exits 1. (exit in a <script>.ps1 handler ends only that script.)
    #
    # The handler's records are logged one per line, as Amazon.Lambda.PowerShellHost logs each stream:
    # with AWS_LAMBDA_LOG_FORMAT=JSON every stream at its level, and in text mode its errors, which pwsh
    # would otherwise print over several lines naming this file. The handler's output is wrapped first,
    # so a record it returns stays in the response.
    # The pipeline wraps each value in a PSObject, which ConvertTo-Json writes with its type's extended
    # properties ([guid] gains Guid); the wrapper is kept only for members the handler added. Only values
    # whose type names carry extended properties are checked, found once per list of type names (type
    # data attaches by name, and a value's names can be changed): listing a value's properties costs
    # about 30 us, and even reading .PSObject 20 us.
    $script:__LambdaPSRuntimeTypeExtended = @{}
    $private:handlerRun = {
        & $private:handlerCommand $LambdaInput $LambdaContext | & {
            process {
                $private:item = $_
                if ($null -eq $private:item) { return , [ref]$private:item }
                $private:typeNames = $private:item.PSTypeNames -join '|'
                $private:extended = $script:__LambdaPSRuntimeTypeExtended[$private:typeNames]
                if ($null -eq $private:extended) {
                    $private:extended = $script:__LambdaPSRuntimeTypeExtended[$private:typeNames] =
                        [bool]@($private:item.PSObject.Properties | Microsoft.PowerShell.Core\Where-Object { -not $_.IsInstance -and $_.MemberType -ne 'Property' }).Count
                }
                if (-not $private:extended) { return , [ref]$private:item }
                foreach ($private:property in $private:item.PSObject.Properties) {
                    if ($private:property.IsInstance -and $private:property.MemberType -ne 'Property') { return , [ref]$private:item }
                }
                [ref]($private:item.PSObject.BaseObject)
            }
        }
    }
    $private:logRecord = {
        process {
            if ($_ -is [ref]) { return , $_.Value }
            & $script:__LambdaPSRuntimeWriteLambdaLog -Record $_
        }
    }
    $private:handlerCompleted = $false
    do {
        $private:functionInvocationResponse = if ($env:AWS_LAMBDA_LOG_FORMAT -ne 'JSON') { . $private:handlerRun 2>&1 | & $private:logRecord }
        else { . $private:handlerRun *>&1 | & $private:logRecord }
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
            & $script:__LambdaPSRuntimeWriteLambdaLog WARN "[RUNTIME-Invoke-FunctionHandler]POWERSHELL_RUNTIME_JSON_DEPTH is '$private:depthValue', which is not a whole number from 1 to 100; using 100."
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
