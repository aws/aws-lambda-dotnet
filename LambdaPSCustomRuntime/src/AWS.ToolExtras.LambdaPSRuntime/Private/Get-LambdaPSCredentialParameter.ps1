function Get-LambdaPSCredentialParameter {
    <#
    .SYNOPSIS
        Collects the AWS credential parameters a public cmdlet was given, for splatting onto AWS.Tools.
    .DESCRIPTION
        The AWS-calling public cmdlets accept the AWS.Tools credential parameters -ProfileName,
        -ProfileLocation and -Credential. This returns the ones the caller actually bound, as a
        hashtable that each seam splats onto every AWS.Tools cmdlet it calls (-CredentialParameter).
        When none were bound the hashtable is empty, so AWS.Tools resolves credentials the usual way:
        the shell's default credentials (Set-AWSCredential), environment variables, the default
        profile, or the instance or container role. With -SessionState, credentials the caller set
        with Set-AWSCredential in its own scope (a deploy script, for example) are returned as
        -Credential, because the module's AWS.Tools calls cannot see them there; global ones are left
        to AWS.Tools, which reads them itself.

        A relative -ProfileLocation is returned resolved against the current PowerShell location, so
        the profile check below and the AWS.Tools calls read the same file. A -ProfileLocation on
        another provider (Env:, Variable:, Function:) is a terminating PathNotFileSystem error.

        A -ProfileName that is not in the local credential store (read from -ProfileLocation when
        given) is a terminating ProfileNotFound error, because AWS.Tools would otherwise run the call
        with its default credentials. The lookup reads the credential files only; it makes no AWS call.
    .PARAMETER BoundParameters
        The calling cmdlet's $PSBoundParameters.
    .PARAMETER SessionState
        The public cmdlet's $PSCmdlet.SessionState, the session state of its caller. Read only when
        none of -ProfileName, -ProfileLocation and -Credential was bound.
    .OUTPUTS
        System.Collections.Hashtable
    .EXAMPLE
        $credentialParameter = Get-LambdaPSCredentialParameter -BoundParameters $PSBoundParameters -SessionState $PSCmdlet.SessionState

        Returns @{ ProfileName = 'dev' } when the cmdlet was called with -ProfileName dev.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [System.Collections.Generic.IDictionary[string, object]]$BoundParameters,

        [Parameter()]
        [System.Management.Automation.SessionState]$SessionState
    )

    $credentialParameter = @{}
    foreach ($name in 'ProfileName', 'ProfileLocation', 'Credential') {
        if ($BoundParameters.ContainsKey($name) -and $null -ne $BoundParameters[$name]) {
            $credentialParameter[$name] = $BoundParameters[$name]
        }
    }
    if ($credentialParameter.Count -eq 0 -and $null -ne $SessionState) {
        # Set-AWSCredential stores them in StoredAWSCredentials. Get-AWSCredential returns them as the
        # AWSCredentials -Credential takes, but only when it runs in the caller's session state.
        $stored = $SessionState.PSVariable.GetValue('StoredAWSCredentials')
        $globalStored = Get-Variable -Name 'StoredAWSCredentials' -Scope Global -ValueOnly -ErrorAction Ignore
        if ($null -ne $stored -and -not [object]::ReferenceEquals($stored, $globalStored)) {
            $credentials = @($SessionState.InvokeCommand.InvokeScript($SessionState, [scriptblock]::Create('AWS.Tools.Common\Get-AWSCredential'), @()))
            $credentialsType = 'Amazon.Runtime.AWSCredentials' -as [type]
            if ($credentials.Count -eq 1 -and $null -ne $credentialsType) {
                $credential = $credentials[0].PSObject.BaseObject
                if ($credentialsType.IsInstanceOfType($credential)) { $credentialParameter.Credential = $credential }
            }
        }
    }
    if ($credentialParameter.ContainsKey('ProfileLocation')) {
        $credentialParameter.ProfileLocation = Resolve-LambdaPSPath -Path ([string]$credentialParameter.ProfileLocation)
    }

    if ($credentialParameter.ContainsKey('ProfileName')) {
        # The type is looked up by name, so no SDK type is bound when the module is parsed.
        $chainType = 'Amazon.Runtime.CredentialManagement.CredentialProfileStoreChain' -as [type]
        if ($null -ne $chainType) {
            $location = if ($credentialParameter.ContainsKey('ProfileLocation')) { [string]$credentialParameter.ProfileLocation } else { $null }
            $chain = if ($location) { $chainType::new($location) } else { $chainType::new() }
            $credentialProfile = $null
            if (-not $chain.TryGetProfile([string]$credentialParameter.ProfileName, [ref]$credentialProfile)) {
                $store = if ($location) { "the credential file '$location'" } else { 'the local credential store' }
                # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows
                # it with its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also
                # reach the caller's -ErrorVariable naming this internal helper.
                throw [System.Management.Automation.ErrorRecord]::new(
                    [System.ArgumentException]::new(
                        ("AWS credential profile '$($credentialParameter.ProfileName)' was not found in $store. " +
                         'Check the profile name, or pass -ProfileLocation with the file that defines it.'), 'ProfileName'),
                    'ProfileNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $credentialParameter.ProfileName)
            }
        }
    }
    return $credentialParameter
}
