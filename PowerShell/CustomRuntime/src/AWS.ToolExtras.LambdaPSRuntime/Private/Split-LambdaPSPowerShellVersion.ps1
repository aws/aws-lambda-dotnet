function Split-LambdaPSPowerShellVersion {
    <#
    .SYNOPSIS
        Splits a -PowerShellVersion value into the PowerShell line and the exact release it names.
    .DESCRIPTION
        x.y.z names that release. x.y.* names the line, and x.* the highest line of that major
        version in Config/pwsh.psd1 DefaultPatch, with no release, so the caller picks one. A bare
        x.y is a PowerShellVersionNotSpecific error, and an x.* with no supported line
        UnknownPwshVersion.
    .PARAMETER PowerShellVersion
        The value given: x.y.z, x.y.*, x.* or (refused) x.y.
    .OUTPUTS
        PSCustomObject with Line (x.y) and Release (x.y.z, or $null for a wildcard).
    .EXAMPLE
        Split-LambdaPSPowerShellVersion -PowerShellVersion '7.*'

        Returns Line 7.6 and no Release, when 7.6 is the highest 7.x line the module maps.
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [ValidatePattern('\A[0-9]+\.([0-9]+(\.([0-9]+|\*))?|\*)\z')]
        [string]$PowerShellVersion
    )

    # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error
    # with its own $PSCmdlet, so -ErrorVariable never names this helper.
    if ($PowerShellVersion -match '\A([0-9]+\.[0-9]+)\.([0-9]+)\z') {
        return [PSCustomObject]@{ Line = $Matches[1]; Release = $PowerShellVersion }
    }
    if ($PowerShellVersion -match '\A([0-9]+\.[0-9]+)\.\*\z') {
        return [PSCustomObject]@{ Line = $Matches[1]; Release = $null }
    }
    if ($PowerShellVersion -match '\A([0-9]+)\.\*\z') {
        $major = $Matches[1]
        $line = @($script:Config.pwsh.DefaultPatch.Keys | Where-Object { $_.StartsWith("$major.", [System.StringComparison]::Ordinal) } |
                Sort-Object { [version]$_ }) | Select-Object -Last 1
        if (-not $line) {
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.ArgumentException]::new(
                    ("No PowerShell $major line is supported, so -PowerShellVersion '$PowerShellVersion' names no release. " +
                     "Supported lines: $((@($script:Config.pwsh.DefaultPatch.Keys | Sort-Object { [version]$_ })) -join ', ')."), 'PowerShellVersion'),
                'UnknownPwshVersion', [System.Management.Automation.ErrorCategory]::InvalidArgument, $PowerShellVersion)
        }
        return [PSCustomObject]@{ Line = $line; Release = $null }
    }
    $example = $script:Config.pwsh.DefaultPatch[$PowerShellVersion]
    if (-not $example) { $example = "$PowerShellVersion.0" }
    throw [System.Management.Automation.ErrorRecord]::new(
        [System.ArgumentException]::new(
            ("-PowerShellVersion '$PowerShellVersion' names no release. Pass '$PowerShellVersion.*' for the newest " +
             "PowerShell $PowerShellVersion release, or an exact release such as '$example'."), 'PowerShellVersion'),
        'PowerShellVersionNotSpecific', [System.Management.Automation.ErrorCategory]::InvalidArgument, $PowerShellVersion)
}
