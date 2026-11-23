function Test-LambdaPSServiceError {
    <#
    .SYNOPSIS
        Tests whether a caught error is a specific AWS service error, by exception identity.
    .DESCRIPTION
        Walks the exception and its InnerException chain and returns $true when any level's type NAME,
        or its AmazonServiceException.ErrorCode, is one of -ErrorCode.

        The chain walk is not optional. AWS.Tools cmdlets surface a service error as an outer
        System.InvalidOperationException with the SDK exception INSIDE it (for example
        Get-ECRRepository: InvalidOperationException -> Amazon.ECR.Model.RepositoryNotFoundException).
        A check of only the outermost type never matches.

        Types are matched by NAME rather than with a type literal, because a literal is resolved when
        the file is parsed and would force the AWS SDK assembly to load at module import. Message text
        is deliberately not consulted: wording and locale change, so it is not a stable contract.

        This helper does not throw and never writes to the error stream.
    .PARAMETER ErrorRecord
        The caught error record (in a catch block, $_).
    .PARAMETER ErrorCode
        The service exception names / error codes to match, for example 'RepositoryNotFoundException'.
    .OUTPUTS
        System.Boolean
    .EXAMPLE
        try { Get-ECRRepository -RepositoryName rt -ErrorAction Stop }
        catch { if (-not (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'RepositoryNotFoundException')) { throw } }

        Treats only a missing repository as expected; rethrows anything else.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [System.Management.Automation.ErrorRecord]$ErrorRecord,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string[]]$ErrorCode
    )

    for ($exception = $ErrorRecord.Exception; $null -ne $exception; $exception = $exception.InnerException) {
        if ($ErrorCode -contains $exception.GetType().Name) {
            return $true
        }

        $code = $exception.PSObject.Properties['ErrorCode']
        if ($null -ne $code -and $ErrorCode -contains [string]$code.Value) {
            return $true
        }
    }

    return $false
}
