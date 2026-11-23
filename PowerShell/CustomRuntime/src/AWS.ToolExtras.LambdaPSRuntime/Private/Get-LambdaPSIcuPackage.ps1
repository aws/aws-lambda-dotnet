function Get-LambdaPSIcuPackage {
    <#
    .SYNOPSIS
        Returns a local path to the verified ICU NuGet package the no-Docker runtime image ships.
    .DESCRIPTION
        Downloads Microsoft.ICU.ICU4C.Runtime.linux-<x64|arm64> at the version Config/pwsh.psd1 pins,
        with the pwsh download's retry and timeout settings, and checks it against the pinned SHA256.
        The caller deletes the file.
    .PARAMETER Architecture
        The target architecture: x86_64 or arm64.
    .OUTPUTS
        System.String
    .EXAMPLE
        Get-LambdaPSIcuPackage -Architecture arm64

        Returns the path of the verified linux-arm64 ICU package.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture
    )

    $icu = $script:Config.pwsh.Icu
    $rid = if ($Architecture -eq 'arm64') { 'arm64' } else { 'x64' }
    $id = "microsoft.icu.icu4c.runtime.linux-$rid"
    $url = "$($icu.PackageUrl)/$id/$($icu.Version)/$id.$($icu.Version).nupkg"
    $destination = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "lambdaps-$([System.IO.Path]::GetRandomFileName())-$id.nupkg")
    $download = $script:Config.pwsh.Download
    $webParams = @{
        Uri                      = $url
        OutFile                  = $destination
        UseBasicParsing          = $true
        ErrorAction              = 'Stop'
        MaximumRetryCount        = $download.RetryCount
        RetryIntervalSec         = $download.RetryDelaySeconds
        ConnectionTimeoutSeconds = $download.ConnectTimeoutSeconds
        OperationTimeoutSeconds  = $download.OperationTimeoutSeconds
    }
    $verified = $false
    try {
        Invoke-WebRequest @webParams
        $actual = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        if ($actual -ne $icu.Sha256[$rid]) {
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.Security.SecurityException]::new("SHA256 mismatch for '$url': expected $($icu.Sha256[$rid]) but got $actual."),
                'IcuPackageHashMismatch', [System.Management.Automation.ErrorCategory]::SecurityError, $url)
        }
        $verified = $true
    }
    finally {
        if (-not $verified) {
            $removeParams = @{ LiteralPath = $destination; Force = $true; ErrorAction = 'SilentlyContinue'; WhatIf = $false; Confirm = $false }
            Remove-Item @removeParams
        }
    }
    $destination
}
