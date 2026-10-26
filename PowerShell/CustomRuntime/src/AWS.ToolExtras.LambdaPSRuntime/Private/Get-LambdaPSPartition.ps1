function Get-LambdaPSPartition {
    <#
    .SYNOPSIS
        Returns a region's AWS partition: its name (for ARNs) and DNS suffix (for host names).
    .DESCRIPTION
        ARNs are arn:<partition>:... and ECR registry hosts are <account>.dkr.ecr.<region>.<suffix>,
        and both depend on the partition: aws / amazonaws.com for the commercial regions, aws-cn /
        amazonaws.com.cn for the China regions, aws-us-gov / amazonaws.com for GovCloud, and others for
        the isolated partitions. The SDK's region table knows them; the type is looked up by name, so
        no SDK type is bound when the module is parsed. Falls back to aws / amazonaws.com when the SDK
        is not loaded.
    .PARAMETER Region
        The region system name, for example us-east-1.
    .OUTPUTS
        System.Management.Automation.PSCustomObject
    .EXAMPLE
        (Get-LambdaPSPartition -Region cn-north-1).DnsSuffix

        Returns 'amazonaws.com.cn'.
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Region
    )

    $name = 'aws'
    $suffix = 'amazonaws.com'
    $regionType = 'Amazon.RegionEndpoint' -as [type]
    if ($null -ne $regionType) {
        $endpoint = $regionType::GetBySystemName($Region)
        if (-not [string]::IsNullOrWhiteSpace($endpoint.PartitionName)) { $name = $endpoint.PartitionName }
        if (-not [string]::IsNullOrWhiteSpace($endpoint.PartitionDnsSuffix)) { $suffix = $endpoint.PartitionDnsSuffix }
    }
    [PSCustomObject]@{ Name = $name; DnsSuffix = $suffix }
}
