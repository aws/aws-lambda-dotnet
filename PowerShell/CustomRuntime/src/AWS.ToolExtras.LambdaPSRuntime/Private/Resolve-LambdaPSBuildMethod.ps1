function Resolve-LambdaPSBuildMethod {
    <#
    .SYNOPSIS
        Decides whether an image build runs docker or the no-Docker builder.
    .DESCRIPTION
        -BuildMethod on the image cmdlets: Docker and NoDocker are taken as given; Auto picks Docker when
        Test-LambdaPSContainerRuntime finds docker with buildx, and NoDocker when it reports DockerNotFound,
        DockerBuildxNotFound or DockerDaemonNotRunning. Auto never changes what happens where Docker works: it turns only the
        missing-Docker error into a build. Docker is required, whatever the method, for a local build
        (-Tag), since the no-Docker builder only pushes to ECR.
    .PARAMETER BuildMethod
        Auto, Docker or NoDocker.
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
    [PSCustomObject]@{ Method = 'Docker'; ContainerRuntime = $containerRuntime }
}
