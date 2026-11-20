function Get-LambdaPSRealPath {
    <#
    .SYNOPSIS
        Returns the real location of a path, with every link along it resolved as the OS resolves it.
    .DESCRIPTION
        Walks the path component by component. A component that is a symbolic link is replaced by its
        own target text before the next component is applied, so 'link/../x' climbs out of where the
        link points, not out of the link's folder, and '..' is applied to the real folder reached so
        far. A relative link target is resolved against the link's folder. Joining the link text and
        collapsing '..' as text (what [System.IO.File]::ResolveLinkTarget does) would not give the
        location the OS opens.

        The path need not exist: components that are not there are kept as they are. A relative
        -Path is made full against the process's current directory first. More than 40 links on one
        path (a loop) is an IOException. Reads link targets only; writes nothing.

        This is the one link resolver: Copy-LambdaPSFolderContent decides every link with it, so a
        caller that checks a path the same way gets the same answer.
    .PARAMETER Path
        The path to resolve, normally a full path.
    .OUTPUTS
        System.String
    .EXAMPLE
        Get-LambdaPSRealPath -Path /work/project/awsdir/../.aws/credentials

        Returns /home/me/.aws/credentials when /work/project/awsdir is a link to /home/me/.aws.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path
    )

    $separator = [System.IO.Path]::DirectorySeparatorChar
    $splitOn = [char[]]@('/', '\')
    $fullPath = if ([System.IO.Path]::IsPathRooted($Path)) { $Path } else { [System.IO.Path]::GetFullPath($Path) }
    $real = [System.IO.Path]::GetPathRoot($fullPath)
    $rest = [System.Collections.Generic.List[string]]::new([string[]]$fullPath.Substring($real.Length).Split($splitOn, [System.StringSplitOptions]::RemoveEmptyEntries))
    $links = 0
    while ($rest.Count -gt 0) {
        $part = $rest[0]
        $rest.RemoveAt(0)
        if ($part -eq '.') { continue }
        if ($part -eq '..') {
            $parent = [System.IO.Path]::GetDirectoryName($real)
            if ($parent) { $real = $parent }
            continue
        }
        $next = [System.IO.Path]::Combine($real, $part)
        $linkText = ([System.IO.FileInfo]::new($next)).LinkTarget
        if ($null -eq $linkText) {
            $real = $next
            continue
        }
        if (++$links -gt 40) { throw [System.IO.IOException]::new("Too many levels of links resolving '$Path'.") }
        # A relative target is resolved against the link's folder, which is $real.
        if ([System.IO.Path]::IsPathRooted($linkText)) { $real = [System.IO.Path]::GetPathRoot($linkText) }
        $rest.InsertRange(0, [string[]]$linkText.Substring([System.IO.Path]::GetPathRoot($linkText).Length).Split($splitOn, [System.StringSplitOptions]::RemoveEmptyEntries))
    }
    if ($real.Length -gt [System.IO.Path]::GetPathRoot($real).Length) { $real = $real.TrimEnd($separator) }
    $real
}
