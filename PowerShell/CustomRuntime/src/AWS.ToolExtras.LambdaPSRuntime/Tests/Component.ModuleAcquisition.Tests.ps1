BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeModule.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

# Save-LambdaPSModule end to end: the real walk, lookup and copy, with only the download seam mocked.
Describe 'Module acquisition edge cases' -Tag 'Component' {
    It 'Save-LambdaPSModule copies an already-installed version into -Path and reports it as Copied' {
        $installed = Join-Path $TestDrive 'installed-modules'
        $null = New-FakeModule -Root $installed -Name 'LambdaPSFake.LocalDep' -Version '1.0.0'
        $null = New-FakeModule -Root $installed -Name 'LambdaPSFake.Local' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.LocalDep'; RequiredVersion = '1.0.0' })"
        $mods = Join-Path $TestDrive 'save-local'
        $saved = $env:PSModulePath
        try {
            $env:PSModulePath = $installed + [System.IO.Path]::PathSeparator + $env:PSModulePath
            $out = @(Save-LambdaPSModule -Module @{ ModuleName = 'LambdaPSFake.Local'; RequiredVersion = '1.0.0' } -Path $mods -WarningAction SilentlyContinue)
        }
        finally {
            $env:PSModulePath = $saved
        }

        # The installed dependency is copied too, so -Path holds the whole set.
        @($out | ForEach-Object { "$($_.Name) $($_.Action) $($_.Source)" }) | Should -Be @('LambdaPSFake.Local Copied Local', 'LambdaPSFake.LocalDep Copied Local')
        Test-Path -LiteralPath ([System.IO.Path]::Combine($mods, 'LambdaPSFake.Local', '1.0.0', 'LambdaPSFake.Local.psd1')) | Should -BeTrue
        Test-Path -LiteralPath ([System.IO.Path]::Combine($mods, 'LambdaPSFake.LocalDep', '1.0.0', 'LambdaPSFake.LocalDep.psd1')) | Should -BeTrue
    }

    It 'Save-LambdaPSModule reports and warns about a download into a -Path that is on the PSModulePath' {
        $mods = Join-Path $TestDrive 'save-on-psmodulepath'
        $null = New-Item -ItemType Directory -Path $mods -Force
        Mock -ModuleName $script:moduleName 'Save-LambdaPSModuleFromRepository' -MockWith {
            $exact = $VersionRange.Trim('[', ']')
            $null = New-FakeModule -Root $Path -Name $Name -Version $exact
            [PSCustomObject]@{ Name = $Name; Version = $exact; Repository = 'Internal' }
        }
        $saved = $env:PSModulePath
        try {
            # The setup the cmdlet's help suggests: -Path added to the PSModulePath to test against it.
            $env:PSModulePath = $mods + [System.IO.Path]::PathSeparator + $env:PSModulePath
            $out = @(Save-LambdaPSModule -Module @{ ModuleName = 'LambdaPSFake.OnPath'; RequiredVersion = '1.0.0' } -Path $mods -WarningVariable warnings -WarningAction SilentlyContinue)
        }
        finally {
            $env:PSModulePath = $saved
        }

        $out.Count | Should -Be 1
        $out[0].Name | Should -Be 'LambdaPSFake.OnPath'
        $out[0].Action | Should -Be 'Saved'
        $out[0].Source | Should -Be 'PSResourceGet'
        @($warnings) -join ' ' | Should -BeLike '*LambdaPSFake.OnPath 1.0.0*before production use*'
    }
}
