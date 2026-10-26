function Get-LambdaPSClientConfig {
    <#
    .SYNOPSIS
        Builds a service-specific AWS client config carrying the module's attribution ClientAppId.
    .DESCRIPTION
        Every seam that calls an AWS.Tools cmdlet passes the result of this helper as -ClientConfig so
        the call emits the module's 'app/<id>' user-agent token (see $script:LambdaPSClientAppId). The
        -ClientConfig parameter is strongly typed per service, so this returns the correct
        config type for the requested service.

        Only ClientAppId is set. Region, credentials, and profile are intentionally NOT set here: the
        seams pass -Region (and any credential parameters) as usual, and AWS.Tools applies those on
        top of this config (an explicit -Region wins over the config, and credentials are passed to
        the client separately), so attribution composes with normal parameter binding without
        overriding it.
    .PARAMETER Service
        The AWS service whose config type to construct: 'Lambda' (functions), 'LambdaCore' (runtimes,
        runtime versions and runtime aliases), 'STS', or 'ECR'.
    .OUTPUTS
        Amazon.Runtime.ClientConfig
    .EXAMPLE
        $config = Get-LambdaPSClientConfig -Service Lambda
        Invoke-LMFunction -FunctionName x -Region us-east-1 -ClientConfig $config

        Invokes the function with the module's attribution token on the user-agent.
    #>
    # OutputType is documented in .OUTPUTS (Amazon.Runtime.ClientConfig) rather than as an attribute:
    # the SDK type is not loadable until an AWS.Tools module is imported, and an attribute referencing
    # it would force resolution during command introspection (e.g. when mocking) even offline.
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Lambda', 'LambdaCore', 'STS', 'ECR')]
        [string]$Service
    )

    $config = switch ($Service) {
        'Lambda' { [Amazon.Lambda.AmazonLambdaConfig]::new() }
        'LambdaCore' { [Amazon.LambdaCore.AmazonLambdaCoreConfig]::new() }
        'STS' { [Amazon.SecurityToken.AmazonSecurityTokenServiceConfig]::new() }
        'ECR' { [Amazon.ECR.AmazonECRConfig]::new() }
    }

    $config.ClientAppId = $script:LambdaPSClientAppId
    return $config
}
