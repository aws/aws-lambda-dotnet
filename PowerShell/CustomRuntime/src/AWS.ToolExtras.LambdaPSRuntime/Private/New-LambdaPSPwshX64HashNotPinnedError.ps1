function New-LambdaPSPwshX64HashNotPinnedError {
    <#
    .SYNOPSIS
        Builds the PwshX64HashNotPinned error for a no-Docker x86_64 build of a pwsh release with no pinned hash.
    .DESCRIPTION
        The no-Docker build installs pwsh from the release tarball on both architectures, checked against
        a pinned SHA256 (the Docker build installs the x86_64 RPM instead). It returns the ErrorRecord,
        naming the pinned releases of the same line; the caller throws it.
    .PARAMETER PwshVersion
        The pwsh release (x.y.z) with no pinned x64 hash.
    .OUTPUTS
        System.Management.Automation.ErrorRecord
    .EXAMPLE
        throw (New-LambdaPSPwshX64HashNotPinnedError -PwshVersion 7.6.8)

        Refuses a no-Docker build of pwsh 7.6.8.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Builds an in-memory ErrorRecord; changes no state.')]
    [CmdletBinding()]
    [OutputType([System.Management.Automation.ErrorRecord])]
    param(
        [Parameter(Mandatory)]
        [ValidatePattern('\A[0-9]+\.[0-9]+\.[0-9]+\z')]
        [string]$PwshVersion
    )

    $line = $PwshVersion.Substring(0, $PwshVersion.LastIndexOf('.'))
    $pinned = @($script:Config.pwsh.ArchiveSha256.Keys | Where-Object {
            $_.StartsWith("$line.") -and -not [string]::IsNullOrWhiteSpace((Get-LambdaPSPwshArchiveHash -PwshVersion $_ -Architecture x86_64)) } |
        Sort-Object { [version]$_ })
    $pinnedList = if ($pinned.Count -gt 0) { " ($($pinned -join ', '))" } else { '' }
    [System.Management.Automation.ErrorRecord]::new(
        [System.InvalidOperationException]::new(
            ("pwsh $PwshVersion has no pinned SHA256 for its x64 tarball, so the no-Docker build cannot install it. " +
             "Use a pwsh release with a pinned hash$pinnedList, or build with -BuildMethod Docker. Nothing was created.")),
        'PwshX64HashNotPinned', [System.Management.Automation.ErrorCategory]::InvalidArgument, $PwshVersion)
}
