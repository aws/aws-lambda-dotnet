BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeModule.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSModuleClosure' -Tag 'Unit' {
    It 'reports a missing module without walking it, and walks what it finds' {
        $root = Join-Path $TestDrive 'closure'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.A' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.B'; ModuleVersion = '2.0.0' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' } -SearchPath $root
            @($closure.Resolved.Key) | Should -Be @('LambdaPSFake.A 1.0.0')
            @($closure.Missing.Key) | Should -Be @('LambdaPSFake.B 2.0.0')
            $closure.Error | Should -BeNullOrEmpty
        }
    }

    It 'does not follow or stage a dependency on a module that ships with PowerShell (<Case>)' -ForEach @(
        @{ Case = 'bare name'; Requires = "@('Microsoft.PowerShell.Utility')" }
        @{ Case = 'versioned'; Requires = "@(@{ ModuleName = 'Microsoft.PowerShell.Utility'; ModuleVersion = '7.0.0.0' })" }
    ) {
        $root = Join-Path $TestDrive "closure-builtin-$($Case -replace ' ', '-')"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.UsesUtility' -Version '1.0.0' -Requires $Requires

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $output = @(Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.UsesUtility'; RequiredVersion = '1.0.0' } -SearchPath $root -Verbose 4>&1)
            $closure = $output | Where-Object { $_ -isnot [System.Management.Automation.VerboseRecord] }
            @($output | Where-Object { $_ -is [System.Management.Automation.VerboseRecord] } | ForEach-Object { $_.Message }) |
                Should -Contain "Skipping 'Microsoft.PowerShell.Utility', required by LambdaPSFake.UsesUtility 1.0.0: it is one of PowerShell's own modules."
            $closure.Error | Should -BeNullOrEmpty
            @($closure.Resolved.Key) | Should -Be @('LambdaPSFake.UsesUtility 1.0.0')
            $closure.Missing.Count | Should -Be 0
        }
    }

    It 'follows a dependency on a module that ships with PowerShell but is updated on its own (<Name>)' -ForEach @(
        @{ Name = 'PSReadLine' }
        @{ Name = 'Microsoft.PowerShell.PSResourceGet' }
    ) {
        $root = Join-Path $TestDrive "closure-shipped-$Name"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.UsesShipped' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = '$Name'; RequiredVersion = '99.0.0' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root; name = $Name } {
            param($root, $name)
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.UsesShipped'; RequiredVersion = '1.0.0' } -SearchPath $root
            $closure.Error | Should -BeNullOrEmpty
            @($closure.Missing.Key) | Should -Be @("$name 99.0.0")
        }
    }

    It 'returns DependencyNameIsPath for a dependency named by a <Case> path' -ForEach @(
        @{ Case = 'rooted'; Name = '/elsewhere/LambdaPSFake.Other' }
        @{ Case = 'relative'; Name = '../LambdaPSFake.Other' }
        @{ Case = 'backslash'; Name = 'sub\LambdaPSFake.Other' }
        @{ Case = "'.'"; Name = '.' }
        @{ Case = "'..'"; Name = '..' }
    ) {
        $root = Join-Path $TestDrive "closure-path-$Case"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.PathDep' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = '$Name'; RequiredVersion = '2.0.0' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root; name = $Name } {
            param($root, $name)
            Mock 'Find-LambdaPSModule' -ParameterFilter { $Name -ne 'LambdaPSFake.PathDep' } -MockWith { throw 'a path dependency must not be looked up' }
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.PathDep'; RequiredVersion = '1.0.0' } -SearchPath $root
            $closure.Error.FullyQualifiedErrorId | Should -Be 'DependencyNameIsPath'
            $closure.Error.TargetObject | Should -Be $name
        }
    }

    It 'settles when a lower version choice would undo the requirement that raised it' {
        # R needs A and B (minimum 1.0.0). A 1.0.0 needs B 2.0.0 or later; B 2.0.0 needs A 2.0.0 or later;
        # A 2.0.0 and B 1.0.0 need only 1.0.0 of the other. A choice that may go down swings for ever.
        $root = Join-Path $TestDrive 'closure-monotone'
        $min = { param($n, $v) "@{ ModuleName = '$n'; ModuleVersion = '$v' }" }
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.RevR' -Version '1.0.0' -Requires "@($(& $min 'LambdaPSFake.RevA' '1.0.0'), $(& $min 'LambdaPSFake.RevB' '1.0.0'))"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.RevA' -Version '1.0.0' -Requires "@($(& $min 'LambdaPSFake.RevB' '2.0.0'))"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.RevA' -Version '2.0.0' -Requires "@($(& $min 'LambdaPSFake.RevB' '1.0.0'))"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.RevB' -Version '1.0.0' -Requires "@($(& $min 'LambdaPSFake.RevA' '1.0.0'))"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.RevB' -Version '2.0.0' -Requires "@($(& $min 'LambdaPSFake.RevA' '2.0.0'))"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.RevR'; RequiredVersion = '1.0.0' } -SearchPath $root
            $closure.Error | Should -BeNullOrEmpty
            @($closure.Resolved.Key | Sort-Object) | Should -Be @('LambdaPSFake.RevA 2.0.0', 'LambdaPSFake.RevB 2.0.0', 'LambdaPSFake.RevR 1.0.0')
        }
    }

    It 'skips, with one warning, one of PowerShell''s own modules named directly in -Module' {
        InModuleScope $script:moduleName {
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'Microsoft.PowerShell.Utility'; RequiredVersion = '7.0.0.0' } -WarningVariable warnings -WarningAction SilentlyContinue
            $closure.Error | Should -BeNullOrEmpty
            $closure.Resolved.Count | Should -Be 0
            $closure.Missing.Count | Should -Be 0
            @($warnings).Count | Should -Be 1
            "$($warnings[0])" | Should -BeLike "Skipping 'Microsoft.PowerShell.Utility'*PowerShell's own modules*"
        }
    }

    It 'returns DependencyVersionUnpinned for a dependency version that is not a version number' {
        $root = Join-Path $TestDrive 'closure-bad-version'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.BadDep' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.Other'; RequiredVersion = '1.0.0,99' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            Mock 'Find-LambdaPSModule' -ParameterFilter { $Name -eq 'LambdaPSFake.Other' } -MockWith { throw 'must not be looked up' }
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.BadDep'; RequiredVersion = '1.0.0' } -SearchPath $root
            $closure.Error.FullyQualifiedErrorId | Should -Be 'DependencyVersionUnpinned'
            $closure.Error.Exception.Message | Should -BeLike "*'1.0.0,99', which is not a version number*"
            # Advice that works: the entry is in the parent's manifest, so a -Module pin cannot fix it.
            $closure.Error.Exception.Message | Should -BeLike "*. Pin a version of 'LambdaPSFake.BadDep'*, or fix that RequiredModules entry in 'LambdaPSFake.BadDep' 1.0.0's manifest*"
            $closure.Error.Exception.Message | Should -Not -BeLike "*Add 'LambdaPSFake.Other' to -Module*"
        }
    }

    # PowerShell matches versions exactly (a 1.0 manifest does not load for a 1.0.0 pin, nor the
    # reverse), so a module present only under the other spelling is an error that names both.
    It 'returns ModuleVersionSpelling when <Case>' -ForEach @(
        @{ Case = 'a 1.0 pin meets a 1.0.0 manifest'; Installed = '1.0.0'; Parent = $null; Pin = '1.0'; Use = "Pin RequiredVersion '1.0.0'" }
        @{ Case = 'a 1.0.0 pin meets a 1.0 manifest'; Installed = '1.0'; Parent = $null; Pin = '1.0.0'; Use = "Pin RequiredVersion '1.0'" }
        @{ Case = 'a RequiredVersion 1.0 entry meets a 1.0.0 manifest'; Installed = '1.0.0'; Parent = "@(@{ ModuleName = 'LambdaPSFake.Spelled'; RequiredVersion = '1.0' })"; Pin = $null; Use = "ModuleVersion '1.0'" }
        @{ Case = 'a ModuleVersion 1.0.0 minimum meets a 1.0 manifest'; Installed = '1.0'; Parent = "@(@{ ModuleName = 'LambdaPSFake.Spelled'; ModuleVersion = '1.0.0' })"; Pin = $null; Use = "ModuleVersion '1.0.0'" }
    ) {
        $root = Join-Path $TestDrive "closure-spelling-$([guid]::NewGuid().ToString('N'))"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Spelled' -Version $Installed
        $spec = if ($Parent) {
            $null = New-FakeModule -Root $root -Name 'LambdaPSFake.SpellParent' -Version '1.0.0' -Requires $Parent
            @{ ModuleName = 'LambdaPSFake.SpellParent'; RequiredVersion = '1.0.0' }
        }
        else { @{ ModuleName = 'LambdaPSFake.Spelled'; RequiredVersion = $Pin } }

        InModuleScope $script:moduleName -Parameters @{ root = $root; spec = $spec; installed = $Installed; use = $Use } {
            param($root, $spec, $installed, $use)
            $closure = Get-LambdaPSModuleClosure -Module $spec -SearchPath $root
            $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleVersionSpelling'
            $closure.Error.Exception.Message | Should -BeLike "*ModuleVersion '$installed'*matches versions exactly*$use*"
        }
    }

    It 'resolves a version only in the spelling asked for, and a minimum by a higher spelling' {
        $root = Join-Path $TestDrive 'closure-spelling-ok'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.TwoPartPin' -Version '1.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Higher' -Version '1.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.NeedsMin' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.Higher'; ModuleVersion = '1.0' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $two = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.TwoPartPin'; RequiredVersion = '1.0' } -SearchPath $root
            @($two.Resolved.Key) | Should -Be @('LambdaPSFake.TwoPartPin 1.0')
            # PowerShell loads 1.0.0 for 'ModuleVersion 1.0' (1.0.0 is higher), so the minimum is met.
            $min = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.NeedsMin'; RequiredVersion = '1.0.0' } -SearchPath $root
            $min.Error | Should -BeNullOrEmpty
            @($min.Resolved | ForEach-Object { "$($_.Name) $($_.Version)" }) | Should -Be @('LambdaPSFake.NeedsMin 1.0.0', 'LambdaPSFake.Higher 1.0.0')
        }
    }

    It 'returns ModuleVersionConflict when a pin and a RequiredModules entry spell one version differently' {
        # Both spellings are present, so each requirement finds its own: they are two versions, not one.
        $root = Join-Path $TestDrive 'closure-spelling-conflict'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Base' -Version '1.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Base' -Version '1.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Exact' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.Base'; RequiredVersion = '1.0' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $specs = @(
                @{ ModuleName = 'LambdaPSFake.Base'; RequiredVersion = '1.0.0' }
                @{ ModuleName = 'LambdaPSFake.Exact'; RequiredVersion = '1.0.0' }
            )
            $closure = Get-LambdaPSModuleClosure -Module $specs -SearchPath $root
            $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleVersionConflict'
            $closure.Error.Exception.Message | Should -BeLike "*1.0.0 (requested), 1.0 (required by LambdaPSFake.Exact 1.0.0)*"
            # A dependency's version is its parent's manifest entry, so the advice is not to pin it.
            $closure.Error.Exception.Message | Should -BeLike ("*change the version requested in -Module, or choose another " +
                "version of the module that requires 'LambdaPSFake.Base', so that one version satisfies them all.*")
        }
    }

    It 'meets two different minimums with a higher spelling (1.0 and 0.9 or later, only 1.0.0 present)' {
        $root = Join-Path $TestDrive 'closure-two-minimums'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.MinB' -Version '1.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.MinA' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.MinB'; ModuleVersion = '1.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.MinC' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.MinB'; ModuleVersion = '0.9' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $specs = @(@{ ModuleName = 'LambdaPSFake.MinA'; RequiredVersion = '1.0.0' }, @{ ModuleName = 'LambdaPSFake.MinC'; RequiredVersion = '1.0.0' })
            $closure = Get-LambdaPSModuleClosure -Module $specs -SearchPath $root
            $closure.Error | Should -BeNullOrEmpty
            @($closure.Resolved | ForEach-Object { "$($_.Name) $($_.Version)" }) | Should -Contain 'LambdaPSFake.MinB 1.0.0'
        }
    }

    It 'meets two different minimums with the exact spelling when it is present' {
        $root = Join-Path $TestDrive 'closure-two-minimums-exact'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.MinB' -Version '1.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.MinA' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.MinB'; ModuleVersion = '1.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.MinC' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.MinB'; ModuleVersion = '0.9' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $specs = @(@{ ModuleName = 'LambdaPSFake.MinA'; RequiredVersion = '1.0.0' }, @{ ModuleName = 'LambdaPSFake.MinC'; RequiredVersion = '1.0.0' })
            $closure = Get-LambdaPSModuleClosure -Module $specs -SearchPath $root
            $closure.Error | Should -BeNullOrEmpty
            @($closure.Resolved | ForEach-Object { "$($_.Name) $($_.Version)" }) | Should -Contain 'LambdaPSFake.MinB 1.0'
        }
    }

    Context 'a version raised by a module that drops out of the set' {
        BeforeEach {
            # X needs A 1.0.0 or later, Y needs A 2.0.0 or later, so A resolves to 2.0.0. A 1.0.0 needs B 3.0.0 or
            # later, but A 2.0.0 needs only B 1.0.0 or later: once A 1.0.0 is gone, nothing asks for B 3.0.0.
            $script:raiseRoot = Join-Path $TestDrive "closure-release-$([guid]::NewGuid().ToString('N'))"
            $min = { param($n, $v) "@(@{ ModuleName = '$n'; ModuleVersion = '$v' })" }
            $null = New-FakeModule -Root $script:raiseRoot -Name 'LambdaPSFake.RelX' -Version '1.0.0' -Requires (& $min 'LambdaPSFake.RelA' '1.0.0')
            $null = New-FakeModule -Root $script:raiseRoot -Name 'LambdaPSFake.RelY' -Version '1.0.0' -Requires (& $min 'LambdaPSFake.RelA' '2.0.0')
            $null = New-FakeModule -Root $script:raiseRoot -Name 'LambdaPSFake.RelA' -Version '1.0.0' -Requires (& $min 'LambdaPSFake.RelB' '3.0.0')
            $null = New-FakeModule -Root $script:raiseRoot -Name 'LambdaPSFake.RelA' -Version '2.0.0' -Requires (& $min 'LambdaPSFake.RelB' '1.0.0')
            $null = New-FakeModule -Root $script:raiseRoot -Name 'LambdaPSFake.RelB' -Version '1.0.0'
        }

        It 'releases the raise, with only the lower version present' {
            InModuleScope $script:moduleName -Parameters @{ root = $script:raiseRoot } {
                param($root)
                $specs = @(@{ ModuleName = 'LambdaPSFake.RelX'; RequiredVersion = '1.0.0' }, @{ ModuleName = 'LambdaPSFake.RelY'; RequiredVersion = '1.0.0' })
                $closure = Get-LambdaPSModuleClosure -Module $specs -SearchPath $root -RequireAll
                $closure.Error | Should -BeNullOrEmpty
                @($closure.Resolved | ForEach-Object { "$($_.Name) $($_.Version)" }) | Should -Contain 'LambdaPSFake.RelB 1.0.0'
            }
        }

        It 'releases the raise when the higher version is present too' {
            $null = New-FakeModule -Root $script:raiseRoot -Name 'LambdaPSFake.RelB' -Version '3.0.0'
            InModuleScope $script:moduleName -Parameters @{ root = $script:raiseRoot } {
                param($root)
                $specs = @(@{ ModuleName = 'LambdaPSFake.RelX'; RequiredVersion = '1.0.0' }, @{ ModuleName = 'LambdaPSFake.RelY'; RequiredVersion = '1.0.0' })
                $closure = Get-LambdaPSModuleClosure -Module $specs -SearchPath $root
                @($closure.Resolved | Where-Object { $_.Name -eq 'LambdaPSFake.RelB' } | ForEach-Object { $_.Version }) | Should -Be @('1.0.0')
            }
        }
    }

    It 'keeps a higher version that a module still in the set asks for' {
        $root = Join-Path $TestDrive 'closure-keep-asked'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.KeepB' -Version '1.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.KeepB' -Version '3.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.KeepX' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.KeepB'; ModuleVersion = '1.0.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.KeepY' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.KeepB'; ModuleVersion = '3.0.0' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $specs = @(@{ ModuleName = 'LambdaPSFake.KeepX'; RequiredVersion = '1.0.0' }, @{ ModuleName = 'LambdaPSFake.KeepY'; RequiredVersion = '1.0.0' })
            $closure = Get-LambdaPSModuleClosure -Module $specs -SearchPath $root
            $closure.Error | Should -BeNullOrEmpty
            @($closure.Resolved | Where-Object { $_.Name -eq 'LambdaPSFake.KeepB' } | ForEach-Object { $_.Version }) | Should -Be @('3.0.0')
        }
    }

    It 'settles two independent swings whose cycles have different lengths' {
        # A pair (X/Y) and a three-module ring (P/Q/W) each swing; together their choice sets only
        # repeat after more passes than the limit, so a swing must be caught per module.
        $root = Join-Path $TestDrive 'closure-two-swings'
        $min = { param([string[]]$Pairs) '@(' + (@(for ($i = 0; $i -lt $Pairs.Count; $i += 2) { "@{ ModuleName = 'LambdaPSFake.Sw$($Pairs[$i])'; ModuleVersion = '$($Pairs[$i + 1])' }" }) -join ', ') + ')' }
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.SwR' -Version '1.0.0' -Requires (& $min 'X', '1.0.0', 'Y', '1.0.0', 'P', '1.0.0', 'Q', '1.0.0', 'W', '1.0.0')
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.SwX' -Version '1.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.SwX' -Version '2.0.0' -Requires (& $min 'Y', '2.0.0')
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.SwY' -Version '1.0.0' -Requires (& $min 'X', '2.0.0')
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.SwY' -Version '2.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.SwP' -Version '1.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.SwP' -Version '2.0.0' -Requires (& $min 'Q', '2.0.0')
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.SwQ' -Version '1.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.SwQ' -Version '2.0.0' -Requires (& $min 'W', '2.0.0')
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.SwW' -Version '1.0.0' -Requires (& $min 'P', '2.0.0')
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.SwW' -Version '2.0.0'

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.SwR'; RequiredVersion = '1.0.0' } -SearchPath $root
            $closure.Error | Should -BeNullOrEmpty
            $versions = @{}
            foreach ($found in $closure.Resolved) { $versions[$found.Name] = $found.Version }
            foreach ($n in 'X', 'Y', 'P', 'Q', 'W') { $versions["LambdaPSFake.Sw$n"] | Should -Be '2.0.0' -Because "LambdaPSFake.Sw$n settles on its higher version" }
        }
    }

    It 'returns DependencyNameMissing, naming the parent manifest, for a RequiredModules entry with no name' {
        $root = Join-Path $TestDrive 'closure-nameless'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Nameless' -Version '1.0.0' -Requires "@('')"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.Nameless'; RequiredVersion = '1.0.0' } -SearchPath $root
            $closure.Error.FullyQualifiedErrorId | Should -Be 'DependencyNameMissing'
            $closure.Error.Exception.Message | Should -BeLike "*'LambdaPSFake.Nameless' 1.0.0 has a RequiredModules entry with no module name*$([System.IO.Path]::Combine($root, 'LambdaPSFake.Nameless', '1.0.0'))*"
        }
    }

    It 'still resolves a parent whose RequiredModules entries all have names' {
        $root = Join-Path $TestDrive 'closure-named'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Named' -Version '1.0.0' -Requires "@('Microsoft.PowerShell.Utility', @{ ModuleName = 'LambdaPSFake.NamedDep'; RequiredVersion = '1.0.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.NamedDep' -Version '1.0.0'

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.Named'; RequiredVersion = '1.0.0' } -SearchPath $root
            $closure.Error | Should -BeNullOrEmpty
            @($closure.Resolved.Name) | Should -Be @('LambdaPSFake.Named', 'LambdaPSFake.NamedDep')
        }
    }

    It 'returns ModuleVersionSpelling when the higher spelling breaks the minimum''s MaximumVersion, and resolves it within the maximum' {
        $root = Join-Path $TestDrive 'closure-spelling-max'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.MaxX' -Version '1.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.MaxTight' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.MaxX'; ModuleVersion = '1.0'; MaximumVersion = '1.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.MaxRoomy' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.MaxX'; ModuleVersion = '1.0'; MaximumVersion = '1.9' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $tight = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.MaxTight'; RequiredVersion = '1.0.0' } -SearchPath $root
            $tight.Error.FullyQualifiedErrorId | Should -Be 'ModuleVersionSpelling'
            $tight.Error.Exception.Message | Should -BeLike "*needed at '1.0' to '1.0'*has ModuleVersion '1.0.0', which is above that maximum*"
            $tight.Error.Exception.Message | Should -Not -BeLike '*matches versions exactly*'
            $roomy = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.MaxRoomy'; RequiredVersion = '1.0.0' } -SearchPath $root
            $roomy.Error | Should -BeNullOrEmpty
            @($roomy.Resolved | ForEach-Object { "$($_.Name) $($_.Version)" }) | Should -Contain 'LambdaPSFake.MaxX 1.0.0'
        }
    }

    It 'checks an exact pin on a version a minimum met with another spelling, in <Order> order' -ForEach @(
        @{ Order = 'minimum-first'; First = 'LambdaPSFake.OrdMin'; Second = 'LambdaPSFake.OrdExact' }
        @{ Order = 'exact-first'; First = 'LambdaPSFake.OrdExact'; Second = 'LambdaPSFake.OrdMin' }
    ) {
        $root = Join-Path $TestDrive "closure-order-$Order"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.OrdB' -Version '1.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.OrdMin' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.OrdB'; ModuleVersion = '1.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.OrdExact' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.OrdB'; RequiredVersion = '1.0' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root; first = $First; second = $Second } {
            param($root, $first, $second)
            $specs = @(@{ ModuleName = $first; RequiredVersion = '1.0.0' }, @{ ModuleName = $second; RequiredVersion = '1.0.0' })
            $closure = Get-LambdaPSModuleClosure -Module $specs -SearchPath $root
            $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleVersionSpelling' -Because 'the exact 1.0 pin cannot load a 1.0.0 manifest, whatever the order'
        }
    }

    It 'names the module that requires the parent when the parent was reached as a dependency' {
        $root = Join-Path $TestDrive 'closure-transitive-advice'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.AdvMid' -Version '1.0.0' -Requires "@('LambdaPSFake.AdvBare')"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.AdvTop' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.AdvMid'; RequiredVersion = '1.0.0' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.AdvTop'; RequiredVersion = '1.0.0' } -SearchPath $root
            $closure.Error.FullyQualifiedErrorId | Should -Be 'DependencyVersionUnpinned'
            $closure.Error.Exception.Message | Should -BeLike "*. Choose another version of LambdaPSFake.AdvTop 1.0.0, the module that requires 'LambdaPSFake.AdvMid'*a version (RequiredVersion or ModuleVersion), or fix that RequiredModules entry*"
        }
    }

    It 'checks a second minimum''s MaximumVersion on a version another minimum met with a higher spelling, in <Order> order' -ForEach @(
        @{ Order = 'loose-first'; Specs = @('LambdaPSFake.MoLoose', 'LambdaPSFake.MoTight'); Fails = $true }
        @{ Order = 'tight-first'; Specs = @('LambdaPSFake.MoTight', 'LambdaPSFake.MoLoose'); Fails = $true }
        @{ Order = 'loose then roomy'; Specs = @('LambdaPSFake.MoLoose', 'LambdaPSFake.MoRoomy'); Fails = $false }
        @{ Order = 'roomy then loose'; Specs = @('LambdaPSFake.MoRoomy', 'LambdaPSFake.MoLoose'); Fails = $false }
    ) {
        $root = Join-Path $TestDrive "closure-max-order-$($Order -replace ' ', '-')"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.MoX' -Version '1.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.MoLoose' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.MoX'; ModuleVersion = '1.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.MoTight' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.MoX'; ModuleVersion = '1.0'; MaximumVersion = '1.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.MoRoomy' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.MoX'; ModuleVersion = '1.0'; MaximumVersion = '1.9' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root; specs = $Specs; fails = $Fails } {
            param($root, $specs, $fails)
            $closure = Get-LambdaPSModuleClosure -Module @($specs | ForEach-Object { @{ ModuleName = $_; RequiredVersion = '1.0.0' } }) -SearchPath $root
            if ($fails) { $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleVersionSpelling' }
            else { $closure.Error | Should -BeNullOrEmpty }
        }
    }

    It 'names the module that requires the parent in DependencyNameMissing advice, and the parent when it was requested' {
        $root = Join-Path $TestDrive 'closure-nameless-advice'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.NaMid' -Version '1.0.0' -Requires "@('')"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.NaTop' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.NaMid'; RequiredVersion = '1.0.0' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $via = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.NaTop'; RequiredVersion = '1.0.0' } -SearchPath $root
            $via.Error.FullyQualifiedErrorId | Should -Be 'DependencyNameMissing'
            $via.Error.Exception.Message | Should -BeLike "*. Choose another version of LambdaPSFake.NaTop 1.0.0, the module that requires 'LambdaPSFake.NaMid'*names every module it requires, or fix that RequiredModules entry*"
            $direct = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.NaMid'; RequiredVersion = '1.0.0' } -SearchPath $root
            $direct.Error.Exception.Message | Should -BeLike "*. Pin a version of 'LambdaPSFake.NaMid' whose manifest names every module it requires, or fix that RequiredModules entry*"
        }
    }

    It 'walks a dependency cycle once per module' {
        $root = Join-Path $TestDrive 'closure-cycle'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.CycleA' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.CycleB'; RequiredVersion = '1.0.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.CycleB' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.CycleA'; RequiredVersion = '1.0.0' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.CycleA'; RequiredVersion = '1.0.0' } -SearchPath $root
            $closure.Error | Should -BeNullOrEmpty
            @($closure.Resolved.Key) | Should -Be @('LambdaPSFake.CycleA 1.0.0', 'LambdaPSFake.CycleB 1.0.0')
        }
    }

    It 'returns ModuleClosureNotSettled when the versions keep changing' {
        InModuleScope $script:moduleName {
            Mock 'Find-LambdaPSModule'
            # A choice the walk never reaches, so every pass asks for another.
            Mock 'Select-LambdaPSModuleVersion' -MockWith { '0.0.1' }
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' }
            $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleClosureNotSettled'
            Should -Invoke 'Select-LambdaPSModuleVersion' -Times 10 -Exactly
        }
    }

    It 'returns (not throws) ModuleVersionRequired for an unpinned request, before walking anything' {
        InModuleScope $script:moduleName {
            Mock 'Find-LambdaPSModule'
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.A'; ModuleVersion = '1.0.0' }
            $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleVersionRequired'
            Should -Invoke 'Find-LambdaPSModule' -Times 0 -Exactly
        }
    }

    It 'returns AWSPowerShellModuleNotSupported for <Case>, before walking anything' -ForEach @(
        @{ Case = "a pinned 'AWSPowerShell' hashtable"; Spec = @{ ModuleName = 'AWSPowerShell'; RequiredVersion = '4.1.0' }; Name = 'AWSPowerShell' }
        @{ Case = "a pinned 'AWSPowerShell.NetCore' hashtable"; Spec = @{ ModuleName = 'AWSPowerShell.NetCore'; RequiredVersion = '4.1.0' }; Name = 'AWSPowerShell.NetCore' }
        @{ Case = "an unpinned 'AWSPowerShell' hashtable"; Spec = @{ ModuleName = 'AWSPowerShell'; ModuleVersion = '4.1.0' }; Name = 'AWSPowerShell' }
        @{ Case = "the string 'AWSPowerShell'"; Spec = 'AWSPowerShell'; Name = 'AWSPowerShell' }
        @{ Case = "the string 'AWSPowerShell.NetCore'"; Spec = 'AWSPowerShell.NetCore'; Name = 'AWSPowerShell.NetCore' }
        @{ Case = "the string 'awspowershell.netcore' in another case"; Spec = 'awspowershell.netcore'; Name = 'awspowershell.netcore' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Spec = $Spec; Name = $Name } {
            param($Spec, $Name)
            Mock 'Find-LambdaPSModule'
            $closure = Get-LambdaPSModuleClosure -Module @(@{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' }, $Spec)
            $closure.Error.FullyQualifiedErrorId | Should -Be 'AWSPowerShellModuleNotSupported'
            $closure.Error.CategoryInfo.Category | Should -Be 'InvalidArgument'
            $closure.Error.TargetObject | Should -Be $Name
            $closure.Error.Exception.Message | Should -BeLike "Module '$Name' (-IncludeModule or -Module) cannot be packaged for Lambda: *. Require the AWS.Tools.* modules *"
            $desktop = '*AWSPowerShell is the Windows PowerShell (Desktop edition) module and does not run in PowerShell 7 on Lambda*'
            $netCore = "*AWSPowerShell.NetCore holds every AWS service (over 600 MB installed): more than Lambda's 250 MB limit for a .zip package and its layers, and in a container image, importing it takes over 10 seconds and over 1 GB of memory on every cold start*"
            # Each module's own reason, and not the other's: -eq ignores case, as the check does.
            $reason, $other = if ($Name -eq 'AWSPowerShell') { $desktop, $netCore } else { $netCore, $desktop }
            $closure.Error.Exception.Message | Should -BeLike $reason
            $closure.Error.Exception.Message | Should -Not -BeLike $other
            @($closure.Resolved).Count | Should -Be 0
            Should -Invoke 'Find-LambdaPSModule' -Times 0 -Exactly
        }
    }

    It 'gives each module its own reason when -Module names both' {
        InModuleScope $script:moduleName {
            $closure = Get-LambdaPSModuleClosure -Module 'AWSPowerShell', 'AWSPowerShell.NetCore'
            $closure.Error.FullyQualifiedErrorId | Should -Be 'AWSPowerShellModuleNotSupported'
            $closure.Error.Exception.Message | Should -BeLike "Modules 'AWSPowerShell' and 'AWSPowerShell.NetCore' (-IncludeModule or -Module) cannot*"
            $closure.Error.Exception.Message | Should -BeLike '*AWSPowerShell is the Windows PowerShell (Desktop edition) module and does not run in PowerShell 7 on Lambda*'
            $closure.Error.Exception.Message | Should -BeLike "*AWSPowerShell.NetCore holds every AWS service (over 600 MB installed): more than Lambda's 250 MB limit for a .zip package and its layers, and in a container image, importing it takes over 10 seconds and over 1 GB of memory on every cold start*"
        }
    }

    It 'returns ModuleNameInvalid for <Case>, before walking anything' -ForEach @(
        @{ Case = "the wildcard 'AWSPowerShell.Net*'"; Name = 'AWSPowerShell.Net*' }
        @{ Case = "the wildcard 'AWS.Tools.*'"; Name = 'AWS.Tools.*' }
        @{ Case = "the wildcard 'LambdaPSFake.?'"; Name = 'LambdaPSFake.?' }
        @{ Case = "the wildcard 'LambdaPSFake.[AB]'"; Name = 'LambdaPSFake.[AB]' }
        @{ Case = 'a manifest path'; Name = 'manifest' }
        @{ Case = 'a relative path'; Name = 'modules/LambdaPSFake.A' }
        @{ Case = "'.', read as the current folder"; Name = '.' }
        @{ Case = "'..', read as the parent folder"; Name = '..' }
    ) {
        if ($Name -eq 'manifest') {
            $root = Join-Path $TestDrive 'closure-name-path'
            $null = New-FakeModule -Root $root -Name 'LambdaPSFake.A' -Version '1.0.0'
            $Name = [System.IO.Path]::Combine($root, 'LambdaPSFake.A', '1.0.0', 'LambdaPSFake.A.psd1')
        }
        InModuleScope $script:moduleName -Parameters @{ Name = $Name } {
            param($Name)
            Mock 'Find-LambdaPSModule'
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = $Name; RequiredVersion = '1.0.0' }
            $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleNameInvalid'
            $closure.Error.CategoryInfo.Category | Should -Be 'InvalidArgument'
            $closure.Error.TargetObject | Should -Be $Name
            $closure.Error.Exception.Message | Should -BeLike "Module '$([WildcardPattern]::Escape($Name))' is a wildcard or a path, not a module name.*"
            Should -Invoke 'Find-LambdaPSModule' -Times 0 -Exactly
        }
    }

    It 'accepts a dotted module name with no wildcard or path (<Name>)' -ForEach @(
        @{ Name = 'LambdaPSFake.Not-Installed_1' }
        @{ Name = 'LambdaPSFake..Two' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Name = $Name } {
            param($Name)
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = $Name; RequiredVersion = '1.0.0' }
            $closure.Error | Should -BeNullOrEmpty
            @($closure.Missing.Key) | Should -Be @("$Name 1.0.0")
        }
    }

    It 'names a module given twice, in another case, once' {
        InModuleScope $script:moduleName {
            $closure = Get-LambdaPSModuleClosure -Module 'AWSPowerShell', 'awspowershell'
            $closure.Error.Exception.Message | Should -BeLike "Module 'AWSPowerShell' (-IncludeModule or -Module) cannot*"
        }
    }

    It 'checks the AWSPowerShell names exactly, so a module whose name only starts with one is not refused' {
        InModuleScope $script:moduleName {
            $closure = Get-LambdaPSModuleClosure -Module 'AWSPowerShell.Extensions'
            $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleVersionRequired'
        }
    }

    It 'says a dependency with only a MaximumVersion has no pin, not no version' {
        $root = Join-Path $TestDrive 'closure-maximum-only'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Top' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.Dep'; MaximumVersion = '1.9.0' })"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.Top'; RequiredVersion = '1.0.0' } -SearchPath $root
            $closure.Error.FullyQualifiedErrorId | Should -Be 'DependencyVersionUnpinned'
            $closure.Error.Exception.Message | Should -BeLike ("Dependency 'LambdaPSFake.Dep' of module 'LambdaPSFake.Top' 1.0.0 has only a " +
                "MaximumVersion ('1.9.0'), which is not a pin. *")
        }
    }

    Context 'a requirement that names a GUID' {
        BeforeAll {
            $script:guidRoot = Join-Path $TestDrive 'closure-guid'
            $null = New-FakeModule -Root $script:guidRoot -Name 'LambdaPSFake.Guid' -Version '1.0.0'
            $script:realGuid = [guid](Import-PowerShellDataFile -LiteralPath ([System.IO.Path]::Combine($script:guidRoot, 'LambdaPSFake.Guid', '1.0.0', 'LambdaPSFake.Guid.psd1'))).GUID
            $script:madeUpGuid = [guid]'0badc0de-0000-4000-8000-000000000001'
        }

        It 'resolves the module when -Module names its own GUID' {
            InModuleScope $script:moduleName -Parameters @{ root = $script:guidRoot; guid = $script:realGuid } {
                param($root, $guid)
                $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.Guid'; RequiredVersion = '1.0.0'; Guid = $guid } -SearchPath $root
                $closure.Error | Should -BeNullOrEmpty
                @($closure.Resolved.Key) | Should -Be @('LambdaPSFake.Guid 1.0.0')
            }
        }

        It 'returns ModuleGuidMismatch when -Module names another GUID' {
            InModuleScope $script:moduleName -Parameters @{ root = $script:guidRoot; guid = $script:madeUpGuid; real = $script:realGuid } {
                param($root, $guid, $real)
                $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.Guid'; RequiredVersion = '1.0.0'; Guid = $guid } -SearchPath $root
                $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleGuidMismatch'
                $closure.Error.TargetObject | Should -Be 'LambdaPSFake.Guid 1.0.0'
                $closure.Error.Exception.Message | Should -BeLike "Module 'LambdaPSFake.Guid' 1.0.0 is required with GUID $guid (requested), but the module 'LambdaPSFake.Guid' 1.0.0 found has GUID $real, so it is a different module.*"
                @($closure.Resolved).Count | Should -Be 0
            }
        }

        It 'returns ModuleGuidMismatch for a RequiredModules entry that names <Case>, and resolves one that names its own' -ForEach @(
            @{ Case = 'another GUID'; Wrong = $true }
            @{ Case = 'the right GUID'; Wrong = $false }
        ) {
            $root = Join-Path $TestDrive "closure-guid-dep-$Wrong"
            $null = New-FakeModule -Root $root -Name 'LambdaPSFake.GuidDep' -Version '2.0.0'
            $real = [guid](Import-PowerShellDataFile -LiteralPath ([System.IO.Path]::Combine($root, 'LambdaPSFake.GuidDep', '2.0.0', 'LambdaPSFake.GuidDep.psd1'))).GUID
            $named = if ($Wrong) { $script:madeUpGuid } else { $real }
            $null = New-FakeModule -Root $root -Name 'LambdaPSFake.GuidTop' -Version '1.0.0' `
                -Requires "@(@{ ModuleName = 'LambdaPSFake.GuidDep'; RequiredVersion = '2.0.0'; GUID = '$named' })"

            InModuleScope $script:moduleName -Parameters @{ root = $root; wrong = $Wrong } {
                param($root, $wrong)
                $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.GuidTop'; RequiredVersion = '1.0.0' } -SearchPath $root
                if ($wrong) {
                    $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleGuidMismatch'
                    $closure.Error.Exception.Message | Should -BeLike "*(required by LambdaPSFake.GuidTop 1.0.0)*"
                }
                else {
                    $closure.Error | Should -BeNullOrEmpty
                    @($closure.Resolved.Key) | Should -Be @('LambdaPSFake.GuidTop 1.0.0', 'LambdaPSFake.GuidDep 2.0.0')
                }
            }
        }

        It 'returns ModuleGuidMismatch when a later requirement names another GUID for a module already resolved' {
            $root = Join-Path $TestDrive 'closure-guid-revisit'
            $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Shared' -Version '1.0.0'
            $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Wants' -Version '1.0.0' `
                -Requires "@(@{ ModuleName = 'LambdaPSFake.Shared'; RequiredVersion = '1.0.0'; GUID = '$($script:madeUpGuid)' })"

            InModuleScope $script:moduleName -Parameters @{ root = $root } {
                param($root)
                $specs = @(
                    @{ ModuleName = 'LambdaPSFake.Shared'; RequiredVersion = '1.0.0' }
                    @{ ModuleName = 'LambdaPSFake.Wants'; RequiredVersion = '1.0.0' }
                )
                $closure = Get-LambdaPSModuleClosure -Module $specs -SearchPath $root
                $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleGuidMismatch'
            }
        }
    }

    It 'returns ModuleNotFoundLocally for a missing module only with -RequireAll' {
        InModuleScope $script:moduleName {
            $spec = @{ ModuleName = 'LambdaPSFake.NotInstalled'; RequiredVersion = '1.0.0' }
            (Get-LambdaPSModuleClosure -Module $spec).Error | Should -BeNullOrEmpty
            $closure = Get-LambdaPSModuleClosure -Module $spec -RequireAll
            $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleNotFoundLocally'
            @($closure.Missing.Key) | Should -Be @('LambdaPSFake.NotInstalled 1.0.0')
            $closure.Error.TargetObject | Should -Be 'LambdaPSFake.NotInstalled 1.0.0'

            $two = @($spec, @{ ModuleName = 'LambdaPSFake.AlsoNotInstalled'; RequiredVersion = '2.0.0' })
            (Get-LambdaPSModuleClosure -Module $two -RequireAll).Error.TargetObject |
                Should -Be @('LambdaPSFake.NotInstalled 1.0.0', 'LambdaPSFake.AlsoNotInstalled 2.0.0')
        }
    }

    It 'treats a -Pending key as present without walking it' {
        InModuleScope $script:moduleName {
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' } -Pending 'LambdaPSFake.A 1.0.0'
            @($closure.Pending) | Should -Be @('LambdaPSFake.A 1.0.0')
            $closure.Missing.Count | Should -Be 0
        }
    }

    It 'returns (not throws) DependencyVersionUnpinned for a dependency with no version' {
        $root = Join-Path $TestDrive 'closure-unpinned'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.A' -Version '1.0.0' -Requires "@('LambdaPSFake.Bare')"

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $closure = Get-LambdaPSModuleClosure -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' } -SearchPath $root
            $closure.Error.FullyQualifiedErrorId | Should -Be 'DependencyVersionUnpinned'
            $closure.Error.Exception.Message | Should -BeLike "*. Pin a version of 'LambdaPSFake.A' whose manifest gives 'LambdaPSFake.Bare' a version (RequiredVersion or ModuleVersion), or fix that RequiredModules entry in 'LambdaPSFake.A' 1.0.0's manifest*"
        }
    }

    # Version conflicts and minimums across the requested set and its dependencies.
    It 'returns ModuleVersionConflict when two requests pin different versions of one module' {
        $root = Join-Path $TestDrive 'conflict-top'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.A' -Version '1.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.A' -Version '2.0.0'

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $specs = @(
                @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' }
                @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '2.0.0' }
            )
            $closure = Get-LambdaPSModuleClosure -Module $specs -SearchPath $root
            $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleVersionConflict'
            $closure.Error.Exception.Message | Should -BeLike "*'LambdaPSFake.A'*1.0.0 (requested)*2.0.0 (requested)*"
        }
    }

    It 'returns ModuleVersionConflict when dependencies need different versions, naming who needs each' {
        $root = Join-Path $TestDrive 'conflict-dep'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.A' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.Common'; RequiredVersion = '1.0.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.B' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.Common'; RequiredVersion = '2.0.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Common' -Version '1.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Common' -Version '2.0.0'

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $specs = @(
                @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' }
                @{ ModuleName = 'LambdaPSFake.B'; RequiredVersion = '1.0.0' }
            )
            $closure = Get-LambdaPSModuleClosure -Module $specs -SearchPath $root
            $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleVersionConflict'
            $closure.Error.Exception.Message | Should -BeLike '*required by LambdaPSFake.A 1.0.0*required by LambdaPSFake.B 1.0.0*'
        }
    }

    It 'satisfies a dependency''s ModuleVersion minimum with a higher pinned version' {
        $root = Join-Path $TestDrive 'minimum-pinned'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Top' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.Dep'; ModuleVersion = '1.1.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Dep' -Version '1.1.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Dep' -Version '1.2.0'

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $specs = @(
                @{ ModuleName = 'LambdaPSFake.Top'; RequiredVersion = '1.0.0' }
                @{ ModuleName = 'LambdaPSFake.Dep'; RequiredVersion = '1.2.0' }
            )
            $closure = Get-LambdaPSModuleClosure -Module $specs -SearchPath $root
            $closure.Error | Should -BeNullOrEmpty
            @($closure.Resolved.Key) | Sort-Object | Should -Be @('LambdaPSFake.Dep 1.2.0', 'LambdaPSFake.Top 1.0.0')
        }
    }

    It 'satisfies a minimum with the version another dependency requires exactly' {
        $root = Join-Path $TestDrive 'minimum-exact-dep'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.A' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.Common'; ModuleVersion = '1.0.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.B' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.Common'; RequiredVersion = '1.5.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Common' -Version '1.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Common' -Version '1.5.0'

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $specs = @(
                @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' }
                @{ ModuleName = 'LambdaPSFake.B'; RequiredVersion = '1.0.0' }
            )
            $closure = Get-LambdaPSModuleClosure -Module $specs -SearchPath $root
            $closure.Error | Should -BeNullOrEmpty
            @($closure.Resolved.Key | Where-Object { $_ -like 'LambdaPSFake.Common *' }) | Should -Be @('LambdaPSFake.Common 1.5.0')
        }
    }

    It 'resolves two minimums to the higher one' {
        $root = Join-Path $TestDrive 'minimum-two'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.A' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.Common'; ModuleVersion = '1.0.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.B' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.Common'; ModuleVersion = '1.1.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Common' -Version '1.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Common' -Version '1.1.0'

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $specs = @(
                @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' }
                @{ ModuleName = 'LambdaPSFake.B'; RequiredVersion = '1.0.0' }
            )
            $closure = Get-LambdaPSModuleClosure -Module $specs -SearchPath $root
            $closure.Error | Should -BeNullOrEmpty
            @($closure.Resolved.Key | Where-Object { $_ -like 'LambdaPSFake.Common *' }) | Should -Be @('LambdaPSFake.Common 1.1.0')
        }
    }

    It 'returns ModuleVersionConflict when a pin is below a dependency''s minimum, naming the range' {
        $root = Join-Path $TestDrive 'minimum-unmet'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Top' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.Dep'; ModuleVersion = '1.1.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Dep' -Version '1.0.0'

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $specs = @(
                @{ ModuleName = 'LambdaPSFake.Top'; RequiredVersion = '1.0.0' }
                @{ ModuleName = 'LambdaPSFake.Dep'; RequiredVersion = '1.0.0' }
            )
            $closure = Get-LambdaPSModuleClosure -Module $specs -SearchPath $root
            $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleVersionConflict'
            $closure.Error.Exception.Message | Should -BeLike '*1.0.0 (requested)*1.1.0 or later (required by LambdaPSFake.Top 1.0.0)*'
        }
    }

    It 'respects a dependency''s MaximumVersion' {
        $root = Join-Path $TestDrive 'minimum-max'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Top' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.Dep'; ModuleVersion = '1.0.0'; MaximumVersion = '1.*' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Dep' -Version '2.0.0'

        InModuleScope $script:moduleName -Parameters @{ root = $root } {
            param($root)
            $specs = @(
                @{ ModuleName = 'LambdaPSFake.Top'; RequiredVersion = '1.0.0' }
                @{ ModuleName = 'LambdaPSFake.Dep'; RequiredVersion = '2.0.0' }
            )
            $closure = Get-LambdaPSModuleClosure -Module $specs -SearchPath $root
            $closure.Error.FullyQualifiedErrorId | Should -Be 'ModuleVersionConflict'
            $closure.Error.Exception.Message | Should -BeLike '*1.0.0 to 1.**'
        }
    }
}
