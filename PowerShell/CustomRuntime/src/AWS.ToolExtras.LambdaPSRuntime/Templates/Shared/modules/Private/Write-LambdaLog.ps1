# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

function private:Write-LambdaLog {
    <#
        .SYNOPSIS
            Write one record to the function log.

        .DESCRIPTION
            Writes the message as one line. With AWS_LAMBDA_LOG_FORMAT=JSON, writes it as a JSON line
            with timestamp, level, requestId, traceId and message, as the .NET runtime client does, and
            drops it when its level is below AWS_LAMBDA_LOG_LEVEL (default INFO).

        .PARAMETER ErrorRecord
            Adds the error's errorType, errorMessage and stackTrace to a JSON record.

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
        $Level = if ($ErrorRecord) { 'ERROR' }
        elseif ($Record -is [System.Management.Automation.WarningRecord]) { 'WARN' }
        elseif ($Record -is [System.Management.Automation.VerboseRecord] -or $Record -is [System.Management.Automation.DebugRecord]) { 'DEBUG' }
        else { 'INFO' }
        $Message = if ($Record -is [System.Management.Automation.InformationalRecord] -or $Record -is [System.Management.Automation.InformationRecord] -or $ErrorRecord) { [string]$Record }
        else { (Microsoft.PowerShell.Utility\Out-String -InputObject $Record).Trim() }
    }

    # Lambda makes each stdout line its own log event, so a record's newlines become carriage returns,
    # as the Node.js and Python runtimes do: text in a message cannot add lines such as 'END RequestId'.
    if ($env:AWS_LAMBDA_LOG_FORMAT -ne 'JSON') {
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
    $private:record.message = $Message
    if ($ErrorRecord) {
        $private:record.errorType = $ErrorRecord.CategoryInfo.Reason
        $private:record.errorMessage = $ErrorRecord.Exception.Message
        $private:record.stackTrace = @([string]$ErrorRecord.ScriptStackTrace -split '\r?\n' | Microsoft.PowerShell.Core\Where-Object { $_ })
    }
    Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue (Microsoft.PowerShell.Utility\ConvertTo-Json -InputObject $private:record -Compress)
}
