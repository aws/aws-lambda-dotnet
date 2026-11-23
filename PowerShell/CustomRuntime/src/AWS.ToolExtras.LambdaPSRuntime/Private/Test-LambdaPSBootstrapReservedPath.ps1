function Test-LambdaPSBootstrapReservedPath {
    <#
    .SYNOPSIS
        Checks bootstrap for writes to read-only reserved Lambda paths.
    .DESCRIPTION
        Pure rule evaluator. The Lambda execution filesystem is read-only except for /tmp, so a
        bootstrap that writes to reserved paths such as /var/runtime or /opt will fail at runtime.
        It checks the bootstrap's code (comments removed) for writes the scaffold's shape can make:
        an output redirect ('>' or '>>', the target quoted or not) or 'tee' into /var/runtime,
        /var/task or /opt. Other commands that write (cp, mv, mkdir, touch) are not checked. Paths
        match case-sensitively and whole. Returns one finding; performs no disk access.
    .PARAMETER Bootstrap
        The bootstrap as Read-LambdaPSBootstrap returns it.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.Finding
    .EXAMPLE
        Test-LambdaPSBootstrapReservedPath -Bootstrap (Read-LambdaPSBootstrap -Path ./bootstrap)

        Returns a finding for any write redirection into a reserved read-only path.
    #>
    [CmdletBinding()]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.Finding')]
    param(
        [Parameter(Mandatory)]
        [PSTypeName('AWS.ToolExtras.LambdaPSRuntime.Bootstrap')]
        [PSCustomObject]$Bootstrap
    )

    if ($Bootstrap.Unrecognised.Count -gt 0) {
        return New-LambdaPSUnverifiedFinding -RuleId 'R-ReservedWrite' -Construct $Bootstrap.Unrecognised -Check 'nothing it runs writes to /var/runtime, /var/task or /opt: only /tmp is writable at runtime.'
    }
    $offenders = [System.Collections.Generic.List[string]]::new()
    foreach ($path in '/var/runtime', '/var/task', '/opt') {
        if (@($Bootstrap.Writes | Where-Object { $_ -ceq $path -or $_.StartsWith("$path/", [System.StringComparison]::Ordinal) }).Count -gt 0) {
            $offenders.Add($path)
        }
    }
    if ($offenders.Count -gt 0) {
        return New-LambdaPSFinding -RuleId 'R-ReservedWrite' -Passed $false -Message "Bootstrap writes to read-only reserved path(s): $($offenders -join ', '). Only /tmp is writable at runtime."
    }
    New-LambdaPSFinding -RuleId 'R-ReservedWrite' -Passed $true -Message 'Bootstrap does not write to reserved read-only paths.'
}
