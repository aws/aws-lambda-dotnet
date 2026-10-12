function Resolve-LambdaPSPwshVersion {
    <#
    .SYNOPSIS
        Resolves the full pwsh version (x.y.z) to install or bundle.
    .DESCRIPTION
        Pure resolver. The RPM install (dnf install powershell-<version>) and the release tarball both
        need a full three-part version, but the cmdlets accept a major.minor value (for example 7.4).
        This function maps a major.minor version to its default patch release (Config/pwsh.psd1), or
        returns an explicit override when one is supplied. A major.minor with no default, and no
        override, is an UnknownPwshVersion error; an override on another major.minor (7.6.6 for
        PowerShell 7.4) is a PwshVersionMismatch error, since the result would report one version
        and install the other. The calling cmdlet reports either as its own.
        Performs no disk access.
    .PARAMETER PowerShellVersion
        The target PowerShell version in major.minor form (for example '7.4').
    .PARAMETER PwshVersion
        An explicit full pwsh version (x.y.z) on the -PowerShellVersion major.minor. When supplied,
        it is returned verbatim.
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

        [Parameter()]
        [string]$PwshVersion
    )

    if (-not [string]::IsNullOrWhiteSpace($PwshVersion)) {
        if (-not $PwshVersion.StartsWith("$PowerShellVersion.", [System.StringComparison]::Ordinal)) {
            # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error with
            # its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
            # caller's -ErrorVariable naming this internal helper.
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.ArgumentException]::new(
                    ("-PwshVersion '$PwshVersion' is not a PowerShell $PowerShellVersion release. Pass a " +
                     "$PowerShellVersion.x version, or set -PowerShellVersion to its major.minor."), 'PwshVersion'),
                'PwshVersionMismatch', [System.Management.Automation.ErrorCategory]::InvalidArgument, $PwshVersion)
        }
        return $PwshVersion
    }

    # The default patch release per supported major.minor is data, in Config/pwsh.psd1.
    $default = $script:Config.pwsh.DefaultPatch[$PowerShellVersion]
    if ($null -eq $default) {
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.ArgumentException]::new(
                ("No default pwsh version is mapped for PowerShell '$PowerShellVersion'. Pass -PwshVersion " +
                 'with a full pwsh version (x.y.z).'), 'PowerShellVersion'),
            'UnknownPwshVersion', [System.Management.Automation.ErrorCategory]::InvalidArgument, $PowerShellVersion)
    }
    return $default
}
