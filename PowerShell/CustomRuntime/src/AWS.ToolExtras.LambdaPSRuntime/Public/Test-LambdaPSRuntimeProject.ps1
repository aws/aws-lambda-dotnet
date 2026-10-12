function Test-LambdaPSRuntimeProject {
    <#
    .SYNOPSIS
        Lints a PowerShell Lambda runtime project against its deployment-target constraints.
    .DESCRIPTION
        Reads the bootstrap of a scaffolded project and evaluates it against the custom-runtime
        constraints. The runtime-path rule (R-RuntimePath) is target-specific:

          - CMR (runtime image): the bootstrap must reference /var/runtime (the runtime image path).
          - Layer: the bootstrap must reference /opt (the layer mount) and must not reference
            /var/runtime.
          - SelfContainedZip: the bootstrap must reference /var/task (the function-code mount) and
            must not reference /var/runtime.
          - Container: the bootstrap must reference /var/runtime (the bundled runtime path, same as
            CMR).

        The container-free targets add one more rule (R-Globalization): the Layer and SelfContainedZip
        bootstraps run on bare provided.al2023 (no libicu), so they must set
        DOTNET_SYSTEM_GLOBALIZATION_INVARIANT for pwsh (export it, or set it on the pwsh command) or
        pwsh aborts at startup. CMR and Container install libicu into the image, so the rule is not
        applicable there.

        The remaining rules are target-agnostic: the bootstrap must resolve pwsh and its libraries
        without image ENV (R-Env-Path, R-Env-LdLibraryPath: export PATH and LD_LIBRARY_PATH or invoke
        pwsh by absolute path), it must not write to read-only reserved paths (R-ReservedWrite; only
        /tmp is writable at runtime), it must use LF line endings (R-LineEndings), and it must not
        start with anything but '#!' (R-Encoding: no byte order mark, blank line or comment before
        the shebang). The kernel executes the bootstrap directly: a CRLF shebang fails with "bad
        interpreter", and a file that does not start with '#!' is not run as a script.

        For every target the bootstrap only starts runtime.ps1, the event loop beside it (every
        target's bootstrap is an sh wrapper that execs pwsh -NoProfile -NonInteractive -File on it).
        This cmdlet lints the bootstrap, not runtime.ps1, and it checks the scaffold's shape, not
        arbitrary shell. The shell rules (R-RuntimePath, R-Env-Path, R-Env-LdLibraryPath,
        R-Globalization, R-ReservedWrite) understand these forms: comments and blank lines;
        assignments and 'export'; an 'exec' that only redirects ('exec 2>&1'); the plain commands
        echo, printf, cd, cat, tee, mkdir, ls, pwd, true, false and ':', with redirects and pipes; and
        one pwsh statement, 'exec .../pwsh ...', 'NAME=1 exec .../pwsh ...',
        'exec env NAME=1 .../pwsh ...' or 'NAME=1 .../pwsh ...' without exec. Names and paths match
        case-sensitively, as Linux reads them, and nothing after an exec of pwsh counts. A bootstrap
        that also uses anything else (any other command word, such as eval, sh -c, trap, env,
        unset, set, timeout or a wrapper around pwsh; export with an option; a '$' expansion in a
        write target or in PATH, LD_LIBRARY_PATH or DOTNET_SYSTEM_GLOBALIZATION_INVARIANT; a
        relative write target; printf -v; a shebang interpreter other than /bin/sh, /usr/bin/sh, /bin/bash or /usr/bin/bash;
        whitespace other than a space or a tab; pwsh as
        the shebang's interpreter, for the R-Env rules; a PATH or LD_LIBRARY_PATH assignment that is
        not exported; a function, an if/case/loop block, a here-doc, a command
        substitution, a variable as the program, a '\' line continuation, an '&&' or '||' list, a
        background '&', a subshell, a sourced file, an exec of another program, or more than one
        pwsh invocation) makes each shell rule UNVERIFIED: a finding with Unverified $true and
        Severity 'Warning', which names the construct and says what to check by hand. It passes,
        and is written as a warning, unless -UnverifiedAsFailure makes it fail. R-Encoding and
        R-LineEndings read the bytes, and are always checked.

        A pass means the bootstrap has the shape the scaffolder writes. It does not prove that a
        hand-written bootstrap starts: shell options in the shebang, or a write the rules do not
        recognise as reserved, can still stop it. Test a hand-written bootstrap by invoking the
        function.

        The target is resolved from the lambdaps-runtime.json marker written at scaffold time; when
        the marker is absent it defaults to CMR. Pass -Target to override the marker explicitly.

        PSModulePath order is intentionally not checked: where a customer places modules (the function
        package, a layer, or the runtime) and in which precedence is their choice, not a runtime
        constraint.

        Returns one finding object per rule. Each finding's Path is the project's full path, so the
        findings from several piped projects can be told apart. This cmdlet reads only local files and
        makes no AWS API calls.

        FAILING IS VISIBLE, NOT JUST REPORTED. A failing rule is written to the ERROR stream as well
        as returned, so the cmdlet behaves like a gate in the documented scaffold -> validate -> build
        sequence: '$ErrorActionPreference = "Stop"' or -ErrorAction Stop halts a script, and $? is
        false afterwards. Without that, a script could run the validator, see no error, and build an
        image whose bootstrap fails the rules. Pass -ErrorAction SilentlyContinue when you only want
        the report, or -Quiet for a single boolean.
    .PARAMETER Path
        The project directory to lint. Must contain a bootstrap file. Accepts pipeline input: a path
        string, a directory from Get-Item (FullName), or a New-LambdaPSRuntimeDeployment result
        (ProjectPath). An empty path, a missing directory (or a file in its place), a missing
        bootstrap (or a link to nothing), a bootstrap that cannot be read (empty, larger than 1 MB,
        not a regular file, such as a FIFO or a device, a link loop, or a file you may not read), or
        a lambdaps-runtime.json that is not
        valid is a non-terminating ProjectNotFound, BootstrapNotFound, BootstrapNotReadable or
        InvalidProjectMarker error for that item, and the next piped project is still checked. The
        TargetObject of each of these errors, and of each rule failure, is the project's full path;
        for a blank path, or a path on a provider other than the file system, it is the path as given.
    .PARAMETER Quiet
        Return a single boolean instead of the finding objects, and write no errors. It is $true
        only when every rule passed for every project; with several piped projects it is still ONE
        boolean, written after the last project. A blank path, a missing directory or bootstrap, a
        bootstrap that cannot be read, or a marker that is not valid makes it $false, and so does a
        pipeline that delivers no project to check (for example Get-ChildItem of an empty folder).
        Any other failure while checking a project also makes it $false, rather than stopping the
        cmdlet. Clear the result variable, assign the result, then test it, so the gate fails closed:
        '$ok = $null; $ok = Test-LambdaPSRuntimeProject -Path ./rt -Quiet; if ($ok -ne $true) {
        throw ... }'. An error that stops the call (an argument that cannot be bound, for example)
        skips the assignment, so $ok keeps the value it had; and inside an 'if (...)' condition it
        skips the whole if statement, and the script carries on.
    .PARAMETER UnverifiedAsFailure
        Fail a rule the bootstrap's shape kept from being checked, instead of passing it with a
        warning. Use it for a gate that must not pass anything it has not checked.
    .PARAMETER Target
        The deployment target to lint against: 'CMR', 'Layer', 'SelfContainedZip', or 'Container'.
        When omitted, the target is read from the project's lambdaps-runtime.json marker, defaulting
        to CMR when no marker is present. An explicit value wins over the marker.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.Finding

        One per rule, with RuleId, Severity ('Error' for a failed rule, 'Warning' for an unverified
        one, 'Information' for a passed one), Passed, Unverified, Message, and Path (the full path
        of the project the rule checked).
    .OUTPUTS
        System.Boolean

        With -Quiet: a single $true when at least one project was checked and every rule passed for
        every project, otherwise $false.
    .EXAMPLE
        Test-LambdaPSRuntimeProject -Path ./powershell-runtime

        Lints the project and returns a finding per rule.
    .EXAMPLE
        Test-LambdaPSRuntimeProject -Path ./layer -Target Layer

        Lints the project against the Layer target constraints.
    .EXAMPLE
        Test-LambdaPSRuntimeProject -Path ./rt -ErrorAction SilentlyContinue |
            Where-Object { -not $_.Passed }

        Shows only the failing findings, without the per-rule errors.
    .EXAMPLE
        $ok = $null
        $ok = Test-LambdaPSRuntimeProject -Path ./rt -Quiet
        if ($ok -ne $true) { throw 'runtime project is invalid' }

        Gates a build on the project validating cleanly. If the call itself stops, the assignment is
        skipped and $ok stays $null from the line before, so the throw still runs.
    #>
    [CmdletBinding(PositionalBinding = $false)]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.Finding', [bool])]
    # PSUseOutputTypeCorrectly: the findings are collected into @(...) so they can be counted and
    # filtered for the error-stream pass, then emitted as a bare '$findings' statement, which UNROLLS
    # the array -- the pipeline sees one Finding per rule, matching the declared OutputType.
    # The rule reads the static type of the collection variable (Object[]) instead.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseOutputTypeCorrectly', '',
        Justification = 'The array is unrolled on emit; each element is a Finding as declared.')]
    param(
        # Unlike the module's other string parameters, a blank -Path is not refused at binding: a
        # binding error would end an 'if (-not (... -Quiet))' gate without running it, and drop the
        # item from a piped -Quiet answer. It is reported as this project's ProjectNotFound instead.
        [Parameter(Mandatory, ValueFromPipeline, ValueFromPipelineByPropertyName, Position = 0)]
        [Alias('FullName', 'ProjectPath')]
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Path,

        [Parameter()]
        [ValidateSet('CMR', 'Layer', 'SelfContainedZip', 'Container')]
        [string]$Target,

        [Parameter()]
        [switch]$Quiet,

        [Parameter()]
        [switch]$UnverifiedAsFailure
    )

    begin {
        # Each block rethrows from this cmdlet, as the process block explains.
        try {
            # Under -Quiet the answer is ONE boolean for everything piped in, written in 'end': a
            # boolean per project would make 'if (-not (... -Quiet))' pass whenever any project was
            # checked. It is $false when no project reached 'process' (an empty pipeline, or only items
            # that failed parameter binding), so the gate fails closed.
            $allPassed = $true
            $checkedCount = 0
        }
        catch { $PSCmdlet.ThrowTerminatingError($_) }
    }

    process {
        try {
            $checkedCount++
            # A problem with THIS project (no directory, no usable bootstrap, a marker that is not
            # valid) is reported for this item alone, and the next piped project is still checked:
            # non-terminating, like the rule failures below. Under -Quiet it only clears the combined
            # answer, with no error record. Every error's TargetObject is the project's full path; a
            # path on another provider stays as given.
            $blank = [string]::IsNullOrWhiteSpace($Path)
            $projectPath = if ($blank) { $Path } else { try { Resolve-LambdaPSPath -Path $Path } catch { $Path } }
            $problem = $null
            if ($blank) {
                $problem = @{ ErrorId = 'ProjectNotFound'; Category = 'ObjectNotFound'; Exception = [System.IO.DirectoryNotFoundException]::new('No project directory was given (the path is empty).') }
            }
            elseif (Test-Path -LiteralPath $Path -PathType Leaf) {
                $problem = @{ ErrorId = 'ProjectNotFound'; Category = 'ObjectNotFound'; Exception = [System.IO.DirectoryNotFoundException]::new("'$Path' is a file, not a project directory.") }
            }
            elseif (-not (Test-Path -LiteralPath $Path -PathType Container)) {
                $problem = @{ ErrorId = 'ProjectNotFound'; Category = 'ObjectNotFound'; Exception = [System.IO.DirectoryNotFoundException]::new("Project directory '$Path' was not found.") }
            }
            else {
                # One read and parse of the bootstrap, which every rule below checks.
                $bootstrap = Read-LambdaPSBootstrap -Path ([System.IO.Path]::Combine($projectPath, 'bootstrap'))
                if ($bootstrap.ErrorId) {
                    $exception = if ($bootstrap.ErrorId -eq 'BootstrapNotFound') { [System.IO.FileNotFoundException]::new($bootstrap.Message) } else { [System.IO.IOException]::new($bootstrap.Message) }
                    $problem = @{ ErrorId = $bootstrap.ErrorId; Category = $bootstrap.Category; Exception = $exception }
                }
            }

            # Resolve the effective target: an explicit -Target wins; otherwise read the marker written
            # at scaffold time; absent a marker, default to CMR.
            $effectiveTarget = $Target
            if (-not $problem -and [string]::IsNullOrEmpty($effectiveTarget)) {
                try { $marker = Get-LambdaPSProjectMarker -Path $Path }
                catch {
                    if ($_.FullyQualifiedErrorId -notlike 'InvalidProjectMarker*') { throw }
                    $problem = @{ ErrorId = 'InvalidProjectMarker'; Category = 'InvalidData'; Exception = $_.Exception }
                }
                $effectiveTarget = if ($marker -and $marker.Target) { $marker.Target } else { 'CMR' }
            }
            if ($problem) {
                if ($Quiet) { $allPassed = $false; return }
                $PSCmdlet.WriteError(
                    [System.Management.Automation.ErrorRecord]::new($problem.Exception, $problem.ErrorId,
                        [System.Management.Automation.ErrorCategory]$problem.Category, $projectPath))
                return
            }

            # The rules check the parsed bootstrap only; each finding is rebuilt here with the
            # project's full path, so findings from several piped projects can be told apart.
            $ruleFindings = @(
                Test-LambdaPSBootstrapRuntimePath -Bootstrap $bootstrap -Target $effectiveTarget
                Test-LambdaPSBootstrapEnvironment -Bootstrap $bootstrap
                Test-LambdaPSBootstrapReservedPath -Bootstrap $bootstrap
                Test-LambdaPSBootstrapGlobalization -Bootstrap $bootstrap -Target $effectiveTarget
                Test-LambdaPSBootstrapLineEnding -Bootstrap $bootstrap
                Test-LambdaPSBootstrapEncoding -Bootstrap $bootstrap
            )
            # Unverified: a rule the bootstrap's shape kept from being checked. It passes, as a
            # warning, unless -UnverifiedAsFailure makes it an error.
            $findings = @(foreach ($ruleFinding in $ruleFindings) {
                $unverified = [bool]$ruleFinding.PSObject.Properties['Unverified'] -and $ruleFinding.Unverified
                $passed = $ruleFinding.Passed -and -not ($unverified -and $UnverifiedAsFailure)
                $finding = New-LambdaPSFinding -RuleId $ruleFinding.RuleId -Passed $passed -Message $ruleFinding.Message -Path $projectPath
                if ($unverified -and $passed) { $finding.Severity = 'Warning' }
                Add-Member -InputObject $finding -NotePropertyName Unverified -NotePropertyValue $unverified -PassThru
            })

            $failed = @($findings | Where-Object { -not $_.Passed })
            foreach ($warning in @($findings | Where-Object { $_.Passed -and $_.Unverified })) {
                $PSCmdlet.WriteWarning("[$($warning.RuleId)] $($warning.Message)")
            }

            if ($Quiet) {
                if ($failed.Count -gt 0) { $allPassed = $false }
                return
            }

            $findings

            # Surface each failure on the error stream so the cmdlet can gate a pipeline. These are
            # NON-terminating (WriteError, not ThrowTerminatingError): all findings are still returned,
            # a caller who only wants the report can pass -ErrorAction SilentlyContinue, and a caller who
            # wants a gate gets one from -ErrorAction Stop, $ErrorActionPreference, or $?.
            # $PSCmdlet.WriteError rather than Write-Error: inside a function Write-Error leaves the
            # caller's $? True, so 'Test-LambdaPSRuntimeProject ...; if (-not $?)' would never see a failure.
            foreach ($failure in $failed) {
                $PSCmdlet.WriteError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.InvalidOperationException]::new("[$($failure.RuleId)] $($failure.Message)"),
                        $failure.RuleId, [System.Management.Automation.ErrorCategory]::InvalidResult, $projectPath))
            }
        }
        catch {
            # Under -Quiet an unexpected failure fails THIS project, and the next one is still
            # checked: a gate must not pass because the check itself broke.
            if ($Quiet) { $allPassed = $false; return }
            # Rethrow from this cmdlet: a helper's terminating error otherwise ends only the
            # statement that called it, and this cmdlet would carry on to its next step.
            $PSCmdlet.ThrowTerminatingError($_)
        }
    }

    end {
        try {
            if ($Quiet) { $allPassed -and $checkedCount -gt 0 }
        }
        catch { $PSCmdlet.ThrowTerminatingError($_) }
    }
}
