function New-LambdaPSRuntimeAccessDeniedError {
    <#
    .SYNOPSIS
        Builds the RuntimeServiceAccessDenied error for a runtime lookup Lambda refused.
    .DESCRIPTION
        Where customer-managed runtimes are not offered, Lambda answers GetRuntime with
        AccessDeniedException ("Unable to determine service/operation name to be authorized"), the
        same code as a real IAM denial. Message text is not a stable contract, so the error names both
        causes. Both deploy cmdlets look the runtime up first and report a refusal with this error.
        It is returned, not thrown, so the error is attributed to the calling cmdlet.
    .PARAMETER RuntimeName
        The runtime that was looked up.
    .PARAMETER Region
        The region the lookup was made in.
    .PARAMETER Exception
        The service's AccessDeniedException, kept as the inner exception.
    .OUTPUTS
        System.Management.Automation.ErrorRecord
    .EXAMPLE
        $PSCmdlet.ThrowTerminatingError((New-LambdaPSRuntimeAccessDeniedError -RuntimeName rt -Region $Region -Exception $_.Exception))

        Stops the calling cmdlet with RuntimeServiceAccessDenied.
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.ErrorRecord])]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Builds an in-memory ErrorRecord; changes no state.')]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RuntimeName,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Region,

        [Parameter()]
        [System.Exception]$Exception
    )

    [System.Management.Automation.ErrorRecord]::new(
        [System.UnauthorizedAccessException]::new(
            ("Lambda refused GetRuntime for '$RuntimeName' in $Region. Customer-managed runtimes may not " +
             "be available in $Region, so check -Region. If they are, the caller lacks lambda:GetRuntime. " +
             'Nothing was created.'), $Exception),
        'RuntimeServiceAccessDenied', [System.Management.Automation.ErrorCategory]::PermissionDenied, $Region)
}
