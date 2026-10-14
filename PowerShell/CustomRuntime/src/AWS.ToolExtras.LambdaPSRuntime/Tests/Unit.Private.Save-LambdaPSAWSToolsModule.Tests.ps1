BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))

    # Writes a fake AWS.Tools.Installer into -Path; its Install-AWSToolsModule runs -Body.
    function New-FakeInstaller {
        [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Test helper: it writes only under the folder a test passes in.')]
        param([string]$Path, [string]$Version, [string]$Body)
        $null = New-Item -ItemType Directory -Path $Path -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($Path, 'AWS.Tools.Installer.psd1')) -Value (
            "@{ ModuleVersion = '$Version'; GUID = '$([guid]::NewGuid())'; RootModule = 'AWS.Tools.Installer.psm1'; FunctionsToExport = @('Install-AWSToolsModule') }")
        Set-Content -LiteralPath ([System.IO.Path]::Combine($Path, 'AWS.Tools.Installer.psm1')) -Value (
            "function Install-AWSToolsModule { [CmdletBinding(SupportsShouldProcess)] param([string[]]`$Name, [string]`$Version, [string]`$Path, [switch]`$Force) $Body }")
        [System.IO.Path]::Combine($Path, 'AWS.Tools.Installer.psd1')
    }
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Save-LambdaPSAWSToolsModule' -Tag 'Unit' {
    It 'saves AWS.Tools.Installer 2.x with PSResourceGet when it is not installed, and calls it with -Path and -Force' {
        try {
            $installerDir = Join-Path $TestDrive 'bootstrap'
            # A fake Installer 2.x whose Install-AWSToolsModule records how it was called. The mocked
            # PSResourceGet save copies it into place, as the real one would.
            $template = [System.IO.Path]::Combine($TestDrive, 'installer-template')
            $null = New-Item -ItemType Directory -Path $template -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($template, 'AWS.Tools.Installer.psd1')) -Value (
                "@{ ModuleVersion = '2.0.9'; GUID = '$([guid]::NewGuid())'; RootModule = 'AWS.Tools.Installer.psm1'; FunctionsToExport = @('Install-AWSToolsModule') }")
            Set-Content -LiteralPath ([System.IO.Path]::Combine($template, 'AWS.Tools.Installer.psm1')) -Value @'
function Install-AWSToolsModule {
    [CmdletBinding(SupportsShouldProcess)]
    param([string[]]$Name, [string]$Version, [string]$Path, [switch]$Force)
    $global:LambdaPSFakeInstallerCall = @{ Name = $Name; Version = $Version; Path = $Path; Force = [bool]$Force }
    Write-Host 'installer chatter'
}
'@

            InModuleScope $script:moduleName -Parameters @{ installerDir = $installerDir; dl = (Join-Path $TestDrive 'aws-dl'); template = $template } {
                param($installerDir, $dl, $template)
                Mock 'Get-Module' -ParameterFilter { $ListAvailable -and $Name -eq 'AWS.Tools.Installer' } -MockWith { }
                Mock 'Save-LambdaPSModuleFromRepository' -MockWith {
                    $d = [System.IO.Path]::Combine($Path, 'AWS.Tools.Installer', '2.0.9')
                    $null = New-Item -ItemType Directory -Path $d -Force
                    Copy-Item -Path ([System.IO.Path]::Combine($template, '*')) -Destination $d
                    [PSCustomObject]@{ Name = $Name; Version = '2.0.9'; Repository = 'PSGallery' }
                }

                Save-LambdaPSAWSToolsModule -Name 'AWS.Tools.S3' -Version '9.9.1' -Path $dl -InstallerPath $installerDir 6>&1 |
                    Should -BeNullOrEmpty -Because 'the installer host output is suppressed'

                Should -Invoke 'Save-LambdaPSModuleFromRepository' -Times 1 -Exactly -ParameterFilter {
                    $Name -eq 'AWS.Tools.Installer' -and $VersionRange -eq '[2.0.5,3.0)' -and $Path -eq $installerDir -and
                    ($ModuleRepository -join ',') -eq 'PSGallery' -and $AuthenticodeCheck -eq $IsWindows -and $InstallerDefault
                }
                # The installer writes into an empty temporary folder, never straight into -Path.
                $global:LambdaPSFakeInstallerCall.Path | Should -Not -Be $dl
                $global:LambdaPSFakeInstallerCall.Path | Should -BeLike "$([System.IO.Path]::GetTempPath().TrimEnd([System.IO.Path]::DirectorySeparatorChar))*lambdaps-awstools-*"
                $global:LambdaPSFakeInstallerCall.Force | Should -BeTrue
                $global:LambdaPSFakeInstallerCall.Version | Should -Be '9.9.1'
            }
        }
        finally {
            Remove-Variable -Name LambdaPSFakeInstallerCall -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'says what AWS.Tools.Installer was needed for when saving it fails' {
        InModuleScope $script:moduleName -Parameters @{ installerDir = (Join-Path $TestDrive 'bootstrap-fails') } {
            param($installerDir)
            Mock 'Get-Module' -ParameterFilter { $ListAvailable -and $Name -eq 'AWS.Tools.Installer' } -MockWith { }
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith {
                throw [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new("Could not download module 'AWS.Tools.Installer' [2.0.5,3.0) from repository 'PSGallery' with PSResourceGet: offline"),
                    'ModuleDownloadFailed', [System.Management.Automation.ErrorCategory]::ResourceUnavailable, 'AWS.Tools.Installer [2.0.5,3.0)')
            }

            $err = { Save-LambdaPSAWSToolsModule -Name 'AWS.Tools.S3' -Version '9.9.1' -Path $TestDrive -InstallerPath $installerDir } |
                Should -Throw -ErrorId 'ModuleDownloadFailed' -PassThru
            $err.Exception.Message | Should -BeLike ("AWS.Tools.Installer 2.0.5 or a later 2.x is needed to download AWS.Tools.S3 9.9.1, and it is not installed. " +
                "Saving it failed: Could not download module 'AWS.Tools.Installer' ``[2.0.5,3.0``) from repository 'PSGallery' with PSResourceGet: offline")
            $err.TargetObject | Should -Be 'AWS.Tools.Installer [2.0.5,3.0)'
        }
    }

    It 'does not use an installed AWS.Tools.Installer older than 2.0.5' {
        InModuleScope $script:moduleName -Parameters @{ installerDir = (Join-Path $TestDrive 'bootstrap-old') } {
            param($installerDir)
            Mock 'Get-Module' -ParameterFilter { $ListAvailable -and $Name -eq 'AWS.Tools.Installer' } -MockWith {
                [PSCustomObject]@{ Name = 'AWS.Tools.Installer'; Version = [version]'2.0.4'; Path = '/old/AWS.Tools.Installer.psd1' }
            }
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith { throw 'saved instead' }

            { Save-LambdaPSAWSToolsModule -Name 'AWS.Tools.S3' -Version '9.9.1' -Path $TestDrive -InstallerPath $installerDir } |
                Should -Throw '*saved instead*'
            Should -Invoke 'Save-LambdaPSModuleFromRepository' -Times 1 -Exactly -ParameterFilter { $VersionRange -eq '[2.0.5,3.0)' }

            { Save-LambdaPSAWSToolsModule -Name 'AWS.Tools.S3' -Version '9.9.1' -Path $TestDrive -InstallerPath $installerDir -ModuleRepository Internal } |
                Should -Throw '*saved instead*'
            Should -Invoke 'Save-LambdaPSModuleFromRepository' -Times 1 -Exactly -ParameterFilter { $Name -eq 'AWS.Tools.Installer' -and $ModuleRepository -eq 'Internal' -and -not $InstallerDefault }
        }
    }

    It 'uses an installed AWS.Tools.Installer 2.x without saving one, and ignores an installed 3.x' {
        # The fake installer writes a module folder for the version it was asked for.
        $supported = New-FakeInstaller -Path (Join-Path $TestDrive 'installed-2.0.6') -Version '2.0.6' -Body '$d = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($Path, "AWS.Tools.S3", $Version)) -Force; Set-Content -LiteralPath ([System.IO.Path]::Combine($d.FullName, "AWS.Tools.S3.psd1")) -Value "@{}"'
        $newer = New-FakeInstaller -Path (Join-Path $TestDrive 'installed-3.0.0') -Version '3.0.0' -Body 'throw "3.x must not be used"'
        $dl = Join-Path $TestDrive 'aws-dl-installed'

        InModuleScope $script:moduleName -Parameters @{ supported = $supported; newer = $newer; dl = $dl } {
            param($supported, $newer, $dl)
            Mock 'Get-Module' -ParameterFilter { $ListAvailable -and $Name -eq 'AWS.Tools.Installer' } -MockWith {
                [PSCustomObject]@{ Name = 'AWS.Tools.Installer'; Version = [version]'3.0.0'; Path = $newer }
                [PSCustomObject]@{ Name = 'AWS.Tools.Installer'; Version = [version]'2.0.6'; Path = $supported }
            }
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith { throw 'must not save an installer' }

            Save-LambdaPSAWSToolsModule -Name 'AWS.Tools.S3' -Version '9.9.1' -Path $dl -InstallerPath (Join-Path $dl '.unused')

            Should -Invoke 'Save-LambdaPSModuleFromRepository' -Times 0 -Exactly
        }
        Test-Path -LiteralPath ([System.IO.Path]::Combine($dl, 'AWS.Tools.S3', '9.9.1', 'AWS.Tools.S3.psd1')) | Should -BeTrue
    }

    It 'saves a 2.x installer when only a 3.x is installed' {
        InModuleScope $script:moduleName -Parameters @{ installerDir = (Join-Path $TestDrive 'bootstrap-3x') } {
            param($installerDir)
            Mock 'Get-Module' -ParameterFilter { $ListAvailable -and $Name -eq 'AWS.Tools.Installer' } -MockWith {
                [PSCustomObject]@{ Name = 'AWS.Tools.Installer'; Version = [version]'3.0.0'; Path = '/new/AWS.Tools.Installer.psd1' }
            }
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith { throw 'saved instead' }

            { Save-LambdaPSAWSToolsModule -Name 'AWS.Tools.S3' -Version '9.9.1' -Path $TestDrive -InstallerPath $installerDir } |
                Should -Throw '*saved instead*'
            Should -Invoke 'Save-LambdaPSModuleFromRepository' -Times 1 -Exactly -ParameterFilter { $VersionRange -eq '[2.0.5,3.0)' }
        }
    }

    It 'leaves nothing in -Path, and removes its temporary folder, when the installer fails partway' {
        # The installer writes the manifests, then fails before the rest of the module is written.
        $body = '$global:LambdaPSFakeStaging = $Path; foreach ($n in @($Name + "AWS.Tools.Common")) { $d = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($Path, $n, $Version)) -Force; Set-Content -LiteralPath ([System.IO.Path]::Combine($d.FullName, "$n.psd1")) -Value "@{}" }; throw "disk full"'
        $installer = New-FakeInstaller -Path (Join-Path $TestDrive 'installed-partial') -Version '2.0.6' -Body $body
        $dl = Join-Path $TestDrive 'aws-dl-partial'
        try {
            InModuleScope $script:moduleName -Parameters @{ installer = $installer; dl = $dl } {
                param($installer, $dl)
                Mock 'Get-Module' -ParameterFilter { $ListAvailable -and $Name -eq 'AWS.Tools.Installer' } -MockWith {
                    [PSCustomObject]@{ Name = 'AWS.Tools.Installer'; Version = [version]'2.0.6'; Path = $installer }
                }
                { Save-LambdaPSAWSToolsModule -Name 'AWS.Tools.S3' -Version '9.9.1' -Path $dl -InstallerPath (Join-Path $TestDrive 'unused') } |
                    Should -Throw -ErrorId 'ModuleDownloadFailed'
            }
            Test-Path -LiteralPath $dl | Should -BeFalse
            Test-Path -LiteralPath $global:LambdaPSFakeStaging | Should -BeFalse
        }
        finally {
            Remove-Variable -Name LambdaPSFakeStaging -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'copies each new module version into -Path, and keeps a version folder already there' {
        $body = 'foreach ($n in @($Name + "AWS.Tools.Common")) { $d = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($Path, $n, $Version)) -Force; Set-Content -LiteralPath ([System.IO.Path]::Combine($d.FullName, "$n.psd1")) -Value "# downloaded" }'
        $installer = New-FakeInstaller -Path (Join-Path $TestDrive 'installed-keep') -Version '2.0.6' -Body $body
        $dl = Join-Path $TestDrive 'aws-dl-keep'
        $common = [System.IO.Path]::Combine($dl, 'AWS.Tools.Common', '9.9.1')
        $null = New-Item -ItemType Directory -Path $common -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($common, 'AWS.Tools.Common.psd1')) -Value '# already here'

        InModuleScope $script:moduleName -Parameters @{ installer = $installer; dl = $dl } {
            param($installer, $dl)
            Mock 'Get-Module' -ParameterFilter { $ListAvailable -and $Name -eq 'AWS.Tools.Installer' } -MockWith {
                [PSCustomObject]@{ Name = 'AWS.Tools.Installer'; Version = [version]'2.0.6'; Path = $installer }
            }
            Save-LambdaPSAWSToolsModule -Name 'AWS.Tools.S3' -Version '9.9.1' -Path $dl -InstallerPath (Join-Path $TestDrive 'unused')
        }
        Get-Content -LiteralPath ([System.IO.Path]::Combine($dl, 'AWS.Tools.S3', '9.9.1', 'AWS.Tools.S3.psd1')) | Should -Be '# downloaded'
        Get-Content -LiteralPath ([System.IO.Path]::Combine($common, 'AWS.Tools.Common.psd1')) | Should -Be '# already here'
    }

    It 'wraps an installer failure as ModuleDownloadFailed' {
        $installer = New-FakeInstaller -Path (Join-Path $TestDrive 'installed-failing') -Version '2.0.6' -Body 'throw "CloudFront unreachable"'

        InModuleScope $script:moduleName -Parameters @{ installer = $installer } {
            param($installer)
            Mock 'Get-Module' -ParameterFilter { $ListAvailable -and $Name -eq 'AWS.Tools.Installer' } -MockWith {
                [PSCustomObject]@{ Name = 'AWS.Tools.Installer'; Version = [version]'2.0.6'; Path = $installer }
            }

            $err = { Save-LambdaPSAWSToolsModule -Name 'AWS.Tools.S3', 'AWS.Tools.EC2' -Version '9.9.1' -Path (Join-Path $TestDrive 'aws-dl-fail') -InstallerPath (Join-Path $TestDrive 'unused') } |
                Should -Throw -ErrorId 'ModuleDownloadFailed' -PassThru
            $err.Exception.Message | Should -BeLike '*AWS.Tools.S3, AWS.Tools.EC2 9.9.1*CloudFront unreachable*'
            $err.Exception | Should -BeOfType [System.InvalidOperationException]
            $err.TargetObject | Should -Be @('AWS.Tools.S3 9.9.1', 'AWS.Tools.EC2 9.9.1')
        }
    }

    It 'throws AWSToolsInstallerImportFailed when the installer cannot be imported' {
        $installerDir = Join-Path $TestDrive 'bootstrap-broken'
        $broken = [System.IO.Path]::Combine($installerDir, 'AWS.Tools.Installer', '2.0.9')
        $null = New-Item -ItemType Directory -Path $broken -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($broken, 'AWS.Tools.Installer.psd1')) -Value (
            "@{ ModuleVersion = '2.0.9'; GUID = '$([guid]::NewGuid())'; RequiredModules = @(@{ ModuleName = 'LambdaPSFake.NotInstalled'; ModuleVersion = '1.0' }) }")

        InModuleScope $script:moduleName -Parameters @{ installerDir = $installerDir } {
            param($installerDir)
            Mock 'Get-Module' -ParameterFilter { $ListAvailable -and $Name -eq 'AWS.Tools.Installer' } -MockWith { }
            Mock 'Save-LambdaPSModuleFromRepository' -MockWith { throw 'an installer saved earlier in the run must be reused' }

            $err = { Save-LambdaPSAWSToolsModule -Name 'AWS.Tools.S3' -Version '9.9.1' -Path $TestDrive -InstallerPath $installerDir } |
                Should -Throw -ErrorId 'AWSToolsInstallerImportFailed' -PassThru
            # The import's own error comes first; PowerShellGet is named as one possible cause, not the cause.
            $err.Exception.Message | Should -BeLike ("Could not import AWS.Tools.Installer 2.0.9: *LambdaPSFake.NotInstalled* " +
                'AWS.Tools.Installer requires the PowerShellGet module; if PowerShellGet is installed, the error above is the cause.')
        }
    }

    # The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
    # surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
    It 'surfaces <ErrorId> (<Case>) from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{
            ErrorId = 'ModuleDownloadFailed'; Case = 'installer failure'
            Prepare = { New-FakeInstaller -Path (Join-Path $TestDrive 'caller-installer') -Version '2.0.6' -Body 'throw "CloudFront unreachable"' }
            Setup = { Mock 'Get-Module' -ParameterFilter { $ListAvailable -and $Name -eq 'AWS.Tools.Installer' } -MockWith { [PSCustomObject]@{ Name = 'AWS.Tools.Installer'; Version = [version]'2.0.6'; Path = $Arg } } }
            Call = { Save-LambdaPSAWSToolsModule -Name 'AWS.Tools.S3' -Version '9.9.1' -Path (Join-Path $TestDrive 'caller-dl') -InstallerPath (Join-Path $TestDrive 'caller-unused') }
        }
        @{
            ErrorId = 'AWSToolsInstallerImportFailed'; Case = 'broken installer'
            Prepare = {
                $installerDir = Join-Path $TestDrive 'caller-broken'
                $broken = [System.IO.Path]::Combine($installerDir, 'AWS.Tools.Installer', '2.0.9')
                $null = New-Item -ItemType Directory -Path $broken -Force
                Set-Content -LiteralPath ([System.IO.Path]::Combine($broken, 'AWS.Tools.Installer.psd1')) -Value (
                    "@{ ModuleVersion = '2.0.9'; GUID = '$([guid]::NewGuid())'; RequiredModules = @(@{ ModuleName = 'LambdaPSFake.NotInstalled'; ModuleVersion = '1.0' }) }")
                $installerDir
            }
            Setup = {
                Mock 'Get-Module' -ParameterFilter { $ListAvailable -and $Name -eq 'AWS.Tools.Installer' } -MockWith { }
                Mock 'Save-LambdaPSModuleFromRepository' -MockWith { throw 'an installer saved earlier in the run must be reused' }
            }
            Call = { Save-LambdaPSAWSToolsModule -Name 'AWS.Tools.S3' -Version '9.9.1' -Path $TestDrive -InstallerPath $Arg }
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
            @($ids | Where-Object { $_ -like '*,Save-LambdaPSAWSToolsModule' }) | Should -BeNullOrEmpty
        }
    }
}
