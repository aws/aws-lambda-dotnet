function Split-LambdaPSFunctionArn {
    <#
    .SYNOPSIS
        Returns the function name, region and qualifier a -FunctionName value carries.
    .DESCRIPTION
        Lambda accepts a function as a name, a full ARN (arn:<partition>:lambda:<region>:<account>:
        function:<name>[:<qualifier>]) or a partial ARN ([<region>:][<account>:]function:<name>[:<qualifier>]).
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
    if ($FunctionName -cmatch '^(?:(?<region>[a-z]+(?:-[a-z]+)*-[0-9]+):)?(?:[0-9]{12}:)?function:(?<name>[^:]+)(?::(?<qualifier>.+))?') {
        return [PSCustomObject]@{ Name = $Matches.name; Region = $Matches.region; Qualifier = $Matches.qualifier }
    }
    # Only Lambda's own name and qualifier characters, so a malformed ARN stays a plain name.
    if ($FunctionName -cmatch '^(?<name>[a-zA-Z0-9_-]+):(?<qualifier>[a-zA-Z0-9_$-]+)\z') {
        return [PSCustomObject]@{ Name = $Matches.name; Region = $null; Qualifier = $Matches.qualifier }
    }
    [PSCustomObject]@{ Name = $FunctionName; Region = $null; Qualifier = $null }
}
