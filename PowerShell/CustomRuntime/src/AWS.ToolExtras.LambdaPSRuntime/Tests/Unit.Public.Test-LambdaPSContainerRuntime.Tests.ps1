BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'DockerStub.ps1'))
    Add-LambdaPSDockerStub
}

AfterAll {
    Remove-LambdaPSTestModule
}

# Docker is the ONLY supported container CLI. The build shells out to 'docker buildx build' and relies
# on --provenance=false / oci-mediatypes=false to emit a Docker v2 Schema 2 manifest; podman, finch and
# nerdctl have no equivalent, so probing for them would report a runtime the build cannot use. These
# tests pin that contract, including the buildx probe.
Describe 'Test-LambdaPSContainerRuntime' -Tag 'Unit' {
    Context 'when docker and buildx are both present' {
        BeforeEach {
            Mock 'Get-Command' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' `
                -ParameterFilter { $Name -eq 'docker' -and $CommandType -eq 'Application' } `
                -MockWith { [PSCustomObject]@{ Name = 'docker'; Source = '/usr/bin/docker' } }
            Mock 'docker' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith {
                $global:LASTEXITCODE = 0
                'github.com/docker/buildx v0.17.1'
            }
        }

        It 'returns a runtime object naming docker' {
            $result = Test-LambdaPSContainerRuntime

            $result.Name | Should -Be 'docker'
            $result.Path | Should -Be '/usr/bin/docker'
            $result.PSTypeNames | Should -Contain 'AWS.ToolExtras.LambdaPSRuntime.ContainerRuntime'
        }

        It 'reports the buildx version it probed' {
            (Test-LambdaPSContainerRuntime).BuildxVersion | Should -Match 'buildx v0\.17\.1'
        }

        It 'writes the docker path and buildx version to the verbose stream' {
            $verbose = "$(Test-LambdaPSContainerRuntime -Verbose 4>&1 | Where-Object { $_ -is [System.Management.Automation.VerboseRecord] })"

            $verbose | Should -BeLike "*Found docker at '/usr/bin/docker'.*docker buildx available: github.com/docker/buildx v0.17.1.*"
        }

        It 'probes buildx rather than assuming it' {
            $null = Test-LambdaPSContainerRuntime

            Should -Invoke 'docker' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -Times 1 -Exactly `
                -ParameterFilter { ($args -join ' ') -match 'buildx\s+version' }
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

        It 'points at the container-free alternative and the Docker install docs' {
            $threw = $false
            try {
                Test-LambdaPSContainerRuntime
            }
            catch {
                $threw = $true
                $_.Exception.Message | Should -Match 'Build-LambdaPSRuntimePackage'
                $_.Exception.Message | Should -Match 'https://docs\.docker\.com/engine/install/'
                $_.Exception.Message | Should -Match 'Layer or self-contained \.zip target'
                $_.CategoryInfo.Category | Should -Be 'NotInstalled'
            }
            $threw | Should -BeTrue
        }
    }

    Context 'when docker is present but buildx is not' {
        BeforeEach {
            Mock 'Get-Command' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' `
                -ParameterFilter { $Name -eq 'docker' -and $CommandType -eq 'Application' } `
                -MockWith { [PSCustomObject]@{ Name = 'docker'; Source = '/usr/bin/docker' } }
            Mock 'docker' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith {
                $global:LASTEXITCODE = 1
                "docker: 'buildx' is not a docker command."
            }
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

    # Only the docker program counts: a function or alias named docker (as the test stub defines on a host
    # without docker) is not the CLI the build runs.
    Context 'when only a docker function exists' {
        It 'ignores a docker function and throws DockerNotFound' {
            $emptyBin = [System.IO.Path]::Combine($TestDrive, 'empty-bin')
            $null = New-Item -ItemType Directory -Path $emptyBin -Force
            $savedPath = $env:PATH
            $added = -not (Test-Path -LiteralPath 'function:global:docker')
            try {
                if ($added) { Set-Item -Path 'function:global:docker' -Value { } }
                $env:PATH = $emptyBin
                { Test-LambdaPSContainerRuntime } |
                    Should -Throw -ErrorId 'DockerNotFound,Test-LambdaPSContainerRuntime'
            }
            finally {
                $env:PATH = $savedPath
                if ($added) { Remove-Item -Path 'Function:\docker' }
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
            # The global stub Add-LambdaPSDockerStub defines on a host without docker. A 'function:global:'
            # path does not resolve with -LiteralPath, so the stub is found and removed by its plain path.
            $stub = Get-Item -Path 'Function:\docker' -ErrorAction SilentlyContinue
            $savedPath = $env:PATH
            $savedNative = $global:PSNativeCommandUseErrorActionPreference
            $savedAction = $global:ErrorActionPreference
            try {
                if ($stub) { Remove-Item -Path 'Function:\docker' }
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
                if ($stub) { Set-Item -Path 'function:global:docker' -Value $stub.ScriptBlock }
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
