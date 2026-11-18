function Get-LambdaPSRuntimeAliasArchitecture {
    <#
    .SYNOPSIS
        Returns the CPU architectures Lambda reports for the current version of a customer-managed
        runtime alias.
    .DESCRIPTION
        Thin, single-purpose seam over Get-LMCRuntimeAlias (AWS.Tools.LambdaCore). It exists so the
        function deploy orchestrator has one mockable point for reading which architecture the
        runtime behind an alias was published for. It binds no AWS SDK assembly directly.

        Lambda now rejects a function whose architecture the customer-managed runtime does not
        support when the function is created. This read lets the deployer check first, before
        creating anything, so the error names the runtime alias.

        Returns the alias's Compatibility.Architectures as a string array ('x86_64' and/or 'arm64').
        Lambda fills these in from the runtime version's image manifest, not from what was declared
        at publish. The array is EMPTY if the service reports none, in which case the caller cannot
        verify the match and should say so rather than assume one.

        An alias that does not exist is a terminating RuntimeAliasNotFound error, and one scheduled for
        deletion RuntimeAliasPendingDeletion: a function bound to it would otherwise be created and fail
        on every invoke. Any other failed read (a refused lambda:GetRuntimeAlias, for one) is a
        terminating RuntimeAliasCheckFailed error that wraps the service's error.
    .PARAMETER RuntimeName
        The customer-managed runtime name the alias belongs to.
    .PARAMETER RuntimeAlias
        The runtime alias name (for example 'default').
    .PARAMETER Region
        The AWS region the runtime lives in.
    .PARAMETER CredentialParameter
        AWS credential parameters (ProfileName, ProfileLocation, Credential) to splat onto every
        AWS.Tools call. Empty by default, so AWS.Tools uses its default credentials.
    .OUTPUTS
        System.String[]
    .EXAMPLE
        Get-LambdaPSRuntimeAliasArchitecture -RuntimeName my-ps -RuntimeAlias default -Region us-east-1

        Returns 'arm64' for a runtime whose default alias points at an arm64 version.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    # PSUseOutputTypeCorrectly: the return is comma-wrapped ('return , ([string[]]...)'), which emits
    # the array as ONE object whose runtime type is string[] -- exactly what OutputType declares. The
    # rule infers Object[] from the static shape.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseOutputTypeCorrectly', '',
        Justification = 'Comma-wrapped return emits a single string[]; the declared OutputType is correct at runtime.')]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RuntimeName,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RuntimeAlias,

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

    # Get-LMCRuntimeAlias takes -RuntimeName (the runtime name) and -Name (the alias name), matching
    # New-LMCRuntimeAlias. Its response carries the alias's Compatibility block directly, so one call
    # answers the question without a second Get-LMCRuntimeVersion round-trip.
    $clientConfig = Get-LambdaPSClientConfig -Service LambdaCore
    try {
        $aliasInfo = Get-LMCRuntimeAlias -RuntimeName $RuntimeName -Name $RuntimeAlias -Region $Region -ClientConfig $clientConfig @CredentialParameter -ErrorAction Stop
    }
    catch {
        # Matched by exception identity (type name / SDK ErrorCode), not message text.
        if (-not (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'ResourceNotFoundException')) {
            # The service text ends the sentence, so it gets a full stop unless it has one; empty text is left out.
            $serviceText = $_.Exception.Message.Trim()
            if ($serviceText -and $serviceText -notmatch '[.!?]["'')\]]*\z') { $serviceText += '.' }
            $serviceDetail = if ($serviceText) { ": $serviceText" } else { '.' }
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.InvalidOperationException]::new(
                    ("Could not read alias '$RuntimeAlias' of runtime '$RuntimeName' in region '$Region' to check " +
                     "its architecture$serviceDetail Nothing was packaged or created."), $_.Exception),
                'RuntimeAliasCheckFailed', [System.Management.Automation.ErrorCategory]::ReadError, "$RuntimeName/$RuntimeAlias")
        }

        throw [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new(
                ("Runtime alias '$RuntimeAlias' was not found on runtime '$RuntimeName' in region '$Region'. " +
                 'A function bound to it would deploy and then fail on every invoke. Create or point the ' +
                 "alias first in the runtime's account (for example with New-LambdaPSRuntimeDeployment -RuntimeAlias " +
                 "$RuntimeAlias), or pass the alias that exists.")),
            'RuntimeAliasNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, "$RuntimeName/$RuntimeAlias")
    }

    # Lambda resolves an alias scheduled for deletion as if it were missing.
    if ([string]$aliasInfo.State -eq 'PENDING_DELETION') {
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new(
                ("Runtime alias '$RuntimeAlias' of runtime '$RuntimeName' in region '$Region' is scheduled for " +
                 'deletion, so a function bound to it would deploy and then fail on every invoke. Pass another ' +
                 'alias (New-LambdaPSRuntimeDeployment -RuntimeAlias creates one).')),
            'RuntimeAliasPendingDeletion', [System.Management.Automation.ErrorCategory]::ResourceUnavailable, "$RuntimeName/$RuntimeAlias")
    }

    $compatibility = if ($null -ne $aliasInfo) { $aliasInfo.Compatibility } else { $null }
    [string[]]$architectures = Get-LambdaPSReportedArchitecture -Compatibility $compatibility
    return , $architectures
}
