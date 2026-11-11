function Select-LambdaPSUnmetModule {
    <#
    .SYNOPSIS
        Returns the module requirements that still have to be pinned and staged.
    .DESCRIPTION
        The packagers read a handler's '#Requires -Modules' and stage each module under modules/. One of
        PowerShell's own modules (Test-LambdaPSBuiltInModule) is part of every pwsh, so it is dropped
        before the pin check and any download. Every other requirement is returned as it is and goes
        through the pin check and the dependency walk, including one for a module the source keeps in
        its own modules/ folder: the walk finds a module on the PSModulePath or downloads it.

        A pinned requirement whose module is also in -ModulesPath is warned about: the staged copy is
        written over the folder's copy of that version, or ships beside the folder's other copy.
    .PARAMETER Module
        The requirements, as Get-LambdaPSRequiredModule returns them.
    .PARAMETER ModulesPath
        The source's own modules/ folder, as the packagers pass it. It only decides the warning.
    .OUTPUTS
        Microsoft.PowerShell.Commands.ModuleSpecification
    .EXAMPLE
        Select-LambdaPSUnmetModule -Module $required -ModulesPath ./fn/modules

        Returns the requirements that are not one of PowerShell's own modules.
    #>
    [CmdletBinding()]
    [OutputType([Microsoft.PowerShell.Commands.ModuleSpecification])]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [Microsoft.PowerShell.Commands.ModuleSpecification[]]$Module,

        [Parameter()]
        [string]$ModulesPath
    )

    # Matched by name, ignoring case as module names do, whatever the file system.
    $shipped = @(if (-not [string]::IsNullOrWhiteSpace($ModulesPath)) {
            Get-ChildItem -LiteralPath $ModulesPath -Directory -ErrorAction SilentlyContinue
        })
    foreach ($spec in $Module) {
        if (Test-LambdaPSBuiltInModule -Name $spec.Name) {
            Write-Verbose "'$($spec.Name)' is one of PowerShell's own modules, so it is not staged."
            continue
        }
        # An unpinned requirement fails the pin check next, so it is not warned about as well.
        foreach ($folder in @(if ($spec.RequiredVersion) { $shipped | Where-Object Name -EQ $spec.Name })) {
            $copy = [System.IO.Path]::Combine($folder.FullName, $spec.RequiredVersion)
            $result = if (Test-Path -LiteralPath $copy) {
                "and in '$copy'. The package stages it from the PSModulePath or a download, written over the folder's copy, so it can ship a mix of the two."
            }
            else {
                "and '$($folder.FullName)' holds another copy. The package ships both."
            }
            Write-Warning ("'$($spec.Name)' $($spec.RequiredVersion) is in the handler's '#Requires -Modules' $result " +
                "Remove it from the folder, or from the '#Requires'.")
        }
        $spec
    }
}
