function Split-LambdaPSFunctionArn {
    <#
    .SYNOPSIS
        Returns the function name, region and qualifier a -FunctionName value carries.
    .DESCRIPTION
        Lambda accepts a function as a name, a full ARN (arn:<partition>:lambda:<region>:<account>:
        function:<name>[:<qualifier>]) or a partial ARN ([<region>:][<account>:][function:]<name>[:<qualifier>]).
        The function cmdlets need the bare name (for a default package file name, which must not
        contain ':') and, from a full or partial ARN, the region, which they use when -Region is omitted. A plain
        name (no ':') is returned as it is, with no region. The account is matched as [0-9]{12}, so non-ASCII
        digits do not make a value look like an ARN. A region in capitals is still read, as it is.
    .PARAMETER FunctionName
        The function name, full ARN or partial ARN.
    .OUTPUTS
        System.Management.Automation.PSCustomObject (Name, Region, Qualifier; Region is $null unless an
        ARN names it, Qualifier is $null unless a version or alias follows the name)
    .EXAMPLE
        Split-LambdaPSFunctionArn -FunctionName arn:aws:lambda:us-east-1:111122223333:function:hello

        Returns Name 'hello' and Region 'us-east-1'.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$FunctionName
    )

    if ($FunctionName -cmatch '^arn:aws[a-z-]*:lambda:(?<region>[a-zA-Z0-9-]+):[0-9]{12}:function:(?<name>[^:]+)(?::(?<qualifier>.+))?') {
        return [PSCustomObject]@{ Name = $Matches.name; Region = $Matches.region; Qualifier = $Matches.qualifier }
    }
    # Lambda's FunctionName pattern, so a malformed ARN stays a plain name.
    if ($FunctionName -cmatch '^(?:(?<region>(?:eusc-)?[a-z]{2}(?:-gov|-iso[a-z]?)?-[a-z]+-[0-9]):)?(?:[0-9]{12}:)?(?:function:)?(?<name>[a-zA-Z0-9_-]+)(?::(?<qualifier>[a-zA-Z0-9_$-]+))?\z') {
        $parts = [PSCustomObject]@{ Name = $Matches.name; Region = $Matches.region; Qualifier = $Matches.qualifier }
        # Like Lambda, read <region>:<name> as a name and qualifier when the region is not a known one (my-func-2:live).
        $regionType = 'Amazon.RegionEndpoint' -as [type]
        if ($regionType -and $FunctionName -ceq "$($parts.Region):$($parts.Name)" -and $parts.Region -notin $regionType::EnumerableAllRegions.SystemName) {
            $parts = [PSCustomObject]@{ Name = $parts.Region; Region = $null; Qualifier = $parts.Name }
        }
        return $parts
    }
    [PSCustomObject]@{ Name = $FunctionName; Region = $null; Qualifier = $null }
}
