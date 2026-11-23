BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Save-LambdaPSModuleFromRepository' -Tag 'Unit' {
    It 'calls Save-PSResource with an exact version, no dependency resolution, and no trust bypass' {
        InModuleScope $script:moduleName -Parameters @{ dl = (Join-Path $TestDrive 'spr') } {
            param($dl)
            Mock 'Save-PSResource' -MockWith { [PSCustomObject]@{ Name = $Name; Version = [version]'1.0.0'; Repository = 'PSGallery' } }

            $saved = Save-LambdaPSModuleFromRepository -Name 'LambdaPSFake.A' -VersionRange '[1.0.0]' -Path $dl

            $saved.Repository | Should -Be 'PSGallery'
            Test-Path -LiteralPath $dl | Should -BeTrue
            Should -Invoke 'Save-PSResource' -Times 1 -Exactly -ParameterFilter {
                $Name -eq 'LambdaPSFake.A' -and $Version -eq '[1.0.0]' -and $Path -eq $dl -and $SkipDependencyCheck -and $IncludeXml -and
                $PassThru -and -not $TrustRepository -and -not $AuthenticodeCheck
            }
        }
    }

    It 'names the repositories to search when -ModuleRepository is given, and none otherwise' {
        InModuleScope $script:moduleName -Parameters @{ dl = (Join-Path $TestDrive 'spr-repo') } {
            param($dl)
            Mock 'Save-PSResource' -MockWith { [PSCustomObject]@{ Name = $Name; Version = [version]'1.0.0'; Repository = 'Internal' } }
            Mock 'Get-PSResourceRepository' -MockWith { [PSCustomObject]@{ Name = 'Internal' }, [PSCustomObject]@{ Name = 'PSGallery' } }

            $null = Save-LambdaPSModuleFromRepository -Name 'LambdaPSFake.A' -VersionRange '[1.0.0]' -Path $dl -ModuleRepository 'Internal', 'PSGallery'
            $null = Save-LambdaPSModuleFromRepository -Name 'LambdaPSFake.A' -VersionRange '[1.0.0]' -Path $dl

            Should -Invoke 'Save-PSResource' -Times 1 -Exactly -ParameterFilter { ($Repository -join ',') -eq 'Internal,PSGallery' }
            Should -Invoke 'Save-PSResource' -Times 1 -Exactly -ParameterFilter { $null -eq $Repository }
        }
    }

    It 'passes -AuthenticodeCheck to Save-PSResource when asked' {
        InModuleScope $script:moduleName -Parameters @{ dl = (Join-Path $TestDrive 'spr-signed') } {
            param($dl)
            Mock 'Save-PSResource' -MockWith { [PSCustomObject]@{ Name = $Name; Version = [version]'2.0.6'; Repository = 'PSGallery' } }
            $null = Save-LambdaPSModuleFromRepository -Name 'AWS.Tools.Installer' -VersionRange '[2.0.5,3.0)' -Path $dl -AuthenticodeCheck
            Should -Invoke 'Save-PSResource' -Times 1 -Exactly -ParameterFilter { $AuthenticodeCheck }
        }
    }

    It 'throws ModuleRepositoryNotFound for an unregistered repository, before downloading anything' {
        InModuleScope $script:moduleName {
            Mock 'Save-PSResource' -MockWith { throw 'must not download' }
            Mock 'Get-PSResourceRepository' -MockWith { [PSCustomObject]@{ Name = 'PSGallery' } }
            { Save-LambdaPSModuleFromRepository -Name 'LambdaPSFake.A' -VersionRange '[1.0.0]' -Path $TestDrive -ModuleRepository 'no-such-repo' } |
                Should -Throw -ErrorId 'ModuleRepositoryNotFound'
            Should -Invoke 'Save-PSResource' -Times 0 -Exactly
        }
    }

    It 'passes -InstallerDefault on to the repository check, so the message names the PSGallery default' {
        InModuleScope $script:moduleName {
            Mock 'Save-PSResource' -MockWith { throw 'must not download' }
            Mock 'Get-PSResourceRepository' -MockWith { [PSCustomObject]@{ Name = 'InternalMirror' } }
            $err = { Save-LambdaPSModuleFromRepository -Name 'AWS.Tools.Installer' -VersionRange '[2.0.5,3.0)' -Path $TestDrive -ModuleRepository 'PSGallery' -InstallerDefault } |
                Should -Throw -ErrorId 'ModuleRepositoryNotFound' -PassThru
            $err.Exception.Message | Should -BeLike 'AWS.Tools.Installer is downloaded from PSGallery when -ModuleRepository is not given*'
            Should -Invoke 'Save-PSResource' -Times 0 -Exactly
        }
    }

    It 'throws ModuleInstallerNotFound when PSResourceGet is not available' {
        InModuleScope $script:moduleName {
            Mock 'Get-Command' -ParameterFilter { $Name -eq 'Save-PSResource' } -MockWith { }
            { Save-LambdaPSModuleFromRepository -Name 'LambdaPSFake.A' -VersionRange '[1.0.0]' -Path $TestDrive } |
                Should -Throw -ErrorId 'ModuleInstallerNotFound'
        }
    }

    It 'throws ModuleDownloadFailed when PSResourceGet reports no module' {
        InModuleScope $script:moduleName {
            Mock 'Save-PSResource' -MockWith { }
            $err = { Save-LambdaPSModuleFromRepository -Name 'LambdaPSFake.A' -VersionRange '[1.0.0]' -Path $TestDrive } |
                Should -Throw -ErrorId 'ModuleDownloadFailed' -PassThru
            $err.TargetObject | Should -Be 'LambdaPSFake.A 1.0.0'
            $err.Exception | Should -BeOfType [System.InvalidOperationException]
        }
    }

    It 'names the repositories it used, and removes the -Path folder it created, when the download fails (<Case>)' -ForEach @(
        @{ Case = 'no -ModuleRepository'; Repository = @(); From = 'the registered repositories' }
        @{ Case = 'one repository'; Repository = @('Internal'); From = "repository 'Internal'" }
        @{ Case = 'two repositories'; Repository = @('Internal', 'Mirror'); From = "repositories 'Internal', 'Mirror'" }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Repository = $Repository; From = $From } {
            param($Repository, $From)
            Mock 'Get-PSResourceRepository' -MockWith { [PSCustomObject]@{ Name = 'Internal' }, [PSCustomObject]@{ Name = 'Mirror' } }
            Mock 'Save-PSResource' -MockWith { throw 'not found' }
            $target = Join-Path $TestDrive "created-$([guid]::NewGuid().ToString('N'))"
            $repositoryParam = if ($Repository.Count -gt 0) { @{ ModuleRepository = $Repository } } else { @{} }

            $err = { Save-LambdaPSModuleFromRepository -Name 'LambdaPSFake.A' -VersionRange '[1.0.0]' -Path $target @repositoryParam } |
                Should -Throw -ErrorId 'ModuleDownloadFailed' -PassThru
            $err.Exception.Message | Should -BeLike "Could not download module 'LambdaPSFake.A' ``[1.0.0``] from $From with PSResourceGet: not found*"
            Test-Path -LiteralPath $target | Should -BeFalse
        }
    }

    It 'leaves a -Path folder that was already there when the download fails' {
        InModuleScope $script:moduleName {
            Mock 'Save-PSResource' -MockWith { }
            $target = New-Item -ItemType Directory -Path (Join-Path $TestDrive 'existing-target') -Force
            { Save-LambdaPSModuleFromRepository -Name 'LambdaPSFake.A' -VersionRange '[1.0.0]' -Path $target.FullName } |
                Should -Throw -ErrorId 'ModuleDownloadFailed'
            Test-Path -LiteralPath $target.FullName | Should -BeTrue
        }
    }

    It 'wraps a PSResourceGet failure as ModuleDownloadFailed with the trust hint' {
        InModuleScope $script:moduleName {
            Mock 'Save-PSResource' -MockWith { throw 'PowerShell is in NonInteractive mode.' }
            $err = { Save-LambdaPSModuleFromRepository -Name 'LambdaPSFake.A' -VersionRange '[1.0.0]' -Path $TestDrive } |
                Should -Throw -ErrorId 'ModuleDownloadFailed' -PassThru
            $err.Exception.Message | Should -BeLike '*NonInteractive*Set-PSResourceRepository*'
            $err.Exception | Should -BeOfType [System.InvalidOperationException]
            $err.TargetObject | Should -Be 'LambdaPSFake.A 1.0.0'
        }
    }

    # The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
    # surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
    It 'surfaces <ErrorId> (<Case>) from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{
            ErrorId = 'ModuleInstallerNotFound'; Case = 'no PSResourceGet'
            Setup = { Mock 'Get-Command' -ParameterFilter { $Name -eq 'Save-PSResource' } -MockWith { } }
            Call = { Save-LambdaPSModuleFromRepository -Name 'LambdaPSFake.A' -VersionRange '[1.0.0]' -Path $Arg }
        }
        @{ ErrorId = 'ModuleDownloadFailed'; Case = 'no module reported'; Setup = { Mock 'Save-PSResource' -MockWith { } }; Call = { Save-LambdaPSModuleFromRepository -Name 'LambdaPSFake.A' -VersionRange '[1.0.0]' -Path $Arg } }
        @{
            ErrorId = 'ModuleDownloadFailed'; Case = 'PSResourceGet failure'
            Setup = { Mock 'Save-PSResource' -MockWith { throw 'PowerShell is in NonInteractive mode.' } }
            Call = { Save-LambdaPSModuleFromRepository -Name 'LambdaPSFake.A' -VersionRange '[1.0.0]' -Path $Arg }
        }
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
            @($ids | Where-Object { $_ -like '*,Save-LambdaPSModuleFromRepository' }) | Should -BeNullOrEmpty
        }
    }
}
