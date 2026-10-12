function Test-LambdaPSBootstrapLineEnding {
    <#
    .SYNOPSIS
        Checks that the bootstrap uses LF line endings (R-LineEndings).
    .DESCRIPTION
        Pure rule evaluator. Lambda executes the bootstrap directly on Linux, so the kernel reads its
        shebang byte for byte. With CRLF line endings the interpreter path ends in a carriage return
        ('/bin/sh\r') and the runtime fails to start, typically with "bad interpreter".

        The scaffolder writes the bootstrap with LF, the scaffolded Dockerfile strips carriage returns
        from it, and Build-LambdaPSRuntimePackage normalizes it for the layer and .zip packages. This
        rule catches a bootstrap saved with Windows line endings before it reaches a build that does
        none of these, such as one from an edited Dockerfile.

        Returns one finding; performs no disk access.
    .PARAMETER Bootstrap
        The bootstrap as Read-LambdaPSBootstrap returns it.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.Finding
    .EXAMPLE
        Test-LambdaPSBootstrapLineEnding -Bootstrap (Read-LambdaPSBootstrap -Path ./bootstrap)

        Returns a finding describing whether the bootstrap uses LF line endings.
    #>
    [CmdletBinding()]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.Finding')]
    param(
        [Parameter(Mandatory)]
        [PSTypeName('AWS.ToolExtras.LambdaPSRuntime.Bootstrap')]
        [PSCustomObject]$Bootstrap
    )

    if ($Bootstrap.Text.Contains("`r")) {
        return New-LambdaPSFinding -RuleId 'R-LineEndings' -Passed $false -Message 'Bootstrap has CRLF (Windows) line endings. Lambda executes it directly on Linux, so the shebang gains a trailing carriage return and the runtime fails to start ("bad interpreter"). Convert it to LF.'
    }
    New-LambdaPSFinding -RuleId 'R-LineEndings' -Passed $true -Message 'Bootstrap uses LF line endings.'
}
