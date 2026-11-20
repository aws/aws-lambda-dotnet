function New-LambdaPSFinding {
    <#
    .SYNOPSIS
        Builds one Test-LambdaPSRuntimeProject finding.
    .DESCRIPTION
        The single shape every bootstrap rule returns: RuleId, Severity, Passed, Message and Path,
        typed AWS.ToolExtras.LambdaPSRuntime.Finding. Severity follows from Passed: a failed rule is an
        Error, a passed one is Information. Performs no disk access.
    .PARAMETER RuleId
        The rule's identifier, for example 'R-LineEndings'.
    .PARAMETER Passed
        Whether the bootstrap satisfies the rule.
    .PARAMETER Message
        What the rule found, and for a failure, why it matters.
    .PARAMETER Path
        The full path of the project the rule checked, so findings from several projects can be told
        apart. Path is $null when it is not given.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.Finding
    .EXAMPLE
        New-LambdaPSFinding -RuleId 'R-LineEndings' -Passed $true -Message 'Bootstrap uses LF line endings.'

        Returns a passed (Information) finding.
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
        [bool]$Passed,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Message,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path
    )

    [PSCustomObject]@{
        PSTypeName = 'AWS.ToolExtras.LambdaPSRuntime.Finding'
        RuleId     = $RuleId
        Severity   = if ($Passed) { 'Information' } else { 'Error' }
        Passed     = $Passed
        Message    = $Message
        Path       = if ($PSBoundParameters.ContainsKey('Path')) { $Path } else { $null }
    }
}
