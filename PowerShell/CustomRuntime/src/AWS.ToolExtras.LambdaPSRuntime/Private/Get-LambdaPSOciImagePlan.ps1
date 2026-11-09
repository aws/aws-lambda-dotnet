function Get-LambdaPSOciImagePlan {
    <#
    .SYNOPSIS
        Translates a scaffolded runtime-image or container-function project into the no-Docker image recipe.
    .DESCRIPTION
        The no-Docker build cannot run a Dockerfile's RUN steps, so it builds a fixed recipe that matches
        the scaffolded Dockerfile instead, step for step:

          1. pwsh: the official release tarball, verified against the module's pinned SHA256, on both
             architectures, unpacked into /var/runtime/powershell (the Dockerfile uses the RPM on amd64).
             The base has no libicu, so Microsoft's ICU package goes next to pwsh and the image runs with
             DOTNET_SYSTEM_GLOBALIZATION_APPLOCALICU, so cultures work as in the Docker image.
          2. PowerShellLambdaContext.dll: compiled with the bundled pwsh when the host can run it (a Linux
             host of the target architecture); otherwise the runtime compiles the .cs at cold start.
          3. lambda-entrypoint.sh and bootstrap (0755, BOM and CRLF stripped), runtime.ps1,
             PowerShellLambdaContext.cs and modules/ under /var/runtime.
          4. ENTRYPOINT /var/runtime/lambda-entrypoint.sh.
          5. Container only: the build context, less what .dockerignore excludes and the runtime files,
             under /var/task, and the Dockerfile's CMD.

        That holds only while the Dockerfile is the one the scaffolder wrote, so the project's Dockerfile
        is compared with the template rendered for the project's marker (and, for a Container project,
        the handler its CMD names). Any other edit (an extra RUN dnf install, a different base image) is
        refused with DockerfileModified, which names both ways forward. This reads files only.
    .PARAMETER Path
        The project directory (resolved, with a Dockerfile).
    .PARAMETER Architecture
        The target architecture: x86_64 or arm64.
    .OUTPUTS
        System.Management.Automation.PSCustomObject

        Target, PwshVersion, Architecture, Platform, BaseImage, Entrypoint, Cmd, Environment, RuntimeFiles
        and FunctionFiles (each file a LocalPath, ContainerPath, Executable and Normalize).
    .EXAMPLE
        Get-LambdaPSOciImagePlan -Path ./rt -Architecture arm64

        Returns the recipe for an arm64 runtime image of ./rt.
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter(Mandatory)]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture
    )

    $root = Resolve-LambdaPSPath -Path $Path
    $dockerfilePath = [System.IO.Path]::Combine($root, 'Dockerfile')
    $marker = Get-LambdaPSProjectMarker -Path $root
    $newModified = {
        param([string]$Reason)
        [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new(
                ("The no-Docker build cannot build '$root': $Reason It builds only the Dockerfile the module scaffolds, because " +
                 'it cannot run RUN steps. Build with -BuildMethod Docker, or re-scaffold the project and move your changes ' +
                 'into modules/ or the function folder.')),
            'DockerfileModified', [System.Management.Automation.ErrorCategory]::InvalidData, $dockerfilePath)
    }
    if ($null -eq $marker -or @('CMR', 'Container') -notcontains $marker.Target -or [string]::IsNullOrWhiteSpace($marker.PwshVersion)) {
        throw (& $newModified 'it has no lambdaps-runtime.json naming a CMR or Container target and its pwsh version.')
    }
    $target = if ($marker.Target -eq 'Container') { 'Container' } else { 'CMR' }
    if ($Architecture -eq 'x86_64' -and [string]::IsNullOrWhiteSpace((Get-LambdaPSPwshArchiveHash -PwshVersion $marker.PwshVersion -Architecture x86_64))) {
        throw (New-LambdaPSPwshX64HashNotPinnedError -PwshVersion $marker.PwshVersion)
    }

    # The Dockerfile must be the rendered template. A Container project's CMD may name another handler,
    # so the template is rendered for the handler the CMD names.
    $normalize = { param([string]$Text) ($Text.TrimStart([char]0xFEFF) -replace "`r`n", "`n").TrimEnd() }
    $actual = & $normalize ([System.IO.File]::ReadAllText($dockerfilePath))
    $handler = 'handler.ps1'
    if ($target -eq 'Container') {
        if ($actual -notmatch '(?m)^CMD \[ "([A-Za-z][A-Za-z0-9_-]*\.ps1)" \]$') {
            throw (& $newModified 'its Dockerfile has no CMD [ "<handler>.ps1" ] line.')
        }
        $handler = $Matches[1]
    }
    $renderRoot = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "lambdaps-oci-plan-$([System.IO.Path]::GetRandomFileName())")
    try {
        $templateParams = @{
            Path              = Get-LambdaPSProjectTemplateRoot -Target $target
            Destination       = $renderRoot
            PwshVersion       = $marker.PwshVersion
            PowerShellVersion = $marker.PowerShellVersion
            Target            = $target
            HandlerScript     = $handler
        }
        $null = New-Item -ItemType Directory -Path $renderRoot -Force -WhatIf:$false -Confirm:$false
        $null = Copy-LambdaPSProjectTemplate @templateParams -WarningAction SilentlyContinue -WhatIf:$false -Confirm:$false
        $expected = & $normalize ([System.IO.File]::ReadAllText([System.IO.Path]::Combine($renderRoot, 'Dockerfile')))
    }
    finally {
        Remove-Item -LiteralPath $renderRoot -Recurse -Force -ErrorAction SilentlyContinue -WhatIf:$false -Confirm:$false
    }
    if ($actual -cne $expected) {
        throw (& $newModified "its Dockerfile differs from the one scaffolded for pwsh $($marker.PwshVersion).")
    }
    $baseImage = if ($actual -match '(?m)^FROM (\S+)$') { $Matches[1] }

    # Step 3: the runtime files the Dockerfile copies into /var/runtime.
    $runtimeFiles = [System.Collections.Generic.List[object]]::new()
    foreach ($name in 'lambda-entrypoint.sh', 'bootstrap', 'runtime.ps1', 'PowerShellLambdaContext.cs') {
        $local = [System.IO.Path]::Combine($root, $name)
        if (-not [System.IO.File]::Exists($local)) {
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.IO.FileNotFoundException]::new("The project '$root' has no $name, which the image needs.", $local),
                'ProjectFileNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $local)
        }
        $executable = @('lambda-entrypoint.sh', 'bootstrap') -ccontains $name
        $runtimeFiles.Add([PSCustomObject]@{ LocalPath = $local; ContainerPath = "/var/runtime/$name"; Executable = $executable; Normalize = $executable })
    }
    $modulesRoot = [System.IO.Path]::Combine($root, 'modules')
    if ([System.IO.Directory]::Exists($modulesRoot)) {
        foreach ($file in [System.IO.Directory]::EnumerateFiles($modulesRoot, '*', [System.IO.SearchOption]::AllDirectories)) {
            $relative = $file.Substring($modulesRoot.Length).TrimStart('\', '/').Replace('\', '/')
            $runtimeFiles.Add([PSCustomObject]@{ LocalPath = $file; ContainerPath = "/var/runtime/modules/$relative"; Executable = $false; Normalize = $false })
        }
    }

    # Step 5: COPY . /var/task, then the rm of the runtime files.
    $functionFiles = [System.Collections.Generic.List[object]]::new()
    if ($target -eq 'Container') {
        $runtimeNames = @('lambda-entrypoint.sh', 'bootstrap', 'runtime.ps1', 'PowerShellLambdaContext.cs')
        foreach ($relative in @(Select-LambdaPSBuildContextFile -Path $root)) {
            if ($runtimeNames -ccontains $relative -or $relative.StartsWith('modules/', [System.StringComparison]::Ordinal)) { continue }
            $functionFiles.Add([PSCustomObject]@{
                    LocalPath     = [System.IO.Path]::Combine($root, $relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
                    ContainerPath = "/var/task/$relative"
                    Executable    = $false
                    Normalize     = $false
                })
        }
    }

    [PSCustomObject]@{
        PSTypeName    = 'AWS.ToolExtras.LambdaPSRuntime.OciImagePlan'
        Target        = $target
        PwshVersion   = $marker.PwshVersion
        Architecture  = $Architecture
        Platform      = if ($Architecture -eq 'arm64') { 'linux/arm64' } else { 'linux/amd64' }
        BaseImage     = $baseImage
        Entrypoint    = [string[]]@('/var/runtime/lambda-entrypoint.sh')
        Cmd           = if ($target -eq 'Container') { [string[]]@($handler) } else { [string[]]@() }
        Environment   = [ordered]@{ DOTNET_SYSTEM_GLOBALIZATION_APPLOCALICU = $script:Config.pwsh.Icu.Version }
        RuntimeFiles  = $runtimeFiles.ToArray()
        FunctionFiles = $functionFiles.ToArray()
    }
}
