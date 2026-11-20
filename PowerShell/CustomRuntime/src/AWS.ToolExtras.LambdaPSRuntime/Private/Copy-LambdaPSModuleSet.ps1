function Copy-LambdaPSModuleSet {
    <#
    .SYNOPSIS
        Copies a version-pinned set of modules (with transitive dependencies) that are already on disk
        into a project modules directory.
    .DESCRIPTION
        The single place module staging happens. It walks the requested specifications and their
        transitive RequiredModules with Get-LambdaPSModuleClosure, resolving each from the PSModulePath
        and then from -SearchPath (the folder Save-LambdaPSMissingModule downloaded into), and copies
        each into <Path>/<Name>/<Version>/. It never downloads: acquiring modules is
        Save-LambdaPSMissingModule's job, which the callers run first unless -MissingModuleAction is
        Stop. It binds no AWS SDK assembly and makes no network call.

        AWS.Tools.* modules are staged without their *.xml (cmdlet help) files, about half of a service
        module's size, which a Lambda function never reads; their *.ps1xml format and type data is kept. Other modules are copied in full, since a module may read its own
        .xml files at run time.

        Every specification must pin an exact RequiredVersion (Get-LambdaPSModuleClosure enforces
        the shared Get-LambdaPSModulePinError rule): minimum-version and version-range specs are
        rejected because this tooling never resolves 'latest'. The whole set is resolved before
        anything is copied. A module found nowhere is a terminating ModuleNotFoundLocally error
        naming every missing module; a dependency with no version is a terminating
        DependencyVersionUnpinned error (this seam never picks a version), and one named by a path a
        terminating DependencyNameIsPath error.

        The distinct "Name Version" list (top-level plus dependencies) of the modules copied is
        returned in walk order. Honors ShouldProcess. -Confirm asks once for the whole set (its target
        names the modules folder and every module), and declining it copies nothing and returns an
        empty list. -WhatIf lists each module's copy without touching disk, and returns the modules
        it would copy; modules named in -Pending (downloads that -WhatIf only previewed) are listed
        as copies to come instead of failing as missing. -Pending is read only under -WhatIf: on a
        real run a module that is not on disk (for example one whose download was declined) is
        missing, and fails as ModuleNotFoundLocally.
    .PARAMETER Module
        The module specifications to stage. Each must pin an exact RequiredVersion.
    .PARAMETER Path
        The project modules directory to copy into.
    .PARAMETER SearchPath
        Extra folders, laid out as <Name>/<Version>/, to resolve modules from after the PSModulePath.
    .PARAMETER Pending
        'Name Version' keys of modules a -WhatIf run would have downloaded. Ignored outside -WhatIf.
    .PARAMETER SourceModulesPath
        The source's modules/ folder, passed to New-LambdaPSModuleNotFoundError.
    .OUTPUTS
        System.String[]
    .EXAMPLE
        Copy-LambdaPSModuleSet -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' } -Path ./rt/modules

        Copies the locally installed AWS.Tools.S3 5.0.274 (and its dependencies) into ./rt/modules and
        returns the staged 'Name Version' strings.
    .EXAMPLE
        Copy-LambdaPSModuleSet -Module $specs -Path ./rt/modules -SearchPath $downloadRoot

        Stages modules from the local machine or, when not installed, from a download folder.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([string[]])]
    # PSUseOutputTypeCorrectly: the return is 'return , ([string[]]$staged.ToArray())'. The unary comma
    # wraps the array so the pipeline emits it as ONE object whose runtime type is string[] -- which is
    # exactly what OutputType declares. The rule infers Object[] from the static expression shape.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseOutputTypeCorrectly', '',
        Justification = 'Comma-wrapped return emits a single string[]; the declared OutputType is correct at runtime.')]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [Microsoft.PowerShell.Commands.ModuleSpecification[]]$Module,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter()]
        [AllowEmptyCollection()]
        [string[]]$SearchPath = @(),

        [Parameter()]
        [AllowEmptyCollection()]
        [string[]]$Pending = @(),

        [Parameter()]
        [string]$SourceModulesPath
    )

    $target = Resolve-LambdaPSPath -Path $Path
    $staged = [System.Collections.Generic.List[string]]::new()

    # Resolve the whole set before copying anything, so an unpinned spec, a missing module or an
    # unpinned dependency never leaves a partly staged modules folder behind.
    # Only a -WhatIf preview may stand in for a module that is not on disk.
    $previewed = if ($WhatIfPreference) { $Pending } else { @() }
    $closure = Get-LambdaPSModuleClosure -Module $Module -SearchPath $SearchPath -Pending $previewed -RequireAll -SourceModulesPath $SourceModulesPath
    if ($null -ne $closure.Error) {
        # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error
        # with its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
        # caller's -ErrorVariable naming this internal helper.
        throw $closure.Error
    }

    # One stage, one question: -Confirm asks once for the whole set. -WhatIf still lists each copy.
    $copyAll = $false
    if (-not $WhatIfPreference -and $closure.Resolved.Count -gt 0) {
        $names = @($closure.Resolved | ForEach-Object { "$($_.Name) $($_.Version)" })
        $copyAll = $PSCmdlet.ShouldProcess("'$target': $($names.Count) module(s): $($names -join ', ')", 'Copy modules')
    }
    foreach ($found in $closure.Resolved) {
        $destination = [System.IO.Path]::Combine($target, $found.Name, $found.Version)
        if ($WhatIfPreference) {
            $null = $PSCmdlet.ShouldProcess($destination, "Copy module '$($found.Name)' $($found.Version) from '$($found.ModuleBase)'")
        }
        elseif ($copyAll) {
            # The set was confirmed above; the copy itself does not ask again.
            $strip = @(if ($found.Name -like 'AWS.Tools.*') { '.xml' })
            Copy-LambdaPSModuleFolder -Path $found.ModuleBase -Destination $destination -ExcludeExtension $strip
        }
        else {
            continue
        }
        # The returned key names the folder written: the module's own name and version.
        $staged.Add("$($found.Name) $($found.Version)")
    }

    # Under -WhatIf a module that would be downloaded is not on disk yet: preview its copy instead of
    # failing. Its own dependencies are unknown until its manifest has been downloaded. Pending is
    # empty outside -WhatIf, so -Confirm never asks about a copy it cannot make.
    if ($WhatIfPreference) {
        foreach ($key in $closure.Pending) {
            $name, $version = $key -split ' ', 2
            $destination = [System.IO.Path]::Combine($target, $name, $version)
            $null = $PSCmdlet.ShouldProcess($destination, "Copy module '$name' $version after it is downloaded")
            $staged.Add($key)
        }
    }

    return , ([string[]]$staged.ToArray())
}
