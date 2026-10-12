BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSTarCommand' -Tag 'Unit' {
    BeforeAll {
        # A stand-in Windows folder with System32\tar.exe in it.
        $script:systemRoot = [System.IO.Path]::Combine($TestDrive, 'Windows')
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($script:systemRoot, 'System32'))
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:systemRoot, 'System32', 'tar.exe')) -Value ''
    }

    It 'returns System32\tar.exe on Windows, not the tar on PATH' {
        InModuleScope $script:moduleName -Parameters @{ SystemRoot = $script:systemRoot } {
            param($SystemRoot)
            Get-LambdaPSTarCommand -OnWindows $true -SystemRoot $SystemRoot |
                Should -Be ([System.IO.Path]::Combine($SystemRoot, 'System32', 'tar.exe'))
        }
    }

    It 'returns tar on Windows when System32 has no tar.exe' {
        $empty = [System.IO.Path]::Combine($TestDrive, 'NoTarWindows')
        $null = New-Item -ItemType Directory -Path $empty
        InModuleScope $script:moduleName -Parameters @{ SystemRoot = $empty } {
            param($SystemRoot)
            Get-LambdaPSTarCommand -OnWindows $true -SystemRoot $SystemRoot | Should -Be 'tar'
            Get-LambdaPSTarCommand -OnWindows $true -SystemRoot '' | Should -Be 'tar'
        }
    }

    It 'returns tar off Windows' {
        InModuleScope $script:moduleName -Parameters @{ SystemRoot = $script:systemRoot } {
            param($SystemRoot)
            Get-LambdaPSTarCommand -OnWindows $false -SystemRoot $SystemRoot | Should -Be 'tar'
        }
    }

    It 'chooses for the current host by default' {
        InModuleScope $script:moduleName {
            Get-LambdaPSTarCommand | Should -Be (Get-LambdaPSTarCommand -OnWindows $IsWindows -SystemRoot $env:SystemRoot)
        }
    }
}
