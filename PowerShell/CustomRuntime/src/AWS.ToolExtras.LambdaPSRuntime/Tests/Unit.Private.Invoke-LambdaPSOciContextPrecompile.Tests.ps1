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
    Describe 'Invoke-LambdaPSOciContextPrecompile' -Tag 'Unit' {
        BeforeAll {
            $script:hostArch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x86_64' }
            $script:otherArch = if ($script:hostArch -eq 'arm64') { 'x86_64' } else { 'arm64' }
            $script:hostPwsh = [System.IO.Path]::Combine($TestDrive, 'host-powershell', 'pwsh')
            $script:precompile = @{
                BundledPwshPath = [System.IO.Path]::Combine($TestDrive, 'powershell', 'pwsh')
                PwshVersion     = '7.6.9'
                SourcePath      = [System.IO.Path]::Combine($TestDrive, 'PowerShellLambdaContext.cs')
                OutputPath      = [System.IO.Path]::Combine($TestDrive, 'PowerShellLambdaContext.dll')
                StagePath       = $TestDrive
            }
            $script:once = @{ Times = 1; Exactly = $true }
        }

        BeforeEach {
            Mock 'Invoke-LambdaPSContextPrecompile' -MockWith { $true }
        }

        It 'Should compile with the bundled pwsh when the host has the image architecture' -Skip:(-not $IsLinux) {
            # Arrange
            Mock 'Get-LambdaPSHostPwsh' -MockWith { }

            # Act
            $result = Invoke-LambdaPSOciContextPrecompile @precompile -Architecture $hostArch

            # Assert
            $result | Should -BeTrue
            Should -Invoke 'Invoke-LambdaPSContextPrecompile' @once -ParameterFilter { $PwshPath -eq $precompile.BundledPwshPath }
            Should -Not -Invoke 'Get-LambdaPSHostPwsh'
        }

        It 'Should compile with the host build of the bundled release when the image has another architecture' -Skip:(-not $IsLinux) {
            # Arrange
            Mock 'Get-LambdaPSHostPwsh' -ParameterFilter { $PwshVersion -eq '7.6.9' -and $Architecture -eq $hostArch } -MockWith { $hostPwsh }

            # Act
            $result = Invoke-LambdaPSOciContextPrecompile @precompile -Architecture $otherArch

            # Assert
            $result | Should -BeTrue
            Should -Invoke 'Get-LambdaPSHostPwsh' @once -ParameterFilter { $PwshVersion -eq '7.6.9' -and $Architecture -eq $hostArch }
            Should -Invoke 'Invoke-LambdaPSContextPrecompile' @once -ParameterFilter { $PwshPath -eq $hostPwsh }
        }

        It 'Should return $false, leaving the compile to cold start, when the host build cannot be fetched' -Skip:(-not $IsLinux) {
            # Arrange
            Mock 'Get-LambdaPSHostPwsh' -MockWith { $null }

            # Act
            $result = Invoke-LambdaPSOciContextPrecompile @precompile -Architecture $otherArch

            # Assert
            $result | Should -BeFalse
            Should -Invoke 'Get-LambdaPSHostPwsh' @once
            Should -Not -Invoke 'Invoke-LambdaPSContextPrecompile'
        }
    }
}
