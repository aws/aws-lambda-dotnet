function Copy-LambdaPSFolderContent {
    <#
    .SYNOPSIS
        Copies a folder's contents into another folder, leaving out .git and links that leave it.
    .DESCRIPTION
        The one copy rule for everything that ends up in a package or image: module folders
        (Copy-LambdaPSModuleFolder) and function source. Everything under -Path is copied, hidden files
        included, except:

          - .git directories, at any depth, and links that resolve to a .git folder or into one:
            repository history is never payload, and .git/config can hold a remote URL with a token.
          - Symbolic links whose target is outside -Path. A link inside -Path is copied as the content
            it points at; a link that leaves it (for example to ~/.aws or ~/.ssh) is skipped with a
            warning, so files from elsewhere on the machine are never copied in.
          - Folder links back to a folder they were reached through (-Path, a folder above the link,
            or two folders that link to each other), which would copy a folder into itself; links whose
            target cannot be resolved (for example two links that point at each other); and links to
            something that does not exist. They are skipped with a warning.
          - The paths in -ExcludePath, compared case-insensitively, what is inside them, and links to
            them. Excluding a link leaves out the link, not what it points to.

        Every link is decided by the real location of its final target, resolved as the OS resolves
        it by Get-LambdaPSRealPath (each link along the way followed, and '..' applied after it), so
        'link/../x' through a link that points outside is outside too. Paths are compared by their
        real location, so a -Path that is itself reached through a link (a module folder linked into
        the PSModulePath) is handled like any other, and a folder link is walked at the folder it
        points to. A file link is copied from the real path that was checked, not followed again. A
        warning names a skipped link by the path it was reached through, starting from -Path as
        given rather than its real location (for example <Path>/a/toB/toA).

        A hard link is an ordinary file to this check, so a hard link to a file outside -Path is
        copied like any other file.

        A file or folder that cannot be listed or copied, including one the user may not read, is a
        terminating error, so a copy is never left with part of its source silently missing. The
        caller has already confirmed the write; this helper does not ask again.
    .PARAMETER Path
        The source folder.
    .PARAMETER Destination
        The folder to copy into. It must exist.
    .PARAMETER ExcludePath
        Full paths to leave out.
    .OUTPUTS
        None.
    .EXAMPLE
        Copy-LambdaPSFolderContent -Path $found.ModuleBase -Destination ./rt/modules/AWS.Tools.S3/5.0.274

        Copies the module folder's contents without .git or links that point outside it.
    #>
    [CmdletBinding()]
    [OutputType([void])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Destination,

        [Parameter()]
        [AllowEmptyCollection()]
        [string[]]$ExcludePath = @()
    )

    $separator = [System.IO.Path]::DirectorySeparatorChar
    $root = Get-LambdaPSRealPath -Path (Resolve-LambdaPSPath -Path $Path)
    $rootPrefix = $root.TrimEnd($separator) + $separator
    $isRoot = { param([string]$Candidate) [string]::Equals($Candidate, $root, [System.StringComparison]::Ordinal) }

    # An exclude names an item, so only its folder is resolved: excluding a link leaves out the link,
    # not the file it points to. A link whose target is excluded, or inside an excluded folder, is left
    # out too.
    $excluded = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($exclude in @($ExcludePath | Where-Object { $_ })) {
        $null = $excluded.Add($exclude.TrimEnd($separator))
        $parent = [System.IO.Path]::GetDirectoryName($exclude.TrimEnd($separator))
        if ($parent) {
            $null = $excluded.Add([System.IO.Path]::Combine((Get-LambdaPSRealPath -Path $parent), [System.IO.Path]::GetFileName($exclude.TrimEnd($separator))))
        }
    }
    $isExcluded = {
        param([string]$Candidate)
        foreach ($entry in $excluded) {
            if ($Candidate.Equals($entry, [System.StringComparison]::OrdinalIgnoreCase) -or
                $Candidate.StartsWith($entry + $separator, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
        }
        $false
    }

    # Each queued folder carries the real folders it was reached through, so a folder link back to any
    # of them (up, or across through another link) is caught rather than copied without end.
    $pending = [System.Collections.Generic.Queue[object]]::new()
    # It also carries the path it was reached through from -Path as given, which names a skipped link
    # in a warning.
    $pending.Enqueue(@($root, (Resolve-LambdaPSPath -Path $Destination), [string[]]@($root), (Resolve-LambdaPSPath -Path $Path)))
    while ($pending.Count -gt 0) {
        $from, $to, $chain, $reachedFrom = $pending.Dequeue()
        foreach ($item in Get-ChildItem -LiteralPath $from -Force -ErrorAction Stop) {
            if (& $isExcluded $item.FullName) { continue }
            if ($item.PSIsContainer -and $item.Name -eq '.git') { continue }
            $walkFrom = $item.FullName
            $copyFrom = $item.FullName
            $reached = [System.IO.Path]::Combine($reachedFrom, $item.Name)
            if ($item.LinkType) {
                try {
                    $targetPath = Get-LambdaPSRealPath -Path $item.FullName
                }
                catch {
                    # A .NET IOException reaches here, possibly wrapped in a MethodInvocationException.
                    $reason = if ($_.Exception.InnerException) { $_.Exception.InnerException.Message } else { $_.Exception.Message }
                    Write-Warning "Skipped '$reached': its link target cannot be resolved ($reason)."
                    continue
                }
                if (-not ((& $isRoot $targetPath) -or $targetPath.StartsWith($rootPrefix, [System.StringComparison]::Ordinal))) {
                    Write-Warning "Skipped '$reached': it links to '$targetPath', outside '$root'."
                    continue
                }
                # Only a target that is not there is skipped; one the user may not read stops the copy.
                try {
                    $null = [System.IO.File]::GetAttributes($targetPath)
                }
                catch {
                    $cause = if ($_.Exception.InnerException) { $_.Exception.InnerException } else { $_.Exception }
                    if ($cause -is [System.IO.FileNotFoundException] -or $cause -is [System.IO.DirectoryNotFoundException]) {
                        Write-Warning "Skipped '$reached': it links to '$targetPath', which does not exist."
                        continue
                    }
                    throw
                }
                # A link into a .git folder is repository history under another name: left out like .git.
                $relative = if (& $isRoot $targetPath) { '' } else { $targetPath.Substring($rootPrefix.Length) }
                if (@($relative.Split([char[]]@('/', '\'), [System.StringSplitOptions]::RemoveEmptyEntries)) -contains '.git') { continue }
                if (& $isExcluded $targetPath) { continue }
                # A folder link to a folder this one was reached through would be copied into itself
                # until the path is too long, so it is skipped too.
                if ($item.PSIsContainer) {
                    $targetPrefix = $targetPath.TrimEnd($separator) + $separator
                    $loops = @($chain | Where-Object { ($_ + $separator).StartsWith($targetPrefix, [System.StringComparison]::Ordinal) })
                    if ($loops.Count -gt 0) {
                        Write-Warning "Skipped '$reached': it links to '$targetPath', a folder it was reached through."
                        continue
                    }
                }
                # Walk a folder link at its real location, so a link further down is checked against
                # real paths too.
                $walkFrom = $targetPath
                $copyFrom = $targetPath
            }
            $copy = [System.IO.Path]::Combine($to, $item.Name)
            if ($item.PSIsContainer) {
                $null = New-Item -ItemType Directory -Path $copy -Force -Confirm:$false -WhatIf:$false -ErrorAction Stop
                $pending.Enqueue(@($walkFrom, $copy, [string[]]@($chain + $walkFrom), $reached))
            }
            else {
                Copy-Item -LiteralPath $copyFrom -Destination $copy -Force -Confirm:$false -WhatIf:$false -ErrorAction Stop
            }
        }
    }
}
