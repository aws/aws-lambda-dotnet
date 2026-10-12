function Copy-LambdaPSFunctionSource {
    <#
    .SYNOPSIS
        Copies a function source folder into a staging folder, leaving out what must not ship.
    .DESCRIPTION
        Used by Compress-LambdaPSFunctionPackage and Build-LambdaPSRuntimePackage to stage a folder
        before zipping it, so both packagers ship the same files. The copy is
        Copy-LambdaPSFolderContent's, whose help lists what it leaves out (.git, links that leave -Path
        or loop back into it, links that cannot be resolved, the paths in -ExcludePath) and which skips
        warn. The packagers pass their own output file there, so packaging a folder
        into a .zip inside that folder does not nest the previous package in the next one.

        A .zip at the top of -Path is copied with a warning, since it is usually an earlier package.

        A file or folder that cannot be listed or copied, including one the user may not read, is a
        terminating error, so a package is never built with part of its source missing. The caller has
        already confirmed the write; this helper does not ask again.
    .PARAMETER Path
        The source folder.
    .PARAMETER Destination
        The staging folder to copy into. It must exist and be empty: the .zip warning is read from it.
    .PARAMETER ExcludePath
        Full paths to leave out.
    .OUTPUTS
        None.
    .EXAMPLE
        Copy-LambdaPSFunctionSource -Path ./hello -Destination $stage -ExcludePath (Resolve-LambdaPSPath ./hello/hello.zip)

        Stages ./hello into $stage without .git, outside links, or the package being written.
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

    $root = Resolve-LambdaPSPath -Path $Path
    Copy-LambdaPSFolderContent -Path $root -Destination $Destination -ExcludePath $ExcludePath
    # A .zip at the top of the folder is usually an earlier package, which would ship nested inside
    # this one. It is still copied (it may be payload), with a warning. Read from what was staged, so
    # the warning matches what ships, however -Path and the excluded output were reached.
    foreach ($item in Get-ChildItem -LiteralPath $Destination -Force -File -Filter '*.zip') {
        $sourceItem = [System.IO.Path]::Combine($root, $item.Name)
        Write-Warning "'$sourceItem' is a .zip at the top of '$root', so it ships inside the package. If it is an earlier package, move it out of the folder."
    }
}
