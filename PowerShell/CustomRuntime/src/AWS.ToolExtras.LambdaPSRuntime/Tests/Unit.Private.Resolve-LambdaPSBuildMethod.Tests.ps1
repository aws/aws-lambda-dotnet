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
    Describe 'Resolve-LambdaPSBuildMethod' -Tag 'Unit' {
        BeforeAll {
            $script:missing = {
                param([string]$ErrorId)
                Mock 'Test-LambdaPSContainerRuntime' -MockWith {
                    throw [System.Management.Automation.ErrorRecord]::new(
                        [System.InvalidOperationException]::new('no docker'), $ErrorId, [System.Management.Automation.ErrorCategory]::NotInstalled, 'docker')
                }.GetNewClosure()
            }
        }

        It 'picks Docker under Auto when docker and buildx are installed' {
            Mock 'Test-LambdaPSContainerRuntime' -MockWith { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker' } }

            $resolved = Resolve-LambdaPSBuildMethod -BuildMethod Auto

            $resolved.Method | Should -Be 'Docker'
            $resolved.ContainerRuntime.Path | Should -Be '/usr/bin/docker'
        }

        It 'falls back to NoDocker under Auto on <ErrorId>' -ForEach @(@{ ErrorId = 'DockerNotFound' }, @{ ErrorId = 'DockerBuildxNotFound' }, @{ ErrorId = 'DockerDaemonNotRunning' }) {
            & $script:missing $ErrorId

            (Resolve-LambdaPSBuildMethod -BuildMethod Auto).Method | Should -Be 'NoDocker'
        }

        It 'keeps the docker error for a local build under Auto, which needs docker' {
            & $script:missing 'DockerNotFound'

            { Resolve-LambdaPSBuildMethod -BuildMethod Auto -LocalBuild } | Should -Throw -ErrorId 'DockerNotFound'
        }

        It 'keeps the docker error under -BuildMethod Docker' {
            & $script:missing 'DockerNotFound'

            { Resolve-LambdaPSBuildMethod -BuildMethod Docker } | Should -Throw -ErrorId 'DockerNotFound'
        }

        Context 'docker''s builder platforms' {
            BeforeAll {
                Mock 'Test-LambdaPSContainerRuntime' -MockWith { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker' } }
            }

            It 'Should pick NoDocker under Auto when the builder lists platforms without the image''s' {
                # Arrange
                Mock 'Get-LambdaPSDockerBuilderPlatform' -ParameterFilter { $Path -eq '/usr/bin/docker' } -MockWith { 'linux/amd64', 'linux/386' }

                # Act
                $resolved = Resolve-LambdaPSBuildMethod -BuildMethod Auto -Architecture arm64

                # Assert
                $resolved.Method | Should -Be 'NoDocker'
                Should -Invoke 'Get-LambdaPSDockerBuilderPlatform' -ParameterFilter { $Path -eq '/usr/bin/docker' } -Times 1 -Exactly
            }

            It 'Should keep Docker when the builder lists the image''s platform, lists none, or the build is -BuildMethod Docker or local' {
                # Arrange
                Mock 'Get-LambdaPSDockerBuilderPlatform' -MockWith { 'linux/amd64', 'linux/arm64/v8' }

                # Act
                $listed = Resolve-LambdaPSBuildMethod -BuildMethod Auto -Architecture arm64
                Mock 'Get-LambdaPSDockerBuilderPlatform' -MockWith { }
                $unknown = Resolve-LambdaPSBuildMethod -BuildMethod Auto -Architecture arm64
                Mock 'Get-LambdaPSDockerBuilderPlatform' -MockWith { 'linux/amd64' }
                $docker = Resolve-LambdaPSBuildMethod -BuildMethod Docker -Architecture arm64
                $local = Resolve-LambdaPSBuildMethod -BuildMethod Auto -Architecture arm64 -LocalBuild

                # Assert
                @($listed.Method, $unknown.Method, $docker.Method, $local.Method) | Should -Be @('Docker', 'Docker', 'Docker', 'Docker')
            }
        }

        It 'does not probe docker for NoDocker, and refuses NoDocker for a local build' {
            Mock 'Test-LambdaPSContainerRuntime' -MockWith { throw 'not called' }

            (Resolve-LambdaPSBuildMethod -BuildMethod NoDocker).Method | Should -Be 'NoDocker'
            { Resolve-LambdaPSBuildMethod -BuildMethod NoDocker -LocalBuild } | Should -Throw -ErrorId 'NoDockerLocalBuild'
            Should -Invoke 'Test-LambdaPSContainerRuntime' -Times 0 -Exactly
        }
    }
}
