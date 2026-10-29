BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSModuleDependency' -Tag 'Unit' {
    It 'reads <Case> from the manifest as <Kind>' -ForEach @(
        @{ Case = 'a bare name'; Entry = "'LambdaPSFake.Dep'"; Kind = 'Minimum'; Version = ''; Maximum = $null }
        @{ Case = 'a RequiredVersion'; Entry = "@{ ModuleName = 'LambdaPSFake.Dep'; RequiredVersion = '1.0' }"; Kind = 'Exact'; Version = '1.0'; Maximum = $null }
        @{ Case = 'a ModuleVersion'; Entry = "@{ ModuleName = 'LambdaPSFake.Dep'; ModuleVersion = '1.0.0' }"; Kind = 'Minimum'; Version = '1.0.0'; Maximum = $null }
        @{ Case = 'a ModuleVersion and a MaximumVersion'; Entry = "@{ ModuleName = 'LambdaPSFake.Dep'; ModuleVersion = '1.0.0'; MaximumVersion = '1.9.0' }"; Kind = 'Minimum'; Version = '1.0.0'; Maximum = '1.9.0' }
        @{ Case = 'only a MaximumVersion'; Entry = "@{ ModuleName = 'LambdaPSFake.Dep'; MaximumVersion = '1.9.0' }"; Kind = 'Minimum'; Version = ''; Maximum = '1.9.0' }
    ) {
        $manifest = Join-Path $TestDrive "dependency-$([guid]::NewGuid().ToString('N')).psd1"
        Set-Content -LiteralPath $manifest -Value "@{ ModuleVersion = '1.0.0'; RequiredModules = @($Entry) }"

        InModuleScope $script:moduleName -Parameters @{ Manifest = $manifest; Kind = $Kind; Version = $Version; Maximum = $Maximum } {
            param($Manifest, $Kind, $Version, $Maximum)
            $entries = @(Get-LambdaPSModuleDependency -Module ([PSCustomObject]@{ Path = $Manifest; RequiredModules = @() }))
            $entries.Count | Should -Be 1
            $entries[0].Name | Should -Be 'LambdaPSFake.Dep'
            $entries[0].Kind | Should -Be $Kind
            $entries[0].Version | Should -Be $Version
            $entries[0].MaximumVersion | Should -Be $Maximum
        }
    }

    It 'treats every dependency as Exact when the manifest cannot be read (<Case>)' -ForEach @(
        @{ Case = 'not valid PowerShell data'; Content = '@{ RequiredModules = @(' ; Name = 'broken.psd1' }
        @{ Case = 'not a .psd1 file'; Content = ''; Name = 'module.psm1' }
    ) {
        $path = Join-Path $TestDrive "unreadable-$([guid]::NewGuid().ToString('N'))-$Name"
        Set-Content -LiteralPath $path -Value $Content

        InModuleScope $script:moduleName -Parameters @{ Path = $path } {
            param($Path)
            $info = [PSCustomObject]@{ Path = $Path; RequiredModules = @([PSCustomObject]@{ Name = 'LambdaPSFake.Dep'; Version = [version]'1.0' }) }
            $entries = @(Get-LambdaPSModuleDependency -Module $info)
            $entries.Count | Should -Be 1
            $entries[0].Kind | Should -Be 'Exact'
            $entries[0].Version | Should -Be '1.0'
        }
    }

    It 'reads the GUID a RequiredModules entry names, and $null for an entry without one' {
        $manifest = Join-Path $TestDrive 'dependency-guid.psd1'
        Set-Content -LiteralPath $manifest -Value ("@{ ModuleVersion = '1.0.0'; RequiredModules = @(" +
            "@{ ModuleName = 'LambdaPSFake.WithGuid'; RequiredVersion = '1.0'; GUID = '0badc0de-0000-4000-8000-000000000002' }, " +
            "@{ ModuleName = 'LambdaPSFake.NoGuid'; RequiredVersion = '1.0' }) }")
        InModuleScope $script:moduleName -Parameters @{ Manifest = $manifest } {
            param($Manifest)
            $entries = @(Get-LambdaPSModuleDependency -Module ([PSCustomObject]@{ Path = $Manifest; RequiredModules = @() }))
            $entries[0].Guid | Should -Be ([guid]'0badc0de-0000-4000-8000-000000000002')
            $entries[1].Guid | Should -BeNullOrEmpty
        }
    }

    It 'returns nothing for a manifest with no RequiredModules' {
        $manifest = Join-Path $TestDrive 'no-dependencies.psd1'
        Set-Content -LiteralPath $manifest -Value "@{ ModuleVersion = '1.0.0' }"
        InModuleScope $script:moduleName -Parameters @{ Manifest = $manifest } {
            param($Manifest)
            @(Get-LambdaPSModuleDependency -Module ([PSCustomObject]@{ Path = $Manifest; RequiredModules = @() })).Count | Should -Be 0
        }
    }
}
