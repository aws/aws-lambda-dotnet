function Invoke-LambdaPSProjectScaffold {
    <#
    .SYNOPSIS
        Writes a scaffolded project: fetches its modules, replaces the target, writes the files, and
        stages the modules.
    .DESCRIPTION
        The write phase shared by New-LambdaPSRuntimeProject and New-LambdaPSFunctionProject, run
        after their up-front checks (Assert-LambdaPSScaffoldPath). In order:

          1. Modules that are not available locally are downloaded to a temporary folder, BEFORE
             anything is written or replaced, so a failed download leaves no half-written project
             behind. -MissingModuleAction Install also installs them for the current user. With Stop
             nothing is downloaded, but the set is still walked here and every module that is not
             installed is named, for the same reason.
          2. The existing contents of -Path (-Existing) are deleted. -Force REPLACES the target rather
             than merging into it: writing over the top would leave any file the new target does not
             also emit, which silently produces a hybrid project. A CMR scaffold followed by
             '-Target Layer -Force', for example, would keep the CMR Dockerfile, and
             Build-LambdaPSRuntimeImage would build an image whose bootstrap execs
             /opt/powershell/pwsh, a path that does not exist in it. The validator cannot catch this,
             because the marker and the bootstrap are both consistently Layer.
          3. The project's template tree is written with Copy-LambdaPSProjectTemplate.
          4. -IncludeModule is staged into <Path>/modules with Copy-LambdaPSModuleSet.

        The temporary download folder is always deleted. Every write is gated by ShouldProcess, here or
        in the helper that performs it: the download in step 1, the install under -MissingModuleAction
        Install (also step 1), and steps 2, 3 and 4 ask once each. Declining the download or step 2
        under -Confirm skips the later steps with a warning (naming the modules, for the download), so
        nothing is written; declining the install still stages the downloaded modules, without
        installing them; declining step 3 skips step 4. Either way no paths are returned. Declining
        step 4 writes the project without its modules. Returns the paths
        written: the project files, then the staged module directories.
    .PARAMETER Path
        The project directory.
    .PARAMETER Existing
        The current contents of -Path, as returned by Assert-LambdaPSScaffoldPath, to delete first.
    .PARAMETER IncludeModule
        The version-pinned modules to stage into the project's modules directory.
    .PARAMETER MissingModuleAction
        What to do with a module that is not available locally: Save (download it), Install (download
        and install it for the current user), or Stop (fail with ModuleNotFoundLocally).
    .PARAMETER ModuleRepository
        Passed to Save-LambdaPSMissingModule: the PSResourceGet repositories to download from.
    .PARAMETER TemplateParameter
        Parameters for Copy-LambdaPSProjectTemplate (Destination is set to -Path).
    .OUTPUTS
        System.String[]
    .EXAMPLE
        Invoke-LambdaPSProjectScaffold -Path ./rt -Existing $existing -TemplateParameter $templateParams

        Replaces ./rt with the template tree described by $templateParams.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([string[]])]
    # PSUseOutputTypeCorrectly: the return is comma-wrapped ('return , ([string[]]$written)'). The unary
    # comma wraps the array so the pipeline emits it as ONE object whose runtime type is string[] --
    # which is exactly what OutputType declares. The rule infers Object[] from the static shape.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseOutputTypeCorrectly', '',
        Justification = 'Comma-wrapped return emits a single string[]; the declared OutputType is correct at runtime.')]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter()]
        [AllowEmptyCollection()]
        [object[]]$Existing = @(),

        [Parameter()]
        [AllowEmptyCollection()]
        [Microsoft.PowerShell.Commands.ModuleSpecification[]]$IncludeModule = @(),

        [Parameter()]
        [ValidateSet('Save', 'Install', 'Stop')]
        [string]$MissingModuleAction = 'Save',

        [Parameter()]
        [AllowNull()]
        [string[]]$ModuleRepository,

        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [hashtable]$TemplateParameter
    )

    $downloadRoot = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "lambdaps-modules-$([System.IO.Path]::GetRandomFileName())")
    try {
        # 1. Modules first, so nothing is written or replaced until every module is available.
        $pendingSave = @()
        if ($IncludeModule.Count -gt 0 -and $MissingModuleAction -ne 'Stop') {
            $resolution = Save-LambdaPSMissingModule -Module $IncludeModule -Path $downloadRoot -MissingModuleAction $MissingModuleAction -ModuleRepository $ModuleRepository
            $pendingSave = @($resolution.Pending)
            # A declined download stops the scaffold, as a declined delete does: a project without the
            # modules it was asked to include is not the project requested.
            $declined = @($resolution.Declined | Where-Object { $_ })
            if ($declined.Count -gt 0) {
                Write-Warning "Skipped writing the project to '$Path', because downloading $($declined -join ', ') was declined."
                return , ([string[]]@())
            }
        }
        elseif ($IncludeModule.Count -gt 0) {
            $closure = Get-LambdaPSModuleClosure -Module $IncludeModule -RequireAll
            if ($null -ne $closure.Error) {
                # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error with
                # its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
                # caller's -ErrorVariable naming this internal helper.
                throw $closure.Error
            }
        }

        # 2. Replace the existing target only now: every check and every download has succeeded, so a
        # failure above leaves the existing project untouched.
        # The delete was confirmed for the whole stage, so Remove-Item must not ask again per item.
        # Declining it under -Confirm skips the writes too: writing over the old contents is the hybrid
        # project -Force exists to prevent. Under -WhatIf the later stages are still previewed.
        if ($Existing.Count -gt 0) {
            if ($PSCmdlet.ShouldProcess($Path, 'Delete existing contents before scaffolding (-Force)')) {
                # Remove the enumerated children rather than a '<Path>/*' wildcard: -LiteralPath does
                # not expand wildcards, so the wildcard form fails with "Cannot find path".
                $Existing | Remove-Item -Recurse -Force -ErrorAction Stop -WhatIf:$false -Confirm:$false
            }
            elseif (-not $WhatIfPreference) {
                Write-Warning "Skipped writing the project to '$Path', because deleting its existing contents was declined."
                return , ([string[]]@())
            }
        }

        # 3. The project files. The helper returns one comma-wrapped string[]; take it with a typed
        # assignment. '@(...)' would nest that array as the single element of an object[], so the
        # final [string[]] cast would join every path into one space-separated string.
        $copyParams = @{} + $TemplateParameter
        $copyParams.Destination = $Path
        [string[]]$written = Copy-LambdaPSProjectTemplate @copyParams
        # The template always has files, so none written outside -WhatIf means the write was declined
        # under -Confirm. Skip the modules too: a modules/ folder alone is not a project.
        if ($written.Count -eq 0 -and -not $WhatIfPreference) {
            if ($IncludeModule.Count -gt 0) {
                Write-Warning "Skipped staging modules into '$Path', because writing the project files was declined."
            }
            return , ([string[]]@())
        }

        # 4. Stage the requested modules (local, or just downloaded) into the project's modules directory.
        if ($IncludeModule.Count -gt 0) {
            $modulesDir = [System.IO.Path]::Combine($Path, 'modules')
            # Copy-LambdaPSModuleSet returns 'Name Version' strings, not paths. The output is the list of
            # paths written, so report the staged module DIRECTORIES rather than mixing the two.
            [string[]]$staged = Copy-LambdaPSModuleSet -Module $IncludeModule -Path $modulesDir -SearchPath $downloadRoot -Pending $pendingSave
            foreach ($key in $staged) {
                $name, $version = $key -split ' ', 2
                $moduleDir = Resolve-LambdaPSPath -Path ([System.IO.Path]::Combine($modulesDir, $name, $version))
                # Report only what this run wrote: under -WhatIf the copy is only previewed, and a
                # folder left by an earlier scaffold (-Force -WhatIf deletes nothing) was not written.
                if (-not $WhatIfPreference -and (Test-Path -LiteralPath $moduleDir -PathType Container)) { $written += $moduleDir }
            }
        }
    }
    finally {
        if (Test-Path -LiteralPath $downloadRoot) {
            Remove-Item -LiteralPath $downloadRoot -Recurse -Force -ErrorAction SilentlyContinue -WhatIf:$false -Confirm:$false
        }
    }

    return , ([string[]]$written)
}
