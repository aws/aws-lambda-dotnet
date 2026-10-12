BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeModule.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Copy-LambdaPSModuleSet' -Tag 'Unit' {
    It 'copies a single no-dependency module to <Path>/<Name>/<Version> and returns its Name Version' {
        InModuleScope $script:moduleName {
            Mock 'Get-Module' -ParameterFilter { $FullyQualifiedName.Name -eq 'AWS.Tools.S3' } -MockWith {
                [PSCustomObject]@{
                    Name            = 'AWS.Tools.S3'
                    Version         = [version]'5.0.103'
                    ModuleBase      = [System.IO.Path]::Combine('/local', 'AWS.Tools.S3', '5.0.103')
                    RequiredModules = @()
                }
            }
            Mock 'Copy-LambdaPSModuleFolder' -MockWith { }

            $spec = @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }
            $result = Copy-LambdaPSModuleSet -Module $spec -Path ([System.IO.Path]::Combine($TestDrive, 'modules'))

            $result | Should -Contain 'AWS.Tools.S3 5.0.103'
            $expectedDestination = [System.IO.Path]::Combine(
                [System.IO.Path]::GetFullPath(([System.IO.Path]::Combine($TestDrive, 'modules'))), 'AWS.Tools.S3', '5.0.103')
            Should -Invoke 'Copy-LambdaPSModuleFolder' -Times 1 -Exactly -ParameterFilter {
                $Destination -eq $expectedDestination -and $Path -eq [System.IO.Path]::Combine('/local', 'AWS.Tools.S3', '5.0.103')
            }
        }
    }

    It 'resolves and copies a transitive RequiredModules dependency and returns it too' {
        InModuleScope $script:moduleName {
            Mock 'Get-Module' -ParameterFilter { $FullyQualifiedName.Name -eq 'AWS.Tools.S3' } -MockWith {
                [PSCustomObject]@{
                    Name            = 'AWS.Tools.S3'
                    Version         = [version]'5.0.103'
                    ModuleBase      = [System.IO.Path]::Combine('/local', 'AWS.Tools.S3', '5.0.103')
                    RequiredModules = @([PSCustomObject]@{ Name = 'AWS.Tools.Common'; Version = [version]'5.0.103' })
                }
            }
            Mock 'Get-Module' -ParameterFilter { $FullyQualifiedName.Name -eq 'AWS.Tools.Common' } -MockWith {
                [PSCustomObject]@{
                    Name            = 'AWS.Tools.Common'
                    Version         = [version]'5.0.103'
                    ModuleBase      = [System.IO.Path]::Combine('/local', 'AWS.Tools.Common', '5.0.103')
                    RequiredModules = @()
                }
            }
            Mock 'Copy-LambdaPSModuleFolder' -MockWith { }

            $spec = @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }
            $result = Copy-LambdaPSModuleSet -Module $spec -Path ([System.IO.Path]::Combine($TestDrive, 'modules'))

            $result | Should -Contain 'AWS.Tools.S3 5.0.103'
            $result | Should -Contain 'AWS.Tools.Common 5.0.103'
            Should -Invoke 'Copy-LambdaPSModuleFolder' -Times 2 -Exactly
        }
    }

    It 'throws ModuleNotFoundLocally when the module is not installed on this machine' {
        InModuleScope $script:moduleName {
            Mock 'Get-Module' -ParameterFilter { $FullyQualifiedName.Name -eq 'AWS.Tools.S3' } -MockWith { }
            Mock 'Copy-LambdaPSModuleFolder' -MockWith { }

            $spec = @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }
            { Copy-LambdaPSModuleSet -Module $spec -Path ([System.IO.Path]::Combine($TestDrive, 'modules')) } |
                Should -Throw -ErrorId 'ModuleNotFoundLocally'
        }
    }

    It 'throws ModuleVersionRequired for a minimum-version spec' {
        InModuleScope $script:moduleName {
            $spec = @{ ModuleName = 'AWS.Tools.S3'; ModuleVersion = '5.0.103' }
            { Copy-LambdaPSModuleSet -Module $spec -Path ([System.IO.Path]::Combine($TestDrive, 'modules')) } |
                Should -Throw -ErrorId 'ModuleVersionRequired'
        }
    }

    It 'throws ModuleVersionRequired for a version-range spec' {
        InModuleScope $script:moduleName {
            $spec = @{ ModuleName = 'AWS.Tools.S3'; ModuleVersion = '5.0.0'; MaximumVersion = '5.9.9' }
            { Copy-LambdaPSModuleSet -Module $spec -Path ([System.IO.Path]::Combine($TestDrive, 'modules')) } |
                Should -Throw -ErrorId 'ModuleVersionRequired'
        }
    }

    It 'throws DependencyVersionUnpinned when a RequiredModules entry has no version' {
        InModuleScope $script:moduleName {
            Mock 'Get-Module' -ParameterFilter { $FullyQualifiedName.Name -eq 'AWS.Tools.S3' } -MockWith {
                [PSCustomObject]@{
                    Name            = 'AWS.Tools.S3'
                    Version         = [version]'5.0.103'
                    ModuleBase      = [System.IO.Path]::Combine('/local', 'AWS.Tools.S3', '5.0.103')
                    RequiredModules = @([PSCustomObject]@{ Name = 'AWS.Tools.Common'; Version = $null })
                }
            }
            Mock 'Copy-LambdaPSModuleFolder' -MockWith { }

            $spec = @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }
            { Copy-LambdaPSModuleSet -Module $spec -Path ([System.IO.Path]::Combine($TestDrive, 'modules')) } |
                Should -Throw -ErrorId 'DependencyVersionUnpinned'
        }
    }
}

