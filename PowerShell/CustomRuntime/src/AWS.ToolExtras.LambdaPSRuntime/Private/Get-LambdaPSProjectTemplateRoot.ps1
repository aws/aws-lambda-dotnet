function Get-LambdaPSProjectTemplateRoot {
    <#
    .SYNOPSIS
        Resolves the on-disk template directories for a deployment target shipped with the module.
    .DESCRIPTION
        Locates the template trees for the requested deployment target relative to the module root.
        Private helpers live under Private/, which is one level below the module root, and Templates/
        is a sibling of Private/ and Public/, so the module root is the parent of this file's
        directory.

        Every target starts from Templates/Shared, which holds what all of them ship: runtime.ps1
        (the runtime and its event loop), the runtime interface client modules under modules/, and
        PowerShellLambdaContext.cs. The target's own tree follows: Templates/RuntimeImage for CMR
        (the runtime image that lands at /var/runtime), Templates/LayerRuntime for Layer (the layer
        that mounts at /opt), Templates/SelfContainedZip for SelfContainedZip (the single .zip that
        mounts at /var/task). Container lays the runtime out as CMR does, so it takes
        Templates/RuntimeImage (the bootstrap and Dockerfile) and then Templates/ContainerFunction:
        its handler, and Dockerfile.append, the function steps added to the end of the Dockerfile.
        RuntimeBound, the function .zip project that binds to a separate runtime, ships no runtime:
        it is Templates/Shared, from which Copy-LambdaPSProjectTemplate takes only the README's
        runtime settings (README.md.append), then Templates/FunctionZip: the handler, manifest.json
        and README.

        Returns the absolute paths of the directories in merge order, for
        Copy-LambdaPSProjectTemplate -Path. A directory that does not exist (an incomplete
        install) is a TemplateNotFound error.
    .PARAMETER Target
        The deployment target whose template tree to resolve: 'CMR' (runtime image, the default),
        'Layer' (Lambda layer), 'SelfContainedZip' (self-contained function .zip), 'Container'
        (container-per-function image), or 'RuntimeBound' (function .zip bound to a runtime).
    .OUTPUTS
        System.String[]
    .EXAMPLE
        Get-LambdaPSProjectTemplateRoot

        Returns the absolute paths to the module's Templates/Shared and Templates/RuntimeImage directories (CMR default).
    .EXAMPLE
        Get-LambdaPSProjectTemplateRoot -Target Layer

        Returns the absolute paths to the module's Templates/Shared and Templates/LayerRuntime directories.
    .EXAMPLE
        Get-LambdaPSProjectTemplateRoot -Target SelfContainedZip

        Returns the absolute paths to the module's Templates/Shared and Templates/SelfContainedZip directories.
    .EXAMPLE
        Get-LambdaPSProjectTemplateRoot -Target Container

        Returns the absolute paths to the module's Templates/Shared, Templates/RuntimeImage and
        Templates/ContainerFunction directories.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    # PSUseOutputTypeCorrectly: the return is comma-wrapped so the caller gets the string[] intact.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseOutputTypeCorrectly', '',
        Justification = 'Comma-wrapped return emits a single string[]; the declared OutputType is correct at runtime.')]
    param(
        [Parameter()]
        [ValidateSet('CMR', 'Layer', 'SelfContainedZip', 'Container', 'RuntimeBound')]
        [string]$Target = 'CMR'
    )

    # Container lays the runtime out exactly as CMR does, so it takes the CMR tree (the bootstrap and
    # Dockerfile), then adds its handler and appends its function steps (Dockerfile.append).
    $subfolders = switch ($Target) {
        'Layer' { 'Shared', 'LayerRuntime' }
        'SelfContainedZip' { 'Shared', 'SelfContainedZip' }
        'RuntimeBound' { 'Shared', 'FunctionZip' }
        'Container' { 'Shared', 'RuntimeImage', 'ContainerFunction' }
        default { 'Shared', 'RuntimeImage' }
    }
    $moduleRoot = Split-Path -Parent $PSScriptRoot
    $templateRoots = [string[]]@(
        $subfolders | ForEach-Object { [System.IO.Path]::Combine($moduleRoot, 'Templates', $_) }
    )

    foreach ($root in $templateRoots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error with
            # its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
            # caller's -ErrorVariable naming this internal helper.
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.IO.DirectoryNotFoundException]::new(
                    "The template tree for target '$Target' was not found at '$root'. Reinstall the module."),
                'TemplateNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $Target)
        }
    }

    return , $templateRoots
}
