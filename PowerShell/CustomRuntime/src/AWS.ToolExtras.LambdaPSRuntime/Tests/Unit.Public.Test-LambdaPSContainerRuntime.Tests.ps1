BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'DockerStub.ps1'))
    Add-LambdaPSDockerStub

    # A stand-in docker program: a script that writes its arguments next to itself, prints -Output and
    # exits with -ExitCode. The probe runs it by the path Get-Command returns.
    # 'docker version' (the daemon probe) exits with -VersionExitCode.
    function script:New-FakeDockerProgram {
        param([string]$Name, [string]$Output, [int]$ExitCode, [int]$VersionExitCode)
        $path = [System.IO.Path]::Combine($TestDrive, $Name)
        Remove-Item -LiteralPath "$path.args" -ErrorAction Ignore
        Set-Content -LiteralPath $path -Value @(
            "Add-Content -LiteralPath '$path.args' -Value (`$args -join ' ')"
            "if (`$args[0] -eq 'version') { 'Cannot connect to the Docker daemon. Is the docker daemon running?'; exit $VersionExitCode }"
            "'$($Output -replace "'", "''")'"
            "exit $ExitCode")
        $path
    }
}

AfterAll {
    Remove-LambdaPSDockerStub
    Remove-LambdaPSTestModule
}

