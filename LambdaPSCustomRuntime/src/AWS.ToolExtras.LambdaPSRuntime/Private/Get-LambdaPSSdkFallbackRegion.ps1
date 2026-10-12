function Get-LambdaPSSdkFallbackRegion {
    <#
    .SYNOPSIS
        Returns the region the SDK's fallback chain finds, or $null.
    .DESCRIPTION
        Calls Amazon.Runtime.FallbackRegionFactory: AWS_REGION, the AWS_PROFILE (or default) profile,
        and the instance or container metadata. AWS_DEFAULT_REGION is not read. The type is looked up
        by name, so no SDK type is bound when the module is parsed. It is step 4, the last, of
        Resolve-LambdaPSRegion.
    .OUTPUTS
        System.String
    .EXAMPLE
        Get-LambdaPSSdkFallbackRegion

        Returns, for example, 'us-west-2' when AWS_REGION is us-west-2.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    $fallbackType = 'Amazon.Runtime.FallbackRegionFactory' -as [type]
    if ($null -eq $fallbackType) { return $null }
    try {
        $endpoint = $fallbackType::GetRegionEndpoint()
        if ($null -ne $endpoint) { return $endpoint.SystemName }
    }
    catch {
        Write-Verbose "The SDK found no default region: $($_.Exception.Message)"
    }
    return $null
}
