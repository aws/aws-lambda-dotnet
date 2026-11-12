BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeModule.ps1'))

    $script:modules = [System.IO.Path]::Combine($TestDrive, 'modules')
    $null = New-FakeModule -Root $script:modules -Name 'LambdaPSFake.Vendored' -Version '1.2.0'

    function Select-Unmet {
        param([object[]]$Module, [string]$ModulesPath = $script:modules)
        InModuleScope $script:moduleName -Parameters @{ Module = $Module; ModulesPath = $ModulesPath } {
            param($Module, $ModulesPath)
            @(Select-LambdaPSUnmetModule -Module $Module -ModulesPath $ModulesPath | ForEach-Object Name)
        }
    }

    # Read inside the module scope: a -WarningVariable outside InModuleScope does not see its warnings.
    function Get-UnmetWarning {
        param([object[]]$Module, [string]$ModulesPath = $script:modules)
        InModuleScope $script:moduleName -Parameters @{ Module = $Module; ModulesPath = $ModulesPath } {
            param($Module, $ModulesPath)
            @(Select-LambdaPSUnmetModule -Module $Module -ModulesPath $ModulesPath 3>&1 |
                    Where-Object { $_ -is [System.Management.Automation.WarningRecord] } | ForEach-Object Message)
        }
    }
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Select-LambdaPSUnmetModule' -Tag 'Unit' {
    It 'drops one of PowerShell''s own modules, <Case>' -ForEach @(
        @{ Case = 'by bare name'; Spec = 'Microsoft.PowerShell.Utility' }
        @{ Case = 'with a version'; Spec = @{ ModuleName = 'Microsoft.PowerShell.Management'; ModuleVersion = '7.0.0' } }
    ) {
        Select-Unmet -Module @($Spec) | Should -BeNullOrEmpty
    }

    # The pin check and the dependency walk decide a module the source keeps in its own modules/ folder.
    It 'keeps <Case>' -ForEach @(
        @{ Case = 'a bare name the source keeps in modules/'; Spec = 'LambdaPSFake.Vendored' }
        @{ Case = 'the exact version the source keeps in modules/'; Spec = @{ ModuleName = 'LambdaPSFake.Vendored'; RequiredVersion = '1.2.0' } }
        @{ Case = 'a module the source does not keep'; Spec = @{ ModuleName = 'LambdaPSFake.Other'; RequiredVersion = '1.0.0' } }
    ) {
        Select-Unmet -Module @($Spec) | Should -Be @($(if ($Spec -is [string]) { $Spec } else { $Spec.ModuleName }))
    }

    It 'warns, naming the folder, about a pinned version the source keeps in modules/, <Case>' -ForEach @(
        @{ Case = 'by its name'; Name = 'LambdaPSFake.Vendored' }
        @{ Case = 'by its name in another case'; Name = 'lambdapsfake.vendored' }
    ) {
        $spec = @{ ModuleName = $Name; RequiredVersion = '1.2.0' }
        $warnings = @(Get-UnmetWarning -Module @($spec))
        $warnings.Count | Should -Be 1
        "$($warnings[0])" | Should -BeLike "*'#Requires -Modules' and in '$([System.IO.Path]::Combine($script:modules, 'LambdaPSFake.Vendored', '1.2.0'))'*"
    }

    It 'warns, naming the folder, about a pinned version when the source keeps another version in modules/' {
        $warnings = @(Get-UnmetWarning -Module @(@{ ModuleName = 'LambdaPSFake.Vendored'; RequiredVersion = '1.3.0' }))
        $warnings.Count | Should -Be 1
        "$($warnings[0])" | Should -BeLike "*'#Requires -Modules' and '$([System.IO.Path]::Combine($script:modules, 'LambdaPSFake.Vendored'))' holds another copy. The package ships both.*"
    }

    It 'does not warn about <Case>' -ForEach @(
        @{ Case = 'a module the source does not keep'; Spec = @{ ModuleName = 'LambdaPSFake.Other'; RequiredVersion = '1.0.0' }; Path = $null }
        @{ Case = 'an unpinned requirement, which the pin check refuses'; Spec = 'LambdaPSFake.Vendored'; Path = $null }
        @{ Case = 'a source with no modules/ folder'; Spec = @{ ModuleName = 'LambdaPSFake.Vendored'; RequiredVersion = '1.2.0' }; Path = '' }
    ) {
        $modulesPath = if ($null -eq $Path) { $script:modules } else { $Path }
        Get-UnmetWarning -Module @($Spec) -ModulesPath $modulesPath | Should -BeNullOrEmpty
    }

    It 'keeps the others in order when some are PowerShell''s own' {
        Select-Unmet -Module @('LambdaPSFake.A', 'Microsoft.PowerShell.Utility', 'LambdaPSFake.B') -ModulesPath '' | Should -Be @('LambdaPSFake.A', 'LambdaPSFake.B')
    }
}
