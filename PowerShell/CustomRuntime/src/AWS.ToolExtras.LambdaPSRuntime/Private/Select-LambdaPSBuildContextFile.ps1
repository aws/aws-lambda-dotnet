function Select-LambdaPSBuildContextFile {
    <#
    .SYNOPSIS
        Lists the files of a build context that its .dockerignore does not exclude.
    .DESCRIPTION
        The no-Docker build's stand-in for the build context docker sends: every file under -Path, hidden
        ones included, less those a .dockerignore pattern excludes. It follows docker's rules for the
        patterns the scaffolded .dockerignore and common edits use:

          - one pattern per line; blank lines and lines starting with '#' are ignored, and a leading '/'
            or './' is dropped, since every pattern is relative to the context root;
          - '*' matches any run of characters but '/', '?' any one character but '/', and '**' any number
            of folders, so '**/.git' matches .git at any depth;
          - a pattern that matches a folder excludes everything under it;
          - a pattern starting with '!' includes again what an earlier pattern excluded, and the last
            pattern that matches a path decides.

        Character classes ('[a-z]') and escapes are not supported; a pattern that uses one is refused with
        DockerIgnorePatternUnsupported rather than matched differently from docker.
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
            $pattern = $pattern.Replace('\', '/') -replace '^(\./|/)+', ''
            if ($pattern -match '[\[\]]') {
                throw [System.Management.Automation.ErrorRecord]::new(
                    [System.NotSupportedException]::new(
                        ("The .dockerignore pattern '$line' in '$root' uses a character class, which the no-Docker build does " +
                         'not match. Rewrite it with * and ?, or build with -BuildMethod Docker.')),
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

    $files = [System.IO.Directory]::EnumerateFiles($root, '*', [System.IO.SearchOption]::AllDirectories)
    $selected = foreach ($file in $files) {
        $relative = $file.Substring($root.Length).TrimStart('\', '/').Replace('\', '/')
        # A pattern excludes a path when it matches the path or any folder above it.
        $prefixes = [System.Collections.Generic.List[string]]::new()
        $segments = $relative.Split('/')
        for ($n = 1; $n -le $segments.Count; $n++) { $prefixes.Add(($segments[0..($n - 1)] -join '/')) }
        $excluded = $false
        foreach ($rule in $rules) {
            foreach ($prefix in $prefixes) {
                if ($rule.Regex.IsMatch($prefix)) { $excluded = -not $rule.Include; break }
            }
        }
        if (-not $excluded) { $relative }
    }
    @($selected) | Sort-Object -Culture '' -CaseSensitive
}
