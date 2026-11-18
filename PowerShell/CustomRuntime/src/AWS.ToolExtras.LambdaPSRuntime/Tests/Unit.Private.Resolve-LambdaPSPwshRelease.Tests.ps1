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
    Describe 'Resolve-LambdaPSPwshRelease' -Tag 'Unit' {
        BeforeAll {
            # The result, and the warnings it wrote as text.
            $script:invokeResolve = {
                param([hashtable]$Splat)
                $output = @(Resolve-LambdaPSPwshRelease @Splat 3>&1)
                [PSCustomObject]@{
                    Result   = $output | Where-Object { $_ -isnot [System.Management.Automation.WarningRecord] }
                    Warnings = @($output | Where-Object { $_ -is [System.Management.Automation.WarningRecord] } | ForEach-Object { "$_" })
                }
            }
            $script:default76 = $script:Config.pwsh.DefaultPatch['7.6']
        }

        BeforeEach {
            Mock 'Get-LambdaPSPwshRepositoryRelease' -MockWith { '7.6.8', '7.6.9', '7.6.30' }
        }

        Context '-PwshVersion given' {
            It 'returns it without reading the repository for PinnedArchive' {
                $run = & $script:invokeResolve @{ PowerShellVersion = '7.6'; PwshVersion = '7.6.1'; InstallSource = 'PinnedArchive'; Architecture = 'arm64' }

                $run.Result.Version | Should -Be '7.6.1'
                $run.Result.Source | Should -Be 'Parameter'
                Should -Invoke 'Get-LambdaPSPwshRepositoryRelease' -Times 0 -Exactly
            }

            It 'returns it for PackageRepository when <Case>' -ForEach @(
                @{ Case = 'the repository lists it'; Listed = { '7.6.8', '7.6.9' } }
                @{ Case = 'the repository cannot be read'; Listed = { throw 'offline' } }
            ) {
                Mock 'Get-LambdaPSPwshRepositoryRelease' -MockWith $Listed

                $run = & $script:invokeResolve @{ PowerShellVersion = '7.6'; PwshVersion = '7.6.8'; InstallSource = 'PackageRepository'; Architecture = 'x86_64' }

                $run.Result.Version | Should -Be '7.6.8'
                Should -Invoke 'Get-LambdaPSPwshRepositoryRelease' -Times 1 -Exactly
            }

            It 'throws PwshVersionNotFound for PackageRepository when the repository does not list it' {
                { Resolve-LambdaPSPwshRelease -PowerShellVersion 7.6 -PwshVersion 7.6.99 -InstallSource PackageRepository -Architecture x86_64 } |
                    Should -Throw -ErrorId 'PwshVersionNotFound' -ExpectedMessage '*newest PowerShell 7.6 release is 7.6.30*'
            }
        }

        Context 'PackageRepository' {
            BeforeAll {
                $script:splat = @{ PowerShellVersion = '7.6'; InstallSource = 'PackageRepository'; Architecture = 'x86_64' }
            }

            It 'returns the newest release of the line in the repository' {
                $run = & $script:invokeResolve $script:splat
                $run.Result.Version | Should -Be '7.6.30'
                $run.Result.Source | Should -Be 'PackageRepository'
                $run.Warnings | Should -BeNullOrEmpty
                Should -Invoke 'Get-LambdaPSPwshRepositoryRelease' -Times 1 -Exactly -ParameterFilter { $PowerShellVersion -eq '7.6' }
            }

            It 'returns the pinned default, with a warning, when <Case>' -ForEach @(
                @{ Case = 'the lookup fails'; Releases = { throw 'unreachable' }; Warning = '*Could not find*unreachable*' }
                @{ Case = 'the line has no release there'; Releases = { }; Warning = '*lists no PowerShell 7.6 release*' }
                @{ Case = 'its newest is older than the pin'; Releases = { '7.6.0' }; Warning = '*only up to 7.6.0*' }
            ) {
                Mock 'Get-LambdaPSPwshRepositoryRelease' -MockWith $Releases
                $run = & $script:invokeResolve $script:splat
                $run.Result.Version | Should -Be $script:default76
                $run.Result.Source | Should -Be 'Pinned'
                $run.Warnings | Should -BeLike $Warning
            }

            It 'throws UnknownPwshVersion for an unsupported line before reading the repository' {
                { & $script:invokeResolve @{ PowerShellVersion = '7.9'; InstallSource = 'PackageRepository'; Architecture = 'x86_64' } } |
                    Should -Throw -ErrorId 'UnknownPwshVersion'
                Should -Invoke 'Get-LambdaPSPwshRepositoryRelease' -Times 0 -Exactly
            }
        }

        Context 'PinnedArchive' {
            BeforeEach {
                Mock 'Get-LambdaPSPwshArchiveHash' -ParameterFilter { $PwshVersion -in '7.6.6', '7.6.7' -and $Architecture -eq 'arm64' } -MockWith { 'ab' * 32 }
                Mock 'Get-LambdaPSPwshArchiveHash' -ParameterFilter { $PwshVersion -notin '7.6.6', '7.6.7' -or $Architecture -ne 'arm64' } -MockWith { $null }
            }

            It 'returns the newest release with a pinned hash for the architecture, and warns that a newer one exists' {
                $run = & $script:invokeResolve @{ PowerShellVersion = '7.6'; InstallSource = 'PinnedArchive'; Architecture = 'arm64' }
                $run.Result.Version | Should -Be '7.6.7'
                $run.Result.Source | Should -Be 'Pinned'
                $run.Warnings | Should -BeLike '*pwsh 7.6.30 is the newest PowerShell 7.6 release*newest pinned is 7.6.7*'
            }

            It 'does not warn when the repository has nothing newer, or cannot be read' -ForEach @(
                @{ Releases = { '7.6.7' } }, @{ Releases = { throw 'unreachable' } }
            ) {
                Mock 'Get-LambdaPSPwshRepositoryRelease' -MockWith $Releases
                $run = & $script:invokeResolve @{ PowerShellVersion = '7.6'; InstallSource = 'PinnedArchive'; Architecture = 'arm64' }
                $run.Result.Version | Should -Be '7.6.7'
                $run.Warnings | Should -BeNullOrEmpty
            }

            It 'returns the pinned default when no release of the line has a hash for the architecture' {
                $run = & $script:invokeResolve @{ PowerShellVersion = '7.6'; InstallSource = 'PinnedArchive'; Architecture = 'x86_64' }
                $run.Result.Version | Should -Be $script:default76
            }
        }
    }
}
