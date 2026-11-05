BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSFunctionBinding' -Tag 'Unit' {
    BeforeEach {
        Mock 'Get-LambdaPSClientConfig' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ ClientAppId = 'test' } }
    }

    It 'returns the handler, runtime ARN, alias, architecture and tenant isolation mode of an existing function' {
        InModuleScope $script:moduleName {
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMFunctionConfiguration' -MockWith {
                [PSCustomObject]@{ Handler = 'handler.ps1::Invoke'; Architectures = @('arm64'); Runtime = $null; PackageType = 'Zip'
                    TenancyConfig = [PSCustomObject]@{ TenantIsolationMode = 'PER_TENANT' }
                    RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'staging' } }
            }

            $binding = Get-LambdaPSFunctionBinding -FunctionName hello -Region us-east-1

            $binding.RuntimeArn | Should -Be 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'
            $binding.RuntimeAlias | Should -Be 'staging'
            $binding.Architecture | Should -Be 'arm64'
            $binding.Handler | Should -Be 'handler.ps1::Invoke'
            $binding.PackageType | Should -Be 'Zip'
            $binding.TenantIsolationMode | Should -Be 'PER_TENANT'
            Should -Invoke 'Get-LMFunctionConfiguration' -Times 1 -Exactly -ParameterFilter { $FunctionName -eq 'hello' -and $Region -eq 'us-east-1' }
        }
    }

    It 'returns an empty runtime ARN and alias, and the runtime, for a function not bound to a customer-managed runtime' {
        InModuleScope $script:moduleName {
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMFunctionConfiguration' -MockWith {
                [PSCustomObject]@{ Handler = 'lambda_function.lambda_handler'; Architectures = @('x86_64'); Runtime = 'python3.12'; PackageType = 'Zip'; RuntimeConfig = $null }
            }

            $binding = Get-LambdaPSFunctionBinding -FunctionName hello -Region us-east-1

            $binding.RuntimeArn | Should -BeExactly ''
            $binding.RuntimeAlias | Should -BeExactly ''
            $binding.Runtime | Should -Be 'python3.12'
            $binding.Handler | Should -Be 'lambda_function.lambda_handler'
        }
    }

    It 'returns nothing for a function that does not exist' {
        InModuleScope $script:moduleName {
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMFunctionConfiguration' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Function not found') }

            Get-LambdaPSFunctionBinding -FunctionName hello -Region us-east-1 | Should -BeNullOrEmpty
        }
    }

    It 'rethrows any other error' {
        InModuleScope $script:moduleName {
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMFunctionConfiguration' -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('denied') }

            { Get-LambdaPSFunctionBinding -FunctionName hello -Region us-east-1 } | Should -Throw -ExceptionType ([LambdaPSTests.Fakes.AccessDeniedException])
        }
    }
}
