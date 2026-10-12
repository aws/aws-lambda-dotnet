function Test-LambdaPSEcrRepositoryName {
    <#
    .SYNOPSIS
        Tests whether a string is a valid Amazon ECR repository name.
    .DESCRIPTION
        New-LambdaPSRuntimeDeployment defaults the ECR repository to the runtime name. The runtime
        name rule allows names that ECR rejects: names with consecutive separators ('my..runtime').
        So the deploy orchestrator uses this helper to decide whether the defaulted name is usable,
        and if not, to ask the caller for an explicit -RepositoryName rather than silently altering
        the name.

        The rule mirrors the ECR repository-name constraints: 2 to 256 characters, lowercase letters,
        digits, and the separators '.', '_', '-', and '/', where each name component starts and ends
        with a letter or digit. This helper does not throw; it returns $true or $false.
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

    # A slash-delimited path of components, each lowercase-alnum with optional single '.', '_', '-'
    # separators between alnum runs. Matches the ECR CreateRepository name constraint. \z, not $:
    # .NET's $ also matches before a final newline.
    $pattern = '^(?:[a-z0-9]+(?:[._-][a-z0-9]+)*/)*[a-z0-9]+(?:[._-][a-z0-9]+)*\z'
    return [regex]::IsMatch($Name, $pattern)
}
