function Invoke-LambdaPSOciImageBuild {
    <#
    .SYNOPSIS
        Builds an image from a no-Docker recipe with AWS.OCI.Builder and pushes it to ECR.
    .DESCRIPTION
        The no-Docker counterpart of 'docker buildx build --push', and the only function that calls the
        AWS.OCI.Builder library, so the rest of the module can mock it. It takes the recipe
        Get-LambdaPSOciImagePlan returns:

          1. Downloads and verifies the pwsh tarball (Get-LambdaPSPwshArchive) and unpacks it with
             System.Formats.Tar, keeping each entry's Unix mode, which a Windows file system cannot hold,
             and adds the ICU libraries (Get-LambdaPSIcuPackage) next to pwsh.
          2. Stages the executable scripts LF-only and without a BOM, and on Linux precompiles
             PowerShellLambdaContext.dll (Invoke-LambdaPSOciContextPrecompile).
          3. Pulls the base image's manifest for the platform, adds one layer for pwsh, one for the runtime
             and (Container) one for the function, so a rebuild that changes only the function re-pushes
             only that layer, and sets the entrypoint, CMD and environment.
          4. Pushes with the ECR authorization token, reporting each layer with Write-Progress.

        Every library call is awaited in short waits on the pipeline thread, so Ctrl+C cancels the push
        and progress is drained where Write-Progress can run. The staging folder is removed on the way out.
    .PARAMETER Plan
        The recipe from Get-LambdaPSOciImagePlan.
    .PARAMETER ImageUri
        The ECR image reference to push to.
    .PARAMETER AuthorizationToken
        The ECR authorization token (base64 'AWS:<password>') from Get-ECRAuthorizationToken.
    .OUTPUTS
        System.String

        The pushed manifest's digest (sha256:...).
    .EXAMPLE
        Invoke-LambdaPSOciImageBuild -Plan $plan -ImageUri $uri -AuthorizationToken $token

        Builds the image the plan describes and returns its digest.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [PSTypeName('AWS.ToolExtras.LambdaPSRuntime.OciImagePlan')]
        [PSCustomObject]$Plan,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ImageUri,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$AuthorizationToken
    )

    Import-LambdaPSOciBuilder
    $cancellation = [System.Threading.CancellationTokenSource]::new()
    $stage = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "lambdaps-oci-$([System.IO.Path]::GetRandomFileName())")
    $builder = $null
    $archive = $null
    $icuPackage = $null
    $sink = [AWS.OCI.Builder.LayerPushProgressQueue]::new()
    $activity = "Building image $ImageUri"
    $layerProgress = @{}
    # Waits on a library task in short slices, so Ctrl+C stops the wait (and the finally block cancels the
    # task) and queued push events reach Write-Progress on this thread.
    $await = {
        param([System.Threading.Tasks.Task]$Task)
        while (-not $Task.AsyncWaitHandle.WaitOne(200)) { & $drain }
        & $drain
        # GetResult rethrows the task's own exception rather than an AggregateException.
        $Task.GetAwaiter().GetResult()
    }
    $drain = {
        $pushEvent = [AWS.OCI.Builder.LayerPushProgress]::new()
        while ($sink.TryDequeue([ref]$pushEvent)) {
            $layerProgress[$pushEvent.LayerIndex] = $pushEvent
            $done = @($layerProgress.Values | Where-Object { $_.Phase -eq 'Completed' }).Count
            $status = switch ($pushEvent.Phase) {
                'Completed' { if ($pushEvent.IsSkipped) { 'already in the repository' } else { 'pushed' } }
                default { '{0:N1} of {1:N1} MB' -f ($pushEvent.BytesPushed / 1MB), ($pushEvent.LayerSizeBytes / 1MB) }
            }
            Write-Progress -Activity $activity -Status "Layer $($pushEvent.LayerIndex) of $($pushEvent.TotalLayers) ($($pushEvent.Kind)): $status" `
                -PercentComplete ([int](100 * $done / [Math]::Max(1, $pushEvent.TotalLayers)))
        }
    }

    # Lambda does not accept a runtime image whose layer has files in a folder no layer creates (the version
    # settles FAILED), so every folder the image adds below /var/runtime or /var/task gets its own entry.
    $addFolders = {
        param([AWS.OCI.Builder.FileManifest]$Manifest)
        $folders = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($containerPath in @($Manifest.Entries.ContainerPath)) {
            $folder = $containerPath.Substring(0, $containerPath.LastIndexOf('/'))
            while ($folder.StartsWith('/var/runtime/', [System.StringComparison]::Ordinal) -or $folder.StartsWith('/var/task/', [System.StringComparison]::Ordinal)) {
                $null = $folders.Add($folder)
                $folder = $folder.Substring(0, $folder.LastIndexOf('/'))
            }
        }
        foreach ($folder in $folders) { $Manifest.AddEmptyDirectory($folder) }
    }

    try {
        $null = New-Item -ItemType Directory -Path $stage -Force -WhatIf:$false -Confirm:$false

        # 1. pwsh, with the tarball's own modes.
        Write-Progress -Activity $activity -Status "Downloading pwsh $($Plan.PwshVersion) ($($Plan.Architecture))"
        $archive = Get-LambdaPSPwshArchive -PwshVersion $Plan.PwshVersion -Architecture $Plan.Architecture
        $pwshRoot = [System.IO.Path]::Combine($stage, 'powershell')
        $pwshFiles = [AWS.OCI.Builder.FileManifest]::new()
        $stream = [System.IO.File]::OpenRead($archive)
        try {
            $reader = [System.Formats.Tar.TarReader]::new([System.IO.Compression.GZipStream]::new($stream, [System.IO.Compression.CompressionMode]::Decompress))
            while ($null -ne ($entry = $reader.GetNextEntry())) {
                $relative = $entry.Name -replace '^(\./)+', ''
                if ($relative.Length -eq 0 -or $entry.EntryType -eq [System.Formats.Tar.TarEntryType]::Directory) { continue }
                if (@([System.Formats.Tar.TarEntryType]::RegularFile, [System.Formats.Tar.TarEntryType]::V7RegularFile) -notcontains $entry.EntryType) {
                    # The library writes regular files only; the pinned tarballs have nothing else.
                    throw [System.Management.Automation.ErrorRecord]::new(
                        [System.NotSupportedException]::new("The pwsh tarball entry '$relative' is a $($entry.EntryType), which the no-Docker build cannot add to an image."),
                        'PwshArchiveEntryUnsupported', [System.Management.Automation.ErrorCategory]::NotImplemented, $relative)
                }
                if ($relative.Split('/') -contains '..') {
                    throw [System.Management.Automation.ErrorRecord]::new(
                        [System.Security.SecurityException]::new("The pwsh tarball entry '$relative' leaves the extraction folder."),
                        'PwshArchiveEntryUnsafe', [System.Management.Automation.ErrorCategory]::SecurityError, $relative)
                }
                $local = [System.IO.Path]::Combine($pwshRoot, $relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
                $null = [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($local))
                $entry.ExtractToFile($local, $true)
                $pwshFiles.Add($local, "/var/runtime/powershell/$relative", $entry.Mode)
            }
        }
        finally { $stream.Dispose() }
        $icuPackage = Get-LambdaPSIcuPackage -Architecture $Plan.Architecture
        $zip = [System.IO.Compression.ZipFile]::OpenRead($icuPackage)
        try {
            foreach ($entry in @($zip.Entries | Where-Object FullName -Like 'runtimes/linux-*/native/*')) {
                $local = [System.IO.Path]::Combine($pwshRoot, $entry.Name)
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $local, $true)
                $pwshFiles.Add($local, "/var/runtime/powershell/$($entry.Name)", [System.IO.UnixFileMode]'UserRead, UserWrite, GroupRead, OtherRead')
            }
        }
        finally { $zip.Dispose() }

        # 2. Runtime files: executables staged LF-only without a BOM (the Dockerfile's sed).
        $runtimeFiles = [AWS.OCI.Builder.FileManifest]::new()
        $runtimeStage = [System.IO.Path]::Combine($stage, 'runtime')
        $null = [System.IO.Directory]::CreateDirectory($runtimeStage)
        foreach ($file in $Plan.RuntimeFiles) {
            $local = $file.LocalPath
            if ($file.Normalize) {
                $local = [System.IO.Path]::Combine($runtimeStage, [System.IO.Path]::GetFileName($file.LocalPath))
                $text = [System.IO.File]::ReadAllText($file.LocalPath).TrimStart([char]0xFEFF).Replace("`r`n", "`n")
                [System.IO.File]::WriteAllText($local, $text, [System.Text.UTF8Encoding]::new($false))
            }
            if ($file.Executable) { $runtimeFiles.AddExecutable($local, $file.ContainerPath) }
            # The source's mode, as docker COPY keeps it (a module's native helper stays executable).
            else { $runtimeFiles.Add($local, $file.ContainerPath) }
        }
        $contextDll = [System.IO.Path]::Combine($runtimeStage, 'PowerShellLambdaContext.dll')
        $precompile = @{
            BundledPwshPath = [System.IO.Path]::Combine($pwshRoot, 'pwsh')
            PwshVersion     = $Plan.PwshVersion
            Architecture    = $Plan.Architecture
            SourcePath      = @($Plan.RuntimeFiles | Where-Object { $_.ContainerPath -ceq '/var/runtime/PowerShellLambdaContext.cs' })[0].LocalPath
            OutputPath      = $contextDll
            StagePath       = $stage
        }
        Write-Progress -Activity $activity -Status 'Precompiling PowerShellLambdaContext.dll'
        if (Invoke-LambdaPSOciContextPrecompile @precompile) {
            $runtimeFiles.Add($contextDll, '/var/runtime/PowerShellLambdaContext.dll', [System.IO.UnixFileMode]'UserRead, UserWrite, GroupRead, OtherRead')
        }
        else {
            Write-Verbose 'PowerShellLambdaContext.dll was not precompiled, so the runtime compiles PowerShellLambdaContext.cs at cold start.'
        }

        # 3. Base image, layers and config.
        Write-Progress -Activity $activity -Status "Reading base image $($Plan.BaseImage) ($($Plan.Platform))"
        $builder = & $await ([AWS.OCI.Builder.OciImageBuilder]::FromBaseImageAsync($Plan.BaseImage, $Plan.Platform, $cancellation.Token))
        & $addFolders $pwshFiles
        & $addFolders $runtimeFiles
        Write-Progress -Activity $activity -Status 'Building the pwsh layer'
        $null = & $await ($builder.AddLayerAsync($pwshFiles, "LambdaPSRuntime: pwsh $($Plan.PwshVersion)", $cancellation.Token))
        Write-Progress -Activity $activity -Status 'Building the runtime layer'
        $null = & $await ($builder.AddLayerAsync($runtimeFiles, 'LambdaPSRuntime: runtime', $cancellation.Token))
        if ($Plan.FunctionFiles.Count -gt 0) {
            $functionFiles = [AWS.OCI.Builder.FileManifest]::new()
            foreach ($file in $Plan.FunctionFiles) {
                if ($file.Executable) { $functionFiles.AddExecutable($file.LocalPath, $file.ContainerPath) }
                else { $functionFiles.Add($file.LocalPath, $file.ContainerPath) }
            }
            & $addFolders $functionFiles
            Write-Progress -Activity $activity -Status 'Building the function layer'
            $null = & $await ($builder.AddLayerAsync($functionFiles, 'LambdaPSRuntime: function', $cancellation.Token))
        }
        $builder.Config.SetEntrypoint([string[]]$Plan.Entrypoint)
        if ($Plan.Cmd.Count -gt 0) { $builder.Config.SetCmd([string[]]$Plan.Cmd) }
        foreach ($variable in $Plan.Environment.GetEnumerator()) { $builder.Config.SetEnvironmentVariable($variable.Key, $variable.Value) }
        $process = [System.Diagnostics.Process]::GetCurrentProcess()
        $process.Refresh()
        Write-Verbose ('Layers built; peak working set {0:N0} MB.' -f ($process.PeakWorkingSet64 / 1MB))

        # 4. Push.
        $options = [AWS.OCI.Builder.EcrPushOptions]@{ Progress = $sink }
        $digest = & $await ($builder.PushToEcrAsync($ImageUri, $AuthorizationToken, $options, $cancellation.Token))
        Write-Verbose "Pushed '$ImageUri' as $digest."
        $digest
    }
    catch [AWS.OCI.Builder.Registry.RegistryException] {
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new("The no-Docker build could not push '$ImageUri': $($_.Exception.Message)", $_.Exception),
            'OciImagePushFailed', [System.Management.Automation.ErrorCategory]::OperationStopped, $ImageUri)
    }
    finally {
        # In finally, not catch: stopping the pipeline (Ctrl+C) skips catch but still runs finally.
        $cancellation.Cancel()
        if ($null -ne $builder) { $builder.Dispose() }
        $cancellation.Dispose()
        Write-Progress -Activity $activity -Completed
        foreach ($download in @($archive, $icuPackage) | Where-Object { $_ }) {
            Remove-Item -LiteralPath $download -Force -ErrorAction SilentlyContinue -WhatIf:$false -Confirm:$false
        }
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue -WhatIf:$false -Confirm:$false
    }
}
