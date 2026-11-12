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
    Describe 'Get-LambdaPSRuntimeAliasCompletion' -Tag 'Unit' {
        BeforeEach {
            Mock 'Get-LMCRuntimeAliasList' -MockWith { [PSCustomObject]@{ Name = 'test' }, [PSCustomObject]@{ Name = 'default' }, [PSCustomObject]@{ Name = 'prod' } }
        }

        It 'Should offer only default, without calling AWS, when -RuntimeName is not bound' {
            # Act
            $result = @(Get-LambdaPSRuntimeAliasCompletion -WordToComplete '' -FakeBoundParameters @{ Region = 'eu-west-1' })

            # Assert
            $result.CompletionText | Should -Be @('default')
            Should -Not -Invoke 'Get-LMCRuntimeAliasList'
        }

        It 'Should offer default first, then the runtime''s other aliases, with one quick attempt' {
            # Arrange
            $profileLocation = [System.IO.Path]::Combine($TestDrive, 'credentials')
            Set-Content -LiteralPath $profileLocation -Value "[dev]`naws_access_key_id = AKIDEXAMPLE`naws_secret_access_key = example"
            $bound = @{ RuntimeName = 'my-ps'; Region = 'eu-west-1'; ProfileName = 'dev'; ProfileLocation = $profileLocation }

            # Act
            $result = @(Get-LambdaPSRuntimeAliasCompletion -WordToComplete '' -FakeBoundParameters $bound)

            # Assert
            $result.CompletionText | Should -Be @('default', 'test', 'prod')
            $result[0].ResultType | Should -Be 'ParameterValue'
            Should -Invoke 'Get-LMCRuntimeAliasList' -Times 1 -Exactly -ParameterFilter {
                $RuntimeName -eq 'my-ps' -and $Region -eq 'eu-west-1' -and $ProfileName -eq 'dev' -and
                $ClientConfig.MaxErrorRetry -eq 0 -and $ClientConfig.Timeout -eq [timespan]::FromSeconds(3)
            }
        }

        It 'Should offer only the names that start with the typed text, case-sensitively' {
            # Act
            $result = @(Get-LambdaPSRuntimeAliasCompletion -WordToComplete 'p' -FakeBoundParameters @{ RuntimeName = 'my-ps'; Region = 'eu-west-1' })
            $upper = @(Get-LambdaPSRuntimeAliasCompletion -WordToComplete 'P' -FakeBoundParameters @{ RuntimeName = 'my-ps'; Region = 'eu-west-1' })
            $quoted = @(Get-LambdaPSRuntimeAliasCompletion -WordToComplete "'p'" -FakeBoundParameters @{ RuntimeName = 'my-ps'; Region = 'eu-west-1' })

            # Assert
            $result.CompletionText | Should -Be @('prod')
            $quoted.CompletionText | Should -Be @('prod')
            $upper | Should -BeNullOrEmpty
        }

        It 'Should list the aliases in the resolved region when -Region is not bound' {
            # Arrange
            Mock 'Resolve-LambdaPSRegion' -MockWith { 'eu-west-1' }

            # Act
            $null = Get-LambdaPSRuntimeAliasCompletion -WordToComplete '' -FakeBoundParameters @{ RuntimeName = 'my-ps' }

            # Assert
            Should -Invoke 'Get-LMCRuntimeAliasList' -Times 1 -Exactly -ParameterFilter { $Region -eq 'eu-west-1' }
        }

        It 'Should offer only default when listing the aliases fails' {
            # Arrange
            Mock 'Get-LMCRuntimeAliasList' -MockWith { throw 'Runtime not found' }

            # Act
            $result = @(Get-LambdaPSRuntimeAliasCompletion -WordToComplete '' -FakeBoundParameters @{ RuntimeName = 'my-ps'; Region = 'eu-west-1' })

            # Assert
            $result.CompletionText | Should -Be @('default')
        }
    }
}
