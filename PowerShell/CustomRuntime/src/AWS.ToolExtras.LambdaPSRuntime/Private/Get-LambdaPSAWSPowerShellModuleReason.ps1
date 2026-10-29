function Get-LambdaPSAWSPowerShellModuleReason {
    <#
    .SYNOPSIS
        Says which of a list of module names are AWSPowerShell or AWSPowerShell.NetCore, and why each
        cannot be used on Lambda.
    .DESCRIPTION
        The one copy of the AWSPowerShellModuleNotSupported reasons, for a handler's #Requires
        (Get-LambdaPSRequiredModule) and for -IncludeModule and -Module (Get-LambdaPSModuleClosure):
          AWSPowerShell          the Windows PowerShell (Desktop edition) module; it does not run in
                                 PowerShell 7 on Lambda.
          AWSPowerShell.NetCore  every AWS service (over 600 MB installed): more than Lambda's 250 MB
                                 limit for a .zip package and its layers, and in a container image,
                                 importing it takes over 10 seconds and over 1 GB of memory on every
                                 cold start.
        The AWS.Tools.* modules split the same cmdlets by service. Names are compared
        case-insensitively, as PowerShell compares them, and a name given twice (in any case) is
        listed once, as first spelled.
    .PARAMETER Name
        The module names to check.
    .OUTPUTS
        System.Management.Automation.PSCustomObject
    .EXAMPLE
        Get-LambdaPSAWSPowerShellModuleReason -Name 'AWS.Tools.S3', 'awspowershell.netcore'

        Returns Names @('awspowershell.netcore') and the AWSPowerShell.NetCore reason. Returns $null
        when no name is either module.
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [string[]]$Name
    )

    $reasons = [ordered]@{
        'AWSPowerShell'         = 'AWSPowerShell is the Windows PowerShell (Desktop edition) module and does not run in ' +
            'PowerShell 7 on Lambda'
        'AWSPowerShell.NetCore' = "AWSPowerShell.NetCore holds every AWS service (over 600 MB installed): more than Lambda's " +
            '250 MB limit for a .zip package and its layers, and in a container image, importing it takes over ' +
            '10 seconds and over 1 GB of memory on every cold start'
    }
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    # -in ignores case, so 'awspowershell.netcore' is AWSPowerShell.NetCore.
    $names = @($Name | Where-Object { $_ -in $reasons.Keys -and $seen.Add($_) })
    if ($names.Count -eq 0) {
        return $null
    }
    [PSCustomObject]@{
        Names  = $names
        Reason = @($reasons.Keys | Where-Object { $names -contains $_ } | ForEach-Object { $reasons[$_] }) -join '; '
    }
}
