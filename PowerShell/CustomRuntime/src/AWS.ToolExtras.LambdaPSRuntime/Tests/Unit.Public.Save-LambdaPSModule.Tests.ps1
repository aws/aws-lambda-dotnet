BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeModule.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Save-LambdaPSModule' -Tag 'Unit' {
    It 'is exported and returns what Save-LambdaPSMissingModule downloaded' {
        Get-Command 'Save-LambdaPSModule' -Module $script:moduleName | Should -Not -BeNullOrEmpty

        Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{
                Downloaded = @([PSCustomObject]@{ PSTypeName = 'AWS.ToolExtras.LambdaPSRuntime.DownloadedModule'; Name = 'LambdaPSFake.A'; Version = '1.0.0' })
                Pending    = @()
            }
        }

        $out = @(Save-LambdaPSModule -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' } -Path $TestDrive)
        $out.Count | Should -Be 1
        $out[0].Name | Should -Be 'LambdaPSFake.A'
        Should -Invoke 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $MissingModuleAction -eq 'Save' }
    }

    It 'passes -ModuleRepository to the download, which then skips installed copies' {
        Mock 'Get-PSResourceRepository' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = 'Internal' } }
        Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Downloaded = @(); Pending = @() } }
        $null = Save-LambdaPSModule -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' } -Path $TestDrive -ModuleRepository Internal
        Should -Invoke 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $ModuleRepository -eq 'Internal' -and $SkipInstalled }
    }

    It 'warns, naming the modules, when the download is declined' {
        Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{ Downloaded = @(); Pending = @(); Declined = @('LambdaPSFake.A 1.0.0'); Resolved = @() }
        }

        $out = @(Save-LambdaPSModule -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' } -Path $TestDrive -WarningVariable warnings -WarningAction SilentlyContinue)

        $out.Count | Should -Be 0
        @($warnings).Count | Should -Be 1
        "$($warnings[0])" | Should -BeLike '*LambdaPSFake.A 1.0.0*declined*'
    }

    It 'stops with ModuleRepositoryNotFound for an unregistered -ModuleRepository' {
        Mock 'Get-PSResourceRepository' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = 'PSGallery' } }
        Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith { throw 'must not download' }
        $target = Join-Path $TestDrive 'unregistered-repo'
        { Save-LambdaPSModule -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' } -Path $target -ModuleRepository 'no-such-repo' } |
            Should -Throw -ErrorId 'ModuleRepositoryNotFound,Save-LambdaPSModule'
        Test-Path -LiteralPath $target | Should -BeFalse -Because 'the repository is checked before anything is written'
    }

    It 'stops with ModuleVersionRequired for an unpinned -Module' {
        { Save-LambdaPSModule -Module 'LambdaPSFake.A' -Path (Join-Path $TestDrive 'unpinned') } |
            Should -Throw -ErrorId 'ModuleVersionRequired,Save-LambdaPSModule'
    }

    It 'stops with AWSPowerShellModuleNotSupported for -Module AWSPowerShell, writing nothing' {
        $target = Join-Path $TestDrive 'monolithic'
        { Save-LambdaPSModule -Module 'AWSPowerShell' -Path $target } |
            Should -Throw -ErrorId 'AWSPowerShellModuleNotSupported,Save-LambdaPSModule'
        Test-Path -LiteralPath $target | Should -BeFalse
    }

    Context 'modules the set already has installed' {
        BeforeEach {
            Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{
                    Downloaded = @(); Pending = @()
                    Resolved   = @([PSCustomObject]@{ Name = 'LambdaPSFake.Local'; Version = '1.0.0'; ModuleBase = '/installed/LambdaPSFake.Local/1.0.0'; Source = 'Local'; Key = 'LambdaPSFake.Local 1.0.0' })
                }
            }
            Mock 'Get-LambdaPSModuleClosure' -ModuleName $script:moduleName -MockWith { throw 'the download already walked the set' }
            Mock 'Copy-LambdaPSModuleFolder' -ModuleName $script:moduleName
        }

        It 'copies it into -Path from the download''s resolved set, as Copied with a [version] Version' {
            $mods = Join-Path $TestDrive 'copy-local'

            $out = @(Save-LambdaPSModule -Module @{ ModuleName = 'LambdaPSFake.Local'; RequiredVersion = '1.0.0' } -Path $mods)

            $out.Count | Should -Be 1
            $out[0].PSTypeNames | Should -Contain 'AWS.ToolExtras.LambdaPSRuntime.DownloadedModule'
            $out[0].Action | Should -Be 'Copied'
            $out[0].Version | Should -BeOfType [version]
            Should -Invoke 'Copy-LambdaPSModuleFolder' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
                $Path -eq '/installed/LambdaPSFake.Local/1.0.0' -and $Destination -eq [System.IO.Path]::Combine((Resolve-Path -LiteralPath $TestDrive).ProviderPath, 'copy-local', 'LambdaPSFake.Local', '1.0.0')
            }
        }

        It 'leaves a module that is already in -Path alone and does not return it' {
            $mods = Join-Path $TestDrive 'already-there'
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($mods, 'LambdaPSFake.Local', '1.0.0')) -Force

            $out = @(Save-LambdaPSModule -Module @{ ModuleName = 'LambdaPSFake.Local'; RequiredVersion = '1.0.0' } -Path $mods)

            $out.Count | Should -Be 0
            Should -Invoke 'Copy-LambdaPSModuleFolder' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'copies nothing and returns nothing under -WhatIf' {
            $mods = Join-Path $TestDrive 'whatif'

            $out = @(Save-LambdaPSModule -Module @{ ModuleName = 'LambdaPSFake.Local'; RequiredVersion = '1.0.0' } -Path $mods -WhatIf)

            $out.Count | Should -Be 0
            Should -Invoke 'Copy-LambdaPSModuleFolder' -ModuleName $script:moduleName -Times 0 -Exactly
        }
    }
}
