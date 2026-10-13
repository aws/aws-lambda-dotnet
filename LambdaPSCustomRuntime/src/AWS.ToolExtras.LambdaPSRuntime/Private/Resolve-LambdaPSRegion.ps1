function Resolve-LambdaPSRegion {
    <#
    .SYNOPSIS
        Returns the AWS region a deploy uses when -Region is omitted: the one AWS.Tools would use.
    .DESCRIPTION
        The deploy cmdlets need the region as a string, not just for their AWS calls (which could
        resolve it themselves) but to compose the ECR image URI and to name it in messages. So when
        -Region is omitted, this resolves it the way an AWS.Tools cmdlet would, in this order:

          1. The shell's default region (Set-DefaultAWSRegion). With -SessionState, it is read from the
             public cmdlet's caller, so a region a script set is found; without it, Get-DefaultAWSRegion
             finds only a global one.
          2. The region of the -ProfileName profile, read from -ProfileLocation or the default
             credential files.
          3. AWS.Tools' own resolver: the default profile, read from -ProfileLocation or the default
             credential files, then AWS_REGION.
          4. The SDK's fallback chain: the AWS_PROFILE profile, and the instance or container
             metadata.

        AWS_DEFAULT_REGION is not read.

        When none supplies one it is a terminating RegionNotResolved error that names the options.
        Steps 3 and 4 are Get-LambdaPSAWSToolsRegion and Get-LambdaPSSdkFallbackRegion. The SDK types
        are looked up by name, so no SDK type is bound when the module is parsed.
    .PARAMETER CredentialParameter
        The caller's credential parameters (from Get-LambdaPSCredentialParameter). Only ProfileName
        and ProfileLocation are read.
    .PARAMETER SessionState
        The public cmdlet's $PSCmdlet.SessionState, the session state of its caller. Set-DefaultAWSRegion
        without -Scope sets the region in the caller's scope (a deploy script, for example), where the
        module's own code cannot see it.
    .OUTPUTS
        System.String
    .EXAMPLE
        $Region = Resolve-LambdaPSRegion -CredentialParameter $credentialParameter -SessionState $PSCmdlet.SessionState

        Returns, for example, 'us-east-1' after the caller ran Set-DefaultAWSRegion us-east-1.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter()]
        [ValidateNotNull()]
        [hashtable]$CredentialParameter = @{},

        [Parameter()]
        [System.Management.Automation.SessionState]$SessionState
    )

    $shellDefault = if ($null -ne $SessionState) {
        [string]$SessionState.PSVariable.GetValue('StoredAWSRegion')
    }
    else {
        [string](Get-DefaultAWSRegion -ErrorAction SilentlyContinue).Region
    }
    if (-not [string]::IsNullOrWhiteSpace($shellDefault)) {
        return $shellDefault
    }

    if ($CredentialParameter.ContainsKey('ProfileName')) {
        $chainType = 'Amazon.Runtime.CredentialManagement.CredentialProfileStoreChain' -as [type]
        if ($null -ne $chainType) {
            $chain = if ($CredentialParameter.ContainsKey('ProfileLocation')) { $chainType::new([string]$CredentialParameter.ProfileLocation) } else { $chainType::new() }
            $credentialProfile = $null
            if ($chain.TryGetProfile([string]$CredentialParameter.ProfileName, [ref]$credentialProfile) -and $null -ne $credentialProfile.Region) {
                return $credentialProfile.Region.SystemName
            }
        }
    }

    $toolsArguments = @{}
    if ($CredentialParameter.ContainsKey('ProfileLocation')) { $toolsArguments.ProfileLocation = [string]$CredentialParameter.ProfileLocation }
    $region = Get-LambdaPSAWSToolsRegion @toolsArguments
    if ($region) { return $region }

    # Instance metadata is left to the SDK chain, which also reads AWS_PROFILE.
    $region = Get-LambdaPSSdkFallbackRegion
    if ($region) { return $region }

    # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error with
    # its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
    # caller's -ErrorVariable naming this internal helper.
    throw [System.Management.Automation.ErrorRecord]::new(
        [System.InvalidOperationException]::new(
            ('No AWS region was given or configured. Pass -Region, set a default with ' +
             'Set-DefaultAWSRegion, set AWS_REGION, or give the profile a region.')),
        'RegionNotResolved', [System.Management.Automation.ErrorCategory]::InvalidArgument, $null)
}
