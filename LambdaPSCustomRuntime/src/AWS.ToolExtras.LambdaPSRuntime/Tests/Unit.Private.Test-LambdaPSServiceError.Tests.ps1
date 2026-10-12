BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Test-LambdaPSServiceError' -Tag 'Unit' {
    It 'matches a service exception wrapped inside an outer exception' {
        InModuleScope $script:moduleName {
            try {
                throw [System.InvalidOperationException]::new('outer',
                    [LambdaPSTests.Fakes.RepositoryNotFoundException]::new('inner'))
            }
            catch { Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'RepositoryNotFoundException' | Should -BeTrue }
        }
    }

    It 'matches on ErrorCode when the type name differs' {
        InModuleScope $script:moduleName {
            try { throw [LambdaPSTests.Fakes.ServiceException]::new('denied', 'AccessDeniedException') }
            catch { Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'AccessDeniedException' | Should -BeTrue }
        }
    }

    It 'does not match a different ErrorCode' {
        InModuleScope $script:moduleName {
            try { throw [LambdaPSTests.Fakes.ServiceException]::new('throttled', 'ThrottlingException') }
            catch { Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'AccessDeniedException' | Should -BeFalse }
        }
    }

    It 'does not match on message text alone' {
        InModuleScope $script:moduleName {
            try { throw [System.InvalidOperationException]::new('RepositoryNotFoundException: does not exist') }
            catch { Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'RepositoryNotFoundException' | Should -BeFalse }
        }
    }
}
