function Test-LambdaPSBootstrapEnvironment {
    <#
    .SYNOPSIS
        Checks that the bootstrap resolves pwsh and its libraries without image ENV.
    .DESCRIPTION
        Pure rule evaluator. Whether Lambda applies a runtime image's Dockerfile ENV directives to a
        customer-managed runtime is not documented, so the bootstrap itself must guarantee the
        interpreter and its native libraries resolve. There are two valid strategies, and this rule accepts either:

          1. Export PATH and LD_LIBRARY_PATH in the bootstrap (the AWSLabs /opt layer approach,
             where pwsh is found via PATH and its libs via LD_LIBRARY_PATH), OR
          2. Invoke pwsh by an ABSOLUTE path (for example an exec line pointing at
             /var/runtime/powershell/pwsh). A self-contained relocated interpreter needs no PATH
             lookup, and the Microsoft pwsh build resolves its own libraries via $ORIGIN RPATH, so
             no LD_LIBRARY_PATH export is required.

        Only the code before exec counts, since exec replaces the shell. A variable counts as exported
        when an export line assigns it ('export PATH=...'), when it is assigned and named on an
        export line in either order, or when it is set on the pwsh command ('exec env PATH=...').
        Names match case-sensitively, as the shell reads them. The pwsh path must end in /pwsh.
        Two shapes are reported as unverified rather than judged: pwsh as the shebang's interpreter,
        and a PATH or LD_LIBRARY_PATH assignment that is not exported (it reaches pwsh only if the
        variable was already exported).

        A bootstrap that does neither (bare `exec pwsh` with no exports) relies on inherited ENV
        that Lambda may not apply, and is flagged. Returns one finding per concern; performs no disk access.
    .PARAMETER Bootstrap
        The bootstrap as Read-LambdaPSBootstrap returns it.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.Finding
    .EXAMPLE
        Test-LambdaPSBootstrapEnvironment -Bootstrap (Read-LambdaPSBootstrap -Path ./bootstrap)

        Returns findings describing whether the bootstrap resolves pwsh and its libraries.
    #>
    [CmdletBinding()]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.Finding')]
    param(
        [Parameter(Mandatory)]
        [PSTypeName('AWS.ToolExtras.LambdaPSRuntime.Bootstrap')]
        [PSCustomObject]$Bootstrap
    )

    if ($Bootstrap.Unrecognised.Count -gt 0) {
        New-LambdaPSUnverifiedFinding -RuleId 'R-Env-Path' -Construct $Bootstrap.Unrecognised -Check 'pwsh is found without PATH from the image: PATH is exported before pwsh runs, or pwsh is run by an absolute path.'
        New-LambdaPSUnverifiedFinding -RuleId 'R-Env-LdLibraryPath' -Construct $Bootstrap.Unrecognised -Check "pwsh's native libraries resolve without LD_LIBRARY_PATH from the image: LD_LIBRARY_PATH is exported before pwsh runs, or pwsh is run by an absolute path."
        return
    }
    $pwsh = $Bootstrap.Pwsh
    # pwsh as the shebang's interpreter is not one of the known forms.
    if ($null -eq $pwsh -and $Bootstrap.ShebangRunsPwsh) {
        New-LambdaPSUnverifiedFinding -RuleId 'R-Env-Path' -Construct 'pwsh as the shebang interpreter' -Check 'pwsh is found without PATH from the image: the shebang names it by an absolute path.'
        New-LambdaPSUnverifiedFinding -RuleId 'R-Env-LdLibraryPath' -Construct 'pwsh as the shebang interpreter' -Check "pwsh's native libraries resolve without LD_LIBRARY_PATH from the image."
        return
    }
    if ($null -eq $pwsh) {
        $message = 'Bootstrap does not run pwsh: no statement runs pwsh by name or by a path ending in /pwsh.'
        New-LambdaPSFinding -RuleId 'R-Env-Path' -Passed $false -Message $message
        New-LambdaPSFinding -RuleId 'R-Env-LdLibraryPath' -Passed $false -Message $message
        return
    }
    # An absolute-path pwsh is self-locating, so neither PATH nor LD_LIBRARY_PATH must be set.
    $absolute = $pwsh.Program.StartsWith('/')
    # An empty value finds nothing, so it counts as not set.
    $setsPath = $pwsh.Environment.ContainsKey('PATH') -and $pwsh.Environment['PATH'].Length -gt 0
    $setsLdLibraryPath = $pwsh.Environment.ContainsKey('LD_LIBRARY_PATH') -and $pwsh.Environment['LD_LIBRARY_PATH'].Length -gt 0
    $scaffoldedPwsh = '/var/runtime/powershell/pwsh for CMR and Container, /opt/powershell/pwsh for Layer, /var/task/powershell/pwsh for SelfContainedZip'
    $noInheritedEnvironment = 'A bootstrap cannot rely on inherited environment variables (a customer-managed runtime may not apply the image''s ENV, and a layer or .zip has none)'

    # Assigned but not exported: it reaches pwsh only if the variable was already exported, which
    # this check cannot see.
    $unexportedPath = -not $setsPath -and -not $absolute -and $pwsh.Unexported -ccontains 'PATH'
    $unexportedLdLibraryPath = -not $setsLdLibraryPath -and -not $absolute -and $pwsh.Unexported -ccontains 'LD_LIBRARY_PATH'

    if ($unexportedPath) { New-LambdaPSUnverifiedFinding -RuleId 'R-Env-Path' -Construct 'a PATH assignment that is not exported' -Check 'PATH is exported before pwsh runs.' }
    else {
        $message = if ($setsPath) { 'Bootstrap exports PATH.' }
        elseif ($absolute) { 'Bootstrap invokes pwsh by absolute path, so PATH need not be exported.' }
        else { "Bootstrap neither exports PATH nor invokes pwsh by absolute path. $noInheritedEnvironment, so the interpreter would not be found. Run pwsh by its absolute path, as the scaffolded bootstraps do ($scaffoldedPwsh), or export PATH." }
        New-LambdaPSFinding -RuleId 'R-Env-Path' -Passed ($setsPath -or $absolute) -Message $message
    }

    if ($unexportedLdLibraryPath) { return New-LambdaPSUnverifiedFinding -RuleId 'R-Env-LdLibraryPath' -Construct 'an LD_LIBRARY_PATH assignment that is not exported' -Check 'LD_LIBRARY_PATH is exported before pwsh runs.' }
    $message = if ($setsLdLibraryPath) { 'Bootstrap exports LD_LIBRARY_PATH.' }
    elseif ($absolute) { 'Bootstrap invokes pwsh by absolute path, and pwsh finds its native libraries in its own folder, so LD_LIBRARY_PATH need not be exported.' }
    else { "Bootstrap neither exports LD_LIBRARY_PATH nor invokes pwsh by absolute path. $noInheritedEnvironment, so native libraries would not resolve. Run pwsh by its absolute path, as the scaffolded bootstraps do ($scaffoldedPwsh), or export LD_LIBRARY_PATH." }
    New-LambdaPSFinding -RuleId 'R-Env-LdLibraryPath' -Passed ($setsLdLibraryPath -or $absolute) -Message $message
}
