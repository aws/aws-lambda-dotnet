function Test-LambdaPSBuiltInModule {
    <#
    .SYNOPSIS
        Says whether a module name is one of PowerShell's own modules.
    .DESCRIPTION
        PowerShell's own modules (Microsoft.PowerShell.Utility, .Management, .Security, .Host,
        .Diagnostics, .Core, CimCmdlets, Microsoft.WSMan.Management, PSDiagnostics; some exist only on
        Windows) are part of PowerShell itself and are never published separately, so they are never
        staged or downloaded. The list is fixed rather than read from $PSHOME, which also holds
        modules that are updated on their own (PSReadLine, Microsoft.PowerShell.ThreadJob,
        PSResourceGet, PowerShellGet, PackageManagement, Microsoft.PowerShell.Archive) and whose
        version on Lambda may differ. Names are compared case-insensitively. This is the one copy of
        the list.
    .PARAMETER Name
        The module name.
    .OUTPUTS
        System.Boolean
    .EXAMPLE
        Test-LambdaPSBuiltInModule -Name 'Microsoft.PowerShell.Utility'

        Returns $true.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Name
    )

    $Name -in @(
        'Microsoft.PowerShell.Core', 'Microsoft.PowerShell.Diagnostics', 'Microsoft.PowerShell.Host',
        'Microsoft.PowerShell.Management', 'Microsoft.PowerShell.Security', 'Microsoft.PowerShell.Utility',
        'CimCmdlets', 'Microsoft.WSMan.Management', 'PSDiagnostics'
    )
}
