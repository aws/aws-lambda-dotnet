function Copy-LambdaPSModuleFolder {
    <#
    .SYNOPSIS
        Copies one module version's folder (its ModuleBase) to a destination folder.
    .DESCRIPTION
        The copy that Copy-LambdaPSModuleSet, Save-LambdaPSModule and Save-LambdaPSMissingModule
        make for a found module.
        The contents are copied with Copy-LambdaPSFolderContent, so .git folders and symbolic links
        that point outside the module folder are left out (with a warning for a link): a module
        installed from a checkout cannot pull credentials or repository history into a package. Paths
        are read literally, so a ModuleBase whose path contains wildcard characters such as [ or ] is
        copied in full rather than matching nothing.

        A new destination is written as a dot-prefixed sibling folder (.<Version>.partial-<random>)
        and renamed into place only when the copy is complete (retried briefly on an I/O or access
        error, which on Windows is usually a scanner holding the new folder). The sibling is removed in a finally block, which also runs when the copy
        is stopped (Ctrl+C), so a failed or stopped copy never leaves a partial module version that a
        later run would trust. An existing destination is copied into in place and left for its owner
        if the copy fails. The caller has already confirmed the write.
    .PARAMETER Path
        The module version's folder (ModuleBase).
    .PARAMETER Destination
        The folder to copy its contents into, normally <modules>/<Name>/<Version>.
    .PARAMETER ExcludeExtension
        File extensions to leave out, passed to Copy-LambdaPSFolderContent.
    .OUTPUTS
        None.
    .EXAMPLE
        Copy-LambdaPSModuleFolder -Path $found.ModuleBase -Destination ./rt/modules/AWS.Tools.S3/5.0.274

        Copies the installed AWS.Tools.S3 5.0.274 into the project's modules folder.
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
        [string[]]$ExcludeExtension = @()
    )

    if (Test-Path -LiteralPath $Destination) {
        # An existing folder belongs to its owner: copy into it, and leave it as it is on failure.
        Copy-LambdaPSFolderContent -Path $Path -Destination $Destination -ExcludeExtension $ExcludeExtension
        return
    }

    $parent = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($Destination))
    $null = [System.IO.Directory]::CreateDirectory($parent)
    $staging = [System.IO.Path]::Combine($parent, ".$([System.IO.Path]::GetFileName($Destination)).partial-$([System.IO.Path]::GetRandomFileName())")
    $null = [System.IO.Directory]::CreateDirectory($staging)
    $moved = $false
    try {
        Copy-LambdaPSFolderContent -Path $Path -Destination $staging -ExcludeExtension $ExcludeExtension
        # On Windows an antivirus scanner or the search indexer can hold a handle on a fresh folder for
        # a moment, so the rename fails as "in use". Any I/O or access error is retried briefly, on
        # every OS; a destination that appeared in the meantime is a conflict and fails at once, as
        # does any other error.
        for ($attempt = 1; ; $attempt++) {
            if (Test-Path -LiteralPath $Destination) {
                throw [System.IO.IOException]::new("Cannot move '$staging' to '$Destination': the destination already exists.")
            }
            try {
                Move-Item -LiteralPath $staging -Destination $Destination -Force:$false -Confirm:$false -WhatIf:$false -ErrorAction Stop
                break
            }
            catch {
                $cause = if ($_.Exception.InnerException) { $_.Exception.InnerException } else { $_.Exception }
                $inUse = $_.Exception -is [System.IO.IOException] -or $_.Exception -is [System.UnauthorizedAccessException] -or
                    $cause -is [System.IO.IOException] -or $cause -is [System.UnauthorizedAccessException]
                if (-not $inUse -or $attempt -ge 5) { throw }
                Start-Sleep -Milliseconds 200
            }
        }
        $moved = $true
    }
    finally {
        # Runs on failure and when the copy is stopped, unlike catch.
        if (-not $moved -and (Test-Path -LiteralPath $staging)) {
            Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue -Confirm:$false -WhatIf:$false
        }
    }
}
