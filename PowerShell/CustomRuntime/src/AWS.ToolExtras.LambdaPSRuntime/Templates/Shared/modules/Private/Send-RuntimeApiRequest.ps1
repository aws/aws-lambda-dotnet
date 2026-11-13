# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

# Not declared private: like its siblings, because they call it from their own scope, and a private:
# function is not visible there.
function Send-RuntimeApiRequest {
    <#
        .SYNOPSIS
            POST a body to the Lambda Runtime API.

        .DESCRIPTION
            Sends the body to the given Runtime API URI with the runtime's User-Agent, the invocation's
            Lambda-Runtime-Invocation-Id when Lambda sent one, and any extra headers, and throws when the
            Runtime API does not accept it (any status but 2xx), so the caller can report or log why.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Position=0)]
        [System.Net.Http.HttpClient]$private:HttpClient,

        [Parameter(Position=1)]
        [string]$private:Uri,

        [Parameter(Position=2)]
        [string]$private:Body,

        [Parameter(Position=3)]
        [array]$private:Header
    )

    $private:request = [System.Net.Http.HttpRequestMessage]::new()
    $private:request.Headers.Add('User-Agent', "aws-lambda-powershell/$env:POWERSHELL_VERSION")
    # The Runtime API checks it against the invocation it sent, so a post cannot complete another invocation.
    if ($script:__LambdaPSRuntimeInvocationId) {
        $private:request.Headers.Add('Lambda-Runtime-Invocation-Id', $script:__LambdaPSRuntimeInvocationId)
    }
    if ($private:Header) {
        foreach ($h in $private:Header) {
            $private:request.Headers.Add($h.Key, $h.Value)
        }
    }
    $private:request.Method = 'POST'
    $private:request.RequestUri = $private:Uri
    $private:request.Content = [System.Net.Http.StringContent]::new($private:Body)

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue "[RUNTIME-Send-RuntimeApiRequest]Sending POST $private:Uri ($($private:Body.Length) characters)" }

    try {
        $private:runtimeResponse = $private:HttpClient.SendAsync($private:request).GetAwaiter().GetResult()
    }
    finally {
        $private:request.Dispose()
    }
    try {
        $private:runtimeResponseContent = $private:runtimeResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue "[RUNTIME-Send-RuntimeApiRequest]Runtime API Response: $private:runtimeResponseContent" }

        # The Runtime API answers 202 when it accepts a post. Anything else means Lambda did NOT take
        # it -- for example 413 for a response over the payload limit. Ignoring the status would send
        # the loop on to the next event with the invocation still open, so it would end as 'Task timed
        # out' with nothing in the log naming the cause. Throwing lets the caller report it.
        if (-not $private:runtimeResponse.IsSuccessStatusCode) {
            throw [System.InvalidOperationException]::new(
                ('Lambda Runtime API rejected POST {0}: HTTP {1} {2}. {3}' -f
                    $private:Uri, [int]$private:runtimeResponse.StatusCode,
                    $private:runtimeResponse.ReasonPhrase, $private:runtimeResponseContent))
        }
    }
    finally {
        $private:runtimeResponse.Dispose()
    }
}
