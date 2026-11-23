BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeModule.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Find-LambdaPSModule' -Tag 'Unit' {
    It 'returns nothing for a module that is neither installed nor in a search folder' {
        InModuleScope $script:moduleName {
            Find-LambdaPSModule -Name 'LambdaPSFake.None' -Version '1.0.0' -SearchPath $TestDrive | Should -BeNullOrEmpty
        }
    }

    It 'finds a module under a search folder and reads its RequiredModules without needing them present' {
        $root = Join-Path $TestDrive 'find'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.A' -Version '1.2.3' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.B'; RequiredVersion = '2.0.0' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $found = Find-LambdaPSModule -Name 'LambdaPSFake.A' -Version '1.2.3' -SearchPath $root
            $found.Source | Should -Be 'SearchPath'
            $found.ModuleBase | Should -Be ([System.IO.Path]::Combine($root, 'LambdaPSFake.A', '1.2.3'))
            $found.Dependencies[0].Name | Should -Be 'LambdaPSFake.B'
            $found.Dependencies[0].Version | Should -Be '2.0.0'
            $found.Dependencies[0].Kind | Should -Be 'Exact'
        }
    }

    It 'matches a search folder case-insensitively and reports the real module name' {
        $root = Join-Path $TestDrive 'find-case'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Case' -Version '1.0.0'
        $target = Join-Path $TestDrive 'find-case-dst'

        InModuleScope $script:moduleName -Parameters @{ root = $root; target = $target } {
            param($root, $target)
            (Find-LambdaPSModule -Name 'lambdapsfake.case' -Version '1.0.0' -SearchPath $root).Name | Should -BeExactly 'LambdaPSFake.Case'

            # Staged under the real casing, so <Name>/<Version>/<Name>.psd1 lines up on a case-sensitive file system.
            $staged = Copy-LambdaPSModuleSet -Module @{ ModuleName = 'lambdapsfake.case'; RequiredVersion = '1.0.0' } -Path $target -SearchPath $root
            (Get-ChildItem -LiteralPath $target -Directory).Name | Should -BeExactly 'LambdaPSFake.Case'
            (Get-LambdaPSStagedModule -Path $target).Name | Should -BeExactly 'LambdaPSFake.Case'
        }
    }

    It 'finds a module whose manifest says 1.0 when asked for 1.0, but not when asked for 1.0.0' {
        $installed = Join-Path $TestDrive 'find-two-part-installed'
        $root = Join-Path $TestDrive 'find-two-part-search'
        $null = New-FakeModule -Root $installed -Name 'LambdaPSFake.TwoPartLocal' -Version '1.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.TwoPartSearch' -Version '1.0'
        $saved = $env:PSModulePath
        try {
            $env:PSModulePath = $installed + [System.IO.Path]::PathSeparator + $env:PSModulePath
            InModuleScope $script:moduleName -Parameters @{ root = $root } {
                param($root)
                (Find-LambdaPSModule -Name 'LambdaPSFake.TwoPartLocal' -Version '1.0').Version | Should -Be '1.0'
                (Find-LambdaPSModule -Name 'LambdaPSFake.TwoPartSearch' -Version '1.0' -SearchPath $root).ModuleBase |
                    Should -Be ([System.IO.Path]::Combine($root, 'LambdaPSFake.TwoPartSearch', '1.0'))
                # PowerShell does not load a 1.0 manifest for a 1.0.0 pin, so it must not count as found.
                Find-LambdaPSModule -Name 'LambdaPSFake.TwoPartLocal' -Version '1.0.0' | Should -BeNullOrEmpty
                Find-LambdaPSModule -Name 'LambdaPSFake.TwoPartSearch' -Version '1.0.0' -SearchPath $root | Should -BeNullOrEmpty
            }
        }
        finally {
            $env:PSModulePath = $saved
        }
    }

    It 'does not find a module whose manifest says 1.0.0 when asked for 1.0' {
        $root = Join-Path $TestDrive 'find-short'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Short' -Version '1.0.0'

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            Find-LambdaPSModule -Name 'LambdaPSFake.Short' -Version '1.0' -SearchPath $root | Should -BeNullOrEmpty
            (Find-LambdaPSModule -Name 'LambdaPSFake.Short' -Version '1.0.0' -SearchPath $root).Version | Should -Be '1.0.0'
        }
    }

    It 'prefers the PSModulePath over a search folder that holds the same version' {
        $root = Join-Path $TestDrive 'search-also'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.A' -Version '1.2.3'

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            Mock 'Get-Module' -ParameterFilter { $FullyQualifiedName.Name -eq 'LambdaPSFake.A' } -MockWith {
                [PSCustomObject]@{ Name = 'LambdaPSFake.A'; ModuleBase = '/local/LambdaPSFake.A/1.2.3'; RequiredModules = @() }
            }
            $found = Find-LambdaPSModule -Name 'LambdaPSFake.A' -Version '1.2.3' -SearchPath $root
            $found.Source | Should -Be 'Local'
            $found.ModuleBase | Should -Be '/local/LambdaPSFake.A/1.2.3'
        }
    }
}
