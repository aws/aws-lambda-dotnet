# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

function _SendRuntimeApiRequest {
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
    if ($private:Header) {
        foreach ($h in $private:Header) {
            $private:request.Headers.Add($h.Key, $h.Value)
        }
    }
    $private:request.Method = 'POST'
    $private:request.RequestUri = $private:Uri
    $private:request.Content = [System.Net.Http.StringContent]::new($private:Body)

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Write-Host "[RUNTIME-SendRuntimeApiRequest]Sending POST $private:Uri ($($private:Body.Length) characters)" }

    try {
        $private:runtimeResponse = $private:HttpClient.SendAsync($private:request).GetAwaiter().GetResult()
    }
    finally {
        $private:request.Dispose()
    }
    try {
        $private:runtimeResponseContent = $private:runtimeResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Write-Host "[RUNTIME-SendRuntimeApiRequest]Runtime API Response: $private:runtimeResponseContent" }

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
