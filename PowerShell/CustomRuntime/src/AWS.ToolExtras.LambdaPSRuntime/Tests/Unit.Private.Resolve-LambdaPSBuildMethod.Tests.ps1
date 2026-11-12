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

        It 'does not probe docker for NoDocker, and refuses NoDocker for a local build' {
            Mock 'Test-LambdaPSContainerRuntime' -MockWith { throw 'not called' }

            (Resolve-LambdaPSBuildMethod -BuildMethod NoDocker).Method | Should -Be 'NoDocker'
            { Resolve-LambdaPSBuildMethod -BuildMethod NoDocker -LocalBuild } | Should -Throw -ErrorId 'NoDockerLocalBuild'
            Should -Invoke 'Test-LambdaPSContainerRuntime' -Times 0 -Exactly
        }
    }
}
