function Invoke-LambdaPSFunction {
    <#
    .SYNOPSIS
        Invokes a PowerShell Lambda function, sending an object payload and returning the parsed
        function output.
    .DESCRIPTION
        A convenience wrapper over Invoke-LMFunction (AWS.Tools.Lambda) for the common
        request/response invoke. It removes the two rough edges of calling the raw cmdlet by hand:

          - Payload in: -Payload takes a PowerShell object, which is serialized to JSON for you. A
            value that is already a JSON string is sent verbatim.
          - Output out: the response Payload (a MemoryStream from the SDK) is read and, when it is
            JSON, deserialized, so the cmdlet returns the parsed function output directly instead of
            a raw stream.

        With -IncludeLog it requests the tail of the execution log, decodes it from base64, and
        writes it to the Information stream (view it with -InformationAction Continue). The parsed
        output remains the object returned on the pipeline, so -IncludeLog does not change the return
        shape.

        If the invocation reports a function error (the response FunctionError is set), this cmdlet
        writes a FunctionInvocationError for that function, whose message includes the returned error
        payload and whose TargetObject is the payload (parsed when it is JSON), rather than returning
        the error payload as if it were a normal result. It is non-terminating, so functions piped in
        after it are still invoked; pass -ErrorAction Stop to stop at the first one. A request that
        fails (permissions, a missing function) is still a terminating error.

        Each invocation is guarded by ShouldProcess with ConfirmImpact Low, as Invoke-LMFunction is:
        it does not ask by default, -Confirm asks once per function, and -WhatIf names the function
        and the payload size without invoking anything.

        It performs a synchronous (RequestResponse) Invoke-LMFunction call; no AWS SDK assembly is
        bound directly. For full control over the raw response (the MemoryStream Payload and base64
        LogResult), call Invoke-LMFunction directly.
    .PARAMETER FunctionName
        The Lambda function name (or ARN) to invoke. Accepts pipeline input, both a name piped in as a
        string and by property name, so the result of New-LambdaPSFunctionDeployment can be piped in
        (its FunctionName and Region bind). An object piped in must carry a FunctionName property;
        anything else is converted to text.
    .PARAMETER Payload
        The event payload. An object is serialized to JSON, to a depth of 32, and an array stays an
        array (@() is sent as [] and @(1) as [1]); a string is sent verbatim (assumed to be JSON
        already). Omit for an empty invoke.
    .PARAMETER IncludeLog
        Request the tail of the execution log and write the decoded log to the Information stream.
    .PARAMETER Qualifier
        An optional function version or alias to invoke.
    .PARAMETER Region
        The AWS region the function lives in. Accepts pipeline input by property name. When omitted
        and -FunctionName is a function ARN, the region in the ARN is used; otherwise a region set with
        Set-DefaultAWSRegion, including one a calling script set for itself, and without one
        Invoke-LMFunction resolves the region the way every AWS.Tools cmdlet does. A -Region that differs from the region
        in a function ARN fails with FunctionRegionMismatch.
    .PARAMETER ProfileName
        The name of the AWS credential profile to use, as for any AWS.Tools cmdlet. When no credential
        parameter is given, credentials set with Set-AWSCredential are used, including ones a calling
        script set for itself; otherwise AWS.Tools resolves them the usual way: environment variables,
        the default profile, or the instance or container role. A profile that is not found (in -ProfileLocation, or the default credential stores) fails
        with ProfileNotFound before any AWS call.
    .PARAMETER ProfileLocation
        The location of the shared credentials file, as for any AWS.Tools cmdlet. Used with
        -ProfileName.
    .PARAMETER Credential
        An AWSCredentials object to use, as for any AWS.Tools cmdlet (for example from
        Get-AWSCredential, or (Use-STSRole ...).Credentials).
    .OUTPUTS
        System.Object
    .EXAMPLE
        Invoke-LambdaPSFunction -FunctionName hello-powershell -Payload @{ name = 'world' } -IncludeLog -InformationAction Continue

        Invokes the function with { "name": "world" }, returns the parsed output, and shows the
        execution log (the Information stream is hidden unless -InformationAction Continue is set).
    .EXAMPLE
        Invoke-LambdaPSFunction -FunctionName hello-powershell -Payload '{"name":"world"}' -Region us-east-1

        Sends a payload that is already JSON verbatim.
    .EXAMPLE
        'fn-a', 'fn-b' | Invoke-LambdaPSFunction -Payload @{ ping = $true } -WhatIf

        Shows which functions would be invoked, and with how large a payload, without invoking them.
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Low', PositionalBinding = $false)]
    [OutputType([System.Object])]
    # PSUseOutputTypeCorrectly: 'return , $parsed' emits the parsed JSON as ONE object -- an array when
    # the function returned a JSON array, otherwise the object -- which [System.Object] covers. The
    # rule infers Object[] from the comma expression.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseOutputTypeCorrectly', '',
        Justification = 'The comma-wrapped return emits the parsed value as one object; Object is declared.')]
    # -Credential is an Amazon.Runtime.AWSCredentials, as on every AWS.Tools cmdlet, not a PSCredential
    # or a password. It is typed [object] so the module binds no SDK type, and AWS.Tools validates it.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUsePSCredentialType', '',
        Justification = 'AWS.Tools -Credential takes an AWSCredentials object, not a PSCredential.')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', '',
        Justification = 'AWS.Tools -Credential takes an AWSCredentials object, not a password string.')]
    param(
        [Parameter(Mandatory, Position = 0, ValueFromPipeline, ValueFromPipelineByPropertyName)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$FunctionName,

        [Parameter()]
        [object]$Payload,

        [Parameter()]
        [switch]$IncludeLog,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Qualifier,

        [Parameter(ValueFromPipelineByPropertyName)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Region,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ProfileName,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ProfileLocation,

        [Parameter()]
        [ValidateNotNull()]
        [object]$Credential
    )

    process {
        try {
            # Credential parameters the caller gave, splatted onto Invoke-LMFunction.
            # The caller's session state, so credentials a script set for itself are used too.
            $credentialParameter = Get-LambdaPSCredentialParameter -BoundParameters $PSBoundParameters -SessionState $PSCmdlet.SessionState
            # Serialize the payload. A string is assumed to be JSON already and sent verbatim; any other
            # object is converted to JSON. An omitted payload sends nothing.
            $payloadJson = $null
            if ($PSBoundParameters.ContainsKey('Payload') -and $null -ne $Payload) {
                $payloadJson = if ($Payload -is [string]) {
                    $Payload
                }
                else {
                    # -InputObject, not the pipeline, so an array is sent as an array: @(1) as [1], @() as [].
                    ConvertTo-Json -InputObject $Payload -Depth 32 -Compress
                }
            }

            $invokeParams = @{
                FunctionName = $FunctionName
                ClientConfig = (Get-LambdaPSClientConfig -Service Lambda)
                ErrorAction  = 'Stop'
            }
            if (-not [string]::IsNullOrEmpty($payloadJson)) { $invokeParams['Payload'] = $payloadJson }
            # -LogType Tail returns the tail of the execution log as the base64 LogResult.
            if ($IncludeLog) { $invokeParams['LogType'] = 'Tail' }
            if (-not [string]::IsNullOrWhiteSpace($Qualifier)) { $invokeParams['Qualifier'] = $Qualifier }
            # A function ARN names its region; use it when -Region is not given, rather than send the
            # call to the ambient region, where the function cannot be found.
            $arnRegion = (Split-LambdaPSFunctionArn -FunctionName $FunctionName).Region
            # A function ARN names its region; a different -Region would only reach Lambda as a not-found.
            if ($arnRegion -and -not [string]::IsNullOrWhiteSpace($Region) -and $Region -ne $arnRegion) {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.ArgumentException]::new(
                            ("-FunctionName '$FunctionName' is a function in region '$arnRegion', but -Region is '$Region'. " +
                             "Omit -Region, or pass -Region $arnRegion."), 'Region'),
                        'FunctionRegionMismatch', [System.Management.Automation.ErrorCategory]::InvalidArgument, $FunctionName))
            }
            $effectiveRegion = $Region
            if ([string]::IsNullOrWhiteSpace($effectiveRegion)) {
                $effectiveRegion = $arnRegion
            }
            # A region a script set with Set-DefaultAWSRegion is in the caller's scope, where
            # Invoke-LMFunction, called from this module, cannot see it.
            if ([string]::IsNullOrWhiteSpace($effectiveRegion)) {
                $effectiveRegion = [string]$PSCmdlet.SessionState.PSVariable.GetValue('StoredAWSRegion')
            }
            if (-not [string]::IsNullOrWhiteSpace($effectiveRegion)) { $invokeParams['Region'] = $effectiveRegion }

            $target = if ([string]::IsNullOrWhiteSpace($Qualifier)) { $FunctionName } else { "$($FunctionName):$Qualifier" }
            $payloadNote = if ([string]::IsNullOrEmpty($payloadJson)) { 'no payload' } else {
                "a $([System.Text.Encoding]::UTF8.GetByteCount($payloadJson))-byte JSON payload"
            }
            if (-not $PSCmdlet.ShouldProcess($target, "Invoke Lambda function with $payloadNote")) { return }
            # Confirmed once: Invoke-LMFunction must not ask again under -Confirm.
            $ConfirmPreference = 'None'

            $response = Invoke-LMFunction @invokeParams @credentialParameter

            # Read the response payload. The SDK returns it as a MemoryStream; tolerate a plain string too.
            $responseText = $null
            $responsePayload = $response.Payload
            if ($responsePayload -is [System.IO.Stream]) {
                if ($responsePayload.CanSeek) { $responsePayload.Position = 0 }
                $reader = [System.IO.StreamReader]::new($responsePayload)
                try { $responseText = $reader.ReadToEnd() }
                finally { $reader.Dispose() }
            }
            elseif ($responsePayload -is [string]) {
                $responseText = $responsePayload
            }

            # Whether the payload is JSON. Test-Json with -ErrorAction Ignore records nothing; a parse that
            # fails, even inside try/catch, would reach the caller's -ErrorVariable.
            $isJson = -not [string]::IsNullOrEmpty($responseText) -and
                (Test-Json -Json $responseText -Options IgnoreComments, AllowTrailingCommas -ErrorAction Ignore)

            # Surface the execution log (base64 in LogResult) on the Information stream when requested.
            if ($IncludeLog -and -not [string]::IsNullOrEmpty($response.LogResult)) {
                $log = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($response.LogResult))
                Write-Information $log -Tags 'LambdaPSInvokeLog'
            }

            # A function error comes back as HTTP 200 with FunctionError set and the error in the payload.
            # Report it as an error for THIS item rather than return the error payload as a normal
            # result; piped functions after it are still invoked unless -ErrorAction Stop is in effect.
            # The record's TargetObject is the error payload (parsed when it is JSON), so a script can
            # read errorType and errorMessage without parsing the message.
            if (-not [string]::IsNullOrEmpty($response.FunctionError)) {
                $detail = if (-not [string]::IsNullOrEmpty($responseText)) { ": $responseText" } else { '.' }
                $errorPayload = $responseText
                if ($isJson) {
                    try { $errorPayload = ConvertFrom-Json -InputObject $responseText -ErrorAction Stop } catch { $errorPayload = $responseText }
                }
                $PSCmdlet.WriteError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.InvalidOperationException]::new(
                            "Function '$FunctionName' returned a function error ($($response.FunctionError))$detail"),
                        'FunctionInvocationError', [System.Management.Automation.ErrorCategory]::InvalidResult, $errorPayload))
                return
            }

            if ([string]::IsNullOrEmpty($responseText)) {
                return
            }

            # Return parsed output when the payload is JSON; fall back to the raw string otherwise.
            # -NoEnumerate and the comma keep a JSON array an array: '[1]' stays a one-element array
            # and '[]' an empty one, rather than being unrolled into a scalar or nothing. JSON that
            # ConvertFrom-Json still refuses (keys that differ only in case) is returned as text.
            if (-not $isJson) { return $responseText }
            try {
                $parsed = ConvertFrom-Json -InputObject $responseText -NoEnumerate -ErrorAction Stop
                return , $parsed
            }
            catch {
                return $responseText
            }
        }
        catch {
            # Rethrow from this cmdlet: a helper's terminating error otherwise ends only the
            # statement that called it, and this cmdlet would carry on to its next step.
            $PSCmdlet.ThrowTerminatingError($_)
        }
    }
}
