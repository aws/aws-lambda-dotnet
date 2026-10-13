function Resolve-LambdaPSHandlerScript {
    <#
    .SYNOPSIS
        Resolves which .ps1 in a function source is the handler script.
    .DESCRIPTION
        The single source of truth for "which script is the entry point", shared by the packager
        (Compress-LambdaPSFunctionPackage, which reads the script's '#Requires -Modules') and the
        deployer (New-LambdaPSFunctionDeployment, which turns it into the Lambda handler string), so
        the two cannot disagree. Lambda does not reject a handler naming a script that is not in the
        package: every invoke hangs to the timeout and reports only 'Task timed out', with nothing
        pointing at the real cause.

        Resolution ladder, most specific first:

          1. An explicit -HandlerScript, used verbatim.
          2. The 'ScriptFile' recorded in a top-level manifest.json (what the scaffolder writes there
             for exactly this purpose).
          3. The sole *.ps1 at the source root, when there is exactly one.

        A folder with several root scripts and no manifest is genuinely ambiguous, so this returns
        $null rather than guessing; callers turn that into a request for an explicit -Handler. A file
        source is always its own handler script.
    .PARAMETER Path
        The function source: a single .ps1 file, or a folder containing the handler.
    .PARAMETER HandlerScript
        An explicit handler script file name, which wins over any inference.
    .OUTPUTS
        System.String
    .EXAMPLE
        Resolve-LambdaPSHandlerScript -Path ./fn

        Returns the ScriptFile from ./fn/manifest.json, or the sole ./fn/*.ps1, or $null.
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

    if (-not [string]::IsNullOrWhiteSpace($HandlerScript)) {
        return $HandlerScript
    }

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    # A single-file source is its own handler script.
    $source = Get-Item -LiteralPath $Path -Force
    if (-not $source.PSIsContainer) {
        return $source.Name
    }

    # Tier 2: the scaffolder's manifest.json records the entry point explicitly. A manifest.json
    # that is not the scaffolder's falls through; one that is not valid JSON is an error.
    $manifest = Get-LambdaPSFunctionManifest -Path $source.FullName
    if ($null -ne $manifest -and $manifest.ScriptFile) {
        return $manifest.ScriptFile
    }

    # Tier 3: exactly one root script is unambiguous. Two or more is not, so say nothing.
    $rootScripts = @(Get-ChildItem -LiteralPath $source.FullName -Filter '*.ps1' -File)
    if ($rootScripts.Count -eq 1) {
        return $rootScripts[0].Name
    }

    return $null
}
