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

    It 'keeps the others in order when some are PowerShell''s own' {
        Select-Unmet -Module @('LambdaPSFake.A', 'Microsoft.PowerShell.Utility', 'LambdaPSFake.B') -ModulesPath '' | Should -Be @('LambdaPSFake.A', 'LambdaPSFake.B')
    }
}
