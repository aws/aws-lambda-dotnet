BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'New-LambdaPSModuleNotFoundError' -Tag 'Unit' {
    It 'names one missing module in the singular, with its key as the target' {
        InModuleScope $script:moduleName {
            $err = New-LambdaPSModuleNotFoundError -Missing @([PSCustomObject]@{ Name = 'LambdaPSFake.A'; Version = '1.0.0'; Key = 'LambdaPSFake.A 1.0.0' })
            $err.FullyQualifiedErrorId | Should -Be 'ModuleNotFoundLocally'
            $err.CategoryInfo.Category | Should -Be 'ObjectNotFound'
            $err.TargetObject | Should -Be 'LambdaPSFake.A 1.0.0'
            $err.Exception | Should -BeOfType [System.IO.FileNotFoundException]
            $err.Exception.Message | Should -BeLike "Module 'LambdaPSFake.A' 1.0.0 is not available on this machine. Rerun with -MissingModuleAction Save (the default) or Install to download it, or save it with *"
        }
    }

    It 'names every missing module in the plural, with every key as the target' {
        InModuleScope $script:moduleName {
            $missing = @(
                [PSCustomObject]@{ Name = 'LambdaPSFake.A'; Version = '1.0.0'; Key = 'LambdaPSFake.A 1.0.0' }
                [PSCustomObject]@{ Name = 'LambdaPSFake.B'; Version = '2.0'; Key = 'LambdaPSFake.B 2.0' }
            )
            $err = New-LambdaPSModuleNotFoundError -Missing $missing
            $err.TargetObject | Should -Be @('LambdaPSFake.A 1.0.0', 'LambdaPSFake.B 2.0')
            $err.Exception.Message | Should -BeLike "Modules 'LambdaPSFake.A' 1.0.0, 'LambdaPSFake.B' 2.0 are not available on this machine. *download them, or save them with *"
        }
    }

    It 'names the source''s modules/ folder for a missing version kept there, with or without a version folder, and only for those' {
        $modules = [System.IO.Path]::Combine($TestDrive, 'src', 'modules')
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($modules, 'LambdaPSFake.A', '1.0.0')) -Force
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($modules, 'LambdaPSFake.B', '1.0')) -Force
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($modules, 'LambdaPSFake.C')) -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($modules, 'LambdaPSFake.C', 'LambdaPSFake.C.psd1')) -Value "@{ ModuleVersion = '3.0.0' }"
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($modules, 'LambdaPSFake.D')) -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($modules, 'LambdaPSFake.D', 'LambdaPSFake.D.psd1')) -Value '@{ ModuleVersion = '
        InModuleScope $script:moduleName -Parameters @{ Modules = $modules } {
            param($Modules)
            $missing = @(
                [PSCustomObject]@{ Name = 'LambdaPSFake.A'; Version = '1.0.0'; Key = 'LambdaPSFake.A 1.0.0' }
                [PSCustomObject]@{ Name = 'LambdaPSFake.B'; Version = '2.0'; Key = 'LambdaPSFake.B 2.0' }
                [PSCustomObject]@{ Name = 'LambdaPSFake.C'; Version = '3.0.0'; Key = 'LambdaPSFake.C 3.0.0' }
                [PSCustomObject]@{ Name = 'LambdaPSFake.D'; Version = '4.0.0'; Key = 'LambdaPSFake.D 4.0.0' }
            )
            $message = (New-LambdaPSModuleNotFoundError -Missing $missing -SourceModulesPath $Modules).Exception.Message
            $folder = [System.IO.Path]::Combine($Modules, 'LambdaPSFake.A')
            $message | Should -BeLike "*folder you add to PSModulePath. 'LambdaPSFake.A' 1.0.0 is in '$folder', which is packaged as it is but not searched for the modules to stage.*"
            $message | Should -BeLike "*'LambdaPSFake.C' 3.0.0 is in '$([System.IO.Path]::Combine($Modules, 'LambdaPSFake.C'))', which is packaged*"
            $message | Should -Not -BeLike "*'LambdaPSFake.B' 2.0 is in*"
            $message | Should -Not -BeLike "*'LambdaPSFake.D' 4.0.0 is in*"
        }
    }
}
