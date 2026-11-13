# InModuleScope wraps the tests at discovery, so the module is imported then too.
BeforeDiscovery {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' {
    Describe 'New-LambdaPSRuntimeDeployment -BuildMethod' -Tag 'Unit' {
        BeforeEach {
            Push-Location $TestDrive
            Mock 'Get-LambdaPSDockerBuilderPlatform' -MockWith { }
            Mock 'Test-LambdaPSContainerRuntime' -MockWith {
                throw [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new('The docker CLI was not found on PATH.'), 'DockerNotFound',
                    [System.Management.Automation.ErrorCategory]::NotInstalled, 'docker')
            }
            Mock 'Import-LambdaPSOciBuilder' -MockWith { }
            Mock 'Get-STSCallerIdentity' -MockWith { [PSCustomObject]@{ Account = '111122223333' } }
            Mock 'Get-LMCRuntime' -MockWith { [PSCustomObject]@{ Name = $RuntimeName } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime version does not exist') }
            Mock 'Get-LMCRuntimeVersionList' -MockWith { }
            Mock 'Get-LMCRuntimeAlias' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime alias does not exist') }
            Mock 'Get-LambdaPSPwshRepositoryRelease' -MockWith { $script:Config.pwsh.DefaultPatch[$PowerShellVersion] }
            Mock 'Get-ECRImageMetadata' -MockWith { throw [LambdaPSTests.Fakes.ImageNotFoundException]::new('The image does not exist') }
            Mock 'New-LambdaPSRuntimeProject' -MockWith { }
            Mock 'Initialize-LambdaPSEcrRepository' -MockWith { $RepositoryName }
            Mock 'Build-LambdaPSRuntimeImage' -MockWith {
                [PSCustomObject]@{ Tag = $Tag; ImageUri = $ImageUri; ImageDigest = $(if ($ImageUri) { 'sha256:abc123' }); Architecture = $Architecture; Pushed = [bool]$ImageUri }
            }
            Mock 'Publish-LambdaPSRuntimeVersion' -MockWith {
                [PSCustomObject]@{ RuntimeName = $RuntimeName; ImageUri = $ImageUri; Version = 'v1'; Arn = "arn:aws:lambda:eu-west-1:111122223333:runtime/$RuntimeName/version/v1" }
            }
            Mock 'Set-LambdaPSRuntimeAlias' -MockWith { $RuntimeAlias }
            $script:splat = @{ RuntimeName = 'my-ps'; PowerShellVersion = '7.4'; Region = 'eu-west-1'; RepositoryName = 'rt' }
        }

        AfterEach {
            Pop-Location
        }

        It 'deploys without Docker under Auto: no local build, no docker login, a NoDocker push with the credentials' {
            Mock 'Get-LambdaPSCredentialParameter' -MockWith { @{ ProfileName = 'dev' } }
            $result = New-LambdaPSRuntimeDeployment @script:splat -ProfileName dev

            $result.ImageUri | Should -Be "111122223333.dkr.ecr.eu-west-1.amazonaws.com/rt:$($script:Config.pwsh.DefaultPatch['7.4'])-1"
            Should -Invoke 'Import-LambdaPSOciBuilder' -Times 1 -Exactly
            Should -Invoke 'Initialize-LambdaPSEcrRepository' -Times 1 -Exactly -ParameterFilter { $SkipDockerLogin }
            Should -Invoke 'Build-LambdaPSRuntimeImage' -Times 1 -Exactly
            Should -Invoke 'Build-LambdaPSRuntimeImage' -Times 1 -Exactly -ParameterFilter {
                $BuildMethod -eq 'NoDocker' -and $ImageUri -and $Region -eq 'eu-west-1' -and $ProfileName -eq 'dev'
            }
            Should -Invoke 'Publish-LambdaPSRuntimeVersion' -Times 1 -Exactly -ParameterFilter {
                $ImageUri -eq '111122223333.dkr.ecr.eu-west-1.amazonaws.com/rt@sha256:abc123'
            }
        }

        It 'keeps the Docker flow (local build, login, push) when docker is installed, for an unpinned x86_64 pwsh too' {
            Mock 'Test-LambdaPSContainerRuntime' -MockWith { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker'; BuildxVersion = 'v0' } }

            $null = New-LambdaPSRuntimeDeployment @script:splat -PwshVersion '7.4.99'

            Should -Invoke 'Import-LambdaPSOciBuilder' -Times 0 -Exactly
            Should -Invoke 'Initialize-LambdaPSEcrRepository' -Times 1 -Exactly -ParameterFilter { -not $SkipDockerLogin }
            Should -Invoke 'Build-LambdaPSRuntimeImage' -Times 2 -Exactly -ParameterFilter { $BuildMethod -ne 'NoDocker' }
        }

        It 'Should deploy without Docker under Auto when docker''s builder lists no linux/arm64 platform' {
            # Arrange
            Mock 'Test-LambdaPSContainerRuntime' -MockWith { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker'; BuildxVersion = 'v0' } }
            Mock 'Get-LambdaPSDockerBuilderPlatform' -ParameterFilter { $Path -eq '/usr/bin/docker' } -MockWith { 'linux/amd64', 'linux/386' }

            # Act
            $null = New-LambdaPSRuntimeDeployment @script:splat -Architecture arm64

            # Assert
            Should -Invoke 'Get-LambdaPSDockerBuilderPlatform' -ParameterFilter { $Path -eq '/usr/bin/docker' } -Times 1 -Exactly
            Should -Invoke 'Build-LambdaPSRuntimeImage' -Times 1 -Exactly -ParameterFilter { $BuildMethod -eq 'NoDocker' -and $Architecture -eq 'arm64' }
        }

        It 'installs the newest pinned x86_64 release, with a warning, when the repository has a newer one' {
            # Arrange
            Mock 'Get-LambdaPSPwshRepositoryRelease' -MockWith { '7.4.99' }
            $pinned = @($script:Config.pwsh.ArchiveSha256.Keys | Where-Object { $_ -like '7.4.*' -and $script:Config.pwsh.ArchiveSha256[$_].x64 } |
                Sort-Object { [version]$_ })[-1]

            # Act
            $result = New-LambdaPSRuntimeDeployment @script:splat -WarningVariable warnings -WarningAction SilentlyContinue

            # Assert
            $result.RuntimeVersion | Should -Be "$pinned-1"
            @($warnings) -like '*pwsh 7.4.99 is the newest*no-Docker x86_64 build*-BuildMethod Docker*' | Should -HaveCount 1
            Should -Invoke 'New-LambdaPSRuntimeProject' -Times 1 -Exactly -ParameterFilter { $PwshVersion -eq $pinned }
        }

        It 'refuses an x86_64 pwsh release with no pinned x64 hash in preflight, under -WhatIf too, before anything is created' {
            { New-LambdaPSRuntimeDeployment @script:splat -PwshVersion '7.4.99' -WhatIf } |
                Should -Throw -ErrorId 'PwshX64HashNotPinned,New-LambdaPSRuntimeDeployment'
            Should -Invoke 'New-LambdaPSRuntimeProject' -Times 0 -Exactly
            Should -Invoke 'Initialize-LambdaPSEcrRepository' -Times 0 -Exactly
        }

        Context 'when the repository has an x86_64 release newer than the pins' {
            BeforeEach {
                Mock 'Get-LambdaPSPwshRepositoryRelease' -MockWith { '7.4.99' }
            }

            It 'reports RuntimeVersionExists for an existing -RuntimeVersion' {
                # Arrange
                Mock 'Get-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ RuntimeVersion = 'v1' } }

                # Act / Assert
                { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 -WarningAction SilentlyContinue } |
                    Should -Throw -ErrorId 'RuntimeVersionExists,New-LambdaPSRuntimeDeployment'
            }

            It 'advises building with Docker when the alias runs a newer release with no pinned x64 hash' {
                # Arrange
                Mock 'Get-LMCRuntimeAlias' -MockWith { [PSCustomObject]@{ Name = 'default'; RuntimeVersion = '7.4.99-1' } }

                # Act / Assert
                { New-LambdaPSRuntimeDeployment @script:splat -WhatIf -WarningAction SilentlyContinue } |
                    Should -Throw -ErrorId 'PwshReleaseOlderThanAlias,New-LambdaPSRuntimeDeployment' -ExpectedMessage '*runs pwsh 7.4.99*Build with Docker (-BuildMethod Docker) to keep it*'
            }

            It 'previews under -WhatIf without building' {
                # Act
                $null = New-LambdaPSRuntimeDeployment @script:splat -WhatIf -WarningAction SilentlyContinue

                # Assert
                Should -Invoke 'Build-LambdaPSRuntimeImage' -Times 0 -Exactly
                Should -Invoke 'Publish-LambdaPSRuntimeVersion' -Times 0 -Exactly
            }
        }

        It 'fails in preflight under -BuildMethod Docker when docker is missing, before anything is created' {
            { New-LambdaPSRuntimeDeployment @script:splat -BuildMethod Docker } | Should -Throw -ErrorId 'DockerNotFound,New-LambdaPSRuntimeDeployment'
            Should -Invoke 'New-LambdaPSRuntimeProject' -Times 0 -Exactly
            Should -Invoke 'Initialize-LambdaPSEcrRepository' -Times 0 -Exactly
        }
    }
}