Describe 'Copy-LambdaPSModuleSet -SearchPath / -Pending' -Tag 'Unit' {
    It 'returns the key of the folder it wrote, in the module''s own version spelling' {
        $root = Join-Path $TestDrive 'copy-two-part'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.Two' -Version '1.0'
        $target = Join-Path $TestDrive 'copy-two-part-out'

        InModuleScope $script:moduleName -Parameters @{ root = $root; target = $target } {
            param($root, $target)
            $staged = Copy-LambdaPSModuleSet -Module @{ ModuleName = 'lambdapsfake.two'; RequiredVersion = '1.0' } -Path $target -SearchPath $root
            $staged | Should -Be @('LambdaPSFake.Two 1.0')
            $name, $version = $staged[0] -split ' ', 2
            [System.IO.Path]::Combine($target, $name, $version, "$name.psd1") | Should -Exist
        }
    }

    It 'stages a module and its dependency from a search folder' {
        $root = Join-Path $TestDrive 'copy-src'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.A' -Version '1.0.0' `
            -Requires "@(@{ ModuleName = 'LambdaPSFake.B'; RequiredVersion = '2.0.0' })"
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.B' -Version '2.0.0'
        $target = Join-Path $TestDrive 'copy-dst'

        InModuleScope $script:moduleName -Parameters @{ root = $root; target = $target } {
            param($root, $target)
            $staged = Copy-LambdaPSModuleSet -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' } -Path $target -SearchPath $root
            $staged | Should -Be @('LambdaPSFake.A 1.0.0', 'LambdaPSFake.B 2.0.0')
            Test-Path -LiteralPath ([System.IO.Path]::Combine($target, 'LambdaPSFake.B', '2.0.0', 'LambdaPSFake.B.psd1')) | Should -BeTrue
        }
    }

    It 'names every missing module and copies nothing' {
        $target = Join-Path $TestDrive 'copy-missing'
        InModuleScope $script:moduleName -Parameters @{ target = $target } {
            param($target)
            $specs = @(
                @{ ModuleName = 'LambdaPSFake.X'; RequiredVersion = '1.0.0' }
                @{ ModuleName = 'LambdaPSFake.Y'; RequiredVersion = '1.0.0' }
            )
            $err = { Copy-LambdaPSModuleSet -Module $specs -Path $target } | Should -Throw -PassThru -ErrorId 'ModuleNotFoundLocally'
            $err.Exception.Message | Should -BeLike "*'LambdaPSFake.X' 1.0.0, 'LambdaPSFake.Y' 1.0.0*"
            $err.Exception.Message | Should -BeLike '*-MissingModuleAction*'
            Test-Path -LiteralPath $target | Should -BeFalse
        }
    }

    It 'previews a pending module instead of failing' {
        $target = Join-Path $TestDrive 'copy-pending'
        InModuleScope $script:moduleName -Parameters @{ target = $target } {
            param($target)
            $staged = Copy-LambdaPSModuleSet -Module @{ ModuleName = 'LambdaPSFake.X'; RequiredVersion = '1.0.0' } -Path $target `
                -Pending 'LambdaPSFake.X 1.0.0' -WhatIf
            $staged | Should -Be @('LambdaPSFake.X 1.0.0')
            Test-Path -LiteralPath $target | Should -BeFalse
        }
    }

    It 'stages nothing when the set conflicts' {
        $root = Join-Path $TestDrive 'conflict-copy'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.A' -Version '1.0.0'
        $null = New-FakeModule -Root $root -Name 'LambdaPSFake.A' -Version '2.0.0'
        $target = Join-Path $TestDrive 'conflict-copy-out'

        InModuleScope $script:moduleName -Parameters @{ root = $root; target = $target } {
            param($root, $target)
            $specs = @(
                @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' }
                @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '2.0.0' }
            )
            { Copy-LambdaPSModuleSet -Module $specs -Path $target -SearchPath $root } |
                Should -Throw -ErrorId 'ModuleVersionConflict'
            Test-Path -LiteralPath $target | Should -BeFalse
        }
    }
}

# -Confirm and -WhatIf run in a child pwsh, whose console answers the prompts.
Describe 'Copy-LambdaPSModuleSet -Confirm and -WhatIf' -Tag 'Unit' {
    BeforeAll {
        $script:setRoot = Join-Path $TestDrive 'confirm-set'
        foreach ($n in 'LambdaPSFake.C1', 'LambdaPSFake.C2', 'LambdaPSFake.C3') {
            $null = New-FakeModule -Root $script:setRoot -Name $n -Version '1.0.0'
        }
        $script:copySet = {
            param([string]$Target, [string]$Switch)
            "`$null = & (Get-Module AWS.ToolExtras.LambdaPSRuntime) { Copy-LambdaPSModuleSet -Module @(" +
            "@{ ModuleName = 'LambdaPSFake.C1'; RequiredVersion = '1.0.0' }, @{ ModuleName = 'LambdaPSFake.C2'; RequiredVersion = '1.0.0' }, " +
            "@{ ModuleName = 'LambdaPSFake.C3'; RequiredVersion = '1.0.0' }) -Path '$Target' -SearchPath '$($script:setRoot)' $Switch }"
        }
    }

    # The prompt text does not reach redirected stdout on every platform, so these assert what was copied.
    # A prompt per module would take an 'N' and leave that module out.
    It 'asks once for the whole set under -Confirm: one yes copies every module' {
        $target = Join-Path $TestDrive 'confirm-yes'
        $null = Invoke-InChildPwsh -Command (& $script:copySet $target '-Confirm') -Answer 'Y', 'N', 'N'

        foreach ($n in 'LambdaPSFake.C1', 'LambdaPSFake.C2', 'LambdaPSFake.C3') {
            [System.IO.Path]::Combine($target, $n, '1.0.0', "$n.psd1") | Should -Exist
        }
    }

    It 'copies nothing when the set is declined' {
        $target = Join-Path $TestDrive 'confirm-no'
        # 'N' declines the set; the 'Y's would let a per-module prompt copy the rest.
        $null = Invoke-InChildPwsh -Command (& $script:copySet $target '-Confirm') -Answer 'N', 'Y', 'Y'

        @(Get-ChildItem -LiteralPath $target -Recurse -File -ErrorAction SilentlyContinue).Count | Should -Be 0
    }

    # The result is what was staged, so a caller does not report a module folder that was never written.
    It 'returns no modules when the set is declined' {
        $target = Join-Path $TestDrive 'confirm-no-result'
        $command = (& $script:copySet $target '-Confirm').Replace('$null = ', '$r = ') + "; 'STAGED:' + (@(`$r) -join ',')"
        $result = Invoke-InChildPwsh -Command $command -Answer 'N', 'Y', 'Y'

        $result.Output -split "`n" | Should -Contain 'STAGED:'
    }

    # A module whose download was declined is not on disk, so a real run must not take -Pending for it.
    It 'stops with ModuleNotFoundLocally, outside -WhatIf, for a module named only in -Pending' {
        $target = Join-Path $TestDrive 'pending-real-run'
        InModuleScope $script:moduleName -Parameters @{ target = $target } {
            param($target)
            $spec = @{ ModuleName = 'LambdaPSFake.NotOnDisk'; RequiredVersion = '1.0.0' }
            { Copy-LambdaPSModuleSet -Module $spec -Path $target -Pending 'LambdaPSFake.NotOnDisk 1.0.0' } |
                Should -Throw -ErrorId 'ModuleNotFoundLocally'
        }
        $target | Should -Not -Exist
    }

    It 'lists each module under -WhatIf and copies nothing' {
        $target = Join-Path $TestDrive 'whatif-set'
        $result = Invoke-InChildPwsh -Command (& $script:copySet $target '-WhatIf')

        @([regex]::Matches($result.Output, "What if: Performing the operation `"Copy module 'LambdaPSFake\.C\d'")).Count | Should -Be 3
        @(Get-ChildItem -LiteralPath $target -Recurse -File -ErrorAction SilentlyContinue).Count | Should -Be 0
    }

    # The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
    # surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
    It 'surfaces <ErrorId> (<Case>) from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'ModuleVersionRequired'; Case = 'closure error'; Call = { Copy-LambdaPSModuleSet -Module @{ ModuleName = 'AWS.Tools.S3'; ModuleVersion = '5.0.103' } -Path (Join-Path $Arg 'modules') } }
    ) {
        $arg = if ($Prepare) { & $Prepare } else { $TestDrive }
        InModuleScope $script:moduleName -Parameters @{ ErrorId = $ErrorId; Setup = "$Setup"; Call = "$Call"; Arg = $arg } {
            param($ErrorId, $Setup, $Call, $Arg)
            . ([scriptblock]::Create($Setup))
            function Invoke-LambdaPSTestCaller {
                [CmdletBinding()]
                param([scriptblock]$Call)
                try { $null = & $Call }
                catch { $PSCmdlet.ThrowTerminatingError($_) }
            }
            $collected = $null
            $err = $null
            try { Invoke-LambdaPSTestCaller -Call ([scriptblock]::Create($Call)) -ErrorVariable collected } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be "$ErrorId,Invoke-LambdaPSTestCaller"
            $ids = @($collected | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.FullyQualifiedErrorId } else { $_.ErrorRecord.FullyQualifiedErrorId } })
            $ids | Should -Contain "$ErrorId,Invoke-LambdaPSTestCaller"
            @($ids | Where-Object { $_ -like '*,Copy-LambdaPSModuleSet' }) | Should -BeNullOrEmpty
        }
    }
}
