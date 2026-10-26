# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

function private:Get-LambdaNextInvocation {
    <#
        .SYNOPSIS
            Get /NEXT invocation from AWS Lambda Runtime API.

        .DESCRIPTION
            Asks the Runtime API for the next event and returns its body and headers.

        .Notes
            Asks again, after a short pause, when the Runtime API cannot be reached, up to 10 attempts (about
            a second), then throws. Throws when the Runtime API answers with an error status: Lambda documents
            that the runtime should then exit. The event loop logs either throw and exits 1, so Lambda can
            restart the runtime, and a runtime started outside Lambda stops instead of retrying forever.
    #>
    param (
        [Parameter(Position=0)]
        [System.Net.Http.HttpClient]$private:HttpClient
    )

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Get-LambdaNextInvocation]Start: Get-LambdaNextInvocation' }

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Get-LambdaNextInvocation]Create GET request to Runtime API' }

    # A request message can be sent only once, so each attempt builds its own.
    $private:maxAttempts = 10
    for ($private:attempt = 1; ; $private:attempt++) {
        $private:request = [System.Net.Http.HttpRequestMessage]::new()
        $private:request.Headers.Add('User-Agent', "aws-lambda-powershell/$env:POWERSHELL_VERSION")
        $private:request.Method = 'GET'
        $private:request.RequestUri = "http://$env:AWS_LAMBDA_RUNTIME_API/2018-06-01/runtime/invocation/next"

        try {
            # Get the next invocation
            if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Get-LambdaNextInvocation]Get the next invocation' }
            $private:response = $private:HttpClient.SendAsync($private:request).GetAwaiter().GetResult()
            break
        }
        catch {
            if ($private:attempt -ge $private:maxAttempts) {
                throw [System.InvalidOperationException]::new(
                    ('Lambda Runtime API at {0} could not be reached after {1} attempts; the runtime is exiting. {2}' -f
                        $env:AWS_LAMBDA_RUNTIME_API, $private:maxAttempts, $_.Exception.GetBaseException().Message))
            }
            Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue "[RUNTIME-Get-LambdaNextInvocation]Runtime API not reachable (attempt $private:attempt of $private:maxAttempts), retrying: $($_.Exception.GetBaseException().Message)"
            Microsoft.PowerShell.Utility\Start-Sleep -Milliseconds 100
        }
        finally {
            $private:request.Dispose()
        }
    }

    if (-not $private:response.IsSuccessStatusCode) {
        $private:errorBody = $private:response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $private:status = [int]$private:response.StatusCode
        $private:response.Dispose()
        throw [System.InvalidOperationException]::new(
            ('Lambda Runtime API answered GET /runtime/invocation/next with HTTP {0}; the runtime is exiting. {1}' -f
                $private:status, $private:errorBody))
    }

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Get-LambdaNextInvocation]Read the response content' }
    $private:incomingEvent = $private:response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Get-LambdaNextInvocation]Generate the correct response headers' }
    $private:incomingHeaders = @{}
    foreach ($private:header in $private:response.Headers) {
        $private:incomingHeaders[$private:header.Key] = $private:header.Value
    }

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Get-LambdaNextInvocation]Create a response object' }
    $private:NextInvocationResponseObject = [pscustomobject]@{
        headers       = $private:incomingHeaders
        incomingEvent = $private:incomingEvent
    }

    $private:response.Dispose()
    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Get-LambdaNextInvocation]Return response object' }
    return [pscustomobject]$private:NextInvocationResponseObject
}
