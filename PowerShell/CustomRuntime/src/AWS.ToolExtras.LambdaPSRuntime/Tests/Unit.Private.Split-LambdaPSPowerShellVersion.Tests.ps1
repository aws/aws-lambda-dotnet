# InModuleScope wraps the tests at discovery, so the module is imported then too.
BeforeDiscovery {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' {
    Describe 'Split-LambdaPSPowerShellVersion' -Tag 'Unit' {
        It 'Should return line <Line> and release <Release> for <Value>' -ForEach @(
            @{ Value = '7.6.9'; Line = '7.6'; Release = '7.6.9' }
            @{ Value = '7.6.*'; Line = '7.6'; Release = $null }
        ) {
            # Act
            $result = Split-LambdaPSPowerShellVersion -PowerShellVersion $Value

            # Assert
            $result.Line | Should -Be $Line
            $result.Release | Should -Be $Release
        }

        It 'Should return the highest mapped line of the major version for 7.*' {
            # Arrange
            $highest = @($script:Config.pwsh.DefaultPatch.Keys | Where-Object { $_ -like '7.*' } | Sort-Object { [version]$_ })[-1]

            # Act
            $result = Split-LambdaPSPowerShellVersion -PowerShellVersion '7.*'

            # Assert
            $result.Line | Should -Be $highest
            $result.Release | Should -BeNullOrEmpty
        }

        It 'Should throw UnknownPwshVersion for a major version with no mapped line' {
            # Act / Assert
            { Split-LambdaPSPowerShellVersion -PowerShellVersion '9.*' } |
                Should -Throw -ErrorId 'UnknownPwshVersion' -ExpectedMessage '*No PowerShell 9 line is supported*'
        }

        It 'Should throw PowerShellVersionNotSpecific for a bare major.minor, suggesting the wildcard and the pinned release' {
            # Arrange
            $pinned = $script:Config.pwsh.DefaultPatch['7.6']

            # Act / Assert
            { Split-LambdaPSPowerShellVersion -PowerShellVersion '7.6' } |
                Should -Throw -ErrorId 'PowerShellVersionNotSpecific' -ExpectedMessage "*Pass '7.6.*' for the newest PowerShell 7.6 release, or an exact release such as '$pinned'.*"
        }
    }
}
