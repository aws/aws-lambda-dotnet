# Every AWS-calling seam requests a client config from Get-LambdaPSClientConfig and passes it as
# -ClientConfig for attribution. The seam tests mock it to return a lightweight stand-in, so they do
# not depend on the SDK config types; its real behavior is tested here.

BeforeDiscovery {
    # The real helper constructs service-specific SDK config types, which require the AWS.Tools
    # assemblies. They are not guaranteed offline, so the type-construction cases are skipped when the
    # types cannot be loaded. The app-id string case does not need them and always runs.
    $HasAwsTypes = $null -ne ('Amazon.Lambda.AmazonLambdaConfig' -as [type])
    if (-not $HasAwsTypes) {
        try {
            Import-Module AWS.Tools.Lambda, AWS.Tools.LambdaCore, AWS.Tools.SecurityToken, AWS.Tools.ECR -ErrorAction Stop
            $HasAwsTypes = $null -ne ('Amazon.Lambda.AmazonLambdaConfig' -as [type])
        }
        catch { $HasAwsTypes = $false }
    }
}

BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSClientConfig' -Tag 'Unit' {
    Context 'attribution identifier' {
        It 'derives a stable app id of the form AWS.ToolExtras.LambdaPSRuntime#<version>' {
            InModuleScope $script:moduleName {
                $script:LambdaPSClientAppId | Should -Match '^AWS\.ToolExtras\.LambdaPSRuntime#\d+\.\d+\.\d+'
                # The prefix is the stable adoption identifier and must not drift.
                $script:LambdaPSClientAppId | Should -BeLike 'AWS.ToolExtras.LambdaPSRuntime#*'
                # Stays within the SDK's recommended ClientAppId length (a warning is logged past 50).
                $script:LambdaPSClientAppId.Length | Should -BeLessOrEqual 50
            }
        }
    }

    Context 'service config construction' {
        It 'returns an <Expected> carrying the module ClientAppId for -Service <Service>' -Skip:(-not $HasAwsTypes) -ForEach @(
            @{ Service = 'Lambda'; Expected = 'Amazon.Lambda.AmazonLambdaConfig' }
            @{ Service = 'LambdaCore'; Expected = 'Amazon.LambdaCore.AmazonLambdaCoreConfig' }
            @{ Service = 'STS'; Expected = 'Amazon.SecurityToken.AmazonSecurityTokenServiceConfig' }
            @{ Service = 'ECR'; Expected = 'Amazon.ECR.AmazonECRConfig' }
        ) {
            InModuleScope $script:moduleName -Parameters @{ svc = $Service; expected = $Expected } {
                param($svc, $expected)
                $config = Get-LambdaPSClientConfig -Service $svc
                $config.GetType().FullName | Should -Be $expected
                $config.ClientAppId | Should -Be $script:LambdaPSClientAppId
            }
        }
    }
}
