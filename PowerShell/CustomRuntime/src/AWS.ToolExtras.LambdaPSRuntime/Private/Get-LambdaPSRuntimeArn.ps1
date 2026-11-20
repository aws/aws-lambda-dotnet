function Get-LambdaPSRuntimeArn {
    <#
    .SYNOPSIS
        Resolves a customer-managed runtime ARN from its name via Get-LMCRuntime.
    .DESCRIPTION
        Thin, single-purpose seam over Get-LMCRuntime (AWS.Tools.LambdaCore). It exists so the function
        deploy orchestrator has one mockable point for runtime-ARN resolution instead of calling the
        LMC* cmdlet inline. It returns the runtime ARN, not a version ARN (the value a function binds
        to via -RuntimeConfig_RuntimeArn); the alias name, passed separately at create time, selects
        the version. It binds no AWS SDK assembly directly.
    .PARAMETER RuntimeName
        The customer-managed runtime name to resolve.
    .PARAMETER Region
        The AWS region the runtime lives in.
    .PARAMETER CredentialParameter
        AWS credential parameters (ProfileName, ProfileLocation, Credential) to splat onto every
        AWS.Tools call. Empty by default, so AWS.Tools uses its default credentials.
    .OUTPUTS
        System.String
    .EXAMPLE
        Get-LambdaPSRuntimeArn -RuntimeName my-ps -Region us-east-1

        Returns the ARN of the 'my-ps' runtime.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RuntimeName,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Region,

        [Parameter()]
        [ValidateNotNull()]
        [hashtable]$CredentialParameter = @{}
    )


    # `throw`, not $PSCmdlet.ThrowTerminatingError, in this helper: the public cmdlet that called it rethrows
    # the error with its own $PSCmdlet, and one raised through this helper's would also reach the caller's
    # -ErrorVariable naming this internal helper.

    # Get-LMCRuntime identifies the runtime with -RuntimeName and returns the resource, whose .Arn is
    # what -RuntimeConfig_RuntimeArn expects. NOTE the inconsistency in the generated cmdlet family:
    # New-LMCRuntime names the runtime with -Name, while every other runtime cmdlet takes -RuntimeName
    # (and uses -Name for the version or alias). -Name here throws ParameterBindingException at runtime.
    $clientConfig = Get-LambdaPSClientConfig -Service LambdaCore
    # A runtime that does not exist is a ResourceNotFoundException from the service; map
    # it to RuntimeArnNotFound below instead of letting the raw SDK error through.
    $runtime = $null
    try {
        $runtime = Get-LMCRuntime -RuntimeName $RuntimeName -Region $Region -ClientConfig $clientConfig @CredentialParameter -ErrorAction Stop
    }
    catch {
        # A region without customer-managed runtimes answers AccessDeniedException; name both causes,
        # as the runtime deploy does.
        if (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'AccessDeniedException') {
            throw (New-LambdaPSRuntimeAccessDeniedError -RuntimeName $RuntimeName -Region $Region -Exception $_.Exception)
        }
        if (-not (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'ResourceNotFoundException')) { throw }
    }
    if ($null -eq $runtime -or [string]::IsNullOrWhiteSpace($runtime.Arn)) {
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new(
                "Runtime '$RuntimeName' was not found or has no ARN in region '$Region'. Create it first (for example with New-LambdaPSRuntimeDeployment)."),
            'RuntimeArnNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $RuntimeName)
    }

    return $runtime.Arn
}
