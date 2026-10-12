BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSStagedModule' -Tag 'Unit' {
    It 'lists Name/Version/Name.psd1 modules, sorted, and ignores the runtime helper files' {
        InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' -Parameters @{ root = (Join-Path $TestDrive 'sm') } {
            param($root)
            foreach ($m in @(@('Zeta', '2.0.0'), @('Alpha', '1.0.0'))) {
                $d = [System.IO.Path]::Combine($root, $m[0], $m[1])
                $null = New-Item -ItemType Directory -Path $d -Force
                Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$($m[0]).psd1")) -Value '@{}'
            }
            # Things that live in a runtime modules/ folder but are NOT versioned modules.
            Set-Content -LiteralPath ([System.IO.Path]::Combine($root, 'pwsh-runtime.psd1')) -Value '@{}'
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($root, 'Private')) -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($root, '.gitkeep')) -Value ''
            # A version folder with no manifest is not a module either.
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($root, 'Empty', '1.0.0')) -Force

            $result = Get-LambdaPSStagedModule -Path $root
            $result.Name | Should -Be @('Alpha', 'Zeta')
            $result[1].Version | Should -Be ([version]'2.0.0')
            $result[0].PSTypeNames | Should -Contain 'AWS.ToolExtras.LambdaPSRuntime.StagedModule'
            # Shown as text, each reads 'Name Version'.
            "$($result[0])" | Should -Be 'Alpha 1.0.0'
        }
    }

    It 'returns an empty array for a missing directory' {
        InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' -Parameters @{ root = (Join-Path $TestDrive 'nope') } {
            param($root)
            $result = Get-LambdaPSStagedModule -Path $root
            , $result | Should -BeOfType [array]
            $result.Count | Should -Be 0
        }
    }
}
