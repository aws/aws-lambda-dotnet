BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSAWSPowerShellModuleReason' -Tag 'Unit' {
    BeforeAll {
        $script:desktop = '*AWSPowerShell is the Windows PowerShell (Desktop edition) module and does not run in PowerShell 7 on Lambda*'
        $script:netCore = "*AWSPowerShell.NetCore holds every AWS service (over 600 MB installed): more than Lambda's 250 MB limit for a .zip package and its layers, and in a container image, importing it takes over 10 seconds and over 1 GB of memory on every cold start*"
    }

    It 'returns nothing when no name is AWSPowerShell or AWSPowerShell.NetCore' {
        InModuleScope $script:moduleName {
            Get-LambdaPSAWSPowerShellModuleReason -Name 'AWS.Tools.S3', 'AWSPowerShell.Extensions' | Should -BeNullOrEmpty
            Get-LambdaPSAWSPowerShellModuleReason -Name @() | Should -BeNullOrEmpty
        }
    }

    It 'gives <Name> its own reason, and not the other module''s' -ForEach @(
        @{ Name = 'AWSPowerShell'; Own = 'desktop'; Other = 'netCore' }
        @{ Name = 'AWSPowerShell.NetCore'; Own = 'netCore'; Other = 'desktop' }
        @{ Name = 'awspowershell.netcore'; Own = 'netCore'; Other = 'desktop' }
    ) {
        $own = Get-Variable -Name $Own -Scope Script -ValueOnly
        $other = Get-Variable -Name $Other -Scope Script -ValueOnly
        InModuleScope $script:moduleName -Parameters @{ Name = $Name; Own = $own; Other = $other } {
            param($Name, $Own, $Other)
            $result = Get-LambdaPSAWSPowerShellModuleReason -Name 'AWS.Tools.S3', $Name
            @($result.Names) | Should -Be @($Name)
            $result.Reason | Should -BeLike $Own
            $result.Reason | Should -Not -BeLike $Other
        }
    }

    It 'lists a name given twice once, as first spelled, and gives both reasons for both modules' {
        InModuleScope $script:moduleName -Parameters @{ Both = "$script:desktop; $script:netCore" } {
            param($Both)
            $result = Get-LambdaPSAWSPowerShellModuleReason -Name 'AWSPowerShell.NetCore', 'awspowershell.netcore', 'AWSPowerShell'
            @($result.Names) | Should -Be @('AWSPowerShell.NetCore', 'AWSPowerShell')
            $result.Reason | Should -BeLike $Both
        }
    }
}
