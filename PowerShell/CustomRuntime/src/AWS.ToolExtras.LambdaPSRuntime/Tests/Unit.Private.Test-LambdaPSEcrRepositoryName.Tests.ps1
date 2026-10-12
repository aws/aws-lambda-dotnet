BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Test-LambdaPSEcrRepositoryName' -Tag 'Unit' {
    It 'accepts a valid lowercase name' {
        InModuleScope $script:moduleName {
            Test-LambdaPSEcrRepositoryName -Name 'my-powershell-runtime' | Should -BeTrue
        }
    }

    It 'accepts a namespaced (slash-delimited) name' {
        InModuleScope $script:moduleName {
            Test-LambdaPSEcrRepositoryName -Name 'team/powershell.runtime_v1' | Should -BeTrue
        }
    }

    It 'rejects uppercase characters' {
        InModuleScope $script:moduleName {
            Test-LambdaPSEcrRepositoryName -Name 'My-Runtime' | Should -BeFalse
        }
    }

    It 'rejects a name shorter than two characters' {
        InModuleScope $script:moduleName {
            Test-LambdaPSEcrRepositoryName -Name 'a' | Should -BeFalse
        }
    }

    It 'rejects an empty or null name' {
        InModuleScope $script:moduleName {
            Test-LambdaPSEcrRepositoryName -Name '' | Should -BeFalse
            Test-LambdaPSEcrRepositoryName -Name $null | Should -BeFalse
        }
    }

    It 'rejects a name with a leading separator' {
        InModuleScope $script:moduleName {
            Test-LambdaPSEcrRepositoryName -Name '-runtime' | Should -BeFalse
        }
    }

    It 'returns <Expected> for <Case>' -ForEach @(
        @{ Case = 'two characters'; Name = 'ab'; Expected = $true }
        @{ Case = '256 characters'; Name = 'a' * 256; Expected = $true }
        @{ Case = '257 characters'; Name = 'a' * 257; Expected = $false }
        @{ Case = 'a trailing newline'; Name = "my-runtime`n"; Expected = $false }
        @{ Case = 'a trailing slash'; Name = 'team/'; Expected = $false }
        @{ Case = 'an empty path component'; Name = 'team//runtime'; Expected = $false }
        @{ Case = 'a doubled separator'; Name = 'my--runtime'; Expected = $false }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Name = $Name; Expected = $Expected } {
            param($Name, $Expected)
            Test-LambdaPSEcrRepositoryName -Name $Name | Should -Be $Expected
        }
    }
}
