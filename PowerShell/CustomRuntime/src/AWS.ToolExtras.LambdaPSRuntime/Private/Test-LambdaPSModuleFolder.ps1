function Test-LambdaPSModuleFolder {
    <#
    .SYNOPSIS
        Says whether a module version folder is already complete, and stops when it exists but is not.
    .DESCRIPTION
        Every place that writes <Root>/<Name>/<Version> (a download, the copy of an installed module,
        an install for the current user) keeps a version folder that is already there. That is right
        for a complete module, but a folder without <Name>.psd1 (left by an earlier run, a scanner, or
        made by hand) would be kept too: the download would then report the module missing, and a copy
        or install would silently leave it out. So:
          - no folder                 returns $false: write it.
          - a folder with <Name>.psd1 returns $true: keep it.
          - a folder without one      a terminating ModuleFolderIncomplete error that names the folder
                                      and says to remove it.
        Reads only.
    .PARAMETER Path
        The version folder, <Root>/<Name>/<Version>.
    .PARAMETER Name
        The module name, which names the manifest.
    .OUTPUTS
        System.Boolean
    .EXAMPLE
        if (-not (Test-LambdaPSModuleFolder -Path ./modules/AWS.Tools.S3/5.0.274 -Name AWS.Tools.S3)) { ... }

        Copies AWS.Tools.S3 5.0.274 only when it is not already in ./modules.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Name
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }
    if (Test-Path -LiteralPath ([System.IO.Path]::Combine($Path, "$Name.psd1")) -PathType Leaf) {
        return $true
    }
    # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error
    # with its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
    # caller's -ErrorVariable naming this internal helper.
    throw [System.Management.Automation.ErrorRecord]::new(
        [System.IO.IOException]::new(
            "'$Path' exists but holds no '$Name.psd1', so it is not a complete module. Remove the folder and run again."),
        'ModuleFolderIncomplete', [System.Management.Automation.ErrorCategory]::ResourceExists, $Path)
}
