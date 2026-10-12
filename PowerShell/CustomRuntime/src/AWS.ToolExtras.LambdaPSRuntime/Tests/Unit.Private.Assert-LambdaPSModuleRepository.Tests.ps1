BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Assert-LambdaPSModuleRepository' -Tag 'Unit' {
    BeforeEach {
        InModuleScope $script:moduleName {
            Mock 'Get-PSResourceRepository' -MockWith { [PSCustomObject]@{ Name = 'PSGallery' }, [PSCustomObject]@{ Name = 'InternalMirror' } }
        }
    }

    It 'accepts registered repositories, whatever their case' {
        InModuleScope $script:moduleName {
            { Assert-LambdaPSModuleRepository -Name 'internalmirror', 'PSGallery' } | Should -Not -Throw
        }
    }

    It 'throws ModuleRepositoryNotFound naming the unregistered one and the registered ones' {
        InModuleScope $script:moduleName {
            $err = { Assert-LambdaPSModuleRepository -Name 'PSGallery', 'no-such-repo' } |
                Should -Throw -ErrorId 'ModuleRepositoryNotFound' -PassThru
            $err.TargetObject | Should -Be 'no-such-repo'
            $err.Exception.Message | Should -BeLike "*'no-such-repo'*registered: PSGallery, InternalMirror*"
            $err.Exception.Message | Should -BeLike "-ModuleRepository names 'no-such-repo', which is not a registered PSResourceGet repository *Register it *"
        }
    }

    It 'names several unregistered repositories in the plural' {
        InModuleScope $script:moduleName {
            $err = { Assert-LambdaPSModuleRepository -Name 'repoA', 'PSGallery', 'repoB' } |
                Should -Throw -ErrorId 'ModuleRepositoryNotFound' -PassThru
            $err.TargetObject | Should -Be @('repoA', 'repoB')
            $err.Exception.Message | Should -BeLike ("-ModuleRepository names 'repoA', 'repoB', which are not registered PSResourceGet " +
                'repositories (registered: PSGallery, InternalMirror). Register them with Register-PSResourceRepository, or name registered ones.*')
        }
    }

    It 'accepts a wildcard that matches a registered repository, as Save-PSResource -Repository does' {
        InModuleScope $script:moduleName {
            { Assert-LambdaPSModuleRepository -Name 'Internal*' } | Should -Not -Throw
            { Assert-LambdaPSModuleRepository -Name '*' } | Should -Not -Throw
        }
    }

    It 'refuses <Case> up front, as Save-PSResource does not support it' -ForEach @(
        @{ Case = "a '?' wildcard"; Names = @('InternalMirro?') }
        @{ Case = "a '[ ]' wildcard"; Names = @('[I]nternalMirror') }
        @{ Case = 'a backtick, which PSResourceGet reads as an escape'; Names = @('Internal`Mirror') }
        @{ Case = 'a pattern mixed with a plain name'; Names = @('PSGallery', 'Internal*') }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Names = $Names } {
            param($Names)
            $err = { Assert-LambdaPSModuleRepository -Name $Names } |
                Should -Throw -ErrorId 'ModuleRepositoryNotFound' -PassThru
            $err.Exception.Message | Should -BeLike "*PSResourceGet does not support*"
        }
    }

    It 'accepts a registered name with a ], and a * pattern containing one, as Save-PSResource does' {
        InModuleScope $script:moduleName {
            Mock 'Get-PSResourceRepository' -MockWith { [PSCustomObject]@{ Name = 'PSGallery' }, [PSCustomObject]@{ Name = 'Re]po' } }
            { Assert-LambdaPSModuleRepository -Name 'Re]po' } | Should -Not -Throw
            { Assert-LambdaPSModuleRepository -Name 'Re]*' } | Should -Not -Throw
        }
    }

    It 'still accepts names, several * patterns, and a * pattern on its own' {
        InModuleScope $script:moduleName {
            { Assert-LambdaPSModuleRepository -Name 'PSGallery', 'InternalMirror' } | Should -Not -Throw
            { Assert-LambdaPSModuleRepository -Name 'PSG*', 'Int*' } | Should -Not -Throw
            { Assert-LambdaPSModuleRepository -Name '*Mirror' } | Should -Not -Throw
        }
    }

    It 'refuses a wildcard that matches no registered repository' {
        InModuleScope $script:moduleName {
            $err = { Assert-LambdaPSModuleRepository -Name 'Mirror*' } |
                Should -Throw -ErrorId 'ModuleRepositoryNotFound' -PassThru
            $err.TargetObject | Should -Be 'Mirror*'
            $err.Exception.Message | Should -BeLike ("-ModuleRepository pattern 'Mirror``*' matches no registered PSResourceGet " +
                'repository (registered: PSGallery, InternalMirror). Register a matching repository*')
        }
    }

    It 'says which of several wildcards match no registered repository, in the plural' {
        InModuleScope $script:moduleName {
            $err = { Assert-LambdaPSModuleRepository -Name 'A*', 'Int*', 'B*' } |
                Should -Throw -ErrorId 'ModuleRepositoryNotFound' -PassThru
            $err.TargetObject | Should -Be @('A*', 'B*')
            $err.Exception.Message | Should -BeLike "-ModuleRepository patterns 'A``*', 'B``*' match no registered PSResourceGet repository *"
        }
    }

    It 'says the PSGallery default is not registered, rather than naming a -ModuleRepository nobody passed' {
        InModuleScope $script:moduleName {
            Mock 'Get-PSResourceRepository' -MockWith { [PSCustomObject]@{ Name = 'InternalMirror' } }
            $err = { Assert-LambdaPSModuleRepository -Name 'PSGallery' -InstallerDefault } |
                Should -Throw -ErrorId 'ModuleRepositoryNotFound' -PassThru
            $err.Exception.Message | Should -BeLike '*AWS.Tools.Installer is downloaded from PSGallery when -ModuleRepository is not given*registered: InternalMirror*). Register it (Register-PSResourceRepository -PSGallery), or pass -ModuleRepository*'
            $err.Exception.Message | Should -Not -BeLike '-ModuleRepository names*'
        }
    }

    It 'checks nothing when PSResourceGet is not available' {
        InModuleScope $script:moduleName {
            Mock 'Get-Command' -ParameterFilter { $Name -eq 'Get-PSResourceRepository' }
            { Assert-LambdaPSModuleRepository -Name 'no-such-repo' } | Should -Not -Throw
            Should -Invoke 'Get-PSResourceRepository' -Times 0 -Exactly
        }
    }

    # The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
    # surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
    It 'surfaces <ErrorId> (<Case>) from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'ModuleRepositoryNotFound'; Case = 'unregistered name'; Call = { Assert-LambdaPSModuleRepository -Name 'no-such-repo' } }
        @{ ErrorId = 'ModuleRepositoryNotFound'; Case = 'unsupported wildcard'; Call = { Assert-LambdaPSModuleRepository -Name 'Mirror?' } }
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
            @($ids | Where-Object { $_ -like '*,Assert-LambdaPSModuleRepository' }) | Should -BeNullOrEmpty
        }
    }
}
