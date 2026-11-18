function Get-LambdaPSDockerBuilderPlatform {
    <#
    .SYNOPSIS
        Lists the platforms docker's current buildx builder can build for.
    .DESCRIPTION
        Reads 'docker buildx inspect', which the daemon side answers, so it covers emulators registered
        on a remote daemon, in Docker Desktop's VM or in a docker-container builder. Returns nothing when
        that can't be told: the command fails, or the builder isn't running and lists no platforms.
    .PARAMETER Path
        The docker executable, as Test-LambdaPSContainerRuntime found it.
    .OUTPUTS
        System.String
    .EXAMPLE
        Get-LambdaPSDockerBuilderPlatform -Path /usr/bin/docker

        Returns, for example, linux/amd64 and linux/386 on an x86_64 host with no emulator registered.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    # A caller's $PSNativeCommandUseErrorActionPreference would throw before $LASTEXITCODE is checked.
    $PSNativeCommandUseErrorActionPreference = $false
    try { $inspect = & $Path buildx inspect 2>&1 }
    catch {
        Write-Verbose "docker buildx inspect failed: $($_.Exception.Message); the builder's platforms are unknown."
        return
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Verbose "docker buildx inspect exited with $LASTEXITCODE; the builder's platforms are unknown."
        return
    }
    # One Platforms line per builder node; buildx marks platforms set by the user with '*' and can
    # group a platform's variants as 'linux/amd64 (+3)'.
    foreach ($line in @($inspect) -match '^\s*Platforms:') {
        foreach ($platform in ($line -replace '^\s*Platforms:', '') -split ',') {
            $platform = ($platform.Trim().TrimEnd('*') -replace '\s*\(\+\d+\)$', '').Trim()
            if ($platform) { $platform }
        }
    }
}
