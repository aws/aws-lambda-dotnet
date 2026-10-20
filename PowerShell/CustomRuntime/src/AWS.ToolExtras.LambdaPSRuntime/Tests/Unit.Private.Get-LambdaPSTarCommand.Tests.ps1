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

    It 'falls back to the tar on PATH on Windows when System32 has no tar.exe' {
        $empty = [System.IO.Path]::Combine($TestDrive, 'NoTarWindows')
        $null = New-Item -ItemType Directory -Path $empty
        InModuleScope $script:moduleName -Parameters @{ SystemRoot = $empty } {
            param($SystemRoot)
            $expected = (Get-LambdaPSTarCommand -OnWindows $false -SystemRoot $SystemRoot)
            Get-LambdaPSTarCommand -OnWindows $true -SystemRoot $SystemRoot | Should -Be $expected
            Get-LambdaPSTarCommand -OnWindows $true -SystemRoot '' | Should -Be $expected
        }
    }

    It 'returns the path of the tar program off Windows' -Skip:(-not (Get-Command -Name tar -CommandType Application -ErrorAction SilentlyContinue)) {
        InModuleScope $script:moduleName -Parameters @{ SystemRoot = $script:systemRoot } {
            param($SystemRoot)
            Get-LambdaPSTarCommand -OnWindows $false -SystemRoot $SystemRoot |
                Should -Be (Get-Command -Name tar -CommandType Application | Select-Object -First 1).Source
        }
    }

    It 'returns the tar program''s path, not a session function or alias named tar' -Skip:(-not (Get-Command -Name tar -CommandType Application -ErrorAction SilentlyContinue)) {
        InModuleScope $script:moduleName -Parameters @{ SystemRoot = $script:systemRoot } {
            param($SystemRoot)
            function script:tar { 'FUNCTION RAN' }
            Set-Alias -Name 'tar' -Value 'Write-Output' -Scope Script
            try {
                $tar = Get-LambdaPSTarCommand -OnWindows $false -SystemRoot $SystemRoot
            }
            finally {
                Remove-Item -LiteralPath 'function:script:tar', 'alias:tar'
            }
            [System.IO.Path]::IsPathRooted($tar) | Should -BeTrue
            (Get-Command -Name $tar).CommandType | Should -Be 'Application'
        }
    }

    It 'returns tar, which callers find is not a program, when no tar program is on PATH' {
        InModuleScope $script:moduleName -Parameters @{ SystemRoot = $script:systemRoot } {
            param($SystemRoot)
            $savedPath = $env:PATH
            $env:PATH = [System.IO.Path]::Combine($SystemRoot, 'no-such-folder')
            try {
                Get-LambdaPSTarCommand -OnWindows $false -SystemRoot $SystemRoot | Should -Be 'tar'
            }
            finally {
                $env:PATH = $savedPath
            }
        }
    }

    It 'returns tar, which is no program, when no tar program is on PATH and a session function is named tar' {
        InModuleScope $script:moduleName -Parameters @{ SystemRoot = $script:systemRoot } {
            param($SystemRoot)
            $savedPath = $env:PATH
            $env:PATH = [System.IO.Path]::Combine($SystemRoot, 'no-such-folder')
            function script:tar { 'FUNCTION RAN' }
            try {
                $tar = Get-LambdaPSTarCommand -OnWindows $false -SystemRoot $SystemRoot
                $tar | Should -Be 'tar'
                Get-Command -Name $tar -CommandType Application -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
            }
            finally {
                $env:PATH = $savedPath
                Remove-Item -LiteralPath 'function:script:tar'
            }
        }
    }

    It 'chooses for the current host by default' {
        InModuleScope $script:moduleName {
            Get-LambdaPSTarCommand | Should -Be (Get-LambdaPSTarCommand -OnWindows $IsWindows -SystemRoot $env:SystemRoot)
        }
    }
}
