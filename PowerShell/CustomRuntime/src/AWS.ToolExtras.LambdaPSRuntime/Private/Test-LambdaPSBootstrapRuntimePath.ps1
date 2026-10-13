function Test-LambdaPSBootstrapRuntimePath {
    <#
    .SYNOPSIS
        Checks that the bootstrap targets the runtime path expected for the deployment target.
    .DESCRIPTION
        Pure rule evaluator for the runtime layout. The expected runtime path depends on the
        deployment target:

          - CMR (runtime image): the runtime bootstrap and language runtime live at /var/runtime
            (the AWSLabs layer path /opt does not host the runtime). The bootstrap MUST reference
            /var/runtime. Other /opt references are not flagged: the layer module tier at
            /opt/modules is on the runtime's PSModulePath.

          - Layer: the runtime, modules, and interpreter mount under /opt (for example
            /opt/powershell or /opt/modules); there is no /var/runtime tier. The bootstrap MUST
            reference /opt and MUST NOT reference /var/runtime, which is the wrong path for a layer.

          - SelfContainedZip: the interpreter, runtime module, LambdaContext, and the handler all
            ship in one function .zip whose contents mount at /var/task. The bootstrap MUST
            reference /var/task and MUST NOT reference /var/runtime, which is the CMR runtime-image
            path.

          - Container: a container-per-function image bundles pwsh and the runtime bootstrap under
            /var/runtime (the CMR layout), with the function's files (the handler and everything
            beside it) baked into /var/task. The runtime-path rule is the same as CMR: the bootstrap
            MUST reference /var/runtime.

        A pwsh run by an absolute path must also be under the target's root: /var/runtime/ for CMR
        and Container, /opt/ for Layer, and /var/task/ for SelfContainedZip. The scaffolded image
        removes the pwsh the package installs (/usr/bin/pwsh, /opt/microsoft/powershell), so a
        bootstrap that runs one of those fails to start.

        Returns one finding; performs no disk access.
    .PARAMETER Bootstrap
        The bootstrap as Read-LambdaPSBootstrap returns it.
    .PARAMETER Target
        The deployment target the bootstrap belongs to: 'CMR' (default), 'Layer',
        'SelfContainedZip', or 'Container'.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.Finding
    .EXAMPLE
        Test-LambdaPSBootstrapRuntimePath -Bootstrap (Read-LambdaPSBootstrap -Path ./bootstrap)

        Returns a finding describing whether the bootstrap uses /var/runtime paths (CMR default).
    .EXAMPLE
        Test-LambdaPSBootstrapRuntimePath -Bootstrap (Read-LambdaPSBootstrap -Path ./bootstrap) -Target Layer

        Returns a finding describing whether the layer bootstrap uses /opt and avoids /var/runtime.
    .EXAMPLE
        Test-LambdaPSBootstrapRuntimePath -Bootstrap (Read-LambdaPSBootstrap -Path ./bootstrap) -Target SelfContainedZip

        Returns a finding describing whether the bootstrap uses /var/task and avoids /var/runtime.
    #>
    [CmdletBinding()]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.Finding')]
    param(
        [Parameter(Mandatory)]
        [PSTypeName('AWS.ToolExtras.LambdaPSRuntime.Bootstrap')]
        [PSCustomObject]$Bootstrap,

        [Parameter()]
        [ValidateSet('CMR', 'Layer', 'SelfContainedZip', 'Container')]
        [string]$Target = 'CMR'
    )

    if ($Bootstrap.Unrecognised.Count -gt 0) {
        return New-LambdaPSUnverifiedFinding -RuleId 'R-RuntimePath' -Construct $Bootstrap.Unrecognised -Check "pwsh and runtime.ps1 are run from the $Target target's runtime path."
    }
    # Code only: the shebang and the code lines with comments removed, so a comment that names the
    # other target's path is not a reference. Paths match case-sensitively and whole ('/opt' is not
    # '/optional'), as Linux resolves them.
    $code = (@($Bootstrap.Shebang) + @($Bootstrap.CodeLines | ForEach-Object { ($_ -split '(?<![\w''"])#', 2)[0] }) | Where-Object { $_ }) -join "`n"
    $references = { param([string]$Reserved) $code -cmatch ('(?<![\w.-])' + [regex]::Escape($Reserved) + '(?![\w.-])') }

    # Each target's bootstrap must reference one path and, for the container-free targets, must not
    # reference the CMR runtime-image path. Missing and Forbidden are the failure messages; Passed is
    # the message when both hold.
    $rule = @{
        CMR              = @{
            Root    = '/var/runtime/'
            Require = '/var/runtime'
            Missing = 'Bootstrap does not reference /var/runtime. Under CMR the runtime bootstrap and language runtime must live at /var/runtime, not the layer path /opt.'
            Passed  = 'Bootstrap targets /var/runtime.'
        }
        Layer            = @{
            Root      = '/opt/'
            Require   = '/opt'
            Forbid    = '/var/runtime'
            Missing   = 'Bootstrap does not reference /opt. A layer mounts at /opt, so the bootstrap, runtime module, and interpreter must live under /opt.'
            Forbidden = 'Bootstrap references /var/runtime, which is the CMR runtime-image path. A layer mounts at /opt, so the bootstrap must reference /opt (for example /opt/powershell or /opt/modules) and must not reference /var/runtime.'
            Passed    = 'Bootstrap targets /opt (layer path) and does not reference /var/runtime.'
        }
        SelfContainedZip = @{
            Root      = '/var/task/'
            Require   = '/var/task'
            Forbid    = '/var/runtime'
            Missing   = 'Bootstrap does not reference /var/task. A self-contained .zip mounts at /var/task, so the bootstrap, runtime module, and interpreter must live under /var/task.'
            Forbidden = 'Bootstrap references /var/runtime, which is the CMR runtime-image path. A self-contained .zip mounts at /var/task, so the bootstrap must reference /var/task (for example /var/task/powershell/pwsh or /var/task/modules) and must not reference /var/runtime.'
            Passed    = 'Bootstrap targets /var/task (self-contained .zip path) and does not reference /var/runtime.'
        }
        Container        = @{
            Root    = '/var/runtime/'
            Require = '/var/runtime'
            Missing = 'Bootstrap does not reference /var/runtime. A container-per-function image bundles the runtime bootstrap and language runtime at /var/runtime (the CMR layout), not the layer path /opt.'
            Passed  = 'Bootstrap targets /var/runtime (container-per-function image path).'
        }
    }[$Target]

    # A forbidden reference is reported ahead of a missing one: it names the likelier mistake, a
    # bootstrap written for another target.
    $failure = if ($rule.Forbid -and (& $references $rule.Forbid)) { $rule.Forbidden }
    elseif (-not (& $references $rule.Require)) { $rule.Missing }
    $program = if ($Bootstrap.Pwsh) { $Bootstrap.Pwsh.Program } else { $null }
    if (-not $failure -and $program -and $program.StartsWith('/') -and -not $program.StartsWith($rule.Root, [System.StringComparison]::Ordinal)) {
        $failure = "Bootstrap runs pwsh from '$program', which is outside $($rule.Root). The $Target target's interpreter is under $($rule.Root), so run pwsh from there (for example $($rule.Root)powershell/pwsh)."
    }
    if ($failure) {
        return New-LambdaPSFinding -RuleId 'R-RuntimePath' -Passed $false -Message $failure
    }
    New-LambdaPSFinding -RuleId 'R-RuntimePath' -Passed $true -Message $rule.Passed
}
