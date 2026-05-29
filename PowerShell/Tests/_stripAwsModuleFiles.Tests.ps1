$module = 'AWSLambdaPSCore'
$moduleManifestPath = [System.IO.Path]::Combine('..', 'Module', "$module.psd1")

if (Get-Module -Name $module) {Remove-Module -Name $module}
Import-Module $moduleManifestPath

  InModuleScope -ModuleName $module -ScriptBlock {
    Describe -Name '_stripAwsModuleFiles' -Fixture {

        BeforeAll {
            function New-FakeModuleDir
            {
                param
                (
                    [Parameter(Mandatory = $true)][string]$Root,
                    [Parameter(Mandatory = $true)][string]$Name,
                    [Parameter()][string[]]$ExtraFiles = @()
                )

                $dir = Join-Path -Path $Root -ChildPath $Name
                New-Item -ItemType Directory -Path $dir -Force | Out-Null

                $defaults = @(
                    'AWS.Tools.S3.dll-Help.xml',
                    'AWS.Tools.S3-Help.xml',
                    'LICENSE',
                    'LICENSE.txt',
                    'NOTICE',
                    'NOTICE.txt',
                    'AWS.Tools.S3.pdb',
                    'AWS.Tools.S3.psm1',
                    'AWS.Tools.S3.psd1'
                )
                foreach ($f in ($defaults + $ExtraFiles))
                {
                    Set-Content -Path (Join-Path -Path $dir -ChildPath $f) -Value 'x' -Force
                }
                return $dir
            }
        }

        Context -Name 'AWS-authored module directories' -Fixture {

            It -Name 'Strips unwanted files from AWS.Tools.* modules' -Test {
                $root = Join-Path -Path $TestDrive -ChildPath 'aws-tools'
                New-Item -ItemType Directory -Path $root -Force | Out-Null
                $moduleDir = New-FakeModuleDir -Root $root -Name 'AWS.Tools.S3'

                _stripAwsModuleFiles `
                    -ModulesRoot $root `
                    -Filters $AwsModuleStripFilters `
                    -ModuleNamePatterns $AwsAuthoredModuleNamePatterns

                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.dll-Help.xml')  | Should -BeFalse
                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3-Help.xml')      | Should -BeFalse
                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.pdb')           | Should -BeFalse
                Test-Path -Path (Join-Path $moduleDir 'LICENSE')                    | Should -BeTrue
                Test-Path -Path (Join-Path $moduleDir 'LICENSE.txt')                | Should -BeTrue
                Test-Path -Path (Join-Path $moduleDir 'NOTICE')                     | Should -BeTrue
                Test-Path -Path (Join-Path $moduleDir 'NOTICE.txt')                 | Should -BeTrue
                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.psm1')          | Should -BeTrue
                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.psd1')          | Should -BeTrue
            }

            It -Name 'Strips unwanted files from AWSPowerShell.NetCore (exact-name match)' -Test {
                $root = Join-Path -Path $TestDrive -ChildPath 'monolithic'
                New-Item -ItemType Directory -Path $root -Force | Out-Null
                $moduleDir = New-FakeModuleDir -Root $root -Name 'AWSPowerShell.NetCore'

                _stripAwsModuleFiles `
                    -ModulesRoot $root `
                    -Filters $AwsModuleStripFilters `
                    -ModuleNamePatterns $AwsAuthoredModuleNamePatterns

                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.pdb')      | Should -BeFalse
                Test-Path -Path (Join-Path $moduleDir 'LICENSE')               | Should -BeTrue
                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.psm1')     | Should -BeTrue
            }

            It -Name 'Recurses into nested version subdirectories' -Test {
                $root = Join-Path -Path $TestDrive -ChildPath 'versioned'
                New-Item -ItemType Directory -Path $root -Force | Out-Null
                $versionDir = Join-Path -Path $root -ChildPath 'AWS.Tools.EC2\1.2.3'
                New-Item -ItemType Directory -Path $versionDir -Force | Out-Null
                Set-Content -Path (Join-Path $versionDir 'AWS.Tools.EC2.dll-Help.xml') -Value 'x'
                Set-Content -Path (Join-Path $versionDir 'AWS.Tools.EC2.pdb') -Value 'x'
                Set-Content -Path (Join-Path $versionDir 'AWS.Tools.EC2.psm1') -Value 'x'

                _stripAwsModuleFiles `
                    -ModulesRoot $root `
                    -Filters $AwsModuleStripFilters `
                    -ModuleNamePatterns $AwsAuthoredModuleNamePatterns

                Test-Path -Path (Join-Path $versionDir 'AWS.Tools.EC2.dll-Help.xml') | Should -BeFalse
                Test-Path -Path (Join-Path $versionDir 'AWS.Tools.EC2.pdb')          | Should -BeFalse
                Test-Path -Path (Join-Path $versionDir 'AWS.Tools.EC2.psm1')         | Should -BeTrue
            }
        }

        Context -Name 'Non-AWS module directories' -Fixture {

            It -Name 'Leaves third-party modules untouched' -Test {
                $root = Join-Path -Path $TestDrive -ChildPath 'third-party'
                New-Item -ItemType Directory -Path $root -Force | Out-Null
                $moduleDir = New-FakeModuleDir -Root $root -Name 'Pester'

                _stripAwsModuleFiles `
                    -ModulesRoot $root `
                    -Filters $AwsModuleStripFilters `
                    -ModuleNamePatterns $AwsAuthoredModuleNamePatterns

                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.dll-Help.xml') | Should -BeTrue
                Test-Path -Path (Join-Path $moduleDir 'LICENSE')                   | Should -BeTrue
                Test-Path -Path (Join-Path $moduleDir 'NOTICE')                    | Should -BeTrue
                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.pdb')          | Should -BeTrue
                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.psm1')         | Should -BeTrue
            }

            It -Name 'Strips AWS module while leaving co-resident third-party module untouched' -Test {
                $root = Join-Path -Path $TestDrive -ChildPath 'mixed'
                New-Item -ItemType Directory -Path $root -Force | Out-Null
                $awsDir   = New-FakeModuleDir -Root $root -Name 'AWS.Tools.Lambda'
                $otherDir = New-FakeModuleDir -Root $root -Name 'PSReadLine'

                _stripAwsModuleFiles `
                    -ModulesRoot $root `
                    -Filters $AwsModuleStripFilters `
                    -ModuleNamePatterns $AwsAuthoredModuleNamePatterns

                # AWS module: .pdb stripped, LICENSE retained
                Test-Path -Path (Join-Path $awsDir 'AWS.Tools.S3.pdb')   | Should -BeFalse
                Test-Path -Path (Join-Path $awsDir 'LICENSE')            | Should -BeTrue

                # Third-party module: nothing touched
                Test-Path -Path (Join-Path $otherDir 'AWS.Tools.S3.pdb') | Should -BeTrue
                Test-Path -Path (Join-Path $otherDir 'LICENSE')          | Should -BeTrue
            }
        }

        Context -Name 'Files intentionally retained' -Fixture {

            It -Name 'Does not remove LICENSE or NOTICE files (Apache 2.0 retention)' -Test {
                $root = Join-Path -Path $TestDrive -ChildPath 'license-retained'
                New-Item -ItemType Directory -Path $root -Force | Out-Null
                $moduleDir = New-FakeModuleDir -Root $root -Name 'AWS.Tools.S3'

                _stripAwsModuleFiles `
                    -ModulesRoot $root `
                    -Filters $AwsModuleStripFilters `
                    -ModuleNamePatterns $AwsAuthoredModuleNamePatterns

                Test-Path -Path (Join-Path $moduleDir 'LICENSE')     | Should -BeTrue
                Test-Path -Path (Join-Path $moduleDir 'LICENSE.txt') | Should -BeTrue
                Test-Path -Path (Join-Path $moduleDir 'NOTICE')      | Should -BeTrue
                Test-Path -Path (Join-Path $moduleDir 'NOTICE.txt')  | Should -BeTrue
            }

            It -Name 'Does not remove Format.ps1xml or Types.ps1xml files' -Test {
                $root = Join-Path -Path $TestDrive -ChildPath 'narrow-xml'
                New-Item -ItemType Directory -Path $root -Force | Out-Null
                $moduleDir = New-FakeModuleDir -Root $root -Name 'AWS.Tools.S3' -ExtraFiles @(
                    'AWS.Tools.S3.Format.ps1xml',
                    'AWS.Tools.S3.Types.ps1xml'
                )

                _stripAwsModuleFiles `
                    -ModulesRoot $root `
                    -Filters $AwsModuleStripFilters `
                    -ModuleNamePatterns $AwsAuthoredModuleNamePatterns

                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.dll-Help.xml')  | Should -BeFalse
                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.Format.ps1xml') | Should -BeTrue
                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.Types.ps1xml')  | Should -BeTrue
            }

            It -Name 'Strips XMLDoc (.XML) and PSGetModuleInfo.xml' -Test {
                $root = Join-Path -Path $TestDrive -ChildPath 'xmldoc'
                New-Item -ItemType Directory -Path $root -Force | Out-Null
                $moduleDir = New-FakeModuleDir -Root $root -Name 'AWS.Tools.S3' -ExtraFiles @(
                    'AWS.Tools.S3.XML',
                    'PSGetModuleInfo.xml'
                )

                _stripAwsModuleFiles `
                    -ModulesRoot $root `
                    -Filters $AwsModuleStripFilters `
                    -ModuleNamePatterns $AwsAuthoredModuleNamePatterns

                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.XML')   | Should -BeFalse
                Test-Path -Path (Join-Path $moduleDir 'PSGetModuleInfo.xml') | Should -BeFalse
            }

            It -Name 'Does not remove Aliases.psm1 or Completers.psm1' -Test {
                $root = Join-Path -Path $TestDrive -ChildPath 'preserve-nested'
                New-Item -ItemType Directory -Path $root -Force | Out-Null
                $moduleDir = New-FakeModuleDir -Root $root -Name 'AWS.Tools.S3' -ExtraFiles @(
                    'AWS.Tools.S3.Aliases.psm1',
                    'AWS.Tools.S3.Completers.psm1'
                )

                _stripAwsModuleFiles `
                    -ModulesRoot $root `
                    -Filters $AwsModuleStripFilters `
                    -ModuleNamePatterns $AwsAuthoredModuleNamePatterns

                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.Aliases.psm1')    | Should -BeTrue
                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.Completers.psm1') | Should -BeTrue
            }
        }

        Context -Name 'Robustness' -Fixture {

            It -Name 'Is idempotent (second run is a clean no-op)' -Test {
                $root = Join-Path -Path $TestDrive -ChildPath 'idempotent'
                New-Item -ItemType Directory -Path $root -Force | Out-Null
                $moduleDir = New-FakeModuleDir -Root $root -Name 'AWS.Tools.S3'

                _stripAwsModuleFiles `
                    -ModulesRoot $root `
                    -Filters $AwsModuleStripFilters `
                    -ModuleNamePatterns $AwsAuthoredModuleNamePatterns

                { _stripAwsModuleFiles `
                    -ModulesRoot $root `
                    -Filters $AwsModuleStripFilters `
                    -ModuleNamePatterns $AwsAuthoredModuleNamePatterns } | Should -Not -Throw

                Test-Path -Path (Join-Path $moduleDir 'AWS.Tools.S3.psm1') | Should -BeTrue
            }

            It -Name 'No-ops gracefully when ModulesRoot does not exist' -Test {
                $missing = Join-Path -Path $TestDrive -ChildPath 'does-not-exist'

                { _stripAwsModuleFiles `
                    -ModulesRoot $missing `
                    -Filters $AwsModuleStripFilters `
                    -ModuleNamePatterns $AwsAuthoredModuleNamePatterns } | Should -Not -Throw
            }

            It -Name 'No-ops on empty ModulesRoot' -Test {
                $root = Join-Path -Path $TestDrive -ChildPath 'empty-root'
                New-Item -ItemType Directory -Path $root -Force | Out-Null

                { _stripAwsModuleFiles `
                    -ModulesRoot $root `
                    -Filters $AwsModuleStripFilters `
                    -ModuleNamePatterns $AwsAuthoredModuleNamePatterns } | Should -Not -Throw
            }
        }
    } # End Describe
} # End InModuleScope
