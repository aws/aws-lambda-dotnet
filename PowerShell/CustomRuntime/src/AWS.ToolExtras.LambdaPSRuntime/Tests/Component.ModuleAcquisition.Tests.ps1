BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeModule.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

# Save-LambdaPSModule end to end: the real walk, lookup and copy, with only the download seam mocked.
Describe 'Module acquisition edge cases' -Tag 'Component' {
    It 'Save-LambdaPSModule returns AWS.Tools.Common as Saved when the installer writes it into -Path, though it is installed' {
        $installed = Join-Path $TestDrive 'installed-common'
        $null = New-FakeModule -Root $installed -Name 'AWS.Tools.Common' -Version '9.9.4'
        $mods = Join-Path $TestDrive 'save-common'
        Mock 'Save-LambdaPSAWSToolsModule' -ModuleName $script:moduleName -MockWith {
            $null = New-FakeModule -Root $Path -Name 'AWS.Tools.FakeSvc' -Version $Version -Requires "@(@{ ModuleName = 'AWS.Tools.Common'; RequiredVersion = '$Version' })"
            $null = New-FakeModule -Root $Path -Name 'AWS.Tools.Common' -Version $Version
        }
        $saved = $env:PSModulePath
        try {
            $env:PSModulePath = $installed + [System.IO.Path]::PathSeparator + $env:PSModulePath
            $out = @(Save-LambdaPSModule -Module @{ ModuleName = 'AWS.Tools.FakeSvc'; RequiredVersion = '9.9.4' } -Path $mods -WarningAction SilentlyContinue)
        }
        finally {
            $env:PSModulePath = $saved
        }

        @($out | ForEach-Object { "$($_.Name) $($_.Action)" } | Sort-Object) | Should -Be @('AWS.Tools.Common Saved', 'AWS.Tools.FakeSvc Saved')
    }

    It 'downloads an installed module from -ModuleRepository instead of copying the installed one' {
        $installed = Join-Path $TestDrive 'mirror-installed'
        $null = New-FakeModule -Root $installed -Name 'LambdaPSFake.Mirrored' -Version '1.0.0'
        $mods = Join-Path $TestDrive 'mirror-mods'
        Mock 'Get-PSResourceRepository' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = 'Mirror' } }
        Mock 'Save-LambdaPSModuleFromRepository' -ModuleName $script:moduleName -MockWith {
            $exact = $VersionRange.Trim('[', ']')
            $null = New-FakeModule -Root $Path -Name $Name -Version $exact
            [PSCustomObject]@{ Name = $Name; Version = $exact; Repository = 'Mirror' }
        }
        $saved = $env:PSModulePath
        try {
            $env:PSModulePath = $installed + [System.IO.Path]::PathSeparator + $env:PSModulePath
            $out = @(Save-LambdaPSModule -Module @{ ModuleName = 'LambdaPSFake.Mirrored'; RequiredVersion = '1.0.0' } -Path $mods -ModuleRepository Mirror -WarningAction SilentlyContinue)
        }
        finally {
            $env:PSModulePath = $saved
        }

        $out.Count | Should -Be 1
        $out[0].Action | Should -Be 'Saved'
        $out[0].Repository | Should -Be 'Mirror'
        Should -Invoke 'Save-LambdaPSModuleFromRepository' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $ModuleRepository -eq 'Mirror' }
    }

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

# -Confirm runs in a child pwsh, whose console answers the prompts: 'Y' for the one copy prompt, then
# 'N', so a prompt per module would leave a module out.
Describe 'Save-LambdaPSModule -Confirm' -Tag 'Component' {
    It 'asks once to copy the installed modules' {
        $installed = Join-Path $TestDrive 'confirm-installed'
        foreach ($n in 'LambdaPSFake.IA', 'LambdaPSFake.IB', 'LambdaPSFake.IC') {
            $null = New-FakeModule -Root $installed -Name $n -Version '1.0.0'
        }
        $mods = Join-Path $TestDrive 'confirm-mods'
        $command = "`$env:PSModulePath = '$installed' + [System.IO.Path]::PathSeparator + `$env:PSModulePath; " +
            "`$null = Save-LambdaPSModule -Module @(@{ ModuleName = 'LambdaPSFake.IA'; RequiredVersion = '1.0.0' }, " +
            "@{ ModuleName = 'LambdaPSFake.IB'; RequiredVersion = '1.0.0' }, @{ ModuleName = 'LambdaPSFake.IC'; RequiredVersion = '1.0.0' }) " +
            "-Path '$mods' -Confirm"

        $null = Invoke-InChildPwsh -Command $command -Answer 'Y', 'N', 'N'

        foreach ($n in 'LambdaPSFake.IA', 'LambdaPSFake.IB', 'LambdaPSFake.IC') {
            [System.IO.Path]::Combine($mods, $n, '1.0.0', "$n.psd1") | Should -Exist
        }
    }

    It 'warns, naming the modules, when the copy is declined' {
        $installed = Join-Path $TestDrive 'decline-installed'
        foreach ($n in 'LambdaPSFake.ID', 'LambdaPSFake.IE') {
            $null = New-FakeModule -Root $installed -Name $n -Version '1.0.0'
        }
        $mods = Join-Path $TestDrive 'decline-mods'
        $command = "`$env:PSModulePath = '$installed' + [System.IO.Path]::PathSeparator + `$env:PSModulePath; " +
            "`$out = @(Save-LambdaPSModule -Module @(@{ ModuleName = 'LambdaPSFake.ID'; RequiredVersion = '1.0.0' }, " +
            "@{ ModuleName = 'LambdaPSFake.IE'; RequiredVersion = '1.0.0' }) -Path '$mods' -Confirm -WarningVariable w -WarningAction SilentlyContinue); " +
            "'OUT:' + `$out.Count; `$w | ForEach-Object { 'WARNING:' + `$_ }"

        $lines = @((Invoke-InChildPwsh -Command $command -Answer 'N').Output -split "`n")

        $lines | Should -Contain 'OUT:0'
        $lines | Should -Contain ("WARNING:Did not copy LambdaPSFake.ID 1.0.0, LambdaPSFake.IE 1.0.0, because the copy was declined, " +
            "so '$mods' does not hold the complete set.")
        Test-Path -LiteralPath ([System.IO.Path]::Combine($mods, 'LambdaPSFake.ID')) | Should -BeFalse
    }
}
