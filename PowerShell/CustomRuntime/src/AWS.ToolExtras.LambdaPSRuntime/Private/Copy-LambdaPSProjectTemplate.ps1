function Copy-LambdaPSProjectTemplate {
    <#
    .SYNOPSIS
        Copies a runtime or function template tree to a target directory with version substitution.
    .DESCRIPTION
        Walks every file under each source template root in turn, recreates the relative layout
        beneath the target root, and writes each file as UTF-8 without BOM. The __PWSH_VERSION__ token is
        replaced with the resolved full pwsh version, __PWSH_RELEASE_URL__ with the pwsh release
        download URL, and __DOWNLOAD_RETRY_COUNT__, __DOWNLOAD_RETRY_DELAY__ and
        __DOWNLOAD_CONNECT_TIMEOUT__ with the download limits (all from Config/pwsh.psd1), in ANY
        file that contains them (the CMR and Container Dockerfiles carry them). For every target,
        __TIMEOUT__ and __MEMORY_SIZE__ are replaced with the function timeout and memory from
        Config/function.psd1 (the READMEs carry them). The
        __PWSH_ARM64_SHA256__ token (the CMR and Container Dockerfiles) is replaced with the module's pinned SHA256 of that
        version's linux-arm64 release tarball; if none is pinned, the token is left in place (so an
        arm64 build fails closed while amd64 still builds) and a warning is written. All other
        content is copied as text unchanged, except that the bootstrap is written with LF line endings
        and each -Token is replaced with its value. Every file is rendered first and then written
        under ONE ShouldProcess for the whole project, so -WhatIf reports a single line (the file count
        and the destination) without touching disk, and -Confirm asks once. Returns the absolute paths
        written.

        Each target's tree carries its own README.md, and Templates/Shared adds the runtime settings
        to it through README.md.append, for every target. Every target except RuntimeBound also gets a
        lambdaps-runtime.json marker recording the deployment target and PowerShell version, so the
        validator and future tooling can identify the project shape.
    .PARAMETER Path
        The template directories to copy from, in order, as returned by
        Get-LambdaPSProjectTemplateRoot: the module's Templates/Shared tree (the runtime interface
        client modules, runtime.ps1 and PowerShellLambdaContext.cs every target ships), then the
        target's own trees: Templates/RuntimeImage for CMR, Templates/LayerRuntime,
        Templates/SelfContainedZip, or Templates/RuntimeImage then Templates/ContainerFunction for
        Container. RuntimeBound (the function .zip project) is Templates/Shared then
        Templates/FunctionZip, but takes only the '<name>.append' files from Templates/Shared (the
        README's runtime settings), since the runtime it binds to brings everything else. The trees are merged into one project; if two
        roots carry the same relative path, the later root's file is the one written. A
        '<name>.append' file is added to the end of <name>, after a blank line, instead of being
        written itself.
    .PARAMETER Destination
        The directory to write the scaffolded project into.
    .PARAMETER PwshVersion
        The resolved full pwsh version (x.y.z) substituted into the __PWSH_VERSION__ token. Required
        for every target except RuntimeBound.
    .PARAMETER Target
        The deployment target being scaffolded ('CMR', 'Layer', 'SelfContainedZip', 'Container', or
        'RuntimeBound'), recorded in the marker file.
    .PARAMETER PowerShellVersion
        The PowerShell version in major.minor form, recorded in the marker file. Required for every
        target except RuntimeBound.
    .PARAMETER HandlerScript
        The script file name the template's root handler.ps1 is emitted as (for example 'myfn.ps1').
        Defaults to 'handler.ps1'. When it differs, the root handler.ps1 is written under this name
        and every 'handler.ps1' reference in it and in the root Dockerfile (the Container target's
        CMD), the root README.md and the root manifest.json is rewritten to match, so the
        scaffolded project still builds and runs and its documentation names the right file.
    .PARAMETER Token
        Placeholder to value, replaced in every file. In a .json file the value is written as a JSON
        value (a quoted, escaped string, or null), so the template writes the placeholder bare:
        "RuntimeAlias": __RUNTIME_ALIAS__.
    .EXAMPLE
        Copy-LambdaPSProjectTemplate -Path (Get-LambdaPSProjectTemplateRoot) -Destination ./rt -PwshVersion '7.4.20' -Target 'CMR' -PowerShellVersion '7.4'

        Copies the shared and CMR template trees into ./rt, substituting 7.4.20 into the Dockerfile.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([string[]])]
    # PSUseOutputTypeCorrectly: the return is comma-wrapped ('return , ([string[]]...)'). The unary
    # comma wraps the array so the pipeline emits it as ONE object whose runtime type is string[] --
    # which is exactly what OutputType declares. The rule infers Object[] from the static shape.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseOutputTypeCorrectly', '',
        Justification = 'Comma-wrapped return emits a single string[]; the declared OutputType is correct at runtime.')]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string[]]$Path,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Destination,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$PwshVersion,

        [Parameter(Mandatory)]
        [ValidateSet('CMR', 'Layer', 'SelfContainedZip', 'Container', 'RuntimeBound')]
        [string]$Target,

        [Parameter()]
        [ValidatePattern('\A[0-9]+\.[0-9]+\z')]
        [string]$PowerShellVersion,

        [Parameter()]
        [ValidatePattern('\A[A-Za-z][A-Za-z0-9_-]*\.ps1\z', Options = 'None')]
        [string]$HandlerScript = 'handler.ps1',

        [Parameter()]
        [ValidateNotNull()]
        [hashtable]$Token = @{}
    )

    # A runtime-carrying project records both versions in its marker and bakes the pwsh version in.
    $carriesRuntime = $Target -ne 'RuntimeBound'
    if ($carriesRuntime -and ([string]::IsNullOrWhiteSpace($PwshVersion) -or [string]::IsNullOrWhiteSpace($PowerShellVersion))) {
        # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error with
        # its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
        # caller's -ErrorVariable naming this internal helper.
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.ArgumentException]::new("-PwshVersion and -PowerShellVersion are required for -Target $Target.", 'PwshVersion'),
            'PwshVersionRequired', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Target)
    }

    $projectRoot = Resolve-LambdaPSPath -Path $Destination
    $encoding = [System.Text.UTF8Encoding]::new($false)
    $rendered = [ordered]@{}
    $warnedArm64Hash = $false

    # Merge the roots by relative path first, so a path carried by two roots is written once, from
    # the later root. A '<name>.append' file is not written itself: its text is added to the end of
    # <name> (the Container target's Dockerfile is the runtime image's plus the function steps).
    # RuntimeBound carries no runtime, so from the roots before its own (Templates/Shared) it takes only
    # the '<name>.append' files: the runtime settings section of its README.
    $files = [ordered]@{}
    $appends = @{}
    for ($rootIndex = 0; $rootIndex -lt $Path.Count; $rootIndex++) {
        $appendsOnly = -not $carriesRuntime -and $rootIndex -lt $Path.Count - 1
        $source = Resolve-LambdaPSPath -Path $Path[$rootIndex]
        $childItemParams = @{
            LiteralPath = $source
            Recurse     = $true
            File        = $true
            Force       = $true
        }
        foreach ($file in @(Get-ChildItem @childItemParams)) {
            $relative = $file.FullName.Substring($source.Length).TrimStart(
                [System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
            if ($relative.EndsWith('.append', [System.StringComparison]::Ordinal)) {
                $appends[$relative.Substring(0, $relative.Length - '.append'.Length)] += [System.IO.File]::ReadAllText($file.FullName)
            }
            elseif (-not $appendsOnly) {
                $files[$relative] = $file
            }
        }
    }
    foreach ($entry in $files.GetEnumerator()) {
        $relative = $entry.Key
        $file = $entry.Value
        $text = [System.IO.File]::ReadAllText($file.FullName)
        # An appended section is separated from the base text by one blank line.
        if ($appends.Contains($relative)) {
            $text = $text.TrimEnd("`r", "`n") + "`n`n" + $appends[$relative]
        }

        # The templates name the handler 'handler.ps1'. For a different -HandlerScript, emit the root
        # handler under that name and point the root Dockerfile (Container: CMD) at it;
        # otherwise the build fails or the image runs a handler the caller did not ask for. The root
        # README and manifest.json name it too. The comparisons are case-sensitive: Lambda runs on
        # Linux, so 'Handler.ps1' is a different file from 'handler.ps1'.
        if ($HandlerScript -cne 'handler.ps1' -and @('handler.ps1', 'Dockerfile', 'README.md', 'manifest.json') -ccontains $relative) {
            $text = $text.Replace('handler.ps1', $HandlerScript)
            if ($relative -ceq 'handler.ps1') { $relative = $HandlerScript }
        }
        $outputPath = [System.IO.Path]::Combine($projectRoot, $relative)

        # Substitute the concrete pwsh version, the release download URL and the download retry
        # limits into ANY file carrying the token (the CMR and Container Dockerfiles).
        if ($carriesRuntime) {
            $download = $script:Config.pwsh.Download
            $text = $text.Replace('__PWSH_VERSION__', $PwshVersion).Replace('__PWSH_RELEASE_URL__', $script:Config.pwsh.ReleaseUrl).
                Replace('__DOWNLOAD_RETRY_COUNT__', [string]$download.RetryCount).
                Replace('__DOWNLOAD_RETRY_DELAY__', [string]$download.RetryDelaySeconds).
                Replace('__DOWNLOAD_CONNECT_TIMEOUT__', [string]$download.ConnectTimeoutSeconds)
        }
        # The function timeout and memory (Config/function.psd1) that the function and layer READMEs
        # suggest; the RuntimeImage README does not.
        $text = $text.Replace('__TIMEOUT__', [string]$script:Config.function.Timeout).
            Replace('__MEMORY_SIZE__', [string]$script:Config.function.MemorySize)
        $isJson = $relative.EndsWith('.json', [System.StringComparison]::OrdinalIgnoreCase)
        foreach ($name in $Token.Keys) {
            $value = if ($isJson) { ConvertTo-Json -InputObject $Token[$name] -Compress } else { [string]$Token[$name] }
            $text = $text.Replace([string]$name, $value)
        }

        # The CMR and Container Dockerfiles install pwsh on arm64 from the release tarball (there is
        # no aarch64 RPM) and verify it against this pinned hash. The architecture is chosen at build
        # time, so substitute it regardless of target. With no pinned hash the token is left in
        # place: an arm64 build then stops at the Dockerfile's hash check, while amd64 still builds.
        if ($text.Contains('__PWSH_ARM64_SHA256__')) {
            $arm64Hash = Get-LambdaPSPwshArchiveHash -PwshVersion $PwshVersion -Architecture 'arm64'
            if ($arm64Hash) {
                $text = $text.Replace('__PWSH_ARM64_SHA256__', $arm64Hash)
            }
            elseif (-not $warnedArm64Hash) {
                Write-Warning ("No SHA256 is pinned for the pwsh $PwshVersion arm64 tarball, so the scaffolded " +
                    "'$relative' can build linux/amd64 only. To build linux/arm64, replace __PWSH_ARM64_SHA256__ " +
                    "in it with the SHA256 of powershell-$PwshVersion-linux-arm64.tar.gz.")
                $warnedArm64Hash = $true
            }
        }
        # Everything else is copied unchanged.

        # The bootstrap is executed directly by the kernel on Linux, so it must be LF. .gitattributes
        # forces that on checkout, but a module installed from a CRLF source (or a hand-copied template)
        # would otherwise scaffold a bootstrap that fails with "bad interpreter".
        if ($file.Name -eq 'bootstrap') {
            $text = $text.Replace("`r`n", "`n")
        }

        $rendered[$outputPath] = $text
    }

    # Marker recording the deployment target, PowerShell version and pwsh version. The validator
    # reads it to apply target-specific rules (absence defaults to CMR), and
    # Build-LambdaPSRuntimePackage bundles the recorded pwsh version unless told otherwise.
    if ($carriesRuntime) {
        $rendered[[System.IO.Path]::Combine($projectRoot, 'lambdaps-runtime.json')] = [ordered]@{
            Target            = $Target
            PowerShellVersion = $PowerShellVersion
            PwshVersion       = $PwshVersion
        } | ConvertTo-Json
    }

    # One ShouldProcess for the whole tree: a per-file prompt or 'What if:' line for every template
    # file buries the stages a caller is deciding on.
    $written = [System.Collections.Generic.List[string]]::new()
    if ($PSCmdlet.ShouldProcess($projectRoot, "Create $($rendered.Count) files from the $Target template")) {
        foreach ($entry in $rendered.GetEnumerator()) {
            $directory = [System.IO.Path]::GetDirectoryName($entry.Key)
            if (-not (Test-Path -LiteralPath $directory)) {
                $null = New-Item -ItemType Directory -Path $directory -Force -WhatIf:$false -Confirm:$false
            }
            [System.IO.File]::WriteAllText($entry.Key, $entry.Value, $encoding)
            # The kernel runs the bootstrap directly, so it is executable (755) like the template. It is
            # set rather than copied: a module installed from a .nupkg keeps no Unix modes.
            if (-not $IsWindows -and [System.IO.Path]::GetFileName($entry.Key) -ceq 'bootstrap') {
                [System.IO.File]::SetUnixFileMode($entry.Key, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute, GroupRead, GroupExecute, OtherRead, OtherExecute')
            }
            $written.Add($entry.Key)
        }
    }

    return , ([string[]]$written)
}
