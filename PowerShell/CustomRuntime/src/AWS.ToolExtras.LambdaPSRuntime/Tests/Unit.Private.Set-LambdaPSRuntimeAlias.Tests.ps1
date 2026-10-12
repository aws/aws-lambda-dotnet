BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Set-LambdaPSRuntimeAlias' -Tag 'Unit' {
    BeforeEach {
        Mock 'Get-LambdaPSClientConfig' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{ ClientAppId = 'AWS.ToolExtras.LambdaPSRuntime#test' }
        }
    }

    It 'creates the alias with -RuntimeName/-Name/-RuntimeVersion' {
        InModuleScope $script:moduleName {
            function New-LMCRuntimeAlias { param($RuntimeName, $Name, $RuntimeVersion, $Region, $ClientConfig, $ErrorAction) }
            # The alias is looked up before it is created; by default it does not exist yet.
            function Get-LMCRuntimeAlias { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntimeAlias' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Alias does not exist') }
            Mock 'New-LMCRuntimeAlias' -MockWith { }

            $splat = @{
                RuntimeName = 'my-ps'; RuntimeAlias = 'production'
                RuntimeVersion = 'v1'; Region = 'us-east-1'
            }
            Set-LambdaPSRuntimeAlias @splat | Should -BeNullOrEmpty
            Should -Invoke 'New-LMCRuntimeAlias' -Times 1 -Exactly `
                -ParameterFilter { $RuntimeName -eq 'my-ps' -and $Name -eq 'production' -and $RuntimeVersion -eq 'v1' }
        }
    }

    It 'repoints an existing alias via Update-LMCRuntimeAlias when New throws already-exists' {
        InModuleScope $script:moduleName {
            function New-LMCRuntimeAlias { param($RuntimeName, $Name, $RuntimeVersion, $Region, $ClientConfig, $ErrorAction) }
            # The alias is looked up before it is created; by default it does not exist yet.
            function Get-LMCRuntimeAlias { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntimeAlias' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Alias does not exist') }
            function Update-LMCRuntimeAlias { param($RuntimeName, $Name, $RuntimeVersion, $Region, $ClientConfig) }
            Mock 'New-LMCRuntimeAlias' -MockWith {
                throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Alias already exists')
            }
            Mock 'Update-LMCRuntimeAlias' -MockWith { }

            $splat = @{
                RuntimeName = 'my-ps'; RuntimeAlias = 'production'
                RuntimeVersion = 'v2'; Region = 'us-east-1'
            }
            Set-LambdaPSRuntimeAlias @splat | Should -BeNullOrEmpty
            Should -Invoke 'Update-LMCRuntimeAlias' -Times 1 -Exactly `
                -ParameterFilter { $RuntimeName -eq 'my-ps' -and $RuntimeVersion -eq 'v2' }
        }
    }

    It 'repoints an alias found by Get-LMCRuntimeAlias without calling New-LMCRuntimeAlias' {
        InModuleScope $script:moduleName {
            function New-LMCRuntimeAlias { param($RuntimeName, $Name, $RuntimeVersion, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeAlias { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMCRuntimeAlias { param($RuntimeName, $Name, $RuntimeVersion, $Region, $ClientConfig) }
            Mock 'Get-LMCRuntimeAlias' -MockWith { [PSCustomObject]@{ Name = 'production'; RuntimeVersion = 'v1' } }
            Mock 'New-LMCRuntimeAlias' -MockWith { throw [System.InvalidOperationException]::new('must not be called') }
            Mock 'Update-LMCRuntimeAlias' -MockWith { }

            $splat = @{ RuntimeName = 'my-ps'; RuntimeAlias = 'production'; RuntimeVersion = 'v2'; Region = 'us-east-1' }
            Set-LambdaPSRuntimeAlias @splat | Should -BeNullOrEmpty
            Should -Invoke 'New-LMCRuntimeAlias' -Times 0 -Exactly
            Should -Invoke 'Update-LMCRuntimeAlias' -Times 1 -Exactly -ParameterFilter { $RuntimeVersion -eq 'v2' }
        }
    }

    It 'rethrows a Get-LMCRuntimeAlias error other than not-found instead of creating' {
        InModuleScope $script:moduleName {
            function New-LMCRuntimeAlias { param($RuntimeName, $Name, $RuntimeVersion, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeAlias { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntimeAlias' -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('not authorized to perform lambda:GetRuntimeAlias') }
            Mock 'New-LMCRuntimeAlias' -MockWith { }

            $splat = @{ RuntimeName = 'my-ps'; RuntimeAlias = 'production'; RuntimeVersion = 'v1'; Region = 'us-east-1' }
            { Set-LambdaPSRuntimeAlias @splat } | Should -Throw -ExceptionType ([LambdaPSTests.Fakes.AccessDeniedException])
            Should -Invoke 'New-LMCRuntimeAlias' -Times 0 -Exactly
        }
    }

    It 'rethrows a New-LMCRuntimeAlias error other than a conflict instead of repointing' {
        InModuleScope $script:moduleName {
            function New-LMCRuntimeAlias { param($RuntimeName, $Name, $RuntimeVersion, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeAlias { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMCRuntimeAlias { param($RuntimeName, $Name, $RuntimeVersion, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntimeAlias' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Alias does not exist') }
            Mock 'New-LMCRuntimeAlias' -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('not authorized to perform lambda:CreateRuntimeAlias') }
            Mock 'Update-LMCRuntimeAlias' -MockWith { }

            $splat = @{ RuntimeName = 'my-ps'; RuntimeAlias = 'production'; RuntimeVersion = 'v1'; Region = 'us-east-1' }
            { Set-LambdaPSRuntimeAlias @splat } | Should -Throw -ExceptionType ([LambdaPSTests.Fakes.AccessDeniedException])
            Should -Invoke 'Update-LMCRuntimeAlias' -Times 0 -Exactly
        }
    }

    It 'passes the caller''s credential parameters to every alias call' {
        InModuleScope $script:moduleName {
            function New-LMCRuntimeAlias { param($RuntimeName, $Name, $RuntimeVersion, $Region, $ClientConfig, $ErrorAction, $ProfileName) }
            function Get-LMCRuntimeAlias { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction, $ProfileName) }
            function Update-LMCRuntimeAlias { param($RuntimeName, $Name, $RuntimeVersion, $Region, $ClientConfig, $ErrorAction, $ProfileName) }
            Mock 'Get-LMCRuntimeAlias' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Alias does not exist') }
            Mock 'New-LMCRuntimeAlias' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Alias already exists') }
            Mock 'Update-LMCRuntimeAlias' -MockWith { }

            $splat = @{ RuntimeName = 'my-ps'; RuntimeAlias = 'production'; RuntimeVersion = 'v1'; Region = 'us-east-1' }
            Set-LambdaPSRuntimeAlias @splat -CredentialParameter @{ ProfileName = 'p' }
            Should -Invoke 'Get-LMCRuntimeAlias' -Times 1 -Exactly -ParameterFilter { $ProfileName -eq 'p' }
            Should -Invoke 'New-LMCRuntimeAlias' -Times 1 -Exactly -ParameterFilter { $ProfileName -eq 'p' }
            Should -Invoke 'Update-LMCRuntimeAlias' -Times 1 -Exactly -ParameterFilter { $ProfileName -eq 'p' }
        }
    }

    It 'calls no alias cmdlet under -WhatIf' {
        InModuleScope $script:moduleName {
            function New-LMCRuntimeAlias { param($RuntimeName, $Name, $RuntimeVersion, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeAlias { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMCRuntimeAlias { param($RuntimeName, $Name, $RuntimeVersion, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntimeAlias' -MockWith { }
            Mock 'New-LMCRuntimeAlias' -MockWith { }
            Mock 'Update-LMCRuntimeAlias' -MockWith { }

            $splat = @{ RuntimeName = 'my-ps'; RuntimeAlias = 'production'; RuntimeVersion = 'v1'; Region = 'us-east-1' }
            Set-LambdaPSRuntimeAlias @splat -WhatIf
            Should -Invoke 'Get-LMCRuntimeAlias' -Times 0 -Exactly
            Should -Invoke 'New-LMCRuntimeAlias' -Times 0 -Exactly
            Should -Invoke 'Update-LMCRuntimeAlias' -Times 0 -Exactly
        }
    }

    It 'rejects a whitespace alias at parameter binding' {
        InModuleScope $script:moduleName {
            $splat = @{
                RuntimeName = 'my-ps'; RuntimeAlias = '   '
                RuntimeVersion = 'v1'; Region = 'us-east-1'
            }
            { Set-LambdaPSRuntimeAlias @splat } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Set-LambdaPSRuntimeAlias'
        }
    }
}
