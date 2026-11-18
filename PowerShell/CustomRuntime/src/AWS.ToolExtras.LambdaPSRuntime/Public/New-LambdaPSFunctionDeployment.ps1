function New-LambdaPSFunctionDeployment {
    <#
    .SYNOPSIS
        Packages your PowerShell handler code and creates, or updates, a Lambda function bound to a
        customer-managed runtime alias, in one command.
    .DESCRIPTION
        The function-side counterpart to New-LambdaPSRuntimeDeployment. It packages the handler code
        you point it at, resolves the runtime ARN from -RuntimeName, and creates (or updates)
        the function bound to the runtime alias.

        -ProjectPath (alias -ScriptPath) is your code, not a scaffold target: a lone .ps1 is zipped
        for you, and a prepared folder is packaged as-is (via Compress-LambdaPSFunctionPackage).
        Modules requested with -IncludeModule, plus any the handler script declares with
        '#Requires -Modules', are staged under modules/ in the package. Nothing is scaffolded, and the
        package is the only file written: by default under the current location, so it lands in the
        source when that is the current location. A .zip at -PackagePath inside a folder source is left
        out of the next package, with a warning.

        You supply -RuntimeName and -RuntimeAlias; the runtime ARN is looked up for you, unless
        -RuntimeName is the ARN. The alias selects the runtime version at cold start, so the function
        follows runtime updates without redeploying.

        The AWS calls are made with AWS.Tools cmdlets. Read-only: Get-LMCRuntime (not for a runtime ARN),
        Get-LMCRuntimeAlias and Get-LMFunctionConfiguration. State-changing: Publish-LMFunction, or Update-LMFunctionCode and
        Update-LMFunctionConfiguration when the function already exists.

        Before packaging, the runtime and its alias are read to check the function's architecture
        against the runtime's (see -Architecture). A missing runtime (RuntimeArnNotFound) or alias
        (RuntimeAliasNotFound), or an alias scheduled for deletion (RuntimeAliasPendingDeletion), is also
        caught here, rather than producing a function that fails on every invoke. A refused runtime read is RuntimeServiceAccessDenied, which also covers a region
        that does not offer customer-managed runtimes; a refused or otherwise failed alias read is
        RuntimeAliasCheckFailed, which wraps the service's error. A runtime ARN is not read, so a missing
        runtime is RuntimeAliasNotFound, and a runtime in another account whose owner has not granted
        lambda:GetRuntimeAlias is RuntimeAliasCheckFailed.

        When the function already exists, its code is updated, and then its configuration: -Role, and
        -Handler, -Timeout, -MemorySize, -RuntimeAlias and -EnvironmentVariable when given. A redeploy
        starts from the live function: its handler and alias are kept over the ones manifest.json
        records (with a warning when they differ), and the handler is replaced only when its script is
        not in the package, or when it is not a PowerShell handler (one written for another runtime,
        such as 'app.handler'). A setting not given keeps the function's existing value, so a redeploy
        changes only what was asked for. The runtime binding also moves when -RuntimeName names another
        runtime. Otherwise the function keeps its alias, and the architecture check (see -Architecture)
        uses that alias: a stated -Architecture it supports is applied, and one it does not is refused
        with ArchitectureMismatch. A function bound with no alias keeps its architecture, with a warning
        if one was stated. A function of that name that is not bound to a customer-managed runtime is
        refused with FunctionNotRuntimeBound before anything is packaged, and left untouched. If the configuration update fails
        after the code update, FunctionConfigurationNotApplied says the function has its new code and
        its previous configuration; fix the cause the service reported, then run the deploy again. A
        wait that times out between the two updates says the configuration was not applied and to
        rerun once the function settles; one that ends in FunctionNotActive says the code update
        failed, so the function keeps its previous code and configuration: fix the cause Lambda names,
        then deploy again.

        The package is uploaded directly, not through S3, so one over the 50 MB Lambda accepts in a
        direct upload stops the deploy with PackageTooLarge, and one over 250 MB unzipped with
        UnzippedPackageTooLarge (over 225 MB it warns), before the function is created or updated.

        The cmdlet returns once the function can be invoked: it waits while Lambda reports the
        function Pending or its last update InProgress (up to 120 seconds, before the code update,
        before the configuration update and after the last update). An existing function that is
        Inactive is updated without waiting, since the update restores it. FunctionNotActive means
        Lambda could not make it ready, and names Lambda's reason; FunctionStateTimeout means it was
        still settling, so check it with Get-LMFunctionConfiguration.

        Each state-changing stage is guarded by ShouldProcess, so -WhatIf previews the plan (including
        the resolved runtime ARN) without packaging or creating anything, while still running the
        packaging checks. The package size limits are the exception: they need the built package, so
        only a real run checks them. -Confirm asks once per stage: the steps inside a confirmed stage do not
        ask again, and declining the packaging skips the create, which would deploy that package.
    .PARAMETER FunctionName
        The Lambda function name to create or update, or its full or partial ARN. When -Region is
        omitted, the region in a full or partial ARN is used (a -Region that differs from it fails with
        FunctionRegionMismatch), and the default -PackagePath is named after the function name in either
        form of ARN. A name or ARN with a version or alias (hello:live) fails with FunctionNameQualified
        before anything is read or packaged: a deploy creates or updates the function itself.
    .PARAMETER ProjectPath
        Your handler code: a single .ps1 script, or a prepared folder packaged as-is. Aliased as
        -ScriptPath and -Path. A path that does not exist fails with ProjectNotFound. A folder with a
        lambdaps-runtime.json marker (a runtime or self-contained function project) is not handler code
        and fails with NotAFunctionProject.
    .PARAMETER Handler
        The Lambda handler string naming the entry point. Passed through verbatim, so the
        runtime-supported forms ('script.ps1', 'script.ps1::function', 'Module::name::function') are
        all accepted; how a given form is dispatched is the runtime's concern. When omitted, an
        existing function keeps its handler while the source still contains its script (with a
        warning if manifest.json records another). Otherwise it is resolved from -ProjectPath: the file
        name of a single-file source; for a folder, the ScriptFile recorded in a scaffolded project's
        manifest.json, or else the only *.ps1 at the folder root.
        A folder with several root scripts and no manifest fails with HandlerNotResolved. A handler
        naming a .ps1 that is not in the source fails with HandlerScriptNotFound before anything is
        packaged or created, as does one whose real path, with every link followed, is outside a folder
        source (for example '../outside.ps1'). For a folder source the handler is a path relative to
        the folder, written with '/': an absolute path, or one containing '\', fails with
        InvalidHandlerPath, because Lambda joins the handler to its task root as written.
        A handler with whitespace in it fails with InvalidHandler.
    .PARAMETER RuntimeName
        The customer-managed runtime the function binds to: its name, whose ARN is resolved in the
        caller's account, or its ARN (arn:<partition>:lambda:<region>:<account>:runtime/<name>), which
        can name a runtime in another account. A runtime name is 2 to 128 lowercase ASCII letters,
        digits, '_', '.' and '-', starting and ending with a letter or digit, as Lambda requires; any
        other value is refused before anything is read or packaged. A runtime ARN in a region other than
        the function's fails with RuntimeRegionMismatch. The runtime's owner must grant the function's
        account access (see the module README).
    .PARAMETER RuntimeAlias
        The runtime alias the function binds to (the alias selects the version at cold start).
        When omitted, an existing function on the same runtime keeps its alias (with a warning if
        manifest.json records another). Otherwise it defaults to the alias recorded in a scaffolded
        project's manifest.json, or else 'default'. An alias name is up to 128 ASCII letters, digits,
        '_', '.' and '-', not starting or ending with '.' or '-', as Lambda requires, and is
        case-sensitive ('Staging' is not 'staging').
        An alias read from manifest.json is checked the same way, and one that does not match is an
        InvalidProjectManifest error naming manifest.json.
    .PARAMETER Role
        The Lambda execution role ARN for the function (arn:aws:iam::<account>:role/<name>). A role
        name alone is refused before anything is packaged. A role Lambda cannot assume yet, as while IAM
        propagates a new role, is retried for up to a minute, then the deploy fails with RoleNotAssumable.
    .PARAMETER Region
        The AWS region to deploy into. When omitted and -FunctionName is a function ARN, the region in
        the ARN, or else the region in a -RuntimeName ARN. Otherwise, when omitted, the region AWS.Tools would use: the shell's
        default (Set-DefaultAWSRegion, including one a calling script set for itself), the -ProfileName profile's region, the default profile's
        region, AWS_REGION, the AWS_PROFILE profile's region, or the instance or container's.
        AWS_DEFAULT_REGION is not read. With none of those it fails with RegionNotResolved.
    .PARAMETER ProfileName
        The name of the AWS credential profile to use, as for any AWS.Tools cmdlet. When no credential
        parameter is given, credentials set with Set-AWSCredential are used, including ones a calling
        script set for itself; otherwise AWS.Tools resolves them the usual way: environment variables,
        the default profile, or the instance or container role. A profile that is not found (in -ProfileLocation, or the default credential stores) fails
        with ProfileNotFound before any AWS call.
    .PARAMETER ProfileLocation
        The location of the shared credentials file, as for any AWS.Tools cmdlet. Used with
        -ProfileName.
    .PARAMETER Credential
        An AWSCredentials object to use, as for any AWS.Tools cmdlet (for example from
        Get-AWSCredential, or (Use-STSRole ...).Credentials).
    .PARAMETER IncludeModule
        Function-local modules to bake into the .zip (with transitive dependencies), combined with any
        the handler declares via '#Requires -Modules'. Accepts a list. Each specification must pin an
        exact RequiredVersion (for example @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' });
        minimum-version and version-range specs are rejected. Versions are never resolved implicitly.
        A staged module whose manifest has a RequiredModules entry with no name fails with
        DependencyNameMissing, and a module present only under another spelling of the version needed
        ('1.0' when '1.0.0' is pinned) fails with ModuleVersionSpelling. AWSPowerShell (the Windows
        PowerShell module, which doesn't run in PowerShell 7) and AWSPowerShell.NetCore (over 600 MB,
        more than Lambda's 250 MB package limit) fail with AWSPowerShellModuleNotSupported, whether
        named in -IncludeModule or in a handler's #Requires; name the AWS.Tools.* modules for the
        services you use instead. An -IncludeModule name with a wildcard (* ? [) or a path (rooted, or
        containing / or \) fails with ModuleNameInvalid. Alias: -Module.
    .PARAMETER MissingModuleAction
        What to do with a module (or dependency) that is not available locally, forwarded to
        Compress-LambdaPSFunctionPackage: Save (default; download into a temporary folder that is
        deleted afterwards), Install (also install it for the current user), or Stop (fail with
        ModuleNotFoundLocally). See Save-LambdaPSModule.
    .PARAMETER ModuleRepository
        The PSResourceGet repositories to download non-AWS.Tools modules from, in order (for example
        an internal mirror). When omitted, every registered repository is searched in priority order,
        so a same-named package in a higher-priority feed would be used. AWS.Tools modules always come
        from the AWS.Tools.zip that AWS.Tools.Installer downloads; when the installer itself must be
        fetched, it comes from these repositories (PSGallery when this is omitted) at the version this
        module pins, and is used only when its files match the pinned SHA256 and, on Windows, its
        Authenticode signature is valid. Every name, or pattern with `*` wildcards, must match
        a registered PSResourceGet repository, or the deploy stops with ModuleRepositoryNotFound before
        anything is downloaded.
    .PARAMETER Architecture
        The function architecture: x86_64 or arm64. When omitted, a scaffolded project's manifest.json
        supplies it (and it is checked like an explicit value: one that is not x86_64 or arm64 is an
        InvalidProjectManifest error naming manifest.json); otherwise a redeploy keeps the
        function's architecture while the alias it will be on supports it, and a new function takes it
        from the runtime: the architecture Lambda reports for the alias's current version, which it
        reads from the image manifest. If it reports several, x86_64 is preferred; if it reports none,
        x86_64 is used with a warning that the match could not be verified.

        When given, it must be one the runtime reports, or the deploy stops before anything is
        created (ArchitectureMismatch). Lambda now rejects a mismatch when the function is created;
        the module checks first, so the error names the runtime alias.
    .PARAMETER Timeout
        The function timeout in seconds. Defaults to 30, NOT Lambda's 3-second default, which a
        PowerShell function cannot reliably meet: a warm invoke can take about 2 seconds at 128 MB,
        and a cold start adds seconds of init on top.
    .PARAMETER MemorySize
        The function memory in MB. Defaults to 512, NOT Lambda's 128 MB default, which a PowerShell
        function all but exhausts on startup: pwsh uses about 115-130 MB of it.
        Memory also scales CPU on Lambda, so 128 MB makes cold starts slower as well as riskier.
    .PARAMETER EnvironmentVariable
        The function's environment variables, as a hashtable of names and values. Each value is sent as
        a string. When given, it replaces the function's environment, on create and on redeploy, as
        Lambda's API does: a variable it does not name is removed, and @{} removes them all. When
        omitted, a redeploy leaves the existing environment unchanged. The runtime reads two settings of
        its own from the environment: POWERSHELL_RUNTIME_VERBOSE (TRUE writes the runtime's own progress
        to the function log) and POWERSHELL_RUNTIME_JSON_DEPTH (1-100, the depth to which the handler's
        output is serialized to JSON; 100 by default, and an invalid value uses 100 and writes a warning
        to the function log).
    .PARAMETER PackagePath
        Where to write the packaged .zip. Defaults to ./<FunctionName>.zip under the current location.
        An existing .zip there is replaced. A path that is the single-file source, or an existing file
        that is not a .zip, fails with PackagePathNotZip before anything is packaged, so that file is
        not overwritten.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.FunctionDeployment

        FunctionArn is the function's ARN, and Updated is $true when the function existed and was
        updated, $false when this call created it. On an update the configuration fields report what
        Lambda returns after the update. ModulesStaged lists every module in the deployed package, as
        AWS.ToolExtras.LambdaPSRuntime.StagedModule objects (Name, and Version as a [version]): those
        from -IncludeModule and '#Requires -Modules', their dependencies, and any already in the
        source's modules/ folder. ProjectPath and PackagePath are full paths.
        FunctionArn and ModulesStaged are empty, and Updated is $false, under -WhatIf and when packaging
        is declined under -Confirm (the create is then skipped). When only the create is declined,
        FunctionArn is empty and Updated is $false, but ModulesStaged lists what was packaged. The
        object is returned in every case.
    .EXAMPLE
        New-LambdaPSFunctionDeployment -FunctionName hello-powershell -ProjectPath ./handler.ps1 -RuntimeName my-ps -Role arn:aws:iam::111122223333:role/hello-role -Region us-east-1

        Packages handler.ps1 (handler defaults to 'handler.ps1') and creates the function bound to the
        'default' alias of runtime 'my-ps'.
    .EXAMPLE
        New-LambdaPSFunctionDeployment -FunctionName hello -ScriptPath ./fn -Handler script.ps1::Invoke -RuntimeName my-ps -RuntimeAlias staging -Role arn:aws:iam::111122223333:role/hello-role -Region us-east-1 -WhatIf

        Previews packaging the ./fn folder with the given handler, without changing anything.
    #>
    [CmdletBinding(SupportsShouldProcess, PositionalBinding = $false)]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.FunctionDeployment')]
    # -Credential is an Amazon.Runtime.AWSCredentials, as on every AWS.Tools cmdlet, not a PSCredential
    # or a password. It is typed [object] so the module binds no SDK type, and AWS.Tools validates it.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUsePSCredentialType', '',
        Justification = 'AWS.Tools -Credential takes an AWSCredentials object, not a PSCredential.')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', '',
        Justification = 'AWS.Tools -Credential takes an AWSCredentials object, not a password string.')]
    param(
        [Parameter(Mandatory, Position = 0)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$FunctionName,

        [Parameter(Mandatory)]
        [Alias('ScriptPath', 'Path')]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ProjectPath,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Handler,

        # Options = 'None': ValidatePattern ignores case by default, and a runtime name is lowercase only.
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [ValidatePattern('^(?:arn:aws[a-z-]*:lambda:[a-z0-9-]+:[0-9]{12}:runtime/)?[a-z0-9](?:[a-z0-9_.-]{0,126})?[a-z0-9]\z', Options = 'None', ErrorMessage = "'{0}' is not a valid runtime name (lowercase letters, digits, '_', '.' and '-', 2 to 128 characters, starting and ending with a letter or digit) or runtime ARN (arn:<partition>:lambda:<region>:<account>:runtime/<name>).")]
        [string]$RuntimeName,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [ValidatePattern('^[A-Za-z0-9_](?:[A-Za-z0-9_.-]{0,126}[A-Za-z0-9_])?\z', Options = 'None', ErrorMessage = "'{0}' is not a valid runtime alias name (letters, digits, '_', '.' and '-', up to 128 characters, not starting or ending with '.' or '-').")]
        [string]$RuntimeAlias = 'default',

        [Parameter(Mandatory)]
        # Options = 'None': ValidatePattern ignores case by default, and an ARN's fixed parts are lowercase.
        [ValidatePattern('^arn:aws[a-z-]*:iam::[0-9]{12}:role/\S+\z', Options = 'None', ErrorMessage = "'{0}' is not an IAM role ARN (arn:aws:iam::<account>:role/<name>).")]
        [string]$Role,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Region,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ProfileName,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ProfileLocation,

        [Parameter()]
        [ValidateNotNull()]
        [object]$Credential,

        [Parameter()]
        [Alias('Module')]
        [Microsoft.PowerShell.Commands.ModuleSpecification[]]$IncludeModule = @(),

        [Parameter()]
        [ValidateSet('Save', 'Install', 'Stop')]
        [string]$MissingModuleAction = 'Save',

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string[]]$ModuleRepository,

        [Parameter()]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture,

        [Parameter()]
        [ValidateRange(1, 900)]
        [int]$Timeout = $script:Config.function.Timeout,

        [Parameter()]
        [ValidateRange(128, 10240)]
        [int]$MemorySize = $script:Config.function.MemorySize,

        [Parameter()]
        [ValidateNotNull()]
        [hashtable]$EnvironmentVariable,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$PackagePath
    )

    # Every AWS call goes through a private seam (Get-LambdaPSRuntimeArn,
    # Get-LambdaPSRuntimeAliasArchitecture, Publish-LambdaPSFunction), so the whole flow is unit
    # testable offline with the seams mocked. No AWS SDK assembly is bound directly.
    try {
        # Credential parameters the caller gave, passed to every seam that calls AWS.
        # The caller's session state, so credentials and a region a script set for itself are used too.
        $credentialParameter = Get-LambdaPSCredentialParameter -BoundParameters $PSBoundParameters -SessionState $PSCmdlet.SessionState
        # A function ARN names its region and the function; use them rather than the ambient region,
        # where the function cannot be found, and rather than an ARN as a file name.
        $functionParts = Split-LambdaPSFunctionArn -FunctionName $FunctionName
        $packageName = $functionParts.Name
        # A deploy changes the unqualified function's code and configuration, never a version's or an
        # alias's, so a qualifier would be dropped or, on Windows, written to an alternate data stream of a
        # file named after the function.
        if ($functionParts.Qualifier) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.ArgumentException]::new(
                        ("-FunctionName '$FunctionName' names the version or alias '$($functionParts.Qualifier)' of function " +
                         "'$($functionParts.Name)'. A deploy creates or updates the function itself, so pass -FunctionName " +
                         'without the qualifier. To move an alias to the new code, publish a version and update the alias ' +
                         'with Publish-LMVersion and Update-LMAlias.')),
                    'FunctionNameQualified', [System.Management.Automation.ErrorCategory]::InvalidArgument, $FunctionName))
        }
        $runtimeRegion = if ($RuntimeName.StartsWith('arn:')) { $RuntimeName.Split(':')[3] }
        if (-not $PSBoundParameters.ContainsKey('Region')) {
            $Region = if ($functionParts.Region) { $functionParts.Region }
            elseif ($runtimeRegion) { $runtimeRegion }
            else { Resolve-LambdaPSRegion -CredentialParameter $credentialParameter -SessionState $PSCmdlet.SessionState }
        }
        elseif ($functionParts.Region -and $Region -ne $functionParts.Region) {
            # A different -Region would only reach Lambda as a not-found for the ARN.
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.ArgumentException]::new(
                        ("-FunctionName '$FunctionName' is a function in region '$($functionParts.Region)', but -Region is " +
                         "'$Region'. Omit -Region, or pass -Region $($functionParts.Region).")),
                    'FunctionRegionMismatch', [System.Management.Automation.ErrorCategory]::InvalidArgument, $FunctionName))
        }
        if ($runtimeRegion -and $Region -ne $runtimeRegion) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.ArgumentException]::new(
                        ("-RuntimeName '$RuntimeName' is a runtime in region '$runtimeRegion', but the function is in " +
                         "region '$Region'. A function binds only to a runtime in its own region, so " +
                         "$(if (-not $functionParts.Region) { "deploy it in '$runtimeRegion' or " })use a runtime in '$Region'.")),
                    'RuntimeRegionMismatch', [System.Management.Automation.ErrorCategory]::InvalidArgument, $RuntimeName))
        }
        if (-not (Test-Path -LiteralPath $ProjectPath)) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.IO.FileNotFoundException]::new(
                        ("Source path '$ProjectPath' was not found. Pass a .ps1 or a folder of handler code, or scaffold " +
                         'one with New-LambdaPSFunctionProject -Path <dir> -Handler handler.')),
                    'ProjectNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $ProjectPath))
        }
        # -Force, so a source whose name starts with '.' (hidden on Linux and macOS) is found too.
        $source = Get-Item -LiteralPath $ProjectPath -Force

        # A folder carrying a lambdaps-runtime.json marker is a RUNTIME (CMR, Layer) or a self-contained
        # function (SelfContainedZip, Container) project, not handler code to bind to a CMR runtime.
        # Packaging it would ship its bootstrap/Dockerfile as function code, and a Container project (one
        # handler.ps1 at its root) would deploy without complaint. Reject it.
        $marker = if ($source.PSIsContainer) { Get-LambdaPSProjectMarker -Path $source.FullName } else { $null }
        if ($null -ne $marker) {
            $projectTarget = $marker.Target
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.ArgumentException]::new(
                        ("'$ProjectPath' is a $(if ($projectTarget) { "$projectTarget " })project (it has a lambdaps-runtime.json marker), not " +
                         'function code for a CMR runtime. Deploy handler code: a .ps1, a folder, or a ' +
                         'New-LambdaPSFunctionProject -Target RuntimeBound project.'), 'ProjectPath'),
                    'NotAFunctionProject', [System.Management.Automation.ErrorCategory]::InvalidArgument, $ProjectPath))
        }

        # A scaffolded project records the runtime alias and architecture it was created for in
        # manifest.json; use them unless the caller passed their own.
        $architectureFromManifest = $false
        $manifestAliasUsed = $false
        $projectManifest = Get-LambdaPSFunctionManifest -Path $source.FullName
        if ($projectManifest) {
            if (-not $PSBoundParameters.ContainsKey('RuntimeAlias') -and $projectManifest.RuntimeAlias) {
                # Checked against -RuntimeAlias's own pattern first, so a bad alias is reported against
                # manifest.json rather than as a failed assignment.
                $manifestAlias = [string]$projectManifest.RuntimeAlias
                $aliasPattern = $MyInvocation.MyCommand.Parameters['RuntimeAlias'].Attributes |
                    Where-Object { $_ -is [System.Management.Automation.ValidatePatternAttribute] }
                if (-not [regex]::IsMatch($manifestAlias, $aliasPattern.RegexPattern, $aliasPattern.Options)) {
                    $manifestPath = [System.IO.Path]::Combine($source.FullName, 'manifest.json')
                    $PSCmdlet.ThrowTerminatingError(
                        [System.Management.Automation.ErrorRecord]::new(
                            [System.IO.InvalidDataException]::new(
                                ("The function manifest '$manifestPath' records RuntimeAlias '$manifestAlias', which is not a " +
                                 "valid runtime alias name (letters, digits, '_', '.' and '-', up to 128 characters, not " +
                                 "starting or ending with '.' or '-'). Fix it in manifest.json, or pass -RuntimeAlias.")),
                            'InvalidProjectManifest', [System.Management.Automation.ErrorCategory]::InvalidData, $manifestPath))
                }
                $RuntimeAlias = $manifestAlias
                $manifestAliasUsed = $true
            }
            if (-not $PSBoundParameters.ContainsKey('Architecture') -and $projectManifest.Architecture) {
                # Checked as -Architecture's ValidateSet would check it, so a bad value stops the deploy
                # rather than be dropped for the alias's architecture.
                if (@('x86_64', 'arm64') -notcontains $projectManifest.Architecture) {
                    $manifestPath = [System.IO.Path]::Combine($source.FullName, 'manifest.json')
                    $PSCmdlet.ThrowTerminatingError(
                        [System.Management.Automation.ErrorRecord]::new(
                            [System.IO.InvalidDataException]::new(
                                ("The function manifest '$manifestPath' records Architecture '$($projectManifest.Architecture)', " +
                                 "which is not x86_64 or arm64. Fix it in manifest.json, or pass -Architecture.")),
                            'InvalidProjectManifest', [System.Management.Automation.ErrorCategory]::InvalidData, $manifestPath))
                }
                $Architecture = $projectManifest.Architecture
                $architectureFromManifest = $true
            }
        }

        # A redeploy starts from the live function: what -Handler, -RuntimeAlias and -Architecture leave
        # out comes from the function as it is, not from manifest.json or the folder, which may be older
        # than it. It is read even when all three are given, to refuse a function this code cannot run.
        $existing = Get-LambdaPSFunctionBinding -FunctionName $FunctionName -Region $Region -CredentialParameter $credentialParameter
        # Only a function bound to a customer-managed runtime can run this code. Refuse any other here,
        # before anything is packaged, and under -WhatIf too.
        if ($null -ne $existing -and [string]::IsNullOrEmpty($existing.RuntimeArn)) {
            $existingKind = if ($existing.PackageType -eq 'Image' -or [string]::IsNullOrEmpty($existing.Runtime)) {
                'is a container-image function'
            }
            else { "already exists on runtime '$($existing.Runtime)'" }
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new(
                        ("Function '$FunctionName' $existingKind and is not bound to a customer-managed runtime, so " +
                         'nothing was packaged or deployed. Use a new -FunctionName, or delete the function first.')),
                    'FunctionNotRuntimeBound', [System.Management.Automation.ErrorCategory]::InvalidOperation, $FunctionName))
        }

        # Resolve the handler string from the source itself rather than assuming a file name: the
        # scaffolder emits '<Handler>.ps1' and records it in manifest.json, and any fixed default would
        # disagree with it for some project. An explicit -Handler is used verbatim.
        # A handler counts as given when the caller passed it or a scaffolded manifest.json records it.
        $handlerGiven = $PSBoundParameters.ContainsKey('Handler') -or
            ($null -ne $projectManifest -and -not [string]::IsNullOrWhiteSpace([string]$projectManifest.ScriptFile))
        if (-not $PSBoundParameters.ContainsKey('Handler') -and $null -ne $existing -and $existing.Handler) {
            # The live handler is kept while the source still has its script, or when it is the
            # 'Module::name::function' form, which names none. Any other handler (one from another
            # runtime, such as 'app.handler') falls back to manifest.json or the folder's root script.
            $liveParts = $existing.Handler.Split('::')
            $liveScript = $liveParts[0]
            $liveScriptShips = if ($liveScript -notlike '*.ps1') {
                $liveParts.Count -eq 3 -and $liveScript -eq 'Module' -and $liveParts -notcontains ''
            }
            elseif ($source.PSIsContainer) {
                -not $liveScript.Contains('\') -and -not [System.IO.Path]::IsPathRooted($liveScript) -and
                    $null -ne (Resolve-LambdaPSHandlerPath -Path $source.FullName -HandlerScript $liveScript)
            }
            else { $source.Name -ceq $liveScript }
            if ($liveScriptShips) {
                $manifestScript = if ($null -ne $projectManifest) { [string]$projectManifest.ScriptFile } else { '' }
                if ($manifestScript -and $manifestScript -cne $liveScript) {
                    Write-Warning ("manifest.json records ScriptFile '$manifestScript', but function '$FunctionName' runs handler " +
                        "'$($existing.Handler)', so the function keeps its handler. Pass -Handler to change it.")
                }
                $Handler = $existing.Handler
                $handlerGiven = $false
            }
        }
        if ([string]::IsNullOrWhiteSpace($Handler)) {
            $resolved = Resolve-LambdaPSHandlerScript -Path $ProjectPath
            if ([string]::IsNullOrWhiteSpace($resolved)) {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.ArgumentException]::new(
                            ("Could not determine the handler script in '$ProjectPath': it has no manifest.json " +
                             'recording a ScriptFile, and it does not contain exactly one .ps1 at its root. ' +
                             'Pass an explicit -Handler (for example -Handler handler.ps1).'), 'Handler'),
                        'HandlerNotResolved', [System.Management.Automation.ErrorCategory]::InvalidArgument, $ProjectPath))
            }
            $Handler = $resolved
        }

        # Lambda refuses whitespace in a handler: on create with a raw error, and on a redeploy only after
        # the code is updated.
        if ($Handler -match '\s') {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.ArgumentException]::new(
                        "Handler '$Handler' contains whitespace, which Lambda does not accept in a handler. Remove it from -Handler, or rename the script or the folder it is in.", 'Handler'),
                    'InvalidHandler', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Handler))
        }

        # The script whose #Requires -Modules to read is the leading token before '::' when it is a .ps1
        # file (the 'Module::name::function' form names no script, so nothing is read there).
        $handlerScript = ($Handler -split '::', 2)[0]
        if ($handlerScript -notlike '*.ps1') { $handlerScript = $null }

        # Fail NOW if the handler names a script the package will not contain. Lambda accepts such a
        # handler, and every invoke then fails at start-up with 'Handler script not found', so this check
        # turns that into an error at deploy time, before anything is packaged or created.
        if ($null -ne $handlerScript) {
            # Lambda joins the handler to its task root as written, so for a folder source an absolute path
            # or a '\' never names a packaged script, even when this host finds the file: each is refused
            # first with a message that says what to write instead (the absolute check before the '\' one,
            # since a Windows path such as 'C:\src\handler.ps1' contains '\' too). The script is then found
            # case-sensitively, as on Lambda's file system, and by its real path, as
            # Compress-LambdaPSFunctionPackage finds it, so one outside the folder ('../x.ps1', or one
            # reached through a link that leaves it) is refused: the package would not contain it.
            $handlerProblem = $null
            if ($source.PSIsContainer -and [System.IO.Path]::IsPathRooted($handlerScript)) {
                $handlerProblem = ("Handler '$Handler' is an absolute path, '$handlerScript', which names a file on this " +
                    "machine rather than in the package. Give the script's path relative to the function source " +
                    "'$ProjectPath' (for example 'handler.ps1' or 'sub/handler.ps1').")
            }
            elseif ($source.PSIsContainer -and $handlerScript.Contains('\')) {
                $handlerProblem = ("Handler '$Handler' uses '\' in the script path '$handlerScript'. Use '/' instead: " +
                    "Lambda does not read '\' as a path separator, so it would not find the script.")
            }
            if ($handlerProblem) {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.ArgumentException]::new($handlerProblem, 'Handler'),
                        'InvalidHandlerPath', [System.Management.Automation.ErrorCategory]::InvalidArgument, $handlerScript))
            }

            $handlerExists = if ($source.PSIsContainer) {
                $null -ne (Resolve-LambdaPSHandlerPath -Path $source.FullName -HandlerScript $handlerScript)
            }
            else {
                $source.Name -ceq $handlerScript
            }

            if (-not $handlerExists) {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.IO.FileNotFoundException]::new(
                            ("Handler '$Handler' names the script '$handlerScript', which is not in the function " +
                             "source '$ProjectPath'. Lambda would accept this and every invoke would fail with " +
                             "'Handler script not found'. Fix -Handler, or add the script.")),
                        'HandlerScriptNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $handlerScript))
            }
        }

        if ([string]::IsNullOrWhiteSpace($PackagePath)) {
            $PackagePath = [System.IO.Path]::Combine((Get-Location).Path, "$packageName.zip")
        }
        # Report full paths, so the result still names the right files after a Set-Location.
        $PackagePath = Resolve-LambdaPSPath -Path $PackagePath
        # Packaging replaces the file at -PackagePath. Only a .zip, an earlier package, is replaced: the
        # single-file source, or any other existing file, is refused here, before anything is packaged.
        $packageExists = Test-Path -LiteralPath $PackagePath -PathType Leaf
        $packageIsSource = $packageExists -and -not $source.PSIsContainer -and
            (Get-LambdaPSRealPath -Path $PackagePath) -eq (Get-LambdaPSRealPath -Path $source.FullName)
        if ($packageIsSource -or ($packageExists -and [System.IO.Path]::GetExtension($PackagePath) -ne '.zip')) {
            $what = if ($packageIsSource) { 'the function source' } else { 'an existing file that is not a .zip' }
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.IO.IOException]::new(
                        ("-PackagePath '$PackagePath' is $what, and packaging would overwrite it. Pass the path " +
                         'of a .zip to write, or omit -PackagePath to write ./<FunctionName>.zip.')),
                    'PackagePathNotZip', [System.Management.Automation.ErrorCategory]::InvalidArgument, $PackagePath))
        }

        # ModulesStaged is taken from what the packager actually staged (Stage 1), not from
        # -IncludeModule, which omits '#Requires'-harvested modules and transitive dependencies. It stays
        # empty under -WhatIf, because nothing was packaged.
        $modulesStaged = @()
        $functionArn = $null
        $updated = $false

        # Runtime ARN resolution (read-only; safe to resolve for the -WhatIf preview). An ARN is used as
        # given, so a runtime in another account needs no lambda:GetRuntime grant from its owner.
        $runtimeArn = if ($runtimeRegion) { $RuntimeName } else { Get-LambdaPSRuntimeArn -RuntimeName $RuntimeName -Region $Region -CredentialParameter $credentialParameter }

        # A redeploy keeps the function's runtime alias unless -RuntimeAlias (or manifest.json) names one
        # and it stays on the same runtime, so check the architecture against the alias it will be on.
        # manifest.json's alias applies to a new function, or one on another runtime; a function that
        # stays on its runtime keeps its live alias, with a warning when the manifest names another.
        $keptBinding = $null
        if (-not $PSBoundParameters.ContainsKey('RuntimeAlias') -and $null -ne $existing -and $existing.RuntimeArn -eq $runtimeArn) {
            $keptBinding = $existing
            if ($manifestAliasUsed) {
                if ($RuntimeAlias -cne $existing.RuntimeAlias) {
                    $boundTo = if ($existing.RuntimeAlias) { "alias '$($existing.RuntimeAlias)', so it keeps that alias" } else { 'the runtime with no alias, so it stays that way' }
                    Write-Warning ("manifest.json records RuntimeAlias '$RuntimeAlias', but function '$FunctionName' is bound to " +
                        "$boundTo. Pass -RuntimeAlias to change it.")
                }
                $manifestAliasUsed = $false
                if (-not $existing.RuntimeAlias) { $RuntimeAlias = 'default' }
            }
        }
        if ($null -ne $keptBinding -and $keptBinding.RuntimeAlias) { $RuntimeAlias = $keptBinding.RuntimeAlias }
        Write-Information "Binding to runtime '$RuntimeName' (arn '$runtimeArn'), alias '$RuntimeAlias'." -Tags 'LambdaPSDeploy'

        # Architecture check (read-only; runs under -WhatIf too). Lambda now rejects a mismatch when the
        # function is created; checking first means the error names the runtime alias. Resolve or verify
        # it here, before anything is packaged or created.
        $architectureStated = $PSBoundParameters.ContainsKey('Architecture') -or $architectureFromManifest
        # A function bound to the runtime with no alias has nothing to check against; it keeps its
        # architecture, and a stated one is not applied (said in a warning).
        $keptWithoutAlias = $null -ne $keptBinding -and -not $keptBinding.RuntimeAlias
        [string[]]$runtimeArchitectures = if ($keptWithoutAlias) { @() } else {
            Get-LambdaPSRuntimeAliasArchitecture -RuntimeName $RuntimeName -RuntimeAlias $RuntimeAlias -Region $Region -CredentialParameter $credentialParameter
        }
        if ($null -eq $runtimeArchitectures) { $runtimeArchitectures = @() }
        $keptHint = if ($null -ne $keptBinding) {
            " The function stays bound to alias '$RuntimeAlias', because -RuntimeAlias was not given; pass -RuntimeAlias to bind it to another alias."
        }
        else { '' }

        if ($keptWithoutAlias) {
            if ($architectureStated) {
                Write-Warning ("Function '$FunctionName' is bound to runtime '$RuntimeName' with no alias, so -Architecture " +
                    "$Architecture cannot be checked and was not applied; it keeps its architecture. Pass -RuntimeAlias to " +
                    'bind it to an alias and apply the architecture.')
            }
            $Architecture = if ($keptBinding.Architecture) { $keptBinding.Architecture } else { 'x86_64' }
        }
        elseif ($runtimeArchitectures.Count -eq 0) {
            # Nothing to check against: an existing function keeps its own architecture, whichever alias
            # it will be on; only a new one takes x86_64.
            if (-not $architectureStated) {
                $Architecture = if ($null -ne $existing -and $existing.Architecture) { $existing.Architecture } else { 'x86_64' }
            }
            Write-Warning ("Lambda reports no architecture for runtime '$RuntimeName' alias '$RuntimeAlias', so " +
                "the function's architecture ($Architecture) cannot be verified against it. If they differ, " +
                'Lambda rejects a new function when it is created. Check the image the alias points at ' +
                "(docker buildx imagetools inspect), and pass the matching -Architecture.")
        }
        elseif ($architectureStated) {
            if ($runtimeArchitectures -notcontains $Architecture) {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.ArgumentException]::new(
                            ("-Architecture $Architecture does not match runtime '$RuntimeName' alias " +
                             "'$RuntimeAlias', which is built for [$($runtimeArchitectures -join ', ')]. " +
                             'Lambda would accept this function and every invoke would fail. Omit ' +
                             '-Architecture to use the runtime''s, or bind to an alias built for ' +
                             "$Architecture.$keptHint")),
                        'ArchitectureMismatch', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Architecture))
            }
        }
        elseif ($null -ne $existing -and $runtimeArchitectures -contains $existing.Architecture) {
            # A redeploy keeps the function's own architecture while the alias it will be on supports it.
            $Architecture = $existing.Architecture
        }
        else {
            $Architecture = if ($runtimeArchitectures -contains 'x86_64') { 'x86_64' } else { $runtimeArchitectures[0] }
            Write-Verbose "Architecture not specified; using '$Architecture' from runtime alias '$RuntimeAlias' ([$($runtimeArchitectures -join ', ')])."
        }

        # Stage 1: Package the supplied handler code, staging -IncludeModule and any #Requires modules.
        # Delegated to the low-level Compress-LambdaPSFunctionPackage so both paths package identically.
        Write-Verbose "Stage 1: Package '$ProjectPath' into '$PackagePath'."
        $packageParams = @{
            Path                = $ProjectPath
            OutFile             = $PackagePath
            IncludeModule       = $IncludeModule
            MissingModuleAction = $MissingModuleAction
            Force               = $true
        }
        # The Module:: handler form names no script, so the packager resolves the script itself.
        if ($handlerScript) { $packageParams.HandlerScript = $handlerScript }
        if ($ModuleRepository) { $packageParams.ModuleRepository = $ModuleRepository }
        # The stage confirms once for everything under it, so say when that includes installing
        # missing modules into the current user's module path, which outlives the deploy.
        $packageAction = "Package function code from '$ProjectPath'"
        if ($MissingModuleAction -eq 'Install') { $packageAction += ', installing missing modules for the current user' }
        # Stage 2 deploys the package Stage 1 writes, so declining Stage 1 under -Confirm skips Stage 2
        # rather than deploying a .zip left at -PackagePath by an earlier run.
        $packageDeclined = $false
        if ($PSCmdlet.ShouldProcess($PackagePath, $packageAction)) {
            $package = Compress-LambdaPSFunctionPackage @packageParams -Confirm:$false
            $modulesStaged = @($package.ModulesStaged)
            Write-Information "Packaged function into '$PackagePath'." -Tags 'LambdaPSDeploy'
        }
        elseif ($WhatIfPreference) {
            # Preview the packaging too: it runs the module checks a real run makes (exact pins,
            # including the handler's #Requires, dependencies, availability) and fails the same way.
            $null = Compress-LambdaPSFunctionPackage @packageParams -WhatIf
        }
        else { $packageDeclined = $true }

        # Stage 2: Create (or update) the function bound to the runtime alias.
        Write-Verbose "Stage 2: Create/update function '$FunctionName'."
        if ($packageDeclined) {
            Write-Warning "Skipped creating or updating function '$FunctionName', because packaging was declined."
        }
        elseif ($PSCmdlet.ShouldProcess($FunctionName, 'Create or update Lambda function')) {
            $publishParams = @{
                FunctionName = $FunctionName
                Handler      = $Handler
                Role         = $Role
                RuntimeArn   = $runtimeArn
                RuntimeAlias = $RuntimeAlias
                Architecture = $Architecture
                Timeout      = $Timeout
                MemorySize   = $MemorySize
                ZipPath      = $PackagePath
                Region       = $Region
            }
            if ($PSBoundParameters.ContainsKey('EnvironmentVariable')) { $publishParams.EnvironmentVariable = $EnvironmentVariable }
            # On a redeploy, only the settings the caller gave (or manifest.json recorded) are applied;
            # the rest keep the existing function's values rather than reset to this cmdlet's defaults.
            $publishParams.UpdateSetting = @(
                # A handler resolved from the source is not "given": the function keeps its own, unless
                # that names a script the package lacks (Publish-LambdaPSFunction checks).
                if ($handlerGiven) { 'Handler' }
                if ($PSBoundParameters.ContainsKey('Timeout')) { 'Timeout' }
                if ($PSBoundParameters.ContainsKey('MemorySize')) { 'MemorySize' }
                if ($PSBoundParameters.ContainsKey('RuntimeAlias') -or $manifestAliasUsed) { 'RuntimeAlias' }
            )
            $published = Publish-LambdaPSFunction @publishParams -CredentialParameter $credentialParameter -Confirm:$false
            Write-Information "Deployed function '$FunctionName'." -Tags 'LambdaPSDeploy'
            $functionArn = $published.Arn
            $updated = [bool]$published.Updated
        }

        # On an update, report the configuration Lambda returned.
        # The live values go into a separate table, not back into the parameter variables: a parameter
        # variable is re-validated on assignment, so a live value its validation refuses (an empty
        # RuntimeAlias, for one) would fail the cmdlet after the code was already deployed.
        $reported = @{
            Handler = $Handler; RuntimeName = $RuntimeName; RuntimeAlias = $RuntimeAlias; RuntimeArn = $runtimeArn
            Architecture = $Architecture; Timeout = $Timeout; MemorySize = $MemorySize; Role = $Role
        }
        if ($updated) {
            foreach ($name in 'Handler', 'RuntimeAlias', 'RuntimeArn', 'Architecture', 'Timeout', 'MemorySize', 'Role') {
                $reported[$name] = $published.$name
            }
        }
        # -RuntimeName may be an ARN; report the name.
        if ($reported.RuntimeArn -match ':runtime/(?<name>[^/]+)') { $reported.RuntimeName = $Matches.name }

        [PSCustomObject]@{
            PSTypeName       = 'AWS.ToolExtras.LambdaPSRuntime.FunctionDeployment'
            FunctionName     = $FunctionName
            FunctionArn      = $functionArn
            Updated          = $updated
            Handler          = $reported.Handler
            RuntimeName      = $reported.RuntimeName
            RuntimeAlias     = $reported.RuntimeAlias
            RuntimeArn       = $reported.RuntimeArn
            Region           = $Region
            Architecture     = $reported.Architecture
            Timeout          = $reported.Timeout
            MemorySize       = $reported.MemorySize
            Role             = $reported.Role
            PackagePath      = $PackagePath
            ProjectPath      = $source.FullName
            ModulesStaged    = $modulesStaged
        }
    }
    catch {
        # Rethrow from this cmdlet: a helper's terminating error otherwise ends only the
        # statement that called it, and this cmdlet would carry on to its next step.
        $PSCmdlet.ThrowTerminatingError($_)
    }
}
