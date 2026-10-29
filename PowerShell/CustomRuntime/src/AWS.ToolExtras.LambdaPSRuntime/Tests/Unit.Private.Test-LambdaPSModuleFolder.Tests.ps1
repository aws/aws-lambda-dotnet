BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Test-LambdaPSModuleFolder' -Tag 'Unit' {
    It 'returns $false when the version folder does not exist' {
        InModuleScope $script:moduleName {
            Test-LambdaPSModuleFolder -Path ([System.IO.Path]::Combine($TestDrive, 'none', 'LambdaPSFake.A', '1.0.0')) -Name 'LambdaPSFake.A' | Should -BeFalse
        }
    }

    It 'returns $true when the version folder holds the manifest' {
        InModuleScope $script:moduleName {
            $d = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($TestDrive, 'complete', 'LambdaPSFake.A', '1.0.0')) -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($d.FullName, 'LambdaPSFake.A.psd1')) -Value '@{}'
            Test-LambdaPSModuleFolder -Path $d.FullName -Name 'LambdaPSFake.A' | Should -BeTrue
        }
    }

    It 'stops with ModuleFolderIncomplete, naming the folder, when it exists without the manifest' {
        InModuleScope $script:moduleName {
            $d = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($TestDrive, 'incomplete', 'LambdaPSFake.A', '1.0.0')) -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($d.FullName, 'LambdaPSFake.A.dll')) -Value 'x'
            $err = { Test-LambdaPSModuleFolder -Path $d.FullName -Name 'LambdaPSFake.A' } | Should -Throw -ErrorId 'ModuleFolderIncomplete' -PassThru
            $err.CategoryInfo.Category | Should -Be 'ResourceExists'
            $err.TargetObject | Should -Be $d.FullName
            $err.Exception.Message | Should -BeLike "'$($d.FullName)' exists but holds no 'LambdaPSFake.A.psd1'*Remove the folder and run again."
        }
    }
}
