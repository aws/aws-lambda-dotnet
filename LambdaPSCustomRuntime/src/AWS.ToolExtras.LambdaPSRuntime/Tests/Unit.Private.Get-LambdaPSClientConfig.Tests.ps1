# Every AWS-calling seam requests a client config from Get-LambdaPSClientConfig and passes it as
# -ClientConfig for attribution. The seam tests mock it to return a lightweight stand-in, so they do
# not depend on the SDK config types; its real behavior is tested here. The module's RequiredModules
# load the AWS.Tools assemblies that hold those types whenever it is imported.

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
        It 'returns an <Expected> carrying the module ClientAppId for -Service <Service>' -ForEach @(
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
