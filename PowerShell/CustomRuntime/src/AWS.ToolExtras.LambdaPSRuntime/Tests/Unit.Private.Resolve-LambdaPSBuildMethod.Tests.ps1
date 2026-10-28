BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))

    $script:missing = {
        param([string]$ErrorId)
        Mock 'Test-LambdaPSContainerRuntime' -ModuleName $script:moduleName -MockWith {
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.InvalidOperationException]::new('no docker'), $ErrorId, [System.Management.Automation.ErrorCategory]::NotInstalled, 'docker')
        }.GetNewClosure()
    }
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Resolve-LambdaPSBuildMethod' -Tag 'Unit' {
    It 'picks Docker under Auto when docker and buildx are installed' {
        Mock 'Test-LambdaPSContainerRuntime' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker' } }
        InModuleScope $script:moduleName {
            $resolved = Resolve-LambdaPSBuildMethod -BuildMethod Auto
            $resolved.Method | Should -Be 'Docker'
            $resolved.ContainerRuntime.Path | Should -Be '/usr/bin/docker'
        }
    }

    It 'falls back to NoDocker under Auto on <ErrorId>' -ForEach @(@{ ErrorId = 'DockerNotFound' }, @{ ErrorId = 'DockerBuildxNotFound' }) {
        & $script:missing $ErrorId
        InModuleScope $script:moduleName { (Resolve-LambdaPSBuildMethod -BuildMethod Auto).Method | Should -Be 'NoDocker' }
    }

    It 'keeps the docker error for a local build under Auto, which needs docker' {
        & $script:missing 'DockerNotFound'
        InModuleScope $script:moduleName { { Resolve-LambdaPSBuildMethod -BuildMethod Auto -LocalBuild } | Should -Throw -ErrorId 'DockerNotFound' }
    }

    It 'keeps the docker error under -BuildMethod Docker' {
        & $script:missing 'DockerNotFound'
        InModuleScope $script:moduleName { { Resolve-LambdaPSBuildMethod -BuildMethod Docker } | Should -Throw -ErrorId 'DockerNotFound' }
    }

    It 'does not probe docker for NoDocker, and refuses NoDocker for a local build' {
        Mock 'Test-LambdaPSContainerRuntime' -ModuleName $script:moduleName -MockWith { throw 'not called' }
        InModuleScope $script:moduleName {
            (Resolve-LambdaPSBuildMethod -BuildMethod NoDocker).Method | Should -Be 'NoDocker'
            { Resolve-LambdaPSBuildMethod -BuildMethod NoDocker -LocalBuild } | Should -Throw -ErrorId 'NoDockerLocalBuild'
        }
        Should -Invoke 'Test-LambdaPSContainerRuntime' -ModuleName $script:moduleName -Times 0 -Exactly
    }
}
