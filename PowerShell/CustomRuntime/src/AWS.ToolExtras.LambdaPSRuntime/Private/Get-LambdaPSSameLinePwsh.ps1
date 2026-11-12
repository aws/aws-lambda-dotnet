function Get-LambdaPSSameLinePwsh {
    <#
    .SYNOPSIS
        Returns the running PowerShell's pwsh when it is of the pwsh release's line (x.y), else $null.
    .DESCRIPTION
        PowerShellLambdaContext.dll references only System.Runtime, so a pwsh of the same line (same .NET)
        compiles a DLL that every release of the line loads, on any operating system.
    .PARAMETER PwshVersion
        The pwsh release (x.y.z) the package or image bundles.
    .OUTPUTS
        System.String
    .EXAMPLE
        Get-LambdaPSSameLinePwsh -PwshVersion 7.6.9

        In PowerShell 7.6.6 on macOS, returns $PSHOME/pwsh.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$PwshVersion
    )

    $running = $PSVersionTable.PSVersion
    if ("$($running.Major).$($running.Minor)" -ne ([version]$PwshVersion).ToString(2)) { return $null }
    $pwsh = [System.IO.Path]::Combine($PSHOME, $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' }))
    if (Test-Path -LiteralPath $pwsh -PathType Leaf) { $pwsh }
}
