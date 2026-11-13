# InModuleScope wraps the tests at discovery, so the module is imported then too.
BeforeDiscovery {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' {
    Describe 'Build-LambdaPSRuntimeImage -BuildMethod' -Tag 'Unit' {
        BeforeAll {
            $script:projectPath = [System.IO.Path]::Combine($TestDrive, 'rt')
            $null = New-LambdaPSRuntimeProject -Path $script:projectPath -PowerShellVersion '7.4' -WarningAction SilentlyContinue
            $script:imageUri = '111122223333.dkr.ecr.eu-west-1.amazonaws.com/rt:v1'
        }

        BeforeEach {
            Mock 'Invoke-LambdaPSExternalTool' -MockWith { }
            Mock 'Get-LambdaPSDockerBuilderPlatform' -MockWith { }
            Mock 'Test-LambdaPSContainerRuntime' -MockWith {
                throw [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new('The docker CLI was not found on PATH.'), 'DockerNotFound',
                    [System.Management.Automation.ErrorCategory]::NotInstalled, 'docker')
            }
            Mock 'Get-LambdaPSEcrAuthorizationToken' -MockWith { 'QVdTOnRva2Vu' }
            Mock 'Invoke-LambdaPSOciImageBuild' -MockWith { 'sha256:feed' }
        }

        It 'pushes without Docker under Auto when docker is missing, with a token for the image URI''s region' {
            $result = Build-LambdaPSRuntimeImage -Path $script:projectPath -ImageUri $script:imageUri -Architecture arm64

            $result.ImageUri | Should -Be $script:imageUri
            $result.ImageDigest | Should -Be 'sha256:feed'
            $result.Architecture | Should -Be 'arm64'
            $result.Pushed | Should -BeTrue
            Should -Invoke 'Get-LambdaPSEcrAuthorizationToken' -Times 1 -Exactly -ParameterFilter {
                $Region -eq 'eu-west-1' -and $CredentialParameter.Count -eq 0
            }
            Should -Invoke 'Invoke-LambdaPSOciImageBuild' -Times 1 -Exactly -ParameterFilter {
                $ImageUri -eq $script:imageUri -and $AuthorizationToken -eq 'QVdTOnRva2Vu' -and $Plan.Platform -eq 'linux/arm64'
            }
            Should -Invoke 'Invoke-LambdaPSExternalTool' -Times 0 -Exactly
        }

        It 'forwards -Region and the credential parameters to the token call' {
            Mock 'Get-LambdaPSCredentialParameter' -MockWith { @{ ProfileName = 'dev' } }
            $null = Build-LambdaPSRuntimeImage -Path $script:projectPath -ImageUri $script:imageUri -BuildMethod NoDocker -Region us-west-2 -ProfileName dev

            Should -Invoke 'Get-LambdaPSEcrAuthorizationToken' -Times 1 -Exactly -ParameterFilter {
                $Region -eq 'us-west-2' -and $CredentialParameter['ProfileName'] -eq 'dev'
            }
            Should -Invoke 'Test-LambdaPSContainerRuntime' -Times 0 -Exactly
        }

        It 'still runs docker under Auto when docker is installed' {
            Mock 'Test-LambdaPSContainerRuntime' -MockWith { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker'; BuildxVersion = 'v0' } }

            $null = Build-LambdaPSRuntimeImage -Path $script:projectPath -ImageUri $script:imageUri

            Should -Invoke 'Invoke-LambdaPSExternalTool' -Times 1 -Exactly
            Should -Invoke 'Invoke-LambdaPSOciImageBuild' -Times 0 -Exactly
        }

        It 'keeps DockerNotFound for a local build (-Tag), and refuses -Tag with NoDocker' {
            { Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'rt:v1' } | Should -Throw -ErrorId 'DockerNotFound,Build-LambdaPSRuntimeImage'
            { Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'rt:v1' -BuildMethod NoDocker } | Should -Throw -ErrorId 'NoDockerLocalBuild,Build-LambdaPSRuntimeImage'
        }

        It 'refuses a target that is not ECR before reading a token' {
            { Build-LambdaPSRuntimeImage -Path $script:projectPath -ImageUri 'ghcr.io/me/rt:v1' -BuildMethod NoDocker } |
                Should -Throw -ErrorId 'NoDockerTargetNotEcr,Build-LambdaPSRuntimeImage'
            Should -Invoke 'Get-LambdaPSEcrAuthorizationToken' -Times 0 -Exactly
        }

        It 'refuses an edited Dockerfile under -WhatIf too, and builds nothing under -WhatIf' {
            $edited = [System.IO.Path]::Combine($TestDrive, 'rt-edited')
            Copy-Item -LiteralPath $script:projectPath -Destination $edited -Recurse
            Add-Content -LiteralPath ([System.IO.Path]::Combine($edited, 'Dockerfile')) -Value 'RUN dnf install -y jq'

            { Build-LambdaPSRuntimeImage -Path $edited -ImageUri $script:imageUri -WhatIf } | Should -Throw -ErrorId 'DockerfileModified,Build-LambdaPSRuntimeImage'
            Build-LambdaPSRuntimeImage -Path $script:projectPath -ImageUri $script:imageUri -WhatIf | Should -BeNullOrEmpty
            Should -Invoke 'Get-LambdaPSEcrAuthorizationToken' -Times 0 -Exactly
            Should -Invoke 'Invoke-LambdaPSOciImageBuild' -Times 0 -Exactly
        }
    }
}
