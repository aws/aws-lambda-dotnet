function Test-LambdaPSEcrRepositoryName {
    <#
    .SYNOPSIS
        Tests whether a string is a valid Amazon ECR repository name.
    .DESCRIPTION
        New-LambdaPSRuntimeDeployment defaults the ECR repository to the runtime name. The runtime
        name rule allows names that ECR rejects: names where '.' touches another separator, '_' touches
        '-', or three or more '_' run together ('my..runtime'). So the deploy orchestrator uses this
        helper to decide whether the defaulted name is usable, and if not, to ask the caller for an
        explicit -RepositoryName rather than silently altering the name.

        The rule is the ECR CreateRepository name constraint: 2 to 256 characters in '/'-separated
        components of lowercase letters and digits, where letters or digits inside a component are
        separated by one '.', one '_', two '_', or any run of '-'. This helper does not throw; it
        returns $true or $false.
    .PARAMETER Name
        The candidate repository name.
    .OUTPUTS
        System.Boolean
    .EXAMPLE
        Test-LambdaPSEcrRepositoryName -Name 'my-powershell-runtime'

        Returns $true.
    .EXAMPLE
        Test-LambdaPSEcrRepositoryName -Name 'My-Runtime'

        Returns $false (uppercase is not allowed in an ECR repository name).
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [AllowNull()]
        [string]$Name
    )

    if ([string]::IsNullOrEmpty($Name)) { return $false }
    if ($Name.Length -lt 2 -or $Name.Length -gt 256) { return $false }

    # The ECR CreateRepository pattern: '/'-separated components of lowercase-alnum runs joined by
    # '.', '_', '__' or a run of '-'. \z, not $: .NET's $ also matches before a final newline.
    $component = '[a-z0-9]+(?:(?:\.|_|__|-+)[a-z0-9]+)*'
    $pattern = "^$component(?:/$component)*\z"
    return [regex]::IsMatch($Name, $pattern)
}
