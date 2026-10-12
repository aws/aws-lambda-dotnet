function Test-LambdaPSBootstrapGlobalization {
    <#
    .SYNOPSIS
        Checks that a container-free bootstrap enables globalization-invariant mode (R-Globalization).
    .DESCRIPTION
        Pure rule evaluator. The Layer and SelfContainedZip targets run on the stock provided.al2023
        base, which ships no ICU (libicu). .NET reads DOTNET_SYSTEM_GLOBALIZATION_INVARIANT at process
        startup, so unless the bootstrap sets it for pwsh, pwsh aborts immediately with
        "Couldn't find a valid ICU package." The scaffolded bootstrap is an sh wrapper that sets the
        flag and then execs pwsh; this rule catches a customer who edits that out. The flag must be 1
        or true (quoted or not), set before exec by an export ('export NAME=1', or 'NAME=1' and
        'export NAME' in either order), or on the pwsh command ('NAME=1 exec ...', 'exec env NAME=1
        ...', or 'NAME=1 pwsh ...' without exec). The last assignment before exec wins, and anything
        after exec never runs. The name matches case-sensitively, as .NET reads it.

        The rule applies only to the container-free targets. CMR and Container install pwsh together
        with libicu into the image (the Microsoft RPM on amd64, dnf on arm64), so invariant mode is
        not required there and the rule reports a passing, not-applicable finding.

        Returns one finding; performs no disk access.
    .PARAMETER Bootstrap
        The bootstrap as Read-LambdaPSBootstrap returns it.
    .PARAMETER Target
        The deployment target the bootstrap belongs to: 'CMR' (default), 'Layer', 'SelfContainedZip',
        or 'Container'.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.Finding
    .EXAMPLE
        Test-LambdaPSBootstrapGlobalization -Bootstrap (Read-LambdaPSBootstrap -Path ./bootstrap) -Target Layer

        Returns a finding describing whether the layer bootstrap enables invariant globalization.
    #>
    [CmdletBinding()]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.Finding')]
    param(
        [Parameter(Mandatory)]
        [PSTypeName('AWS.ToolExtras.LambdaPSRuntime.Bootstrap')]
        [PSCustomObject]$Bootstrap,

        [Parameter()]
        [ValidateSet('CMR', 'Layer', 'SelfContainedZip', 'Container')]
        [string]$Target = 'CMR'
    )

    # CMR and Container images install pwsh with libicu via the RPM, so invariant mode is not needed.
    if ($Target -ne 'Layer' -and $Target -ne 'SelfContainedZip') {
        return New-LambdaPSFinding -RuleId 'R-Globalization' -Passed $true -Message 'Not applicable: CMR and container images install pwsh with libicu, so invariant globalization is not required.'
    }
    if ($Bootstrap.Unrecognised.Count -gt 0) {
        return New-LambdaPSUnverifiedFinding -RuleId 'R-Globalization' -Construct $Bootstrap.Unrecognised -Check 'the pwsh that runs runtime.ps1 starts with DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 (or true).'
    }

    $name = 'DOTNET_SYSTEM_GLOBALIZATION_INVARIANT'
    # The value the pwsh statement starts with: an exported value, or one set on the command itself.
    $value = if ($null -ne $Bootstrap.Pwsh -and $Bootstrap.Pwsh.Environment.ContainsKey($name)) { $Bootstrap.Pwsh.Environment[$name] } else { $null }
    if ($null -ne $value -and @('1', 'true') -contains $value) {
        return New-LambdaPSFinding -RuleId 'R-Globalization' -Passed $true -Message 'Bootstrap enables DOTNET_SYSTEM_GLOBALIZATION_INVARIANT for pwsh, so it runs on provided.al2023 without libicu.'
    }
    New-LambdaPSFinding -RuleId 'R-Globalization' -Passed $false -Message 'Bootstrap does not set DOTNET_SYSTEM_GLOBALIZATION_INVARIANT for pwsh (export it, or set it on the pwsh command). provided.al2023 ships no libicu, so pwsh aborts at startup ("Couldn''t find a valid ICU package").'
}
