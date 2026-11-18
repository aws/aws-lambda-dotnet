# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

function private:Write-LambdaLog {
    <#
        .SYNOPSIS
            Write one record to the function log.

        .DESCRIPTION
            Writes the message as one line. With AWS_LAMBDA_LOG_FORMAT=JSON, writes it as a JSON line
            with timestamp, level, requestId, traceId, tenantId and message, as the .NET runtime client does, and
            drops it when its level is below AWS_LAMBDA_LOG_LEVEL (default INFO).

        .PARAMETER ErrorRecord
            Adds the error's errorType, errorMessage and stackTrace to a JSON record, or its stack to a text line.

        .PARAMETER Record
            A record from the handler's merged streams: an error is ERROR, a warning WARN, a verbose or
            debug record DEBUG, and anything else (information, or start-up output) INFO.
    #>
    [CmdletBinding(DefaultParameterSetName = 'Message')]
    param (
        [Parameter(Mandatory, Position = 0, ParameterSetName = 'Message')]
        [ValidateSet('TRACE', 'DEBUG', 'INFO', 'WARN', 'ERROR', 'FATAL')]
        [string]$Level,

        [Parameter(Mandatory, Position = 1, ParameterSetName = 'Message')]
        [AllowEmptyString()]
        [string]$Message,

        [Parameter(ParameterSetName = 'Message')]
        [System.Management.Automation.ErrorRecord]$ErrorRecord,

        [Parameter(Mandatory, ParameterSetName = 'Record')]
        [AllowNull()]
        $Record
    )

    if ($PSCmdlet.ParameterSetName -eq 'Record') {
        $ErrorRecord = $Record -as [System.Management.Automation.ErrorRecord]
        # A native command's stderr line arrives as an error record, but in text mode it is plain text, which Lambda counts as INFO.
        if ($ErrorRecord.FullyQualifiedErrorId -like 'NativeCommandError*') { $ErrorRecord = $null }
        $Level = if ($ErrorRecord) { 'ERROR' }
        elseif ($Record -is [System.Management.Automation.WarningRecord]) { 'WARN' }
        elseif ($Record -is [System.Management.Automation.VerboseRecord] -or $Record -is [System.Management.Automation.DebugRecord]) { 'DEBUG' }
        else { 'INFO' }
        $Message = if ($Record -is [System.Management.Automation.InformationalRecord] -or $Record -is [System.Management.Automation.InformationRecord] -or $Record -is [System.Management.Automation.ErrorRecord]) { [string]$Record }
        else { (Microsoft.PowerShell.Utility\Out-String -InputObject $Record).Trim() }
    }

    # Lambda makes each stdout line its own log event, so a record's newlines become carriage returns,
    # as the Node.js and Python runtimes do: text in a message cannot add lines such as 'END RequestId'.
    if ($env:AWS_LAMBDA_LOG_FORMAT -ne 'JSON') {
        # A handler's error says where it failed only in its stack. A JSON record carries the stack in
        # stackTrace instead, as the .NET runtime client writes it.
        if ($ErrorRecord.ScriptStackTrace) { $Message += "`n" + $ErrorRecord.ScriptStackTrace }
        # The label Amazon.Lambda.PowerShellHost writes, so an error can be found with a CloudWatch filter.
        if ($Level -eq 'ERROR') { $Message = "[Error] - $Message" }
        Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue ($Message -replace '\r?\n', "`r")
        return
    }

    $private:levels = 'TRACE', 'DEBUG', 'INFO', 'WARN', 'ERROR', 'FATAL'
    $private:minimum = [array]::IndexOf($private:levels, ([string]$env:AWS_LAMBDA_LOG_LEVEL).ToUpperInvariant())
    if ($private:minimum -lt 0) { $private:minimum = 2 }
    if ([array]::IndexOf($private:levels, $Level) -lt $private:minimum) { return }

    $private:record = [ordered]@{
        timestamp = [datetime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffZ', [cultureinfo]::InvariantCulture)
        level     = $Level
    }
    if ($env:AWS_LAMBDA_RUNTIME_AWS_REQUEST_ID) { $private:record.requestId = $env:AWS_LAMBDA_RUNTIME_AWS_REQUEST_ID }
    if ($env:_X_AMZN_TRACE_ID) { $private:record.traceId = $env:_X_AMZN_TRACE_ID }
    if ($script:__LambdaPSRuntimeTenantId) { $private:record.tenantId = $script:__LambdaPSRuntimeTenantId }
    $private:record.message = $Message
    if ($ErrorRecord) {
        $private:record.errorType = $ErrorRecord.CategoryInfo.Reason
        $private:record.errorMessage = $ErrorRecord.Exception.Message
        $private:record.stackTrace = @([string]$ErrorRecord.ScriptStackTrace -split '\r?\n' | Microsoft.PowerShell.Core\Where-Object { $_ })
    }
    Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue (Microsoft.PowerShell.Utility\ConvertTo-Json -InputObject $private:record -Compress)
}