# Docker is the ONLY supported container CLI. The build shells out to 'docker buildx build' and relies
# on --provenance=false / oci-mediatypes=false to emit a Docker v2 Schema 2 manifest; podman, finch and
# nerdctl have no equivalent, so probing for them would report a runtime the build cannot use. These
# tests pin that contract, including the buildx probe.
Describe 'Test-LambdaPSContainerRuntime' -Tag 'Unit' {
    Context 'when docker and buildx are both present' {
        BeforeEach {
            # The docker program, as a script the probe runs by its path: it records its arguments.
            $script:fakeDocker = New-FakeDockerProgram -Name 'docker-buildx.ps1' -Output 'github.com/docker/buildx v0.17.1' -ExitCode 0
            Mock 'Get-Command' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' `
                -ParameterFilter { $Name -eq 'docker' -and $CommandType -eq 'Application' } `
                -MockWith { [PSCustomObject]@{ Name = 'docker'; Source = $script:fakeDocker } }
        }

        It 'returns a runtime object naming docker' {
            $result = Test-LambdaPSContainerRuntime

            $result.Name | Should -Be 'docker'
            $result.Path | Should -Be $script:fakeDocker
            $result.PSTypeNames | Should -Contain 'AWS.ToolExtras.LambdaPSRuntime.ContainerRuntime'
        }

        It 'reports the buildx version it probed' {
            (Test-LambdaPSContainerRuntime).BuildxVersion | Should -Match 'buildx v0\.17\.1'
        }

        It 'writes the docker path and buildx version to the verbose stream' {
            $verbose = "$(Test-LambdaPSContainerRuntime -Verbose 4>&1 | Where-Object { $_ -is [System.Management.Automation.VerboseRecord] })"

            $verbose | Should -BeLike "*Found docker at '$script:fakeDocker'.*docker buildx available: github.com/docker/buildx v0.17.1.*"
        }

        It 'probes buildx rather than assuming it' {
            $null = Test-LambdaPSContainerRuntime

            @(Get-Content -LiteralPath "$script:fakeDocker.args") | Should -Be @('buildx version', 'version --format {{.Server.Version}}')
        }

        # The probe runs the program it found, so a docker function or alias cannot answer for it.
        It 'probes the docker program it found, not a docker function in the session' {
            try {
                Set-Item -Path 'function:global:docker' -Value { 'a docker function' }
                (Test-LambdaPSContainerRuntime).BuildxVersion | Should -Be 'github.com/docker/buildx v0.17.1'
            }
            finally { Remove-Item -Path 'Function:\docker' }
        }
    }

    Context 'when docker is absent' {
        BeforeEach {
            Mock 'Get-Command' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' `
                -ParameterFilter { $CommandType -eq 'Application' } -MockWith { }
        }

        It 'throws DockerNotFound' {
            { Test-LambdaPSContainerRuntime } |
                Should -Throw -ErrorId 'DockerNotFound,Test-LambdaPSContainerRuntime'
        }

        It 'points at the no-Docker build, the container-free alternative and the Docker install docs' {
            $threw = $false
            try {
                Test-LambdaPSContainerRuntime
            }
            catch {
                $threw = $true
                $_.Exception.Message | Should -Match 'Build-LambdaPSRuntimePackage'
                $_.Exception.Message | Should -Match 'https://docs\.docker\.com/engine/install/'
                $_.Exception.Message | Should -Match 'Layer or self-contained \.zip target'
                $_.Exception.Message | Should -Match '-BuildMethod Auto \(the default\) or NoDocker'
                $_.CategoryInfo.Category | Should -Be 'NotInstalled'
            }
            $threw | Should -BeTrue
        }
    }

    Context 'when docker is present but buildx is not' {
        BeforeEach {
            $script:fakeDocker = New-FakeDockerProgram -Name 'docker-no-buildx.ps1' -Output "docker: 'buildx' is not a docker command." -ExitCode 1
            Mock 'Get-Command' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' `
                -ParameterFilter { $Name -eq 'docker' -and $CommandType -eq 'Application' } `
                -MockWith { [PSCustomObject]@{ Name = 'docker'; Source = $script:fakeDocker } }
        }

        # Failing HERE is the whole point: without this probe a deploy would get as far as the image
        # build before discovering buildx is missing.
        It 'throws DockerBuildxNotFound rather than passing preflight' {
            { Test-LambdaPSContainerRuntime } |
                Should -Throw -ErrorId 'DockerBuildxNotFound,Test-LambdaPSContainerRuntime'
        }

        It 'explains why buildx specifically is required' {
            $threw = $false
            try {
                Test-LambdaPSContainerRuntime
            }
            catch {
                $threw = $true
                $_.Exception.Message | Should -Match 'Schema 2'
                $_.Exception.Message | Should -Match 'exited with 1'
            }
            $threw | Should -BeTrue
        }
    }

    Context 'when the Docker daemon cannot be reached' {
        It 'throws DockerDaemonNotRunning with docker''s message' {
            $script:fakeDocker = New-FakeDockerProgram -Name 'docker-stopped.ps1' -Output 'github.com/docker/buildx v0.17.1' -ExitCode 0 -VersionExitCode 1
            Mock 'Get-Command' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' `
                -ParameterFilter { $Name -eq 'docker' -and $CommandType -eq 'Application' } `
                -MockWith { [PSCustomObject]@{ Name = 'docker'; Source = $script:fakeDocker } }
            { Test-LambdaPSContainerRuntime } | Should -Throw -ErrorId 'DockerDaemonNotRunning,Test-LambdaPSContainerRuntime' `
                -ExpectedMessage '*cannot reach the Docker daemon*Cannot connect to the Docker daemon*-BuildMethod Auto*'
        }
    }

    # Only the docker program counts: a function or alias named docker is not the CLI the build runs.
    Context 'when only a docker function exists' {
        It 'ignores a docker function and throws DockerNotFound' {
            $emptyBin = [System.IO.Path]::Combine($TestDrive, 'empty-bin')
            $null = New-Item -ItemType Directory -Path $emptyBin -Force
            $savedPath = $env:PATH
            try {
                Set-Item -Path 'function:global:docker' -Value { }
                $env:PATH = $emptyBin
                { Test-LambdaPSContainerRuntime } |
                    Should -Throw -ErrorId 'DockerNotFound,Test-LambdaPSContainerRuntime'
            }
            finally {
                $env:PATH = $savedPath
                Remove-Item -Path 'Function:\docker'
            }
        }
    }

    # The probe runs docker directly, so a caller's preference must not replace its error ID.
    Context 'when the caller turns on $PSNativeCommandUseErrorActionPreference' {
        It 'still throws DockerBuildxNotFound for a docker without buildx' -Skip:$IsWindows {
            $bin = [System.IO.Path]::Combine($TestDrive, 'no-buildx-bin')
            $null = New-Item -ItemType Directory -Path $bin -Force
            $fakeDocker = [System.IO.Path]::Combine($bin, 'docker')
            Set-Content -LiteralPath $fakeDocker -Value "#!/bin/sh`necho `"docker: 'buildx' is not a docker command.`" >&2`nexit 1"
            & chmod +x $fakeDocker
            # First on PATH, ahead of the stand-in Add-LambdaPSDockerStub puts there on a host without docker.
            $savedPath = $env:PATH
            $savedNative = $global:PSNativeCommandUseErrorActionPreference
            $savedAction = $global:ErrorActionPreference
            try {
                $env:PATH = $bin + [System.IO.Path]::PathSeparator + $env:PATH
                $global:PSNativeCommandUseErrorActionPreference = $true
                $global:ErrorActionPreference = 'Stop'
                { Test-LambdaPSContainerRuntime } |
                    Should -Throw -ErrorId 'DockerBuildxNotFound,Test-LambdaPSContainerRuntime'
            }
            finally {
                $global:PSNativeCommandUseErrorActionPreference = $savedNative
                $global:ErrorActionPreference = $savedAction
                $env:PATH = $savedPath
            }
        }
    }

    Context 'the supported-CLI contract' {
        It 'exposes no parameter for selecting a different container CLI' {
            # A -Name/-ContainerRuntime parameter accepting podman/finch/nerdctl would report a
            # runtime that Build-LambdaPSRuntimeImage (hardcoded to docker buildx) cannot use.
            $keys = (Get-Command Test-LambdaPSContainerRuntime).Parameters.Keys
            $keys | Should -Not -Contain 'Name'
            $keys | Should -Not -Contain 'ContainerRuntime'
        }
    }
}
