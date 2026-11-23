function Get-LambdaPSStagedModule {
    <#
    .SYNOPSIS
        Lists the versioned modules present in a modules directory that is about to ship.
    .DESCRIPTION
        Returns a StagedModule (Name, and Version as a [version]) for every
        <Path>/<Name>/<Version>/<Name>.psd1 found, sorted. It reads as 'Name Version' when shown as
        text. This is the single source for the ModulesStaged reported by the packaging and deploy
        cmdlets.

        It reads the directory rather than trusting an input list because no input list is complete:
        -IncludeModule omits modules harvested from '#Requires -Modules' and the transitive
        RequiredModules that Copy-LambdaPSModuleSet follows, and neither includes modules a customer
        placed in modules/ themselves -- all of which ship. What is on disk in the staged tree is what
        is in the package or image.

        Only the <Name>/<Version>/<Name>.psd1 layout counts. The runtime's own helper files
        (pwsh-runtime.psd1, Private/) and a modules/.gitkeep are not modules in that layout and are
        not reported. A missing directory yields an empty array. Makes no AWS call.
    .PARAMETER Path
        The modules directory to inspect.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.StagedModule
    .EXAMPLE
        Get-LambdaPSStagedModule -Path ./rt/modules

        Returns, for example, AWS.Tools.Common 5.0.274 and AWS.Tools.S3 5.0.274.
    #>
    [CmdletBinding()]
    [OutputType([object[]])]
    # PSUseOutputTypeCorrectly: comma-wrapped return emits ONE array object, as declared; the rule
    # infers Object[] from the static shape.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseOutputTypeCorrectly', '',
        Justification = 'Comma-wrapped return emits a single array; the declared OutputType is correct at runtime.')]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path
    )

    $found = [System.Collections.Generic.List[object]]::new()
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return , ([object[]]@())
    }

    foreach ($nameDir in @(Get-ChildItem -LiteralPath $Path -Directory -Force)) {
        foreach ($versionDir in @(Get-ChildItem -LiteralPath $nameDir.FullName -Directory -Force)) {
            $parsed = $null
            if (-not [version]::TryParse($versionDir.Name, [ref]$parsed)) { continue }

            $manifest = [System.IO.Path]::Combine($versionDir.FullName, "$($nameDir.Name).psd1")
            if (Test-Path -LiteralPath $manifest -PathType Leaf) {
                $module = [PSCustomObject]@{
                    PSTypeName = 'AWS.ToolExtras.LambdaPSRuntime.StagedModule'
                    Name       = $nameDir.Name
                    Version    = $parsed
                }
                # Reads as 'Name Version' when shown or joined into text.
                $module | Add-Member -MemberType ScriptMethod -Name ToString -Value { "$($this.Name) $($this.Version)" } -Force
                $found.Add($module)
            }
        }
    }

    return , ([object[]]@($found | Sort-Object Name, Version))
}
