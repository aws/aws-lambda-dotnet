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
    .PARAMETER Module
        The requirements, as Get-LambdaPSRequiredModule returns them.
    .PARAMETER ModulesPath
        The source's own modules/ folder, as the packagers pass it. It does not change the result.
    .OUTPUTS
        Microsoft.PowerShell.Commands.ModuleSpecification
    .EXAMPLE
        Select-LambdaPSUnmetModule -Module $required -ModulesPath ./fn/modules

        Returns the requirements that are not one of PowerShell's own modules.
    #>
    [CmdletBinding()]
    [OutputType([Microsoft.PowerShell.Commands.ModuleSpecification])]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', 'ModulesPath',
        Justification = 'Kept so the packagers call it the same way; no requirement is met from the source folder.')]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [Microsoft.PowerShell.Commands.ModuleSpecification[]]$Module,

        [Parameter()]
        [string]$ModulesPath
    )

    foreach ($spec in $Module) {
        if (Test-LambdaPSBuiltInModule -Name $spec.Name) {
            Write-Verbose "'$($spec.Name)' is one of PowerShell's own modules, so it is not staged."
            continue
        }
        $spec
    }
}
