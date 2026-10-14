BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeModule.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Save-LambdaPSMissingModule' -Tag 'Unit' {
    BeforeEach {
        $script:dl = Join-Path $TestDrive "dl-$([guid]::NewGuid().ToString('N'))"
        $script:userModules = Join-Path $TestDrive "user-$([guid]::NewGuid().ToString('N'))"
    }

    It 'checks -ModuleRepository before anything is downloaded (<Case>)' -ForEach @(
        @{ Case = 'an AWS.Tools module first'; Name = 'AWS.Tools.FakeSvc' }
        @{ Case = 'nothing to download'; Name = 'LambdaPSFake.Installed' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ dl = $script:dl; Name = $Name } {
            param($dl, $Name)
            Mock 'Get-PSResourceRepository' -MockWith { [PSCustomObject]@{ Name = 'PSGallery' } }
            Mock 'Get-Module' -ParameterFilter { $FullyQualifiedName.Name -eq 'LambdaPSFake.Installed' } -MockWith {
                [PSCustomObject]@{ Name = 'LambdaPSFake.Installed'; ModuleBase = '/local/LambdaPSFake.Installed/1.0.0'; RequiredModules = @() }
            }
            Mock 'Save-LambdaPSAWSToolsModule' -MockWith { throw 'must not download before the repository check' }

            { Save-LambdaPSMissingModule -Module @{ ModuleName = $Name; RequiredVersion = '1.0.0' } -Path $dl -ModuleRepository 'Typo' } |
                Should -Throw -ErrorId 'ModuleRepositoryNotFound'
            Should -Invoke 'Save-LambdaPSAWSToolsModule' -Times 0 -Exactly
        }
    }

    It 'downloads with a registered -ModuleRepository' {
        InModuleScope $script:moduleName -Parameters @{ dl = $script:dl } {
            param($dl)
            Mock 'Get-PSResourceRepository' -MockWith { [PSCustomObject]@{ Name = 'Internal' } }
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith {
                $d = [System.IO.Path]::Combine($Path, $Name, '1.0.0')
                $null = New-Item -ItemType Directory -Path $d -Force
                Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$Name.psd1")) -Value "@{ ModuleVersion = '1.0.0' }"
                [PSCustomObject]@{ Name = $Name; Version = '1.0.0'; Repository = 'Internal' }
            }

            $result = Save-LambdaPSMissingModule -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' } -Path $dl -ModuleRepository 'Internal' -WarningAction SilentlyContinue
            @($result.Downloaded.Name) | Should -Be @('LambdaPSFake.A')
        }
    }

    It 'downloads nothing and warns nothing when every module is available' {
        InModuleScope $script:moduleName {
            Mock 'Get-Module' -ParameterFilter { $FullyQualifiedName.Name -eq 'LambdaPSFake.A' } -MockWith {
                [PSCustomObject]@{ Name = 'LambdaPSFake.A'; ModuleBase = '/local/LambdaPSFake.A/1.0.0'; RequiredModules = @() }
            }
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith { throw 'must not download' }
            Mock 'Save-LambdaPSAWSToolsModule' -MockWith { throw 'must not download' }

            $result = Save-LambdaPSMissingModule -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' } -Path $TestDrive -WarningVariable warnings
            $result.Downloaded.Count | Should -Be 0
            @($warnings).Count | Should -Be 0
        }
    }

    It 'downloads a non-AWS module with PSResourceGet, then its dependency, each at its exact version' {
        InModuleScope $script:moduleName -Parameters @{ dl = $script:dl } {
            param($dl)
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith {
                $requires = if ($Name -eq 'LambdaPSFake.A') { "@(@{ ModuleName = 'LambdaPSFake.B'; ModuleVersion = '2.0.0' })" } else { '@()' }
                $exact = $VersionRange.Trim('[', ']')
                $d = [System.IO.Path]::Combine($Path, $Name, $exact)
                $null = New-Item -ItemType Directory -Path $d -Force
                Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$Name.psd1")) -Value "@{ ModuleVersion = '$exact'; RequiredModules = $requires }"
                [PSCustomObject]@{ Name = $Name; Version = $exact; Repository = 'Internal' }
            }

            $result = Save-LambdaPSMissingModule -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' } -Path $dl -WarningAction SilentlyContinue

            Should -Invoke 'Save-LambdaPSModuleFromRepository' -Times 1 -Exactly -ParameterFilter { $Name -eq 'LambdaPSFake.A' -and $VersionRange -eq '[1.0.0]' }
            Should -Invoke 'Save-LambdaPSModuleFromRepository' -Times 1 -Exactly -ParameterFilter { $Name -eq 'LambdaPSFake.B' -and $VersionRange -eq '[2.0.0]' }
            @($result.Downloaded | ForEach-Object { "$($_.Name) $($_.Version)" }) | Should -Be @('LambdaPSFake.A 1.0.0', 'LambdaPSFake.B 2.0.0')
            $result.Downloaded[0].PSTypeNames | Should -Contain 'AWS.ToolExtras.LambdaPSRuntime.DownloadedModule'
            $result.Downloaded[0].Source | Should -Be 'PSResourceGet'
            $result.Downloaded[0].Action | Should -Be 'Saved'
            $result.Downloaded[0].Version | Should -BeOfType [version]
            @($result.Resolved.Key) | Should -Be @('LambdaPSFake.A 1.0.0', 'LambdaPSFake.B 2.0.0')
        }
    }

    It 'groups AWS.Tools modules into one installer call per version' {
        InModuleScope $script:moduleName -Parameters @{ dl = $script:dl } {
            param($dl)
            Mock 'Save-LambdaPSAWSToolsModule' -MockWith {
                foreach ($n in @($Name + 'AWS.Tools.Common')) {
                    $d = [System.IO.Path]::Combine($Path, $n, $Version)
                    $null = New-Item -ItemType Directory -Path $d -Force
                    Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$n.psd1")) -Value "@{ ModuleVersion = '$Version' }"
                }
            }
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith { throw 'AWS.Tools must not go through PSResourceGet' }
            $specs = @(
                @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '9.9.1' }
                @{ ModuleName = 'AWS.Tools.EC2'; RequiredVersion = '9.9.1' }
                @{ ModuleName = 'AWS.Tools.SQS'; RequiredVersion = '9.9.2' }
            )

            $null = Save-LambdaPSMissingModule -Module $specs -Path $dl -WarningAction SilentlyContinue

            Should -Invoke 'Save-LambdaPSAWSToolsModule' -Times 2 -Exactly
            Should -Invoke 'Save-LambdaPSAWSToolsModule' -Times 1 -Exactly -ParameterFilter {
                $Version -eq '9.9.1' -and $Name.Count -eq 2 -and ($Name -contains 'AWS.Tools.S3') -and ($Name -contains 'AWS.Tools.EC2')
            }
        }
    }

    It 'reports an AWS.Tools.Common the installer brought along as Written, not Downloaded, when it is already installed' {
        InModuleScope $script:moduleName -Parameters @{ dl = $script:dl } {
            param($dl)
            Mock 'Get-Module' -ParameterFilter { $FullyQualifiedName.Name -eq 'AWS.Tools.Common' } -MockWith {
                [PSCustomObject]@{ Name = 'AWS.Tools.Common'; ModuleBase = '/local/AWS.Tools.Common/9.9.1'; RequiredModules = @() }
            }
            Mock 'Save-LambdaPSAWSToolsModule' -MockWith {
                foreach ($n in @($Name + 'AWS.Tools.Common')) {
                    $d = [System.IO.Path]::Combine($Path, $n, $Version)
                    $null = New-Item -ItemType Directory -Path $d -Force
                    $requires = if ($n -eq 'AWS.Tools.Common') { '@()' } else { "@(@{ ModuleName = 'AWS.Tools.Common'; RequiredVersion = '$Version' })" }
                    Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$n.psd1")) -Value "@{ ModuleVersion = '$Version'; RequiredModules = $requires }"
                }
            }

            $result = Save-LambdaPSMissingModule -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '9.9.1' } -Path $dl -WarningVariable warnings -WarningAction SilentlyContinue
            @($result.Downloaded.Name) | Should -Be @('AWS.Tools.S3')
            # The installer also wrote AWS.Tools.Common into -Path: it is reported, at its -Path folder.
            @($result.Written | ForEach-Object { "$($_.Name) $($_.Version) $($_.Action) $($_.Path)" }) |
                Should -Be @("AWS.Tools.Common 9.9.1 Saved $([System.IO.Path]::Combine($dl, 'AWS.Tools.Common', '9.9.1'))")
            "$($warnings[0])" | Should -BeLike 'Downloaded modules that were not available locally: AWS.Tools.S3 9.9.1, AWS.Tools.Common 9.9.1. *'
        }
    }

    It 'stops with ModuleFolderIncomplete, before downloading, when a version folder in -Path holds no manifest (<Case>)' -ForEach @(
        @{ Case = 'the module itself'; Folder = 'LambdaPSFake.A'; Name = 'LambdaPSFake.A'; Version = '1.0.0' }
        @{ Case = 'AWS.Tools.Common for an AWS.Tools module'; Folder = 'AWS.Tools.Common'; Name = 'AWS.Tools.FakeSvc'; Version = '9.9.3' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ dl = $script:dl; Folder = $Folder; Name = $Name; Version = $Version } {
            param($dl, $Folder, $Name, $Version)
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith { throw 'must not download' }
            Mock 'Save-LambdaPSAWSToolsModule' -MockWith { throw 'must not download' }
            $leftover = [System.IO.Path]::Combine($dl, $Folder, $Version)
            $null = New-Item -ItemType Directory -Path $leftover -Force

            $err = { Save-LambdaPSMissingModule -Module @{ ModuleName = $Name; RequiredVersion = $Version } -Path $dl -WarningAction SilentlyContinue } |
                Should -Throw -ErrorId 'ModuleFolderIncomplete' -PassThru
            $err.TargetObject | Should -Be $leftover
            $err.Exception.Message | Should -Be "'$leftover' exists but holds no '$Folder.psd1', so it is not a complete module. Remove the folder and run again."
        }
    }

    It 'does not report AWS.Tools.Common as downloaded when that version was already in -Path' {
        InModuleScope $script:moduleName -Parameters @{ dl = $script:dl } {
            param($dl)
            $common = [System.IO.Path]::Combine($dl, 'AWS.Tools.Common', '9.9.2')
            $null = New-Item -ItemType Directory -Path $common -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($common, 'AWS.Tools.Common.psd1')) -Value "@{ ModuleVersion = '9.9.2' }"
            # As the seam does: a version folder already in -Path is kept.
            Mock 'Save-LambdaPSAWSToolsModule' -MockWith {
                foreach ($n in @($Name + 'AWS.Tools.Common')) {
                    $d = [System.IO.Path]::Combine($Path, $n, $Version)
                    if (Test-Path -LiteralPath $d) { continue }
                    $null = New-Item -ItemType Directory -Path $d -Force
                    Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$n.psd1")) -Value "@{ ModuleVersion = '$Version'; RequiredModules = @(@{ ModuleName = 'AWS.Tools.Common'; RequiredVersion = '$Version' }) }"
                }
            }

            $result = Save-LambdaPSMissingModule -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '9.9.2' } -Path $dl -WarningVariable warnings -WarningAction SilentlyContinue
            @($result.Downloaded.Name) | Should -Be @('AWS.Tools.S3')
            @($result.Resolved.Key) | Should -Contain 'AWS.Tools.Common 9.9.2'
            "$($warnings[0])" | Should -BeLike 'Downloaded modules that were not available locally: AWS.Tools.S3 9.9.2. *'
        }
    }

    It 'writes the test-before-production warning once, and the PSGallery warning only for PSGallery modules' {
        InModuleScope $script:moduleName -Parameters @{ dl = $script:dl } {
            param($dl)
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith {
                $exact = $VersionRange.Trim('[', ']')
                $d = [System.IO.Path]::Combine($Path, $Name, $exact)
                $null = New-Item -ItemType Directory -Path $d -Force
                Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$Name.psd1")) -Value "@{ ModuleVersion = '$exact' }"
                $repo = if ($Name -eq 'LambdaPSFake.Gallery') { 'PSGallery' } else { 'Internal' }
                [PSCustomObject]@{ Name = $Name; Version = $exact; Repository = $repo }
            }
            $specs = @(
                @{ ModuleName = 'LambdaPSFake.Gallery'; RequiredVersion = '1.0.0' }
                @{ ModuleName = 'LambdaPSFake.Private'; RequiredVersion = '1.0.0' }
            )

            $null = Save-LambdaPSMissingModule -Module $specs -Path $dl -WarningVariable warnings -WarningAction SilentlyContinue

            @($warnings).Count | Should -Be 2
            $warnings[0].Message | Should -BeLike '*LambdaPSFake.Gallery 1.0.0, LambdaPSFake.Private 1.0.0*before production use*'
            $warnings[1].Message | Should -BeLike 'LambdaPSFake.Gallery 1.0.0 came from the PowerShell Gallery*'
            $warnings[1].Message | Should -Not -BeLike '*LambdaPSFake.Private*'
        }
    }

    It 'downloads nothing under -WhatIf and returns the modules it would download as Pending' {
        InModuleScope $script:moduleName -Parameters @{ dl = $script:dl } {
            param($dl)
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith { throw 'must not download' }

            $result = Save-LambdaPSMissingModule -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' } -Path $dl -WhatIf -WarningVariable warnings -WarningAction SilentlyContinue

            @($result.Pending) | Should -Be @('LambdaPSFake.A 1.0.0')
            $result.Downloaded.Count | Should -Be 0
            $warnings[0].Message | Should -BeLike 'Would download*LambdaPSFake.A 1.0.0*'
            Test-Path -LiteralPath $dl | Should -BeFalse
        }
    }

    It 'throws ModuleDownloadFailed when a download does not produce the module' {
        InModuleScope $script:moduleName -Parameters @{ dl = $script:dl } {
            param($dl)
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith { [PSCustomObject]@{ Name = $Name; Version = '1.0.0'; Repository = 'Internal' } }

            $err = { Save-LambdaPSMissingModule -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' } -Path $dl } |
                Should -Throw -ErrorId 'ModuleDownloadFailed' -PassThru
            $err.Exception | Should -BeOfType [System.InvalidOperationException]
            $err.TargetObject | Should -Be 'LambdaPSFake.A 1.0.0'
        }
    }

    It 'bootstraps the Installer into a temporary folder that is gone afterwards, never into -Path' {
        InModuleScope $script:moduleName -Parameters @{ dl = $script:dl } {
            param($dl)
            $script:seenInstallerPath = $null
            Mock 'Save-LambdaPSAWSToolsModule' -MockWith {
                $script:seenInstallerPath = $InstallerPath
                $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($InstallerPath, 'AWS.Tools.Installer', '2.0.5')) -Force
                foreach ($n in @($Name + 'AWS.Tools.Common')) {
                    $d = [System.IO.Path]::Combine($Path, $n, $Version)
                    $null = New-Item -ItemType Directory -Path $d -Force
                    Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$n.psd1")) -Value "@{ ModuleVersion = '$Version' }"
                }
            }

            $null = Save-LambdaPSMissingModule -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '9.9.2' } -Path $dl -WarningAction SilentlyContinue

            $script:seenInstallerPath | Should -Not -BeNullOrEmpty
            $script:seenInstallerPath | Should -Not -BeLike "$([System.IO.Path]::GetFullPath($dl))*"
            Test-Path -LiteralPath $script:seenInstallerPath | Should -BeFalse
        }
    }

    It 'removes the temporary Installer folder when the download fails' {
        InModuleScope $script:moduleName -Parameters @{ dl = $script:dl } {
            param($dl)
            $script:seenInstallerPath = $null
            Mock 'Save-LambdaPSAWSToolsModule' -MockWith {
                $script:seenInstallerPath = $InstallerPath
                $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($InstallerPath, 'AWS.Tools.Installer', '2.0.5')) -Force
                throw 'CloudFront unreachable'
            }

            { Save-LambdaPSMissingModule -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '9.9.2' } -Path $dl -WarningAction SilentlyContinue } |
                Should -Throw '*CloudFront unreachable*'
            $script:seenInstallerPath | Should -Not -BeNullOrEmpty
            Test-Path -LiteralPath $script:seenInstallerPath | Should -BeFalse
        }
    }

    It 'names the temporary Installer folder in a verbose message when it cannot be removed' {
        InModuleScope $script:moduleName -Parameters @{ dl = $script:dl } {
            param($dl)
            $script:leftInstallerPath = $null
            Mock 'Save-LambdaPSAWSToolsModule' -MockWith {
                $script:leftInstallerPath = $InstallerPath
                $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($InstallerPath, 'AWS.Tools.Installer', '2.0.5')) -Force
                foreach ($n in @($Name + 'AWS.Tools.Common')) {
                    $d = [System.IO.Path]::Combine($Path, $n, $Version)
                    $null = New-Item -ItemType Directory -Path $d -Force
                    Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$n.psd1")) -Value "@{ ModuleVersion = '$Version' }"
                }
            }
            # Stands in for the Windows case, where the imported installer's assembly locks the folder.
            Mock 'Remove-Item' -ParameterFilter { $LiteralPath -like '*lambdaps-installer-*' }
            try {
                $verbose = Save-LambdaPSMissingModule -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '9.9.2' } -Path $dl -Verbose -WarningAction SilentlyContinue 4>&1 |
                    Where-Object { $_ -is [System.Management.Automation.VerboseRecord] }
                @($verbose.Message) -join ' ' | Should -BeLike "*Could not remove the temporary AWS.Tools.Installer folder '$script:leftInstallerPath'*"
            }
            finally {
                if ($script:leftInstallerPath) {
                    [System.IO.Directory]::Delete($script:leftInstallerPath, $true)
                }
            }
        }
    }

    It 'rejects an unpinned spec before downloading' {
        InModuleScope $script:moduleName {
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith { throw 'must not download' }
            { Save-LambdaPSMissingModule -Module @{ ModuleName = 'LambdaPSFake.A'; ModuleVersion = '1.0.0' } -Path $TestDrive } |
                Should -Throw -ErrorId 'ModuleVersionRequired'
        }
    }

    Context 'a dependency that only a minimum asks for' {
        BeforeEach {
            # XA is installed and needs XCommon 1.0.0 or later. XB is missing; its manifest pins XCommon 1.5.0.
            $script:installed = Join-Path $TestDrive "installed-$([guid]::NewGuid().ToString('N'))"
            $null = New-FakeModule -Root $script:installed -Name 'LambdaPSFake.XA' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.XCommon'; ModuleVersion = '1.0.0' })"
            $script:savedModulePath = $env:PSModulePath
            $env:PSModulePath = $script:installed + [System.IO.Path]::PathSeparator + $env:PSModulePath
        }
        AfterEach {
            $env:PSModulePath = $script:savedModulePath
        }

        It 'is downloaded only at the version a module downloaded later pins it to' {
            InModuleScope $script:moduleName -Parameters @{ dl = $script:dl } {
                param($dl)
                Mock 'Save-LambdaPSModuleFromRepository' -MockWith {
                    $exact = $VersionRange.Trim('[', ']')
                    $requires = if ($Name -eq 'LambdaPSFake.XB') { "@(@{ ModuleName = 'LambdaPSFake.XCommon'; RequiredVersion = '1.5.0' })" } else { '@()' }
                    $d = [System.IO.Path]::Combine($Path, $Name, $exact)
                    $null = New-Item -ItemType Directory -Path $d -Force
                    Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$Name.psd1")) -Value "@{ ModuleVersion = '$exact'; RequiredModules = $requires }"
                    [PSCustomObject]@{ Name = $Name; Version = $exact; Repository = 'Internal' }
                }
                $specs = @(
                    @{ ModuleName = 'LambdaPSFake.XA'; RequiredVersion = '1.0.0' }
                    @{ ModuleName = 'LambdaPSFake.XB'; RequiredVersion = '1.0.0' }
                )

                $result = Save-LambdaPSMissingModule -Module $specs -Path $dl -WarningAction SilentlyContinue

                Should -Invoke 'Save-LambdaPSModuleFromRepository' -Times 0 -Exactly -ParameterFilter { $Name -eq 'LambdaPSFake.XCommon' -and $VersionRange -eq '[1.0.0]' }
                @(Get-ChildItem -LiteralPath ([System.IO.Path]::Combine($dl, 'LambdaPSFake.XCommon'))).Name | Should -Be @('1.5.0')
                @($result.Downloaded | ForEach-Object { "$($_.Name) $($_.Version)" }) | Should -Be @('LambdaPSFake.XB 1.0.0', 'LambdaPSFake.XCommon 1.5.0')
            }
        }

        It 'says in verbose output where each module resolved from, which version it chose, and what it downloads' {
            InModuleScope $script:moduleName -Parameters @{ dl = $script:dl } {
                param($dl)
                Mock 'Save-LambdaPSModuleFromRepository' -MockWith {
                    $exact = $VersionRange.Trim('[', ']')
                    $requires = if ($Name -eq 'LambdaPSFake.XB') { "@(@{ ModuleName = 'LambdaPSFake.XCommon'; RequiredVersion = '1.5.0' })" } else { '@()' }
                    $d = [System.IO.Path]::Combine($Path, $Name, $exact)
                    $null = New-Item -ItemType Directory -Path $d -Force
                    Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$Name.psd1")) -Value "@{ ModuleVersion = '$exact'; RequiredModules = $requires }"
                    [PSCustomObject]@{ Name = $Name; Version = $exact; Repository = 'Internal' }
                }
                $specs = @(
                    @{ ModuleName = 'LambdaPSFake.XA'; RequiredVersion = '1.0.0' }
                    @{ ModuleName = 'LambdaPSFake.XB'; RequiredVersion = '1.0.0' }
                )

                $verbose = @(Save-LambdaPSMissingModule -Module $specs -Path $dl -Verbose -WarningAction SilentlyContinue 4>&1 |
                        Where-Object { $_ -is [System.Management.Automation.VerboseRecord] } | ForEach-Object { $_.Message })

                $verbose | Should -Contain "'LambdaPSFake.XB' 1.0.0 (requested) is not available locally."
                $verbose | Where-Object { $_ -like "Resolved 'LambdaPSFake.XA' 1.0.0 (requested) from the PSModulePath at *" } | Should -Not -BeNullOrEmpty
                $verbose | Where-Object { $_ -like "Waiting to download 'LambdaPSFake.XCommon' 1.0.0*" } | Should -Not -BeNullOrEmpty
                $verbose | Where-Object { $_ -like "Downloading 'LambdaPSFake.XB' 1.0.0 with PSResourceGet to *" } | Should -Not -BeNullOrEmpty
                $verbose | Where-Object { $_ -like "Using 'LambdaPSFake.XCommon' 1.5.0, the one version that meets every requirement on it*" } | Should -Not -BeNullOrEmpty
                $verbose | Where-Object { $_ -like "Resolved 'LambdaPSFake.XCommon' 1.5.0 * from the download folder at *" } | Should -Not -BeNullOrEmpty
            }
        }

        It 'is downloaded at its minimum once nothing else is missing' {
            InModuleScope $script:moduleName -Parameters @{ dl = $script:dl } {
                param($dl)
                Mock 'Save-LambdaPSModuleFromRepository' -MockWith {
                    $exact = $VersionRange.Trim('[', ']')
                    $d = [System.IO.Path]::Combine($Path, $Name, $exact)
                    $null = New-Item -ItemType Directory -Path $d -Force
                    Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$Name.psd1")) -Value "@{ ModuleVersion = '$exact' }"
                    [PSCustomObject]@{ Name = $Name; Version = $exact; Repository = 'Internal' }
                }

                $result = Save-LambdaPSMissingModule -Module @{ ModuleName = 'LambdaPSFake.XA'; RequiredVersion = '1.0.0' } -Path $dl -WarningAction SilentlyContinue

                @($result.Downloaded | ForEach-Object { "$($_.Name) $($_.Version)" }) | Should -Be @('LambdaPSFake.XCommon 1.0.0')
            }
        }
    }

    Context '-MissingModuleAction Install' {
        It 'copies downloaded modules and a bootstrapped installer into the CurrentUser folder, never over an existing version' {
            InModuleScope $script:moduleName -Parameters @{ dl = $script:dl; user = $script:userModules } {
                param($dl, $user)
                Mock 'Get-LambdaPSCurrentUserModulePath' -MockWith { $user }
                Mock 'Save-LambdaPSAWSToolsModule' -MockWith {
                    # Stand in for a run that also had to save AWS.Tools.Installer itself.
                    $i = [System.IO.Path]::Combine($InstallerPath, 'AWS.Tools.Installer', '2.0.5')
                    $null = New-Item -ItemType Directory -Path $i -Force
                    Set-Content -LiteralPath ([System.IO.Path]::Combine($i, 'AWS.Tools.Installer.psd1')) -Value "@{ ModuleVersion = '2.0.5' }"
                    foreach ($n in @($Name + 'AWS.Tools.Common')) {
                        $d = [System.IO.Path]::Combine($Path, $n, $Version)
                        $null = New-Item -ItemType Directory -Path $d -Force
                        Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$n.psd1")) -Value "@{ ModuleVersion = '$Version' }"
                    }
                }
                # A version already in the user folder must be left exactly as it is.
                $existing = [System.IO.Path]::Combine($user, 'AWS.Tools.EC2', '9.9.1')
                $null = New-Item -ItemType Directory -Path $existing -Force
                Set-Content -LiteralPath ([System.IO.Path]::Combine($existing, 'AWS.Tools.EC2.psd1')) -Value '# original'
                $specs = @(
                    @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '9.9.1' }
                    @{ ModuleName = 'AWS.Tools.EC2'; RequiredVersion = '9.9.1' }
                )

                $result = Save-LambdaPSMissingModule -Module $specs -Path $dl -MissingModuleAction Install -WarningVariable warnings -WarningAction SilentlyContinue

                Test-Path -LiteralPath ([System.IO.Path]::Combine($user, 'AWS.Tools.S3', '9.9.1', 'AWS.Tools.S3.psd1')) | Should -BeTrue
                Test-Path -LiteralPath ([System.IO.Path]::Combine($user, 'AWS.Tools.Installer', '2.0.5', 'AWS.Tools.Installer.psd1')) | Should -BeTrue
                Get-Content -LiteralPath ([System.IO.Path]::Combine($existing, 'AWS.Tools.EC2.psd1')) | Should -Be '# original'
                @($result.Downloaded | Where-Object { $_.Name -eq 'AWS.Tools.S3' }).Action | Should -Be 'Installed'
                $warnings[0].Message | Should -BeLike "*Installed for the current user in '$user': AWS.Tools.S3 9.9.1, AWS.Tools.Installer 2.0.5." -Because 'EC2 was not copied, so it is not listed; the bootstrapped Installer was, so it is'
                # The skipped copy (existing folder here; a declined -Confirm takes the same branch)
                # stays Saved rather than claiming a user-folder path.
                $ec2 = @($result.Downloaded | Where-Object { $_.Name -eq 'AWS.Tools.EC2' })[0]
                $ec2.Action | Should -Be 'Saved'
                $ec2.Path | Should -Be ([System.IO.Path]::Combine($dl, 'AWS.Tools.EC2', '9.9.1'))
            }
        }

        It 'names the versions already installed when a later install copy fails' {
            InModuleScope $script:moduleName -Parameters @{ dl = $script:dl; user = $script:userModules } {
                param($dl, $user)
                Mock 'Get-LambdaPSCurrentUserModulePath' -MockWith { $user }
                Mock 'Save-LambdaPSModuleFromRepository' -MockWith {
                    $d = [System.IO.Path]::Combine($Path, $Name, '1.0.0')
                    $null = New-Item -ItemType Directory -Path $d -Force
                    Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$Name.psd1")) -Value "@{ ModuleVersion = '1.0.0' }"
                    [PSCustomObject]@{ Name = $Name; Version = '1.0.0'; Repository = 'Internal' }
                }
                Mock 'Copy-LambdaPSModuleFolder' -ParameterFilter { $Destination -like '*LambdaPSFake.Second*' } -MockWith {
                    throw [System.UnauthorizedAccessException]::new('access denied')
                }
                Mock 'Copy-LambdaPSModuleFolder' -ParameterFilter { $Destination -like '*LambdaPSFake.First*' } -MockWith {
                    $null = New-Item -ItemType Directory -Path $Destination -Force
                }
                $specs = @(
                    @{ ModuleName = 'LambdaPSFake.First'; RequiredVersion = '1.0.0' }
                    @{ ModuleName = 'LambdaPSFake.Second'; RequiredVersion = '1.0.0' }
                )

                $err = $null
                try { $null = Save-LambdaPSMissingModule -Module $specs -Path $dl -MissingModuleAction Install -WarningVariable warnings -WarningAction SilentlyContinue }
                catch { $err = $_ }
                $err.Exception.Message | Should -BeLike '*access denied*'
                @($warnings | ForEach-Object { "$_" }) | Should -Contain ("Installing for the current user stopped partway: LambdaPSFake.First 1.0.0 were " +
                    "installed in '$user' before the failure and are still there. Remove them if you do not want them.")
            }
        }

        It 'fails, and leaves no partial version folder, when an install copy fails' {
            InModuleScope $script:moduleName -Parameters @{ dl = $script:dl; user = $script:userModules } {
                param($dl, $user)
                Mock 'Get-LambdaPSCurrentUserModulePath' -MockWith { $user }
                Mock 'Save-LambdaPSModuleFromRepository' -MockWith {
                    $d = [System.IO.Path]::Combine($Path, $Name, '1.0.0')
                    $null = New-Item -ItemType Directory -Path $d -Force
                    Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$Name.psd1")) -Value "@{ ModuleVersion = '1.0.0' }"
                    [PSCustomObject]@{ Name = $Name; Version = '1.0.0'; Repository = 'Internal' }
                }
                Mock 'Copy-Item' -MockWith { throw [System.IO.IOException]::new('disk full') }

                $err = $null
                try { $null = Save-LambdaPSMissingModule -Module @{ ModuleName = 'LambdaPSFake.Partial'; RequiredVersion = '1.0.0' } -Path $dl -MissingModuleAction Install -WarningVariable warnings -WarningAction SilentlyContinue }
                catch { $err = $_ }
                $err.Exception.Message | Should -BeLike '*disk full*'
                # Nothing was installed before the failure, so there is nothing to name.
                @($warnings | Where-Object { "$_" -like 'Installing for the current user stopped partway*' }) | Should -BeNullOrEmpty
                Test-Path -LiteralPath ([System.IO.Path]::Combine($user, 'LambdaPSFake.Partial', '1.0.0')) | Should -BeFalse
            }
        }

        It 'stops with ModuleFolderIncomplete, installing nothing, when a user-folder version holds no manifest' {
            InModuleScope $script:moduleName -Parameters @{ dl = $script:dl; user = $script:userModules } {
                param($dl, $user)
                Mock 'Get-LambdaPSCurrentUserModulePath' -MockWith { $user }
                Mock 'Save-LambdaPSModuleFromRepository' -MockWith {
                    $d = [System.IO.Path]::Combine($Path, $Name, '1.0.0')
                    $null = New-Item -ItemType Directory -Path $d -Force
                    Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$Name.psd1")) -Value "@{ ModuleVersion = '1.0.0' }"
                    [PSCustomObject]@{ Name = $Name; Version = '1.0.0'; Repository = 'Internal' }
                }
                Mock 'Copy-LambdaPSModuleFolder'
                $leftover = [System.IO.Path]::Combine($user, 'LambdaPSFake.B', '1.0.0')
                $null = New-Item -ItemType Directory -Path $leftover -Force
                $specs = @(
                    @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' }
                    @{ ModuleName = 'LambdaPSFake.B'; RequiredVersion = '1.0.0' }
                )

                $err = { Save-LambdaPSMissingModule -Module $specs -Path $dl -MissingModuleAction Install -WarningAction SilentlyContinue } |
                    Should -Throw -ErrorId 'ModuleFolderIncomplete' -PassThru
                $err.TargetObject | Should -Be $leftover
                Should -Invoke 'Copy-LambdaPSModuleFolder' -Times 0 -Exactly
            }
        }

        It 'installs nothing under -MissingModuleAction Save' {
            InModuleScope $script:moduleName -Parameters @{ dl = $script:dl } {
                param($dl)
                Mock 'Get-LambdaPSCurrentUserModulePath' -MockWith { throw 'Save must not touch the user module folder' }
                Mock 'Save-LambdaPSModuleFromRepository' -MockWith {
                    $d = [System.IO.Path]::Combine($Path, $Name, '1.0.0')
                    $null = New-Item -ItemType Directory -Path $d -Force
                    Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$Name.psd1")) -Value "@{ ModuleVersion = '1.0.0' }"
                    [PSCustomObject]@{ Name = $Name; Version = '1.0.0'; Repository = 'Internal' }
                }

                $result = Save-LambdaPSMissingModule -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' } -Path $dl -WarningAction SilentlyContinue
                $result.Downloaded[0].Action | Should -Be 'Saved'
                Should -Invoke 'Get-LambdaPSCurrentUserModulePath' -Times 0 -Exactly
            }
        }
    }
}

