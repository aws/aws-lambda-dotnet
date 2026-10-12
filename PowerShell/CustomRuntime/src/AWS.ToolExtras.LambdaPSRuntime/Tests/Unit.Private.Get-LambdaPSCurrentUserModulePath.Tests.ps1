BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSCurrentUserModulePath' -Tag 'Unit' -Skip:$IsWindows {
    BeforeEach {
        $script:savedDataHome = $env:XDG_DATA_HOME
    }
    AfterEach {
        $env:XDG_DATA_HOME = $script:savedDataHome
    }

    It 'uses XDG_DATA_HOME when it is set' {
        $env:XDG_DATA_HOME = Join-Path $TestDrive 'data'
        InModuleScope $script:moduleName {
            Get-LambdaPSCurrentUserModulePath | Should -Be ([System.IO.Path]::Combine($env:XDG_DATA_HOME, 'powershell', 'Modules'))
        }
    }

    It 'uses ~/.local/share when XDG_DATA_HOME is not set, on macOS as well as Linux' {
        $env:XDG_DATA_HOME = $null
        InModuleScope $script:moduleName {
            Get-LambdaPSCurrentUserModulePath | Should -Be ([System.IO.Path]::Combine($HOME, '.local', 'share', 'powershell', 'Modules'))
        }
    }
}

Describe 'Get-LambdaPSCurrentUserModulePath on Windows' -Tag 'Unit' -Skip:(-not $IsWindows) {
    It 'is the CurrentUser folder PowerShell puts first on the PSModulePath' {
        # Read from a fresh pwsh, whose PSModulePath no test has changed.
        $first = & (Get-Process -Id $PID).Path -NoProfile -NonInteractive -Command '($env:PSModulePath -split [System.IO.Path]::PathSeparator)[0]'
        InModuleScope $script:moduleName -Parameters @{ First = "$first".Trim() } {
            param($First)
            Get-LambdaPSCurrentUserModulePath | Should -Be $First
        }
    }
}
