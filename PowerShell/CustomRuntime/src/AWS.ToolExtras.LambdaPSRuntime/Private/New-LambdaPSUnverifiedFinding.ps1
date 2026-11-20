function New-LambdaPSUnverifiedFinding {
    <#
    .SYNOPSIS
        Builds a Test-LambdaPSRuntimeProject finding for a rule the bootstrap's shape kept from being checked.
    .DESCRIPTION
        The bootstrap rules check the forms the scaffolds write (see Read-LambdaPSBootstrap). When a
        bootstrap also uses a construct outside them, a rule cannot tell whether it holds, so it
        reports this instead of a pass or a failure: a finding with Passed $true, Severity
        'Warning' and Unverified $true, whose message names the constructs and says what to check
        by hand. Test-LambdaPSRuntimeProject writes it as a warning, and -UnverifiedAsFailure makes
        it fail. Performs no disk access.
    .PARAMETER RuleId
        The rule's identifier, for example 'R-Globalization'.
    .PARAMETER Construct
        The constructs the bootstrap uses that the rule does not evaluate, as Read-LambdaPSBootstrap
        names them.
    .PARAMETER Check
        What to check by hand, completing "Check by hand that ...".
    .PARAMETER Subject
        What uses the constructs, completing "Not verified: <subject> uses ...". Defaults to 'the
        bootstrap'; R-Entrypoint passes 'the Dockerfile'.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.Finding
    .EXAMPLE
        New-LambdaPSUnverifiedFinding -RuleId 'R-Env-Path' -Construct 'a function' -Check 'pwsh is found without PATH from the image.'

        Returns an unverified (Warning) finding.
    #>
    [CmdletBinding()]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.Finding')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Builds an in-memory object; changes no state.')]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RuleId,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string[]]$Construct,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Check,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Subject = 'the bootstrap'
    )

    $finding = New-LambdaPSFinding -RuleId $RuleId -Passed $true -Message "Not verified: $Subject uses $($Construct -join '; '). This check does not evaluate that; check by hand that $Check"
    $finding.Severity = 'Warning'
    Add-Member -InputObject $finding -NotePropertyName Unverified -NotePropertyValue $true
    $finding
}