Describe 'Save-LambdaPSMissingModule -ModuleRepository' -Tag 'Unit' {
    It 'downloads a missing module from the named repository' {
        InModuleScope $script:moduleName -Parameters @{ dl = (Join-Path $TestDrive 'resolve-repo') } {
            param($dl)
            Mock 'Get-PSResourceRepository' -MockWith { [PSCustomObject]@{ Name = 'Internal' } }
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith {
                $d = [System.IO.Path]::Combine($Path, $Name, '1.0.0')
                $null = New-Item -ItemType Directory -Path $d -Force
                Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$Name.psd1")) -Value "@{ ModuleVersion = '1.0.0'; GUID = '$([guid]::NewGuid())' }"
                [PSCustomObject]@{ Name = $Name; Version = '1.0.0'; Repository = 'Internal' }
            }

            $null = Save-LambdaPSMissingModule -Module @{ ModuleName = 'LambdaPSFake.Repo'; RequiredVersion = '1.0.0' } -Path $dl -MissingModuleAction Save -ModuleRepository Internal -WarningAction SilentlyContinue

            Should -Invoke 'Save-LambdaPSModuleFromRepository' -Times 1 -Exactly -ParameterFilter { $Name -eq 'LambdaPSFake.Repo' -and $ModuleRepository -eq 'Internal' }
        }
    }

    It 'fetches AWS.Tools.Installer needed as a dependency only from -ModuleRepository or PSGallery, signature-checked on Windows' {
        InModuleScope $script:moduleName -Parameters @{ dl = (Join-Path $TestDrive 'installer-dependency') } {
            param($dl)
            # A third-party module that requires AWS.Tools.Installer: the Installer is code that runs on
            # this machine, so it must not come from whichever registered repository ranks first.
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith {
                $exact = $VersionRange.Trim('[', ']')
                $requires = if ($Name -eq 'LambdaPSFake.NeedsInstaller') { "@(@{ ModuleName = 'AWS.Tools.Installer'; RequiredVersion = '2.0.6' })" } else { '@()' }
                $d = [System.IO.Path]::Combine($Path, $Name, $exact)
                $null = New-Item -ItemType Directory -Path $d -Force
                Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$Name.psd1")) -Value "@{ ModuleVersion = '$exact'; RequiredModules = $requires }"
                [PSCustomObject]@{ Name = $Name; Version = $exact; Repository = 'Internal' }
            }
            Mock 'Get-Module' -ParameterFilter { $FullyQualifiedName.Name -eq 'AWS.Tools.Installer' }

            $null = Save-LambdaPSMissingModule -Module @{ ModuleName = 'LambdaPSFake.NeedsInstaller'; RequiredVersion = '1.0.0' } -Path $dl -WarningAction SilentlyContinue

            Should -Invoke 'Save-LambdaPSModuleFromRepository' -Times 1 -Exactly -ParameterFilter {
                $Name -eq 'AWS.Tools.Installer' -and ($ModuleRepository -join ',') -eq 'PSGallery' -and [bool]$AuthenticodeCheck -eq [bool]$IsWindows -and $InstallerDefault
            }
            Should -Invoke 'Save-LambdaPSModuleFromRepository' -Times 1 -Exactly -ParameterFilter { $Name -eq 'LambdaPSFake.NeedsInstaller' -and -not $ModuleRepository }
        }
    }

    It 'passes the repositories to the AWS.Tools download, which saves the Installer from them' {
        InModuleScope $script:moduleName -Parameters @{ dl = (Join-Path $TestDrive 'resolve-repo-aws') } {
            param($dl)
            Mock 'Get-PSResourceRepository' -MockWith { [PSCustomObject]@{ Name = 'Internal' } }
            Mock 'Save-LambdaPSAWSToolsModule' -MockWith {
                foreach ($n in @($Name + 'AWS.Tools.Common')) {
                    $d = [System.IO.Path]::Combine($Path, $n, $Version)
                    $null = New-Item -ItemType Directory -Path $d -Force
                    Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$n.psd1")) -Value "@{ ModuleVersion = '$Version' }"
                }
            }

            $null = Save-LambdaPSMissingModule -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '9.9.3' } -Path $dl -ModuleRepository Internal -WarningAction SilentlyContinue

            Should -Invoke 'Save-LambdaPSAWSToolsModule' -Times 1 -Exactly -ParameterFilter { $ModuleRepository -eq 'Internal' }
        }
    }
}

