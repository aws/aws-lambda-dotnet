function Test-LambdaPSContainerRuntime {
    <#
    .SYNOPSIS
        Preflights the Docker CLI used to build Lambda runtime and container-function images.
    .DESCRIPTION
        Preflight probe that New-LambdaPSRuntimeDeployment and Build-LambdaPSRuntimeImage run. It checks
        that the 'docker' CLI is on PATH and that its 'buildx' subcommand is installed, because that is
        exactly what the image build shells out to:

            docker buildx build --provenance=false --sbom=false --output type=image,oci-mediatypes=false ...

        Docker is the only supported container CLI. The image build depends on buildx specifically --
        on '--provenance=false', '--sbom=false' and 'oci-mediatypes=false' -- to emit the Docker v2
        Schema 2 manifest that a customer-managed runtime requires, and neither the flags nor the
        single-step push behaviour carry over to podman, finch, or nerdctl. Probing for those would
        report a runtime the build then cannot use, so this cmdlet does not pretend to support them.
        Without Docker, -BuildMethod Auto (the default) or NoDocker on Build-LambdaPSRuntimeImage and
        New-LambdaPSRuntimeDeployment builds and pushes a scaffolded project's image without it, and the
        Layer and self-contained .zip targets (Build-LambdaPSRuntimePackage) need no container at all.

        The CLI is located with Get-Command docker -CommandType Application, so only a real
        executable on PATH matches (aliases and functions are ignored). buildx is probed by running
        'buildx version' with that executable, and Build-LambdaPSRuntimeImage builds with the same one.

        It then runs 'docker version' to check that the Docker daemon answers.

        Each failure is terminating: DockerNotFound, DockerBuildxNotFound or DockerDaemonNotRunning,
        so the caller can fix it before a deploy gets as far as creating an ECR repository.

        It returns the CLI's name, path and buildx version. This cmdlet changes no state.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.ContainerRuntime
    .EXAMPLE
        Test-LambdaPSContainerRuntime

        Confirms docker and docker buildx are installed and the Docker daemon answers, and returns the CLI path.
    .EXAMPLE
        (Test-LambdaPSContainerRuntime).BuildxVersion

        Shows the buildx version the image build will use.
    #>
    [CmdletBinding(PositionalBinding = $false)]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.ContainerRuntime')]
    param()

    try {

        $command = Get-Command -Name 'docker' -CommandType Application -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if (-not $command) {
            $message = 'The docker CLI was not found on PATH. Docker (with buildx) is required to build ' +
                'runtime and container-function images with -BuildMethod Docker or locally (-Tag). Without ' +
                'Docker, push the image with -BuildMethod Auto (the default) or NoDocker, or use the Layer or ' +
                'self-contained .zip target with Build-LambdaPSRuntimePackage. To install Docker, see ' +
                'https://docs.docker.com/engine/install/.'
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new($message),
                    'DockerNotFound', [System.Management.Automation.ErrorCategory]::NotInstalled, 'docker'))
        }
        Write-Verbose "Found docker at '$($command.Source)'."

        # buildx is a plugin and can be absent on an otherwise working docker install. Probing it here
        # fails the deploy in its preflight, before the scaffold and any AWS call. A caller's
        # $PSNativeCommandUseErrorActionPreference would throw before $LASTEXITCODE is checked.
        $PSNativeCommandUseErrorActionPreference = $false
        $buildxVersion = & $command.Source buildx version 2>&1
        if ($LASTEXITCODE -ne 0) {
            $message = "The docker CLI at '$($command.Source)' does not support 'buildx' " +
                "(docker buildx version exited with $LASTEXITCODE). buildx is required: the image build " +
                'uses --provenance=false, --sbom=false and oci-mediatypes=false to produce the Docker v2 Schema 2 ' +
                'manifest a customer-managed runtime needs. Install the buildx plugin: see ' +
                'https://github.com/docker/buildx#installing.'
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new($message),
                    'DockerBuildxNotFound', [System.Management.Automation.ErrorCategory]::NotInstalled, 'docker buildx'))
        }
        Write-Verbose "docker buildx available: $($buildxVersion -join ' ')."

        # An installed but stopped Docker Desktop passes the checks above and fails only at the build.
        # docker version, not docker info: info --format exits 0 when the daemon can't be reached.
        $daemon = & $command.Source version --format '{{.Server.Version}}' 2>&1
        if ($LASTEXITCODE -ne 0) {
            $message = "The docker CLI at '$($command.Source)' cannot reach the Docker daemon (docker version exited with " +
                "$LASTEXITCODE`: $(@($daemon) -match '\S' | Select-Object -First 1)). Start Docker, or build without it: " +
                '-BuildMethod Auto (the default) does so when the daemon cannot be reached.'
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new($message),
                    'DockerDaemonNotRunning', [System.Management.Automation.ErrorCategory]::ConnectionError, 'docker'))
        }

        [PSCustomObject]@{
            PSTypeName    = 'AWS.ToolExtras.LambdaPSRuntime.ContainerRuntime'
            Name          = 'docker'
            Path          = $command.Source
            BuildxVersion = ($buildxVersion -join ' ').Trim()
        }
    }
    catch {
        # Rethrow from this cmdlet: a helper's terminating error otherwise ends only the
        # statement that called it, and this cmdlet would carry on to its next step.
        $PSCmdlet.ThrowTerminatingError($_)
    }
}
