function Get-LambdaPSHostPwsh {
    <#
    .SYNOPSIS
        Unpacks a pwsh release for the host's architecture and returns its pwsh binary, or $null when the release can't be fetched.
    .DESCRIPTION
        Lets a build for another architecture precompile PowerShellLambdaContext.dll: the DLL is IL, so a pwsh of
        the same release (same .NET) built for the host can compile it. The tarball comes from Get-LambdaPSPwshArchive,
        so it is SHA256-checked, and is removed once unpacked. A failed fetch is reported with Write-Verbose, because
        the runtime then compiles the DLL at cold start, as before; a SHA256 mismatch is a warning.
    .PARAMETER PwshVersion
        The pwsh release (x.y.z) the image bundles.
    .PARAMETER Architecture
        The host's architecture: x86_64 or arm64.
    .PARAMETER Destination
        The folder to unpack into.
    .OUTPUTS
        System.String
    .EXAMPLE
        Get-LambdaPSHostPwsh -PwshVersion 7.6.9 -Architecture x86_64 -Destination $stage/host-powershell

        Returns $stage/host-powershell/pwsh.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$PwshVersion,

        [Parameter(Mandatory)]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture,

        [Parameter(Mandatory)]
        [string]$Destination
    )

    try {
        $archive = Get-LambdaPSPwshArchive -PwshVersion $PwshVersion -Architecture $Architecture
    }
    catch {
        $message = "PowerShellLambdaContext.dll is not precompiled: pwsh $PwshVersion ($Architecture) could not be fetched. $($_.Exception.Message)"
        if ($_.FullyQualifiedErrorId -like 'PwshArchiveHashMismatch*') { Write-Warning $message } else { Write-Verbose $message }
        return $null
    }
    try {
        $null = [System.IO.Directory]::CreateDirectory($Destination)
        $stream = [System.IO.Compression.GZipStream]::new([System.IO.File]::OpenRead($archive), [System.IO.Compression.CompressionMode]::Decompress)
        try { [System.Formats.Tar.TarFile]::ExtractToDirectory($stream, $Destination, $true) }
        finally { $stream.Dispose() }
        [System.IO.Path]::Combine($Destination, 'pwsh')
    }
    finally {
        Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue -WhatIf:$false -Confirm:$false
    }
}
