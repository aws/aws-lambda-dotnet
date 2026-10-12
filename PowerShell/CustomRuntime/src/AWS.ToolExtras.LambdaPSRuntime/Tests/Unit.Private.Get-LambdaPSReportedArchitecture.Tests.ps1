BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSReportedArchitecture' -Tag 'Unit' {
    It 'returns <Expected> for <Case>' -ForEach @(
        @{ Case = 'no Compatibility block'; Compatibility = $null; Expected = @() }
        @{ Case = 'blank entries'; Compatibility = [PSCustomObject]@{ Architectures = @('arm64', '', ' ') }; Expected = @('arm64') }
        @{ Case = 'two architectures'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64', 'arm64') }; Expected = @('x86_64', 'arm64') }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Compatibility = $Compatibility; Expected = $Expected } {
            param($Compatibility, $Expected)
            $result = Get-LambdaPSReportedArchitecture -Compatibility $Compatibility
            , $result | Should -BeOfType ([string[]])
            @($result) | Should -Be $Expected
        }
    }
}
