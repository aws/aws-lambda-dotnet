function Split-LambdaPSFunctionArn {
    <#
    .SYNOPSIS
        Returns the function name and region a -FunctionName value carries.
    .DESCRIPTION
        Lambda accepts a function as a name, a full ARN (arn:<partition>:lambda:<region>:<account>:
        function:<name>[:<qualifier>]) or a partial ARN (<account>:function:<name>[:<qualifier>]).
        The function cmdlets need the bare name (for a default package file name, which must not
        contain ':') and, from a full ARN, the region, which they use when -Region is omitted. A plain
        name is returned as it is, with no region. The account is matched as [0-9]{12}, so non-ASCII
        digits do not make a value look like an ARN.
    .PARAMETER FunctionName
        The function name, full ARN or partial ARN.
    .OUTPUTS
        System.Management.Automation.PSCustomObject (Name, Region; Region is $null unless a full ARN
        names it)
    .EXAMPLE
        Split-LambdaPSFunctionArn -FunctionName arn:aws:lambda:eu-west-1:111122223333:function:hello

        Returns Name 'hello' and Region 'eu-west-1'.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$FunctionName
    )

    if ($FunctionName -cmatch '^arn:aws[a-z-]*:lambda:(?<region>[a-z0-9-]+):[0-9]{12}:function:(?<name>[^:]+)') {
        return [PSCustomObject]@{ Name = $Matches.name; Region = $Matches.region }
    }
    if ($FunctionName -cmatch '^[0-9]{12}:function:(?<name>[^:]+)') {
        return [PSCustomObject]@{ Name = $Matches.name; Region = $null }
    }
    [PSCustomObject]@{ Name = $FunctionName; Region = $null }
}
