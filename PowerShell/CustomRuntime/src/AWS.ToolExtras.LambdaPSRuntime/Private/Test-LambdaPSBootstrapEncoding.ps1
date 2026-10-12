function Test-LambdaPSBootstrapEncoding {
    <#
    .SYNOPSIS
        Checks that the bootstrap starts with the bytes '#!' (R-Encoding).
    .DESCRIPTION
        Pure rule evaluator. Lambda executes the bootstrap directly on Linux, and the kernel runs a
        script only when its first two bytes are '#!'. A bootstrap saved with a byte order mark
        starts with other bytes instead: UTF-8 (EF BB BF), written by Windows PowerShell's
        'Set-Content -Encoding UTF8' and some editors; UTF-16 (FF FE or FE FF), written by Windows
        PowerShell's Out-File and '>'; or UTF-32. So does one with anything before the shebang: a
        blank line, a space, or a comment above '#!/bin/sh'. Neither is run as a script, and the
        runtime fails to start.

        Build-LambdaPSRuntimePackage rewrites the bootstrap as UTF-8 without a byte order mark for
        the layer and .zip packages, and the scaffolded Dockerfile strips a leading UTF-8 mark in
        the CMR and Container images. A UTF-16 or UTF-32 bootstrap still fails in an image, and
        this rule catches any mark before a build that does none of these, such as one from an
        edited Dockerfile.

        Returns one finding; performs no disk access.
    .PARAMETER Bootstrap
        The bootstrap as Read-LambdaPSBootstrap returns it.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.Finding
    .EXAMPLE
        Test-LambdaPSBootstrapEncoding -Bootstrap (Read-LambdaPSBootstrap -Path ./bootstrap)

        Returns a finding describing whether the bootstrap starts with '#!', with no byte order mark
        before it.
    #>
    [CmdletBinding()]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.Finding')]
    param(
        [Parameter(Mandatory)]
        [PSTypeName('AWS.ToolExtras.LambdaPSRuntime.Bootstrap')]
        [PSCustomObject]$Bootstrap
    )

    if ($Bootstrap.ByteOrderMark) {
        return New-LambdaPSFinding -RuleId 'R-Encoding' -Passed $false -Message "Bootstrap starts with a $($Bootstrap.ByteOrderMark) byte order mark. Lambda executes it directly on Linux, where a script must start with ""#!"", so the runtime fails to start. Save it as UTF-8 without a byte order mark."
    }
    if (-not $Bootstrap.StartsWithShebang) {
        return New-LambdaPSFinding -RuleId 'R-Encoding' -Passed $false -Message 'Bootstrap does not start with "#!". Lambda executes it directly on Linux, where a script must start with those two bytes, so the runtime fails to start. Put the shebang (#!/bin/sh) on the first line, with nothing before it.'
    }
    New-LambdaPSFinding -RuleId 'R-Encoding' -Passed $true -Message 'Bootstrap starts with "#!" and has no byte order mark.'
}
