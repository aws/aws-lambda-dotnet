BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))

    # A minimal runtime-image project (only the Dockerfile presence is checked).
    $script:projectPath = [System.IO.Path]::Combine($TestDrive, 'rt')
    $null = New-LambdaPSRuntimeProject -Path $script:projectPath -PowerShellVersion '7.4' -WarningAction SilentlyContinue
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Build-LambdaPSRuntimeImage' -Tag 'Unit' {
    BeforeEach {
        Mock 'Invoke-LambdaPSExternalTool' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { }
        Mock 'Get-LambdaPSDockerBuilderPlatform' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { }
        Mock 'Test-LambdaPSContainerRuntime' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith {
            [PSCustomObject]@{ Name = 'docker'; Path = '/opt/found/bin/docker'; BuildxVersion = 'v0.0.0' }
        }
    }

    Context 'a local build (no push)' {
        It 'uses --load alone (no --output type=image, which buildx rejects together)' {
            $result = Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'powershell-runtime:v1'

            $result.Tag | Should -Be 'powershell-runtime:v1'
            $result.ImageUri | Should -BeNullOrEmpty
            $result.Architecture | Should -Be 'x86_64'
            $result.Pushed | Should -BeFalse
            $result.ImageDigest | Should -BeNullOrEmpty
            $result.PSTypeNames | Should -Contain 'AWS.ToolExtras.LambdaPSRuntime.RuntimeImage'

            Should -Invoke -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -CommandName 'Invoke-LambdaPSExternalTool' -Times 1 -Exactly -ParameterFilter {
                # The docker program the preflight found, not the bare name a function or alias could answer to.
                $Executable -eq '/opt/found/bin/docker' -and
                $ArgumentList[[array]::IndexOf($ArgumentList, '-t') + 1] -eq 'powershell-runtime:v1' -and
                $ArgumentList[-1] -eq $script:projectPath -and
                $ArgumentList -contains '--provenance=false' -and
                $ArgumentList -contains '--sbom=false' -and
                $ArgumentList -contains '--load' -and
                $ArgumentList[[array]::IndexOf($ArgumentList, '--platform') + 1] -eq 'linux/amd64' -and
                # buildx errors with "load and image output can't be used together", so the
                # local-build path must NOT emit --output type=image at all.
                ($ArgumentList -join ' ') -notmatch 'type=image' -and
                ($ArgumentList -join ' ') -notmatch '--output'
            }
        }

        It 'previews the local build under -WhatIf and runs no docker' {
            $child = Invoke-InChildPwsh -Stub 'function script:Test-LambdaPSContainerRuntime { }' -Command "Build-LambdaPSRuntimeImage -Path '$script:projectPath' -Tag 'rt:v2' -WhatIf"

            $child.Output | Should -BeLike "*What if: Performing the operation `"Build image 'rt:v2'`"*"
        }

        It 'builds for linux/arm64 when -Architecture is arm64' {
            $result = Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'rt:v1' -Architecture arm64

            $result.Architecture | Should -Be 'arm64'
            Should -Invoke -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -CommandName 'Invoke-LambdaPSExternalTool' -Times 1 -Exactly -ParameterFilter {
                $ArgumentList[0] -eq 'buildx' -and $ArgumentList -contains '--platform' -and $ArgumentList -contains 'linux/arm64'
            }
        }
    }

    Context 'the base image refresh' {
        It 'passes --pull under -Pull' {
            $null = Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'rt:v1' -Pull

            Should -Invoke -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -CommandName 'Invoke-LambdaPSExternalTool' -Times 1 -Exactly -ParameterFilter {
                $ArgumentList[0] -eq 'buildx' -and @($ArgumentList | Where-Object { $_ -eq '--pull' }).Count -eq 1 -and
                [array]::IndexOf($ArgumentList, '--pull') -lt [array]::IndexOf($ArgumentList, $script:projectPath)
            }
        }

        It 'omits --pull by default' {
            $null = Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'rt:v1'

            Should -Invoke -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -CommandName 'Invoke-LambdaPSExternalTool' -Times 1 -Exactly -ParameterFilter {
                $ArgumentList -notcontains '--pull' -and $ArgumentList -contains '--load'
            }
        }
    }

    Context 'the reserved paths check' {
        BeforeAll {
            $script:runFails = {
                param([string[]]$Output)
                $exception = [System.InvalidOperationException]::new("External tool 'docker' exited with code 3.")
                $exception.Data['Output'] = $Output
                throw [System.Management.Automation.ErrorRecord]::new($exception, 'ExternalToolFailed', [System.Management.Automation.ErrorCategory]::OperationStopped, 'docker')
            }
        }

        It 'lists /opt, /var/task and /tmp in the built image, with no network, for a <Case>' -ForEach @(
            @{ Case = 'local build'; Splat = @{ Tag = 'rt:v1' }; Ref = 'rt:v1'; Pull = 'never' }
            @{ Case = 'push'; Splat = @{ ImageUri = '111122223333.dkr.ecr.us-east-1.amazonaws.com/rt:v1' }; Ref = '111122223333.dkr.ecr.us-east-1.amazonaws.com/rt:v1'; Pull = 'always' }
        ) {
            $script:ref = $Ref
            $script:pull = $Pull
            $null = Build-LambdaPSRuntimeImage -Path $script:projectPath -Architecture arm64 @Splat

            Should -Invoke -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -CommandName 'Invoke-LambdaPSExternalTool' -Times 1 -Exactly -ParameterFilter {
                $ArgumentList[0] -eq 'run' -and $ArgumentList -contains '--rm' -and
                $ArgumentList[[array]::IndexOf($ArgumentList, '--network') + 1] -eq 'none' -and
                $ArgumentList[[array]::IndexOf($ArgumentList, '--user') + 1] -eq '0' -and
                $ArgumentList[[array]::IndexOf($ArgumentList, '--pull') + 1] -eq $script:pull -and
                $ArgumentList[[array]::IndexOf($ArgumentList, '--platform') + 1] -eq 'linux/arm64' -and
                $ArgumentList[[array]::IndexOf($ArgumentList, '--entrypoint') + 1] -eq '/bin/sh' -and
                $ArgumentList -contains $script:ref -and
                $ArgumentList[-1] -match '/opt /var/task /tmp'
            }
        }

        It 'throws ReservedPathNotEmpty naming what it found, and emits no success object' {
            Mock 'Invoke-LambdaPSExternalTool' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -ParameterFilter { $ArgumentList[0] -eq 'run' } -MockWith {
                & $script:runFails @('LAMBDAPS_RESERVED /tmp: .dotnet ', 'LAMBDAPS_RESERVED /opt: tools ')
            }

            $result = $null
            $err = $null
            try { $result = Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'rt:v1' } catch { $err = $_ }

            $result | Should -BeNullOrEmpty
            $err.FullyQualifiedErrorId | Should -Be 'ReservedPathNotEmpty,Build-LambdaPSRuntimeImage'
            $err.CategoryInfo.Category | Should -Be 'InvalidData'
            $err.Exception.Message | Should -BeLike "Image 'rt:v1' has files in /tmp: .dotnet; /opt: tools.*"
            $err.Exception.Message | Should -Not -BeLike '*already pushed*'
        }

        It 'says a pushed image is already pushed when it fails the check' {
            Mock 'Invoke-LambdaPSExternalTool' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -ParameterFilter { $ArgumentList[0] -eq 'run' } -MockWith {
                & $script:runFails @('LAMBDAPS_RESERVED /var/task: handler.ps1 ')
            }
            $uri = '111122223333.dkr.ecr.us-east-1.amazonaws.com/rt:v1'

            $err = $null
            try { $null = Build-LambdaPSRuntimeImage -Path $script:projectPath -ImageUri $uri } catch { $err = $_ }

            $err.Exception.Message | Should -BeLike "*/var/task: handler.ps1*already pushed to '$uri'*"
        }

        It 'warns and still returns the image when the check cannot run' {
            Mock 'Invoke-LambdaPSExternalTool' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -ParameterFilter { $ArgumentList[0] -eq 'run' } -MockWith {
                & $script:runFails @('docker: Error response from daemon: exec: "/bin/sh": stat /bin/sh: no such file or directory')
            }

            $result = Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'rt:v1' -WarningVariable warnings -WarningAction SilentlyContinue

            $result.Tag | Should -Be 'rt:v1'
            @($warnings) | Should -HaveCount 1
            "$warnings" | Should -BeLike "Could not list /opt, /var/task and /tmp in image 'rt:v1'*: docker: Error response from daemon: exec: *"
        }

        It 'does not check a Dockerfile project that has no lambdaps-runtime.json' {
            $bare = [System.IO.Path]::Combine($TestDrive, 'bare-image')
            $null = New-Item -ItemType Directory -Path $bare
            Set-Content -LiteralPath ([System.IO.Path]::Combine($bare, 'Dockerfile')) -Value 'FROM public.ecr.aws/lambda/provided:al2023'

            $null = Build-LambdaPSRuntimeImage -Path $bare -Tag 'bare:v1'

            Should -Invoke -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -CommandName 'Invoke-LambdaPSExternalTool' -Times 0 -Exactly -ParameterFilter { $ArgumentList[0] -eq 'run' }
        }

        It 'does not check a Container function image, whose code belongs in /var/task' {
            $functionPath = [System.IO.Path]::Combine($TestDrive, 'fn-image')
            $null = New-LambdaPSFunctionProject -Path $functionPath -Handler handler -Target Container -PowerShellVersion '7.4' -WarningAction SilentlyContinue

            $null = Build-LambdaPSRuntimeImage -Path $functionPath -Tag 'fn:v1'

            Should -Invoke -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -CommandName 'Invoke-LambdaPSExternalTool' -Times 0 -Exactly -ParameterFilter { $ArgumentList[0] -eq 'run' }
        }
    }

    Context 'a build with push' {
        It 'sets push=true, tags ONLY the registry ImageUri (not the bare local tag), omits --load' {
            $uri = '111122223333.dkr.ecr.us-east-1.amazonaws.com/rt:v1'
            $result = Build-LambdaPSRuntimeImage -Path $script:projectPath -ImageUri $uri

            $result.Pushed | Should -BeTrue
            $result.ImageUri | Should -Be $uri
            $result.ImageDigest | Should -BeNullOrEmpty -Because 'the mocked build writes no metadata file'

            Should -Invoke -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -CommandName 'Invoke-LambdaPSExternalTool' -Times 1 -Exactly -ParameterFilter {
                $ArgumentList -contains 'type=image,oci-mediatypes=false,push=true' -and
                $ArgumentList -contains '--provenance=false' -and
                $ArgumentList -contains '--sbom=false' -and
                $ArgumentList -notcontains '--load' -and
                # Only the registry-qualified URI is tagged; the bare 'rt:v1' must NOT be a -t
                # (buildx --push would try to push it to docker.io and get denied).
                $ArgumentList -contains $uri -and
                $ArgumentList -notcontains 'rt:v1' -and
                (@($ArgumentList | Where-Object { $_ -eq '-t' }).Count -eq 1)
            }
        }
    }

    Context 'the pushed digest' {
        It 'asks buildx for a metadata file and reports the digest it records' {
            Mock 'Invoke-LambdaPSExternalTool' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -ParameterFilter { $ArgumentList[0] -eq 'buildx' } -MockWith {
                $index = [array]::IndexOf($ArgumentList, '--metadata-file')
                Set-Content -LiteralPath $ArgumentList[$index + 1] -Value '{"containerimage.digest":"sha256:abc123"}'
            }
            $uri = '111122223333.dkr.ecr.us-east-1.amazonaws.com/rt:v1'

            $result = Build-LambdaPSRuntimeImage -Path $script:projectPath -ImageUri $uri

            $result.ImageDigest | Should -Be 'sha256:abc123'
            Should -Invoke -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -CommandName 'Invoke-LambdaPSExternalTool' -Times 1 -Exactly -ParameterFilter {
                $ArgumentList[0] -eq 'run' -and $ArgumentList -contains "$uri@sha256:abc123"
            }
            Should -Invoke -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -CommandName 'Invoke-LambdaPSExternalTool' -Times 1 -Exactly -ParameterFilter {
                $index = [array]::IndexOf($ArgumentList, '--metadata-file')
                $index -ge 0 -and -not (Test-Path -LiteralPath $ArgumentList[$index + 1])
            }
        }

        # The image is already pushed, so metadata buildx wrote in another shape leaves the digest unknown.
        It 'reports no digest, and removes the metadata file, for metadata that is <Case>' -ForEach @(
            @{ Case = 'not JSON'; Content = 'not json' }
            @{ Case = 'missing the digest'; Content = '{}' }
        ) {
            $script:metadataContent = $Content
            Mock 'Invoke-LambdaPSExternalTool' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -ParameterFilter { $ArgumentList[0] -eq 'buildx' } -MockWith {
                $index = [array]::IndexOf($ArgumentList, '--metadata-file')
                Set-Content -LiteralPath $ArgumentList[$index + 1] -Value $script:metadataContent
            }

            $result = Build-LambdaPSRuntimeImage -Path $script:projectPath -ImageUri '111122223333.dkr.ecr.us-east-1.amazonaws.com/rt:v1'

            $result.Pushed | Should -BeTrue
            $result.ImageDigest | Should -BeNullOrEmpty
            Should -Invoke -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -CommandName 'Invoke-LambdaPSExternalTool' -Times 1 -Exactly -ParameterFilter {
                $ArgumentList[0] -eq 'buildx' -and
                -not (Test-Path -LiteralPath $ArgumentList[[array]::IndexOf($ArgumentList, '--metadata-file') + 1])
            }
        }
    }

    # A pwsh version with no pinned arm64 hash is scaffolded amd64-only; building it for arm64 would fail
    # only inside docker.
    Context 'arm64 with no pinned pwsh hash' {
        BeforeAll {
            $script:unpinned = [System.IO.Path]::Combine($TestDrive, 'rt-unpinned')
            $null = New-LambdaPSRuntimeProject -Path $script:unpinned -PowerShellVersion '7.4' -PwshVersion '7.4.2' -WarningAction SilentlyContinue
        }

        It 'throws PwshArm64HashNotPinned before running docker' {
            $err = { Build-LambdaPSRuntimeImage -Path $script:unpinned -Tag 'rt:v1' -Architecture arm64 } |
                Should -Throw -ErrorId 'PwshArm64HashNotPinned,Build-LambdaPSRuntimeImage' -PassThru
            $err.Exception.Message | Should -BeLike '*-Architecture x86_64*'
            $err.Exception.Message | Should -BeLike '*powershell-7.4.2-linux-arm64.tar.gz*'
            $err.CategoryInfo.Category | Should -Be 'InvalidArgument'
            Should -Invoke -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -CommandName 'Test-LambdaPSContainerRuntime' -Times 0 -Exactly
            Should -Invoke -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -CommandName 'Invoke-LambdaPSExternalTool' -Times 0 -Exactly
        }

        It 'still builds the same project for x86_64' {
            $result = Build-LambdaPSRuntimeImage -Path $script:unpinned -Tag 'rt:v1'

            $result.Architecture | Should -Be 'x86_64'
        }
    }

    Context 'parameter sets' {
        It 'takes -Tag for a local build and -ImageUri for a push, as separate parameter sets' {
            $command = Get-Command Build-LambdaPSRuntimeImage
            $local = $command.ParameterSets | Where-Object Name -eq 'Local'
            $push = $command.ParameterSets | Where-Object Name -eq 'Push'
            ($local.Parameters | Where-Object Name -eq 'Tag').IsMandatory | Should -BeTrue
            ($push.Parameters | Where-Object Name -eq 'ImageUri').IsMandatory | Should -BeTrue
            $command.Parameters.Keys | Should -Not -Contain 'Push'
            $push.Parameters.Name | Should -Not -Contain 'Tag'
        }

        # A run with no host to prompt for the missing -Tag or -ImageUri is refused, not built.
        It 'refuses a call with neither -Tag nor -ImageUri when it cannot prompt' {
            $run = Invoke-InFreshRunspace -Command "Build-LambdaPSRuntimeImage -Path '$script:projectPath'"

            $run.Errors[0].FullyQualifiedErrorId | Should -Be 'MissingMandatoryParameter,Build-LambdaPSRuntimeImage'
        }
    }

    Context 'error handling' {
        It 'throws the container-runtime preflight error before building, under -WhatIf too' {
            Mock 'Get-LambdaPSDockerBuilderPlatform' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { }
            Mock 'Test-LambdaPSContainerRuntime' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith {
                throw [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new('The docker CLI was not found on PATH.'),
                    'DockerNotFound', [System.Management.Automation.ErrorCategory]::NotInstalled, 'docker')
            }

            { Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'rt:v1' } |
                Should -Throw -ErrorId 'DockerNotFound,Build-LambdaPSRuntimeImage'
            { Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'rt:v1' -WhatIf } |
                Should -Throw -ErrorId 'DockerNotFound,Build-LambdaPSRuntimeImage'
            Should -Invoke -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -CommandName 'Invoke-LambdaPSExternalTool' -Times 0 -Exactly
        }

        It 'refuses -Tag together with -ImageUri instead of ignoring one' {
            $uri = '111122223333.dkr.ecr.us-east-1.amazonaws.com/rt:v1'
            { Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'rt:v1' -ImageUri $uri } |
                Should -Throw -ExceptionType ([System.Management.Automation.ParameterBindingException])
            { Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'rt:v1' -ImageUri $uri } |
                Should -Throw -ErrorId 'AmbiguousParameterSet,Build-LambdaPSRuntimeImage'
            Should -Invoke -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -CommandName 'Invoke-LambdaPSExternalTool' -Times 0 -Exactly
        }

        It 'throws ProjectNotFound for a missing project directory' {
            { Build-LambdaPSRuntimeImage -Path ([System.IO.Path]::Combine($TestDrive, 'no-such-project')) -Tag 'rt:v1' } |
                Should -Throw -ErrorId 'ProjectNotFound,Build-LambdaPSRuntimeImage'
        }

        It 'throws when the project has no Dockerfile' {
            $noDockerfile = [System.IO.Path]::Combine($TestDrive, 'empty')
            $null = New-Item -ItemType Directory -Path $noDockerfile -Force
            { Build-LambdaPSRuntimeImage -Path $noDockerfile -Tag 'rt:v1' } |
                Should -Throw -ErrorId 'DockerfileNotFound,Build-LambdaPSRuntimeImage'
        }

        # A container-free project has no Dockerfile, so the error names the cmdlet that builds it.
        It 'names <Cmdlet> when the project without a Dockerfile is <Case>' -ForEach @(
            @{ Case = 'a Layer project'; Cmdlet = 'Build-LambdaPSRuntimePackage' }
            @{ Case = 'a function project'; Cmdlet = 'Compress-LambdaPSFunctionPackage' }
        ) {
            $proj = [System.IO.Path]::Combine($TestDrive, "no-dockerfile-$Cmdlet")
            if ($Cmdlet -eq 'Build-LambdaPSRuntimePackage') { $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer }
            else { $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' }

            $err = { Build-LambdaPSRuntimeImage -Path $proj -Tag 'rt:v1' } |
                Should -Throw -ErrorId 'DockerfileNotFound,Build-LambdaPSRuntimeImage' -PassThru
            $err.Exception.Message | Should -BeLike "*$Cmdlet*"
        }

        It 'propagates a docker failure and emits no success object' {
            # A failed docker exit must halt the cmdlet, not fall through to a success object.
            Mock 'Invoke-LambdaPSExternalTool' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith {
                throw [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new("External tool 'docker' exited with code 1."),
                    'ExternalToolFailed', [System.Management.Automation.ErrorCategory]::OperationStopped, 'docker')
            }
            # Assigned in this scope, not inside a Should -Throw script block, so the check can fail.
            $result = $null
            $err = $null
            try { $result = Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'rt:v1' } catch { $err = $_ }
            $err.FullyQualifiedErrorId | Should -BeLike 'ExternalToolFailed*'
            $result | Should -BeNullOrEmpty
        }

        Context 'the emulation hint' {
            BeforeAll {
                $script:hostArchitecture = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x86_64' }
                $script:otherArchitecture = if ($script:hostArchitecture -eq 'arm64') { 'x86_64' } else { 'arm64' }
            }

            BeforeEach {
                Mock 'Invoke-LambdaPSExternalTool' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith {
                    throw [System.Management.Automation.ErrorRecord]::new(
                        [System.InvalidOperationException]::new("External tool 'docker' exited with code 1."),
                        'ExternalToolFailed', [System.Management.Automation.ErrorCategory]::OperationStopped, 'docker')
                }
            }

            It 'names emulation conditionally on exec format error when docker''s output is not available' {
                # Without docker's output the hint cannot assert the cause: it must be conditional
                # (Docker Desktop hosts have emulation and fail for other reasons).
                $err = $null
                try { Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'rt:v1' -Architecture $script:otherArchitecture }
                catch { $err = $_ }
                $err | Should -Not -BeNullOrEmpty
                $err.FullyQualifiedErrorId | Should -BeLike 'ExternalToolFailed*'
                $err.Exception.Message | Should -Match "exited with code 1\."
                $err.Exception.Message | Should -Match "If docker reported 'exec format error'"
                $err.Exception.Message | Should -Match 'binfmt'
                $err.Exception.Message | Should -Match 'Docker Desktop includes emulation'
                $err.Exception.Message | Should -Not -Match '-BuildMethod NoDocker'
            }

            It 'Should name -BuildMethod NoDocker in the emulation hint of a push' {
                $err = $null
                try { Build-LambdaPSRuntimeImage -Path $script:projectPath -ImageUri '111122223333.dkr.ecr.us-east-1.amazonaws.com/rt:v1' -Architecture $script:otherArchitecture -BuildMethod Docker }
                catch { $err = $_ }
                $err.FullyQualifiedErrorId | Should -BeLike 'ExternalToolFailed*'
                $err.Exception.Message | Should -Match 'Or build with -BuildMethod NoDocker, which needs no emulator\.'
            }

            # A broken Dockerfile on an arm64 Mac (the default build is cross-architecture there) must not
            # get an emulator hint; only docker's own 'exec format error' warrants one.
            It 'adds the emulation hint <When>' -ForEach @(
                @{ When = 'when docker''s output says exec format error'; Output = @('#5 [2/4] RUN dnf install -y tar', 'exec /bin/sh: exec format error'); Hinted = $true }
                @{ When = 'not when docker''s output names another failure'; Output = @('#7 [3/4] RUN exit 1', 'ERROR: process "/bin/sh -c exit 1" did not complete successfully: exit code: 1'); Hinted = $false }
            ) {
                $script:dockerOutput = $Output
                Mock 'Invoke-LambdaPSExternalTool' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith {
                    $exception = [System.InvalidOperationException]::new("External tool 'docker' exited with code 1.")
                    $exception.Data['Output'] = [string[]]$script:dockerOutput
                    throw [System.Management.Automation.ErrorRecord]::new($exception, 'ExternalToolFailed', [System.Management.Automation.ErrorCategory]::OperationStopped, 'docker')
                }

                $err = $null
                try { Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'rt:v1' -Architecture $script:otherArchitecture }
                catch { $err = $_ }

                $err.FullyQualifiedErrorId | Should -BeLike 'ExternalToolFailed*'
                if ($Hinted) {
                    $err.Exception.Message | Should -Match "docker reported 'exec format error', so no emulator is registered"
                    $err.Exception.Message | Should -Match 'binfmt'
                }
                else {
                    $err.Exception.Message | Should -Not -Match 'binfmt|emulat'
                }
            }

            It 'adds no emulation hint for a native-architecture build' {
                $err = $null
                try { Build-LambdaPSRuntimeImage -Path $script:projectPath -Tag 'rt:v1' -Architecture $script:hostArchitecture }
                catch { $err = $_ }
                $err | Should -Not -BeNullOrEmpty
                $err.Exception.Message | Should -Not -Match 'binfmt|exec format error'
            }
        }
    }
}
