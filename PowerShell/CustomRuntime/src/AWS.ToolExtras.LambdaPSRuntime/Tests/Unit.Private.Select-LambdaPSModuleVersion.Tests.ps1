BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Select-LambdaPSModuleVersion' -Tag 'Unit' {
    It 'handles <Case>' -ForEach @(
        @{ Case = 'one exact version'; Requirement = @(@{ Kind = 'Exact'; Version = '1.2.0' }); Expected = '1.2.0' }
        @{ Case = 'the same exact version twice'; Requirement = @(@{ Kind = 'Exact'; Version = '1.2.0' }, @{ Kind = 'Exact'; Version = '1.2.0' }); Expected = '1.2.0' }
        @{ Case = 'two exact versions'; Requirement = @(@{ Kind = 'Exact'; Version = '1.2.0' }, @{ Kind = 'Exact'; Version = '1.3.0' }); Expected = $null }
        @{ Case = 'an exact version above a minimum'; Requirement = @(@{ Kind = 'Exact'; Version = '1.2.0' }, @{ Kind = 'Minimum'; Version = '1.1.0' }); Expected = '1.2.0' }
        @{ Case = 'an exact version below a minimum'; Requirement = @(@{ Kind = 'Exact'; Version = '1.0.0' }, @{ Kind = 'Minimum'; Version = '1.1.0' }); Expected = $null }
        @{ Case = 'an exact version above a maximum'; Requirement = @(@{ Kind = 'Exact'; Version = '2.0.0' }, @{ Kind = 'Minimum'; Version = '1.0.0'; MaximumVersion = '1.9.0' }); Expected = $null }
        @{ Case = 'only minimums'; Requirement = @(@{ Kind = 'Minimum'; Version = '1.10.0' }, @{ Kind = 'Minimum'; Version = '1.9.0' }); Expected = '1.10.0' }
        @{ Case = 'a minimum above another one''s maximum'; Requirement = @(@{ Kind = 'Minimum'; Version = '2.0.0' }, @{ Kind = 'Minimum'; Version = '1.0.0'; MaximumVersion = '1.9.0' }); Expected = $null }
        @{ Case = 'an exact version within a wildcard maximum'; Requirement = @(@{ Kind = 'Exact'; Version = '1.5.0' }, @{ Kind = 'Minimum'; Version = '1.0.0'; MaximumVersion = '1.*' }); Expected = '1.5.0' }
        @{ Case = 'an exact version beyond a wildcard maximum'; Requirement = @(@{ Kind = 'Exact'; Version = '2.0.0' }, @{ Kind = 'Minimum'; Version = '1.0.0'; MaximumVersion = '1.*' }); Expected = $null }
        @{ Case = 'a non-numeric exact version alone'; Requirement = @(@{ Kind = 'Exact'; Version = '1.0.0-preview' }); Expected = '1.0.0-preview' }
        @{ Case = 'a non-numeric exact version with a minimum'; Requirement = @(@{ Kind = 'Exact'; Version = '1.0.0-preview' }, @{ Kind = 'Minimum'; Version = '1.0.0' }); Expected = $null }
        @{ Case = 'a non-numeric minimum'; Requirement = @(@{ Kind = 'Exact'; Version = '1.0.0' }, @{ Kind = 'Minimum'; Version = 'latest' }); Expected = $null }
    ) {
        $requirements = @($Requirement | ForEach-Object {
                [PSCustomObject]@{ Kind = $_.Kind; Version = $_.Version; MaximumVersion = $_.MaximumVersion }
            })
        InModuleScope $script:moduleName -Parameters @{ requirements = $requirements; expected = $Expected } {
            param($requirements, $expected)
            Select-LambdaPSModuleVersion -Requirement $requirements | Should -Be $expected
        }
    }
}
