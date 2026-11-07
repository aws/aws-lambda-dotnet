function Resolve-LambdaPSPwshRelease {
    <#
    .SYNOPSIS
        Resolves the newest pwsh release of a PowerShell line that a build can verify.
    .DESCRIPTION
        PackageRepository (x86_64 images, which install the signed RPM): the newest release of the line
        in Microsoft's repository, never older than the pin (Config/pwsh.psd1 DefaultPatch). It warns and
        returns the pin when the repository can't be read or has nothing that new.
        PinnedArchive (builds that install the tarball): the newest release with a pinned SHA256 for
        -Architecture, else the pin. It warns when the repository has a newer release.
        An explicit -PwshVersion is returned after Resolve-LambdaPSPwshVersion's checks, without a lookup.
    .PARAMETER PowerShellVersion
        The PowerShell line in major.minor form (for example 7.6).
    .PARAMETER PwshVersion
        An explicit pwsh release (x.y.z) on that line. Empty when not given.
    .PARAMETER InstallSource
        Where the build installs pwsh from: PackageRepository or PinnedArchive.
    .PARAMETER Architecture
        The target architecture, whose pinned tarball hash PinnedArchive needs: x86_64 or arm64.
    .OUTPUTS
        PSCustomObject with Version (x.y.z) and Source: Parameter (-PwshVersion), PackageRepository
        (found there) or Pinned (the module's pins).
    .EXAMPLE
        Resolve-LambdaPSPwshRelease -PowerShellVersion 7.6 -InstallSource PackageRepository -Architecture x86_64

        Returns the newest 7.6 release in Microsoft's RPM repository, for example 7.6.9.
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [ValidatePattern('\A[0-9]+\.[0-9]+\z')]
        [string]$PowerShellVersion,

        [Parameter()]
        [ValidatePattern('\A([0-9]+\.[0-9]+\.[0-9]+)?\z')]
        [string]$PwshVersion,

        [Parameter(Mandatory)]
        [ValidateSet('PackageRepository', 'PinnedArchive')]
        [string]$InstallSource,

        [Parameter(Mandatory)]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture
    )

    if (-not [string]::IsNullOrWhiteSpace($PwshVersion)) {
        return [PSCustomObject]@{
            Version = Resolve-LambdaPSPwshVersion -PowerShellVersion $PowerShellVersion -PwshVersion $PwshVersion
            Source  = 'Parameter'
        }
    }
    # UnknownPwshVersion for a line the module does not support, before any network call.
    $default = Resolve-LambdaPSPwshVersion -PowerShellVersion $PowerShellVersion
    $repositoryUrl = $script:Config.pwsh.PackageRepository.Url

    if ($InstallSource -eq 'PinnedArchive') {
        $pinned = @($script:Config.pwsh.ArchiveSha256.Keys | Where-Object {
                $_.StartsWith("$PowerShellVersion.", [System.StringComparison]::Ordinal) -and
                -not [string]::IsNullOrWhiteSpace((Get-LambdaPSPwshArchiveHash -PwshVersion $_ -Architecture $Architecture)) } |
            Sort-Object { [version]$_ })
        $version = if ($pinned.Count -gt 0) { $pinned[-1] } else { $default }
        try {
            $newest = @(Get-LambdaPSPwshRepositoryRelease -PowerShellVersion $PowerShellVersion)
            if ($newest.Count -gt 0 -and [version]$newest[-1] -gt [version]$version) {
                Write-Warning ("pwsh $($newest[-1]) is the newest PowerShell $PowerShellVersion release, but an $Architecture build " +
                    "installs pwsh from a tarball checked against a SHA256 this module pins, and the newest pinned is $version. " +
                    'Update AWS.ToolExtras.LambdaPSRuntime to install the newer release.')
            }
        }
        catch { Write-Verbose "Could not check for a pwsh release newer than ${version}: $($_.Exception.Message)" }
        return [PSCustomObject]@{ Version = $version; Source = 'Pinned' }
    }

    try {
        $newest = @(Get-LambdaPSPwshRepositoryRelease -PowerShellVersion $PowerShellVersion)
    }
    catch {
        Write-Warning ("Could not find the newest PowerShell $PowerShellVersion release in '$repositoryUrl', so the module's " +
            "pinned release $default is installed. $($_.Exception.Message)")
        return [PSCustomObject]@{ Version = $default; Source = 'Pinned' }
    }
    if ($newest.Count -eq 0) {
        Write-Warning "'$repositoryUrl' lists no PowerShell $PowerShellVersion release, so the module's pinned release $default is installed."
        return [PSCustomObject]@{ Version = $default; Source = 'Pinned' }
    }
    # Never older than the pin: a stale mirror or an altered list must not downgrade the runtime.
    if ([version]$newest[-1] -lt [version]$default) {
        Write-Warning "'$repositoryUrl' lists PowerShell $PowerShellVersion only up to $($newest[-1]), so the module's pinned release $default is installed."
        return [PSCustomObject]@{ Version = $default; Source = 'Pinned' }
    }
    [PSCustomObject]@{ Version = $newest[-1]; Source = 'PackageRepository' }
}
