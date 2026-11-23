function Set-LambdaPSRuntimeAlias {
    <#
    .SYNOPSIS
        Points a Lambda customer-managed runtime alias at a published runtime version.
    .DESCRIPTION
        Thin, single-purpose seam over the runtime alias cmdlets (AWS.Tools.LambdaCore). It reads the
        alias with Get-LMCRuntimeAlias, then repoints it with Update-LMCRuntimeAlias, or creates it with
        New-LMCRuntimeAlias when it does not exist. It exists so the orchestrator has one mockable
        point for alias management. It binds no AWS SDK assembly directly.
    .PARAMETER RuntimeName
        The customer-managed runtime name the alias belongs to.
    .PARAMETER RuntimeAlias
        The alias name to create or repoint (for example 'default').
    .PARAMETER RuntimeVersion
        The published runtime version the alias should point at.
    .PARAMETER Region
        The AWS region the runtime is in.
    .PARAMETER CredentialParameter
        AWS credential parameters (ProfileName, ProfileLocation, Credential) to splat onto every
        AWS.Tools call. Empty by default, so AWS.Tools uses its default credentials.
    .OUTPUTS
        None.
    .EXAMPLE
        Set-LambdaPSRuntimeAlias -RuntimeName my-ps -RuntimeAlias default -RuntimeVersion v1 -Region us-east-1

        Creates or repoints the 'default' alias to runtime version v1.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([void])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RuntimeName,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RuntimeAlias,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RuntimeVersion,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Region,

        [Parameter()]
        [ValidateNotNull()]
        [hashtable]$CredentialParameter = @{}
    )


    if ($PSCmdlet.ShouldProcess("$RuntimeName/$RuntimeAlias", "Point alias at $RuntimeVersion")) {
        # New-LMCRuntimeAlias / Update-LMCRuntimeAlias use -RuntimeName (the runtime name), -Name (the
        # alias name), and -RuntimeVersion. Look the alias up first and create or repoint it
        # accordingly, so a re-deploy makes no failed create call. The conflict fallback covers a
        # create that races another.
        $clientConfig = Get-LambdaPSClientConfig -Service LambdaCore
        $aliasParams = @{
            RuntimeName    = $RuntimeName
            Name           = $RuntimeAlias
            RuntimeVersion = $RuntimeVersion
            Region         = $Region
            ClientConfig   = $clientConfig
        }
        $aliasExists = $true
        try {
            $null = Get-LMCRuntimeAlias -RuntimeName $RuntimeName -Name $RuntimeAlias -Region $Region -ClientConfig $clientConfig @CredentialParameter -ErrorAction Stop
        }
        catch {
            if (-not (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'ResourceNotFoundException')) { throw }
            $aliasExists = $false
        }
        if ($aliasExists) {
            Write-Verbose "Alias '$RuntimeAlias' exists; repointing to $RuntimeVersion."
            $response = Update-LMCRuntimeAlias @aliasParams @CredentialParameter -ErrorAction Stop
        }
        else {
            try {
                $response = New-LMCRuntimeAlias @aliasParams @CredentialParameter -ErrorAction Stop
            }
            catch {
                if (-not (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'ResourceConflictException')) { throw }
                Write-Verbose "Alias '$RuntimeAlias' was created concurrently; repointing to $RuntimeVersion."
                $response = Update-LMCRuntimeAlias @aliasParams @CredentialParameter -ErrorAction Stop
            }
        }
        # The response carries the alias's state and the version it now points at.
        if ($null -ne $response) {
            Write-Verbose "Alias '$RuntimeAlias' points at '$($response.RuntimeVersion)' (state: $($response.State))."
        }
    }
}
