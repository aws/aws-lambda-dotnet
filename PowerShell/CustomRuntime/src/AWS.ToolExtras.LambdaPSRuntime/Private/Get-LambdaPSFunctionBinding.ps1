function Get-LambdaPSFunctionBinding {
    <#
    .SYNOPSIS
        Returns the handler, runtime binding and architecture of an existing function, or $null if
        there is none.
    .DESCRIPTION
        Thin, read-only seam over Get-LMFunctionConfiguration (AWS.Tools.Lambda). The function deploy
        reads it before packaging so that, on a redeploy that keeps the function's runtime alias, it
        checks the architecture against that alias rather than one the function will not be bound to.
        A function that does not exist (ResourceNotFoundException, matched by Test-LambdaPSServiceError)
        returns $null; any other error is rethrown. It binds no AWS SDK assembly directly.
    .PARAMETER FunctionName
        The function name or ARN.
    .PARAMETER Region
        The AWS region the function lives in.
    .PARAMETER CredentialParameter
        AWS credential parameters (ProfileName, ProfileLocation, Credential) to splat onto the call.
    .OUTPUTS
        System.Management.Automation.PSCustomObject (Handler, RuntimeArn, RuntimeAlias, Architecture,
        Runtime, PackageType, Role, TenantIsolationMode), or $null. RuntimeArn and RuntimeAlias are empty
        for a function that is not bound to a customer-managed runtime.
    .EXAMPLE
        Get-LambdaPSFunctionBinding -FunctionName hello -Region us-east-1

        Returns the runtime ARN, alias and architecture function 'hello' has, or $null.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$FunctionName,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Region,

        [Parameter()]
        [ValidateNotNull()]
        [hashtable]$CredentialParameter = @{}
    )

    $clientConfig = Get-LambdaPSClientConfig -Service Lambda
    try {
        $configuration = Get-LMFunctionConfiguration -FunctionName $FunctionName -Region $Region -ClientConfig $clientConfig @CredentialParameter -ErrorAction Stop
    }
    catch {
        if (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'ResourceNotFoundException') { return $null }
        throw
    }
    $runtimeConfig = $configuration.RuntimeConfig
    [PSCustomObject]@{
        Handler      = [string]$configuration.Handler
        RuntimeArn   = if ($null -ne $runtimeConfig) { [string]$runtimeConfig.RuntimeArn } else { '' }
        RuntimeAlias = if ($null -ne $runtimeConfig) { [string]$runtimeConfig.RuntimeAlias } else { '' }
        Architecture = @($configuration.Architectures | ForEach-Object { [string]$_ }) | Select-Object -First 1
        Runtime      = [string]$configuration.Runtime
        PackageType  = [string]$configuration.PackageType
        Role         = [string]$configuration.Role
        TenantIsolationMode = if ($null -ne $configuration.TenancyConfig) { [string]$configuration.TenancyConfig.TenantIsolationMode } else { '' }
    }
}
