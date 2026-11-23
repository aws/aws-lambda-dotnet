BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSPartition' -Tag 'Unit' {
    It 'returns <Name> / <Suffix> for <Region>' -ForEach @(
        @{ Region = 'us-east-1'; Name = 'aws'; Suffix = 'amazonaws.com' }
        @{ Region = 'cn-north-1'; Name = 'aws-cn'; Suffix = 'amazonaws.com.cn' }
        @{ Region = 'us-gov-west-1'; Name = 'aws-us-gov'; Suffix = 'amazonaws.com' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Region = $Region; Name = $Name; Suffix = $Suffix } {
            param($Region, $Name, $Suffix)
            $partition = Get-LambdaPSPartition -Region $Region
            $partition.Name | Should -Be $Name
            $partition.DnsSuffix | Should -Be $Suffix
        }
    }
}
