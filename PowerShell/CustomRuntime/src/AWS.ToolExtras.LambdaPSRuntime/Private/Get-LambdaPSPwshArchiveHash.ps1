function Get-LambdaPSPwshArchiveHash {
    <#
    .SYNOPSIS
        Returns the pinned SHA256 of an official PowerShell Linux release tarball, or $null if none is pinned.
    .DESCRIPTION
        Reads the module's pinned pwsh tarball hashes from Config/pwsh.psd1. Get-LambdaPSPwshArchive uses it
        to verify the container-free (layer / self-contained .zip) download, and
        Copy-LambdaPSProjectTemplate uses it to bake the arm64 hash into the CMR and Container
        Dockerfiles (Microsoft publishes no aarch64 RPM, so the arm64 image build installs the
        tarball and verifies it against this hash). Returns $null rather than throwing for an unpinned
        version so each caller can fail closed in its own way.
    .PARAMETER PwshVersion
        The full pwsh version (x.y.z, for example 7.4.20).
    .PARAMETER Architecture
        The target architecture: x86_64 (the linux-x64 tarball) or arm64 (linux-arm64).
    .OUTPUTS
        System.String
    .EXAMPLE
        Get-LambdaPSPwshArchiveHash -PwshVersion 7.4.20 -Architecture arm64

        Returns the SHA256 of powershell-7.4.20-linux-arm64.tar.gz.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidatePattern('\A[0-9]+\.[0-9]+\.[0-9]+\z')]
        [string]$PwshVersion,

        [Parameter(Mandatory)]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture
    )

    $archToken = if ($Architecture -eq 'arm64') { 'arm64' } else { 'x64' }
    $versionHashes = $script:Config.pwsh.ArchiveSha256[$PwshVersion]
    if ($null -eq $versionHashes) { return $null }
    return $versionHashes[$archToken]
}
