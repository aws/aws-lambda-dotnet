function Get-LambdaPSRuntimeAliasCompletion {
    <#
    .SYNOPSIS
        Completes New-LambdaPSRuntimeDeployment -RuntimeAlias with 'default' and the runtime's aliases.
    .DESCRIPTION
        The runtime's aliases are listed only when -RuntimeName is bound, with one attempt and a short
        timeout so tab completion stays responsive. When they can't be listed (no region or
        credentials, no such runtime, a timeout), only 'default' is offered: a completer must not throw.
    .PARAMETER WordToComplete
        The text typed so far.
    .PARAMETER FakeBoundParameters
        The parameters already on the command line, as PowerShell passes them to a completer.
    .OUTPUTS
        System.Management.Automation.CompletionResult
    .EXAMPLE
        Get-LambdaPSRuntimeAliasCompletion -WordToComplete 'te' -FakeBoundParameters @{ RuntimeName = 'my-ps'; Region = 'us-east-1' }

        Returns 'test' when runtime my-ps has an alias named test.
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.CompletionResult])]
    param(
        [Parameter()]
        [string]$WordToComplete,

        [Parameter(Mandatory)]
        [System.Collections.IDictionary]$FakeBoundParameters
    )

    $names = [System.Collections.Generic.List[string]]::new()
    $names.Add('default')
    $runtimeName = [string]$FakeBoundParameters['RuntimeName']
    if ($runtimeName) {
        try {
            $bound = [System.Collections.Generic.Dictionary[string, object]]::new()
            foreach ($key in $FakeBoundParameters.Keys) { $bound[[string]$key] = $FakeBoundParameters[$key] }
            $credentialParameter = Get-LambdaPSCredentialParameter -BoundParameters $bound
            $region = if ($FakeBoundParameters['Region']) { [string]$FakeBoundParameters['Region'] } else { Resolve-LambdaPSRegion -CredentialParameter $credentialParameter }
            $clientConfig = Get-LambdaPSClientConfig -Service LambdaCore
            $clientConfig.Timeout = [timespan]::FromSeconds(3)
            $clientConfig.MaxErrorRetry = 0
            $aliases = Get-LMCRuntimeAliasList -RuntimeName $runtimeName -Region $region -ClientConfig $clientConfig @credentialParameter -ErrorAction Stop
            foreach ($alias in $aliases) {
                if (-not $names.Contains([string]$alias.Name)) { $names.Add([string]$alias.Name) }
            }
        }
        catch {
            Write-Debug "Runtime aliases not listed for completion: $_"
        }
    }

    # PowerShell passes a quoted value with its quotes; an alias name can't contain one.
    $prefix = $WordToComplete.Trim([char[]]"'`"")
    foreach ($name in $names) {
        if ($name.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
            [System.Management.Automation.CompletionResult]::new($name, $name, 'ParameterValue', $name)
        }
    }
}
