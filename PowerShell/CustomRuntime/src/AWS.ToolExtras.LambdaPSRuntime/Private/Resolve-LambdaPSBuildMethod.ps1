function Resolve-LambdaPSBuildMethod {
    <#
    .SYNOPSIS
        Decides whether an image build runs docker or the no-Docker builder.
    .DESCRIPTION
        -BuildMethod on the image cmdlets: Docker and NoDocker are taken as given; Auto picks Docker when
        Test-LambdaPSContainerRuntime finds docker with buildx, and NoDocker when it reports DockerNotFound,
        DockerBuildxNotFound or DockerDaemonNotRunning. Auto also picks NoDocker when docker's buildx builder lists its platforms
        and the image's isn't one of them (no emulator registered), since docker's build would then fail with 'exec format
        error'. Otherwise Auto doesn't change what happens where Docker works. Docker is required, whatever the method, for a local build
        (-Tag), since the no-Docker builder only pushes to ECR.
    .PARAMETER BuildMethod
        Auto, Docker or NoDocker.
    .PARAMETER Architecture
        The image's architecture, x86_64 or arm64. Without it, Auto doesn't check the builder's platforms.
    .PARAMETER LocalBuild
        The build loads the image into the local docker image store (-Tag) rather than pushing it.
    .OUTPUTS
        System.Management.Automation.PSCustomObject

        Method (Docker or NoDocker) and ContainerRuntime (Test-LambdaPSContainerRuntime's result, $null for NoDocker).
    .EXAMPLE
        Resolve-LambdaPSBuildMethod -BuildMethod Auto

        Returns Docker where docker and buildx are installed, else NoDocker.
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Auto', 'Docker', 'NoDocker')]
        [string]$BuildMethod,

        [Parameter()]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture,

        [Parameter()]
        [switch]$LocalBuild
    )

    if ($BuildMethod -eq 'NoDocker' -and $LocalBuild) {
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new(
                ('-BuildMethod NoDocker pushes to Amazon ECR only; it cannot load an image into a local docker image store. ' +
                 'Use -ImageUri, or build with -BuildMethod Docker.')),
            'NoDockerLocalBuild', [System.Management.Automation.ErrorCategory]::InvalidArgument, 'Tag')
    }
    if ($BuildMethod -eq 'NoDocker') {
        return [PSCustomObject]@{ Method = 'NoDocker'; ContainerRuntime = $null }
    }
    try {
        $containerRuntime = Test-LambdaPSContainerRuntime
    }
    catch {
        if ($BuildMethod -eq 'Auto' -and -not $LocalBuild -and ($_.FullyQualifiedErrorId -split ',')[0] -in 'DockerNotFound', 'DockerBuildxNotFound', 'DockerDaemonNotRunning') {
            Write-Verbose "Docker is not available ($($_.Exception.Message)); building without it."
            return [PSCustomObject]@{ Method = 'NoDocker'; ContainerRuntime = $null }
        }
        throw
    }
    if ($BuildMethod -eq 'Auto' -and -not $LocalBuild -and $Architecture) {
        $platform = if ($Architecture -eq 'arm64') { 'linux/arm64' } else { 'linux/amd64' }
        # Compared on os/architecture, so a variant such as linux/arm64/v8 counts as linux/arm64.
        $platforms = @(Get-LambdaPSDockerBuilderPlatform -Path $containerRuntime.Path) |
            ForEach-Object { ($_ -split '/')[0, 1] -join '/' } | Select-Object -Unique
        if ($platforms.Count -gt 0 -and $platform -notin $platforms) {
            Write-Verbose "docker's builder can't build $platform (it lists $($platforms -join ', ')); building without it."
            return [PSCustomObject]@{ Method = 'NoDocker'; ContainerRuntime = $null }
        }
    }
    [PSCustomObject]@{ Method = 'Docker'; ContainerRuntime = $containerRuntime }
}
