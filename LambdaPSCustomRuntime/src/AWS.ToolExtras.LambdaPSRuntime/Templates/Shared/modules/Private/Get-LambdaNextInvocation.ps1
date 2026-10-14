# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

function private:Get-LambdaNextInvocation {
    <#
        .SYNOPSIS
            Get /NEXT invocation from AWS Lambda Runtime API.

        .DESCRIPTION
            Asks the Runtime API for the next event and returns its body and headers.

        .Notes
            Returns $null, after a short pause, when the Runtime API cannot be reached; the event loop then
            asks again. Throws when the Runtime API answers with an error status: Lambda documents that the
            runtime should then exit, and the event loop exits 1 on this throw so Lambda can restart it.
    #>
    param (
        [Parameter(Position=0)]
        [System.Net.Http.HttpClient]$private:HttpClient
    )

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host '[RUNTIME-Get-LambdaNextInvocation]Start: Get-LambdaNextInvocation' }

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host '[RUNTIME-Get-LambdaNextInvocation]Create GET request to Runtime API' }

    $private:request = [System.Net.Http.HttpRequestMessage]::new()
    $private:request.Headers.Add('User-Agent', "aws-lambda-powershell/$env:POWERSHELL_VERSION")
    $private:request.Method = 'GET'
    $private:request.RequestUri = "http://$env:AWS_LAMBDA_RUNTIME_API/2018-06-01/runtime/invocation/next"

    try {
        # Get the next invocation
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host '[RUNTIME-Get-LambdaNextInvocation]Get the next invocation' }
        $private:response = $private:HttpClient.SendAsync($private:request).GetAwaiter().GetResult()
    }
    catch {
        # The Runtime API could not be reached: let the event loop ask again after a short pause.
        Microsoft.PowerShell.Utility\Write-Host "[RUNTIME-Get-LambdaNextInvocation]Runtime API not reachable, retrying: $($_.Exception.Message)"
        Microsoft.PowerShell.Utility\Start-Sleep -Milliseconds 100
        return $null
    }
    finally {
        $private:request.Dispose()
    }

    if (-not $private:response.IsSuccessStatusCode) {
        $private:errorBody = $private:response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $private:status = [int]$private:response.StatusCode
        $private:response.Dispose()
        throw [System.InvalidOperationException]::new(
            ('Lambda Runtime API answered GET /runtime/invocation/next with HTTP {0}; the runtime is exiting. {1}' -f
                $private:status, $private:errorBody))
    }

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host '[RUNTIME-Get-LambdaNextInvocation]Read the response content' }
    $private:incomingEvent = $private:response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host '[RUNTIME-Get-LambdaNextInvocation]Generate the correct response headers' }
    $private:incomingHeaders = @{}
    foreach ($private:header in $private:response.Headers) {
        $private:incomingHeaders[$private:header.Key] = $private:header.Value
    }

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host '[RUNTIME-Get-LambdaNextInvocation]Create a response object' }
    $private:NextInvocationResponseObject = [pscustomobject]@{
        headers       = $private:incomingHeaders
        incomingEvent = $private:incomingEvent
    }

    $private:response.Dispose()
    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host '[RUNTIME-Get-LambdaNextInvocation]Return response object' }
    return [pscustomobject]$private:NextInvocationResponseObject
}
