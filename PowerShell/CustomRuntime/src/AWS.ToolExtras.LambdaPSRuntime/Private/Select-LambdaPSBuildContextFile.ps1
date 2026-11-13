function Select-LambdaPSBuildContextFile {
    <#
    .SYNOPSIS
        Lists the files of a build context that its .dockerignore does not exclude.
    .DESCRIPTION
        The no-Docker build's stand-in for the build context docker sends: every file under -Path, hidden
        ones included, less those a .dockerignore pattern excludes. It follows docker's rules for the
        patterns the scaffolded .dockerignore and common edits use:

          - one pattern per line; blank lines and lines starting with '#' are ignored, a leading '/'
            or './' is dropped, since every pattern is relative to the context root, and a trailing '/'
            is dropped, as docker cleans each pattern;
          - '*' matches any run of characters but '/', '?' any one character but '/', and '**' any number
            of folders, so '**/.git' matches .git at any depth;
          - a pattern that matches a folder excludes everything under it;
          - a pattern starting with '!' includes again what an earlier pattern excluded, and the last
            pattern that matches a path decides.

        A link is skipped when its target is outside -Path (with a warning), is excluded, or is a folder
        the link is inside (with a warning); a folder link inside -Path is walked.

        Character classes ('[a-z]'), '\' (an escape to docker) and '..' segments are not supported; a pattern
        that uses one is refused with DockerIgnorePatternUnsupported rather than matched differently from docker.
    .PARAMETER Path
        The build context root.
    .OUTPUTS
        System.String

        The relative path of each included file, with '/' separators, in ordinal order.
    .EXAMPLE
        Select-LambdaPSBuildContextFile -Path ./my-function

        Lists the files 'COPY . /var/task' would copy from ./my-function.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path
    )

    $root = Resolve-LambdaPSPath -Path $Path
    $rules = [System.Collections.Generic.List[object]]::new()
    $ignorePath = [System.IO.Path]::Combine($root, '.dockerignore')
    if ([System.IO.File]::Exists($ignorePath)) {
        foreach ($line in [System.IO.File]::ReadAllLines($ignorePath)) {
            $pattern = $line.Trim()
            if ($pattern.Length -eq 0 -or $pattern.StartsWith('#')) { continue }
            $include = $pattern.StartsWith('!')
            if ($include) { $pattern = $pattern.Substring(1).Trim() }
            # Cleaned as docker cleans it (filepath.Clean), so 'secrets/' matches the folder 'secrets'.
            $pattern = ($pattern -replace '/+', '/' -replace '^(\./|/)+', '').TrimEnd('/')
            if ($pattern.Length -eq 0) { continue }
            if ($pattern -match '[\[\]\\]|(^|/)\.\.(/|$)') {
                throw [System.Management.Automation.ErrorRecord]::new(
                    [System.NotSupportedException]::new(
                        ("The .dockerignore pattern '$line' in '$root' uses a character class, a '\' or a '..' segment, which the " +
                         'no-Docker build does not match. Rewrite it with /, * and ?, or build with -BuildMethod Docker.')),
                    'DockerIgnorePatternUnsupported', [System.Management.Automation.ErrorCategory]::InvalidData, $ignorePath)
            }
            # '**/' is any number of leading folders, '**' anything, '*' and '?' stop at '/'.
            $regex = [System.Text.StringBuilder]::new('\A')
            for ($i = 0; $i -lt $pattern.Length; $i++) {
                $c = $pattern[$i]
                if ($c -eq '*' -and $i + 1 -lt $pattern.Length -and $pattern[$i + 1] -eq '*') {
                    if ($i + 2 -lt $pattern.Length -and $pattern[$i + 2] -eq '/') { $null = $regex.Append('(?:.*/)?'); $i += 2 }
                    else { $null = $regex.Append('.*'); $i++ }
                }
                elseif ($c -eq '*') { $null = $regex.Append('[^/]*') }
                elseif ($c -eq '?') { $null = $regex.Append('[^/]') }
                else { $null = $regex.Append([regex]::Escape([string]$c)) }
            }
            $null = $regex.Append('\z')
            $rules.Add([PSCustomObject]@{ Include = $include; Regex = [regex]::new($regex.ToString(), 'CultureInvariant') })
        }
    }

    # A pattern excludes a path when it matches the path or any folder above it; the last match decides.
    $isExcluded = {
        param([string]$Relative)
        $segments = $Relative.Split('/')
        $excluded = $false
        foreach ($rule in $rules) {
            for ($n = 1; $n -le $segments.Count; $n++) {
                if ($rule.Regex.IsMatch(($segments[0..($n - 1)] -join '/'))) { $excluded = -not $rule.Include; break }
            }
        }
        $excluded
    }

    # Docker sends a link as a link, so a link out of the context, or to a path .dockerignore excludes,
    # never brings its target into the image. A folder link inside the context is walked, as the .zip build does.
    $separator = [System.IO.Path]::DirectorySeparatorChar
    $comparison = if ($IsLinux) { [System.StringComparison]::Ordinal } else { [System.StringComparison]::OrdinalIgnoreCase }
    $realRoot = (Get-LambdaPSRealPath -Path $root).TrimEnd($separator)
    $selected = [System.Collections.Generic.List[string]]::new()
    $folders = [System.Collections.Generic.Stack[object]]::new()
    $folders.Push([PSCustomObject]@{ Real = $realRoot; Reached = ''; Chain = @($realRoot) })
    while ($folders.Count -gt 0) {
        $folder = $folders.Pop()
        foreach ($item in [System.IO.DirectoryInfo]::new($folder.Real).EnumerateFileSystemInfos()) {
            $reached = if ($folder.Reached) { "$($folder.Reached)/$($item.Name)" } else { $item.Name }
            $real = $item.FullName
            if ($null -ne $item.LinkTarget) {
                $real = try { Get-LambdaPSRealPath -Path $item.FullName } catch { $null }
                if (-not $real -or -not ($real.Equals($realRoot, $comparison) -or $real.StartsWith($realRoot + $separator, $comparison))) {
                    Write-Warning "Skipped '$reached': it links to '$($item.LinkTarget)', outside '$root'."
                    continue
                }
                if (-not $real.Equals($realRoot, $comparison) -and (& $isExcluded $real.Substring($realRoot.Length + 1).Replace('\', '/'))) { continue }
                if (@($folder.Chain | Where-Object { $real.Equals($_, $comparison) }).Count -gt 0) {
                    Write-Warning "Skipped '$reached': it links to a folder it is inside, '$($item.LinkTarget)'."
                    continue
                }
                if (-not [System.IO.File]::Exists($real) -and -not [System.IO.Directory]::Exists($real)) {
                    Write-Warning "Skipped '$reached': it links to '$($item.LinkTarget)', which does not exist."
                    continue
                }
            }
            # Folders are walked even when excluded, since a later '!' pattern can include a file in them again.
            if ([System.IO.Directory]::Exists($real)) { $folders.Push([PSCustomObject]@{ Real = $real; Reached = $reached; Chain = @($folder.Chain) + $real }) }
            elseif (-not (& $isExcluded $reached)) { $selected.Add($reached) }
        }
    }
    @($selected) | Sort-Object -Culture '' -CaseSensitive
}