# -Confirm runs in a child pwsh, whose console answers the prompts. The answers are 'Y' for each
# prompt a stage should ask, then 'N': a prompt per module would decline something and leave it out.
Describe 'Save-LambdaPSMissingModule -Confirm' -Tag 'Unit' {
    It 'asks once to download the set with its dependencies, and once to install it' {
        $dl = Join-Path $TestDrive 'confirm-dl'
        $user = Join-Path $TestDrive 'confirm-user'
        # A needs C, which is only known once A's manifest is on disk (a second pass).
        $stub = @"
function script:Get-LambdaPSCurrentUserModulePath { '$user' }
function script:Save-LambdaPSModuleFromRepository {
    param([string]`$Name, [string]`$VersionRange, [string]`$Path, [string[]]`$ModuleRepository)
    `$exact = `$VersionRange.Trim('[', ']')
    `$requires = if (`$Name -eq 'LambdaPSFake.CA') { "@(@{ ModuleName = 'LambdaPSFake.CC'; RequiredVersion = '1.0.0' })" } else { '@()' }
    `$d = [System.IO.Path]::Combine(`$Path, `$Name, `$exact)
    # Like the real download, the stand-in writes without asking.
    `$null = New-Item -ItemType Directory -Path `$d -Force -Confirm:`$false
    Set-Content -LiteralPath ([System.IO.Path]::Combine(`$d, "`$Name.psd1")) -Value "@{ ModuleVersion = '`$exact'; RequiredModules = `$requires }" -Confirm:`$false
    [PSCustomObject]@{ Name = `$Name; Version = `$exact; Repository = 'Internal' }
}
"@
        $command = "`$null = & (Get-Module AWS.ToolExtras.LambdaPSRuntime) { Save-LambdaPSMissingModule -Module @(" +
            "@{ ModuleName = 'LambdaPSFake.CA'; RequiredVersion = '1.0.0' }, @{ ModuleName = 'LambdaPSFake.CB'; RequiredVersion = '1.0.0' }) " +
            "-Path '$dl' -MissingModuleAction Install -Confirm -WarningAction SilentlyContinue }"

        $null = Invoke-InChildPwsh -Stub $stub -Command $command -Answer 'Y', 'Y', 'N', 'N', 'N', 'N'

        foreach ($n in 'LambdaPSFake.CA', 'LambdaPSFake.CB', 'LambdaPSFake.CC') {
            [System.IO.Path]::Combine($dl, $n, '1.0.0', "$n.psd1") | Should -Exist
            [System.IO.Path]::Combine($user, $n, '1.0.0', "$n.psd1") | Should -Exist
        }
    }

    # A declined download is not a -WhatIf preview: a caller that stages the set must tell them apart.
    It 'returns a declined download as Declined, not Pending, and downloads nothing more' {
        $dl = Join-Path $TestDrive 'confirm-declined'
        $stub = @"
function script:Save-LambdaPSModuleFromRepository {
    param([string]`$Name, [string]`$VersionRange, [string]`$Path, [string[]]`$ModuleRepository)
    `$script:LambdaPSCalls.Add("download `$Name")
    `$exact = `$VersionRange.Trim('[', ']')
    `$d = [System.IO.Path]::Combine(`$Path, `$Name, `$exact)
    `$null = New-Item -ItemType Directory -Path `$d -Force -Confirm:`$false
    Set-Content -LiteralPath ([System.IO.Path]::Combine(`$d, "`$Name.psd1")) -Value "@{ ModuleVersion = '`$exact' }" -Confirm:`$false
    [PSCustomObject]@{ Name = `$Name; Version = `$exact; Repository = 'Internal' }
}
"@
        $command = "`$r = & (Get-Module AWS.ToolExtras.LambdaPSRuntime) { Save-LambdaPSMissingModule -Module @(" +
            "@{ ModuleName = 'LambdaPSFake.DA'; RequiredVersion = '1.0.0' }, @{ ModuleName = 'LambdaPSFake.DB'; RequiredVersion = '1.0.0' }) " +
            "-Path '$dl' -Confirm -WarningAction SilentlyContinue }; 'PENDING:' + (@(`$r.Pending) -join ','); 'DECLINED:' + (@(`$r.Declined) -join ',')"

        # 'N' declines the download; the 'Y's would download a module a later prompt asked about.
        $result = Invoke-InChildPwsh -Stub $stub -Command $command -Answer 'N', 'Y', 'Y'

        $lines = $result.Output -split "`n"
        $lines | Should -Contain 'PENDING:'
        $lines | Should -Contain 'DECLINED:LambdaPSFake.DA 1.0.0,LambdaPSFake.DB 1.0.0'
        $result.Calls | Should -BeNullOrEmpty
        $dl | Should -Not -Exist
    }

    # The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
    # surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
    It 'surfaces <ErrorId> (<Case>) from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{
            ErrorId = 'ModuleDownloadFailed'; Case = 'download without the module'
            Setup = { Mock 'Save-LambdaPSModuleFromRepository' -MockWith { [PSCustomObject]@{ Name = $Name; Version = '1.0.0'; Repository = 'Internal' } } }
            Call = { Save-LambdaPSMissingModule -Module @{ ModuleName = 'LambdaPSFake.A'; RequiredVersion = '1.0.0' } -Path (Join-Path $Arg 'caller-dl') }
        }
        @{ ErrorId = 'ModuleVersionRequired'; Case = 'closure error'; Call = { Save-LambdaPSMissingModule -Module @{ ModuleName = 'LambdaPSFake.A'; ModuleVersion = '1.0.0' } -Path $Arg } }
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
            @($ids | Where-Object { $_ -like '*,Save-LambdaPSMissingModule' }) | Should -BeNullOrEmpty
        }
    }
}
