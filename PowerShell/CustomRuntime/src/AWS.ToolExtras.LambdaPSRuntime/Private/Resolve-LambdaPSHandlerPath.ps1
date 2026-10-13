function Resolve-LambdaPSHandlerPath {
    <#
    .SYNOPSIS
        Returns the real path of a function source's handler script, or $null when the source does not
        contain it.
    .DESCRIPTION
        The shared check that a handler script is in the function source, used by the packagers
        (Compress-LambdaPSFunctionPackage and Build-LambdaPSRuntimePackage, which read the script's
        '#Requires -Modules') and the deployer (New-LambdaPSFunctionDeployment, which refuses a handler the package would not
        contain). Each caller reports a missing script in its own words.

        Off Windows, a '\' in -HandlerScript is a terminating InvalidHandlerPath error, since Lambda's
        file system reads it as part of a file name: use '/'. On Windows '\' is a separator.

        For a single-file -Path with no -HandlerScript, the file itself is the handler, so its name is
        not matched again. A -HandlerScript given with a single file must name that file.

        For a folder, -HandlerScript is resolved against the folder. The script's file name is
        matched case-sensitively in its folder, as on Lambda's file system. Links are followed in
        every segment of the path (Get-LambdaPSRealPath), and a script whose real path is outside the
        folder's real path returns $null: the package leaves out links that leave the folder, so it
        would not contain the script.
    .PARAMETER Path
        The function source: a single .ps1 file, or a folder.
    .PARAMETER HandlerScript
        The handler script, relative to the folder (for example 'handler.ps1' or 'lib/handler.ps1').
        Required for a folder; for a single file, optional.
    .OUTPUTS
        System.String
    .EXAMPLE
        Resolve-LambdaPSHandlerPath -Path ./fn -HandlerScript handler.ps1

        Returns the real path of ./fn/handler.ps1, or $null when ./fn has no handler.ps1 or it is a
        link that leaves ./fn.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter()]
        [string]$HandlerScript
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }
    # Off Windows '\' is an ordinary file-name character, as on Lambda, so a path written with it
    # would name a different file than the one meant: refuse it rather than guess.
    if (-not $IsWindows -and -not [string]::IsNullOrEmpty($HandlerScript) -and $HandlerScript.Contains('\')) {
        # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error with
        # its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
        # caller's -ErrorVariable naming this internal helper.
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.ArgumentException]::new("The handler path '$HandlerScript' contains '\'. Lambda runs on Linux, where '\' is part of a file name, not a separator: use '/' instead (for example 'lib/handler.ps1')."),
            'InvalidHandlerPath', [System.Management.Automation.ErrorCategory]::InvalidArgument, $HandlerScript)
    }
    $source = Get-Item -LiteralPath $Path -Force
    $name = $HandlerScript

    if (-not $source.PSIsContainer) {
        if ([string]::IsNullOrWhiteSpace($name)) {
            return Get-LambdaPSRealPath -Path $source.FullName
        }
        $candidate = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($source.DirectoryName, $name))
        if ($candidate -cne $source.FullName) { return $null }
        return Get-LambdaPSRealPath -Path $source.FullName
    }

    if ([string]::IsNullOrWhiteSpace($name)) {
        return $null
    }
    $candidate = [System.IO.Path]::Combine($source.FullName, $name)
    $leaf = [System.IO.Path]::GetFileName($candidate)
    $realFolder = Get-LambdaPSRealPath -Path ([System.IO.Path]::GetDirectoryName($candidate))
    if ([string]::IsNullOrEmpty($leaf) -or -not (Test-Path -LiteralPath $realFolder -PathType Container) -or
        @(Get-ChildItem -LiteralPath $realFolder -File -Force | Where-Object { $_.Name -ceq $leaf }).Count -eq 0) {
        return $null
    }

    # Both real paths, so a source reached through a link (macOS /tmp is /private/tmp) still matches.
    $realSource = (Get-LambdaPSRealPath -Path $source.FullName).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $realScript = Get-LambdaPSRealPath -Path ([System.IO.Path]::Combine($realFolder, $leaf))
    if (-not $realScript.StartsWith($realSource, [System.StringComparison]::Ordinal)) {
        return $null
    }
    return $realScript
}
