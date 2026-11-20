function Get-LambdaPSAWSToolsRegion {
    <#
    .SYNOPSIS
        Returns the region AWS.Tools' own resolver finds, or $null.
    .DESCRIPTION
        Calls AWS.Tools' region resolver (IAWSRegionArgumentsMethods.TryGetRegion) without instance
        metadata. It reads, in order, the shell's default region, the default profile (from
        -ProfileLocation or the default credential files) and AWS_REGION. The types are looked up by
        name, so no SDK type is bound when the module is parsed. It is step 3 of Resolve-LambdaPSRegion,
        kept separate so tests can replace it: the SDK reads the profile store once per process.
    .PARAMETER ProfileLocation
        The credential file to read the default profile from.
    .OUTPUTS
        System.String
    .EXAMPLE
        Get-LambdaPSAWSToolsRegion -ProfileLocation ~/.aws/credentials

        Returns, for example, 'eu-central-1' when that file's default profile has that region.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ProfileLocation
    )

    $argumentsType = 'Amazon.PowerShell.Common.StandaloneRegionArguments' -as [type]
    $resolverType = 'Amazon.PowerShell.Common.IAWSRegionArgumentsMethods' -as [type]
    $sourceType = 'Amazon.PowerShell.Common.RegionSource' -as [type]
    if ($null -eq $argumentsType -or $null -eq $resolverType -or $null -eq $sourceType) { return $null }

    $regionArguments = $argumentsType::new()
    if ($PSBoundParameters.ContainsKey('ProfileLocation')) { $regionArguments.ProfileLocation = $ProfileLocation }
    $endpoint = $null
    $source = $sourceType::Unknown
    if ($resolverType::TryGetRegion($regionArguments, $false, [ref]$endpoint, [ref]$source, $ExecutionContext.SessionState)) {
        return $endpoint.SystemName
    }
    return $null
}
