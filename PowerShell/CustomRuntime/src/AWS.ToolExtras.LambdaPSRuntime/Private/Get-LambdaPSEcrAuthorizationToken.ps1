function Get-LambdaPSEcrAuthorizationToken {
    <#
    .SYNOPSIS
        Returns an ECR authorization token for the no-Docker push.
    .DESCRIPTION
        The no-Docker build's replacement for 'docker login': AWS.OCI.Builder has no AWS SDK dependency and
        takes the token the caller gets. It is read with Get-ECRAuthorizationToken and the caller's
        credential parameters, so -ProfileName, -ProfileLocation and -Credential reach the push too (a
        docker credential helper does not see them). The token is valid for 12 hours.
    .PARAMETER Region
        The region of the ECR registry pushed to.
    .PARAMETER CredentialParameter
        The caller's credential parameters (Get-LambdaPSCredentialParameter), splatted onto the cmdlet.
    .OUTPUTS
        System.String

        The base64 'AWS:<password>' token.
    .EXAMPLE
        Get-LambdaPSEcrAuthorizationToken -Region eu-west-1

        Returns a token for the account's eu-west-1 registry.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Region,

        [Parameter()]
        [ValidateNotNull()]
        [hashtable]$CredentialParameter = @{}
    )

    $clientConfig = Get-LambdaPSClientConfig -Service ECR
    $authorization = Get-ECRAuthorizationToken -Region $Region -ClientConfig $clientConfig @CredentialParameter -ErrorAction Stop
    [string]@($authorization)[0].AuthorizationToken
}
