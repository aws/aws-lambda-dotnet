function Resolve-LambdaPSPwshVersion {
    <#
    .SYNOPSIS
        Resolves the full pwsh version (x.y.z) to install or bundle.
    .DESCRIPTION
        Pure resolver. The RPM install (dnf install powershell-<version>) and the release tarball both
        need a full three-part version, but the cmdlets accept a major.minor value (for example 7.4).
        This function maps a major.minor version to its default patch release (Config/pwsh.psd1), or
        returns an explicit override when one is supplied. A major.minor with no default, and no
        override, is an UnknownPwshVersion error, which the calling cmdlet reports as its own.
        Performs no disk access.
    .PARAMETER PowerShellVersion
        The target PowerShell version in major.minor form (for example '7.4').
    .PARAMETER PwshVersion
        An explicit full pwsh version (x.y.z, digits only). When supplied, it is returned verbatim.
    .EXAMPLE
        Resolve-LambdaPSPwshVersion -PowerShellVersion '7.4'

        Returns the default patch release mapped for 7.4 in Config/pwsh.psd1 (for example '7.4.20').
    .EXAMPLE
        Resolve-LambdaPSPwshVersion -PowerShellVersion '7.9' -PwshVersion '7.9.1'

        Returns '7.9.1' because an explicit override was supplied.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidatePattern('\A[0-9]+\.[0-9]+\z')]
        [string]$PowerShellVersion,

        # Empty when not given, as the callers pass their own unset -PwshVersion through.
        [Parameter()]
        [ValidatePattern('\A([0-9]+\.[0-9]+\.[0-9]+)?\z')]
        [string]$PwshVersion
    )

    if (-not [string]::IsNullOrWhiteSpace($PwshVersion)) {
        return $PwshVersion
    }

    # The default patch release per supported major.minor is data, in Config/pwsh.psd1.
    $default = $script:Config.pwsh.DefaultPatch[$PowerShellVersion]
    if ($null -eq $default) {
        # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error with
        # its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
        # caller's -ErrorVariable naming this internal helper.
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.ArgumentException]::new(
                ("No default pwsh version is mapped for PowerShell '$PowerShellVersion'. Pass " +
                 '-PowerShellVersion as a full pwsh release (x.y.z).'), 'PowerShellVersion'),
            'UnknownPwshVersion', [System.Management.Automation.ErrorCategory]::InvalidArgument, $PowerShellVersion)
    }
    return $default
}
