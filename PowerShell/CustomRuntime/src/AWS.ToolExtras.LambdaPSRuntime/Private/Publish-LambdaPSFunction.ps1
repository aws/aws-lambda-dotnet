function Publish-LambdaPSFunction {
    <#
    .SYNOPSIS
        Creates a PowerShell Lambda function bound to a customer-managed runtime alias, or updates its
        code and configuration if it already exists.
    .DESCRIPTION
        Thin, single-purpose seam over the Lambda function cmdlets (AWS.Tools.Lambda). It calls
        Publish-LMFunction to create the function bound to the customer-managed runtime (via the
        RuntimeConfig parameters), tolerating an already-exists condition so re-runs are idempotent:
        on already-exists it updates the deployed code with Update-LMFunctionCode and the configuration
        with Update-LMFunctionConfiguration instead. It exists so the orchestrator has one mockable
        point for function creation. It binds no AWS SDK assembly directly.

        The already-exists condition (ResourceConflictException) is recognized by
        Test-LambdaPSServiceError, which matches the service exception's identity (type name / SDK
        ErrorCode), not its message text.

        The function is created with -Runtime custom and the RuntimeConfig binding
        (-RuntimeConfig_RuntimeArn / -RuntimeConfig_RuntimeAlias) so it runs on the customer-managed
        runtime and follows the alias at cold start.

        This seam returns only once the function can be invoked. Lambda creates a function in State
        Pending, and a code update runs with LastUpdateStatus InProgress; an invoke in either window is
        refused with ResourceConflictException, and so is a code or configuration update. The seam waits
        for State Active and a settled update before updating an existing function's code, before
        updating its configuration, and after the create or update (2 s poll, 120 s budget each). An
        existing function that is Inactive is updated without waiting:
        it does not become Active on its own, and a code update is what restores it. A function that
        settles to Failed, or that is being deleted or deactivated, is FunctionNotActive; one that has
        not settled when the budget runs out is FunctionStateTimeout. Both name the state and the reason
        Lambda reports.

        When the function already exists, this seam reads its configuration with
        Get-LMFunctionConfiguration, updates its code with Update-LMFunctionCode, and then applies
        the settings named in -UpdateSetting, and -EnvironmentVariable when given, with
        Update-LMFunctionConfiguration. Only settings that differ from the function's are sent, and
        the configuration update is skipped when none does.
        The runtime binding (-RuntimeArn, -RuntimeAlias) is applied when RuntimeAlias is named or the
        function is bound to another runtime; otherwise the function keeps its binding. -Architecture is
        applied with the code only when the function ends up bound to the requested runtime and alias,
        because it was checked against that alias. The result reports the configuration Lambda returns. If the configuration update
        fails after the code update, FunctionConfigurationNotApplied says the function has its new code
        and its previous configuration, and to fix the cause and run the deploy again. A wait that times
        out between the two updates says the configuration was not applied and to rerun once the function
        settles; one that ends in FunctionNotActive says the code update failed, so the function keeps its
        previous code and configuration.

        A function that exists but is not bound to a customer-managed runtime (a function on a managed
        runtime such as python3.12, on provided.al2023 without RuntimeConfig, or a container-image
        function) is refused with FunctionNotRuntimeBound, before its code is touched: PowerShell code
        would not run on it.

        The package is sent in the request (-Code_ZipFile, -ZipFile), so a .zip over the 50 MB Lambda
        accepts in a direct upload is refused with PackageTooLarge before any AWS call. This module has
        no S3 upload path.

    .PARAMETER FunctionName
        The Lambda function name to create or update.
    .PARAMETER Handler
        The Lambda handler string, in any form the runtime supports ('script.ps1',
        'script.ps1::Function' or 'Module::Name::Function'), passed through as given.
    .PARAMETER Role
        The Lambda execution role ARN. Required to create the function; when omitted, the function is
        expected to exist, and keeps its role.
    .PARAMETER RuntimeArn
        The customer-managed runtime ARN to bind to.
    .PARAMETER RuntimeAlias
        The runtime alias to bind to.
    .PARAMETER Architecture
        The target architecture: x86_64 or arm64.
    .PARAMETER Timeout
        The function timeout in seconds. Required, with no default here: Lambda's own 3-second default
        is not survivable for a PowerShell function, so the caller must state a value deliberately.
    .PARAMETER MemorySize
        The function memory in MB. Required for the same reason as -Timeout: Lambda's 128 MB default is
        all but exhausted by the PowerShell runtime at startup.
    .PARAMETER ZipPath
        The function package .zip to deploy.
    .PARAMETER EnvironmentVariable
        The function's environment variables. When given, they replace the function's environment, on
        create and on update, and each value is sent as a string. When omitted, an update leaves the
        environment as it is.
    .PARAMETER UpdateSetting
        Which of Role, Handler, Timeout, MemorySize and RuntimeAlias an update of an existing function
        applies. The rest keep the function's existing values. All five when omitted, as on a create.
    .PARAMETER Region
        The AWS region the function lives in.
    .PARAMETER CredentialParameter
        AWS credential parameters (ProfileName, ProfileLocation, Credential) to splat onto every
        AWS.Tools call. Empty by default, so AWS.Tools uses its default credentials.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.PublishedFunction
    .EXAMPLE
        Publish-LambdaPSFunction -FunctionName hello -Handler handler.ps1 -Role arn:...:role/r -RuntimeArn arn:...:runtime/my-ps -RuntimeAlias default -Architecture x86_64 -Timeout 30 -MemorySize 512 -ZipPath ./hello.zip -Region us-east-1

        Creates the function bound to the runtime alias, or updates its code and configuration if it
        already exists.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.PublishedFunction')]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$FunctionName,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Handler,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Role,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RuntimeArn,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RuntimeAlias,

        [Parameter(Mandatory)]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture,

        [Parameter(Mandatory)]
        [ValidateRange(1, 900)]
        [int]$Timeout,

        [Parameter(Mandatory)]
        [ValidateRange(128, 10240)]
        [int]$MemorySize,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ZipPath,

        [Parameter()]
        [ValidateNotNull()]
        [hashtable]$EnvironmentVariable,

        [Parameter()]
        [ValidateSet('Role', 'Handler', 'Timeout', 'MemorySize', 'RuntimeAlias')]
        [string[]]$UpdateSetting = @('Role', 'Handler', 'Timeout', 'MemorySize', 'RuntimeAlias'),

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Region,

        [Parameter()]
        [ValidateNotNull()]
        [hashtable]$CredentialParameter = @{}
    )


    # `throw`, not $PSCmdlet.ThrowTerminatingError, in this helper: the public cmdlet that called it rethrows
    # the error with its own $PSCmdlet, and one raised through this helper's would also reach the caller's
    # -ErrorVariable naming this internal helper.
    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.IO.FileNotFoundException]::new("Function package '$ZipPath' was not found."),
            'PackageNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $ZipPath)
    }
    # Lambda takes at most 50 MB zipped in a direct upload; a larger package would fail at the API
    # after the function's state was read, so it is refused here first.
    $zipLength = ([System.IO.FileInfo]::new((Resolve-Path -LiteralPath $ZipPath).ProviderPath)).Length
    if ($zipLength -gt 50MB) {
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.IO.IOException]::new(
                ("Function package '$ZipPath' is $([math]::Round($zipLength / 1MB, 1)) MB, over the 50 MB Lambda " +
                 'accepts in a direct upload, which is how this module deploys it. Make the package smaller (fewer ' +
                 'modules or files), or upload it to S3 and create or update the function with Publish-LMFunction ' +
                 '-Code_S3Bucket and -Code_S3Key, or Update-LMFunctionCode -S3Bucket and -S3Key.')),
            'PackageTooLarge', [System.Management.Automation.ErrorCategory]::LimitsExceeded, $ZipPath)
    }

    if (-not $PSCmdlet.ShouldProcess($FunctionName, 'Create or update Lambda function')) {
        return
    }

    $clientConfig = Get-LambdaPSClientConfig -Service Lambda
    $created = $null
    $updated = $false
    # Lambda takes environment values as strings only. Environment_IsVariablesSet sends an empty
    # table too, so @{} clears the environment. The table is enumerated rather than read through
    # .Keys, which a variable named Keys would answer instead.
    $environment = @{}
    if ($PSBoundParameters.ContainsKey('EnvironmentVariable')) {
        $variables = @{}
        foreach ($entry in $EnvironmentVariable.GetEnumerator()) { $variables[[string]$entry.Key] = [string]$entry.Value }
        $environment = @{ Environment_Variable = $variables; Environment_IsVariablesSet = $true }
    }

    # Pass the package as BYTES, not as a path. The path-taking -ZipFilename parameter on both
    # Publish-LMFunction and Update-LMFunctionCode is marked [Obsolete] in AWS.Tools.Lambda and warns on
    # every call; the supported parameters are -Code_ZipFile (create) and -ZipFile (update), both byte[].
    $zipBytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $ZipPath).ProviderPath)

    # Polls Get-LMFunctionConfiguration until State is Active and no update is InProgress, and returns
    # the last configuration read. Failed, Deleting, Deactivating and Deactivated never become ready, so
    # they stop at once; any other state (Pending, Inactive, ...) is waited on. -BeforeUpdate is for the
    # read before a code update: an earlier FAILED update does not stop a new one, and an Inactive
    # function is ready for one, because Lambda restores an Inactive function when its code is updated.
    $formatReason = {
        param([string]$Reason, [string]$Code)
        $text = @($Code, $Reason) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        if ($text) { ': ' + ($text -join ' - ') } else { ', no reason reported' }
    }
    # -TimeoutNote and -NotActiveNote are added to those failures, for the wait whose failure leaves a
    # step undone.
    $waitUntilReady = {
        param([switch]$BeforeUpdate, [string]$TimeoutNote, [string]$NotActiveNote)
        $wait = $script:Config.wait.Function
        for ($i = 0; $i -lt $wait.Attempts; $i++) {
            $status = Get-LMFunctionConfiguration -FunctionName $FunctionName -Region $Region -ClientConfig $clientConfig @CredentialParameter -ErrorAction Stop
            $state = [string]$status.State
            $updateStatus = [string]$status.LastUpdateStatus
            $reason = $null
            if (@('Failed', 'Deleting', 'Deactivating', 'Deactivated') -contains $state) {
                $reason = "State $state" + (& $formatReason $status.StateReason $status.StateReasonCode)
            }
            elseif ($updateStatus -eq 'Failed' -and -not $BeforeUpdate) {
                $reason = 'LastUpdateStatus Failed' + (& $formatReason $status.LastUpdateStatusReason $status.LastUpdateStatusReasonCode)
            }
            if ($reason) {
                throw [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new(
                        "Function '$FunctionName' is not ready and will not become ready on its own ($reason).$NotActiveNote"),
                    'FunctionNotActive', [System.Management.Automation.ErrorCategory]::InvalidResult, $FunctionName)
            }
            $settled = $state -eq 'Active' -or ($BeforeUpdate -and $state -eq 'Inactive')
            if ($settled -and $updateStatus -ne 'InProgress') {
                return $status
            }
            Write-Verbose "Function '$FunctionName' is $state (last update: $updateStatus); waiting (attempt $($i + 1)/$($wait.Attempts))."
            Start-Sleep -Seconds $wait.IntervalSeconds
        }
        $pending = if ($state -ne 'Active') { "still $state" } else { "Active but its last update was still $updateStatus" }
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.TimeoutException]::new(
                ("Function '$FunctionName' was $pending after $($wait.Attempts * $wait.IntervalSeconds) seconds. It may still " +
                 "settle; check with Get-LMFunctionConfiguration -FunctionName $FunctionName -Region $Region" +
                 "$(Get-LambdaPSCredentialArgumentText -CredentialParameter $CredentialParameter) " +
                 "before invoking or deploying it again.$TimeoutNote")),
            'FunctionStateTimeout', [System.Management.Automation.ErrorCategory]::OperationTimeout, $FunctionName)
    }

    # Create the function bound to the customer-managed runtime. Tolerate ONLY an already-exists
    # condition so re-runs are idempotent; any other error (auth, throttling, ...) must surface. On
    # already-exists, update the deployed code and configuration instead. Test-LambdaPSServiceError
    # matches ResourceConflictException by the service exception's identity rather than its message text.
    # A create needs a role, so without -Role the function is updated as the existing one the caller found.
    $exists = -not $Role
    if (-not $exists) {
        $publishParams = @{
            FunctionName             = $FunctionName
            Handler                  = $Handler
            Runtime                  = 'custom'
            Role                     = $Role
            Architecture             = $Architecture
            Timeout                  = $Timeout
            MemorySize               = $MemorySize
            Code_ZipFile             = $zipBytes
            RuntimeConfig_RuntimeArn = $RuntimeArn
            RuntimeConfig_RuntimeAlias = $RuntimeAlias
            Region                   = $Region
            ClientConfig             = $clientConfig
            ErrorAction              = 'Stop'
        }
        try { $created = Publish-LMFunction @publishParams @environment @CredentialParameter }
        catch {
            if (-not (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'ResourceConflictException')) { throw }
            $exists = $true
        }
    }
    if ($exists) {
        Write-Verbose "Function '$FunctionName' already exists; updating its code."

        # Lambda refuses a code update while an earlier update or the create is still settling, so wait
        # for that first. A FAILED earlier update does not block a new one, so it is not fatal here.
        $current = & $waitUntilReady -BeforeUpdate

        # Only a function bound to a customer-managed runtime can run this PowerShell code. Replacing the
        # code of any other function (python3.12, or provided.al2023 with no RuntimeConfig) would break it.
        $currentArn = if ($null -ne $current.RuntimeConfig) { [string]$current.RuntimeConfig.RuntimeArn } else { '' }
        if ([string]::IsNullOrEmpty($currentArn)) {
            # A container-image function has no Runtime to name.
            $existing = if ([string]$current.PackageType -eq 'Image' -or [string]::IsNullOrEmpty([string]$current.Runtime)) {
                'is a container-image function'
            }
            else { "already exists on runtime '$([string]$current.Runtime)'" }
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.InvalidOperationException]::new(
                    ("Function '$FunctionName' $existing and is not bound to a customer-managed runtime, so " +
                     'its code was not replaced. Use a new -FunctionName, or delete the function first.')),
                'FunctionNotRuntimeBound', [System.Management.Automation.ErrorCategory]::InvalidOperation, $FunctionName)
        }

        # The binding moves to the requested one when the caller named the alias, or when the function
        # is on another runtime; otherwise it stays. -Architecture was checked against the requested
        # alias, so it is applied only when the function ends up bound to that alias.
        $currentAlias = if ($null -ne $current.RuntimeConfig) { [string]$current.RuntimeConfig.RuntimeAlias } else { '' }
        $rebind = $UpdateSetting -contains 'RuntimeAlias' -or $currentArn -ne $RuntimeArn
        $applyArchitecture = $rebind -or $currentAlias -ceq $RuntimeAlias
        $boundArn = if ($rebind) { $RuntimeArn } else { $currentArn }
        $boundAlias = if ($rebind) { $RuntimeAlias } else { $currentAlias }
        $updateParams = @{
            FunctionName = $FunctionName
            ZipFile      = $zipBytes
            Region       = $Region
            ClientConfig = $clientConfig
        }
        if ($applyArchitecture) { $updateParams.Architecture = $Architecture }
        $created = Update-LMFunctionCode @updateParams @CredentialParameter -ErrorAction Stop
        $updated = $true

        # The function keeps its handler unless the caller gave one, or the kept handler names a script
        # this package does not contain, or is not a PowerShell handler at all (one written for another
        # runtime, such as 'app.handler'); then the package's own handler is sent, so the function never
        # points at a missing script. The 'Module::name::function' form names no script, so it is kept.
        $sendHandler = $UpdateSetting -contains 'Handler'
        if (-not $sendHandler) {
            $keptParts = ([string]$current.Handler).Split('::')
            $keptScript = $keptParts[0]
            if ($keptScript -notlike '*.ps1') {
                $sendHandler = -not ($keptParts.Count -eq 3 -and $keptScript -eq 'Module' -and $keptParts -notcontains '')
            }
            else {
                # A package that cannot be read as a .zip counts as not containing it.
                Add-Type -AssemblyName System.IO.Compression
                $entries = @()
                try {
                    $archive = [System.IO.Compression.ZipArchive]::new([System.IO.MemoryStream]::new($zipBytes), [System.IO.Compression.ZipArchiveMode]::Read)
                    try { $entries = @($archive.Entries | ForEach-Object { $_.FullName -replace '\\', '/' }) }
                    finally { $archive.Dispose() }
                }
                catch { $entries = @() }
                $sendHandler = $entries -cnotcontains (($keptScript -replace '\\', '/') -replace '^\./', '')
            }
        }

        # From here the function has the new code but not yet the configuration. Say so if either step
        # below fails, naming what the function keeps and whether its invokes can fail meanwhile.
        $currentArchitecture = @($current.Architectures | ForEach-Object { [string]$_ }) | Select-Object -First 1
        $architectureNote = if ($applyArchitecture) { " and architecture ($Architecture)" } else { '' }
        $bindingNote = if ($currentAlias) { "runtime binding ('$currentArn', alias '$currentAlias')" } else { "runtime binding ('$currentArn')" }
        $mismatch = ($applyArchitecture -and $currentArchitecture -and $currentArchitecture -ne $Architecture) -or
            ($rebind -and ($currentArn -ne $RuntimeArn -or $currentAlias -cne $RuntimeAlias)) -or
            ($sendHandler -and $Handler -cne [string]$current.Handler)
        $invokeNote = if ($mismatch) {
            ' Invokes may fail until the configuration is applied, because the new code''s architecture, handler or runtime binding differs from what the function still has.'
        }
        else { '' }
        $notAppliedState = "Function '$FunctionName' has its new code$architectureNote, but keeps its previous configuration and $bindingNote.$invokeNote"

        # Only what the caller asked for, and only where it differs from the function: a setting it did
        # not give keeps the function's value, and a redeploy that changes none skips the update.
        $configurationParams = @{}
        if ($UpdateSetting -contains 'Role' -and $Role -and $Role -cne [string]$current.Role) { $configurationParams.Role = $Role }
        if ($sendHandler -and $Handler -cne [string]$current.Handler) { $configurationParams.Handler = $Handler }
        if ($UpdateSetting -contains 'Timeout' -and $Timeout -ne $current.Timeout) { $configurationParams.Timeout = $Timeout }
        if ($UpdateSetting -contains 'MemorySize' -and $MemorySize -ne $current.MemorySize) { $configurationParams.MemorySize = $MemorySize }
        if ($rebind -and ($currentArn -ne $RuntimeArn -or $currentAlias -cne $RuntimeAlias)) {
            $configurationParams.RuntimeConfig_RuntimeArn = $RuntimeArn
            $configurationParams.RuntimeConfig_RuntimeAlias = $RuntimeAlias
        }
        if ($environment.Count) {
            $liveVariables = if ($null -ne $current.Environment -and $null -ne $current.Environment.Variables) { $current.Environment.Variables } else { @{} }
            $environmentChanged = $liveVariables.get_Count() -ne $variables.get_Count() -or
                @($variables.GetEnumerator() | Where-Object { -not $liveVariables.ContainsKey($_.Key) -or $liveVariables[$_.Key] -cne $_.Value }).Count
            if ($environmentChanged) { $configurationParams += $environment }
        }
        $configure = $configurationParams.Count -gt 0

        # Lambda refuses a configuration update while the code update is InProgress.
        # FunctionNotActive here means the code update itself failed: Lambda keeps the previous code.
        $timeoutNote = if ($configure) { " The configuration was not applied. $notAppliedState Run the deploy again once the function settles to apply it." } else { '' }
        $null = & $waitUntilReady -TimeoutNote $timeoutNote `
            -NotActiveNote ' The code update failed, so the function keeps its previous code and configuration. Fix the cause Lambda names, then deploy again.'
        if (-not $configure) {
            Write-Verbose "Function '$FunctionName' already has the configuration asked for; skipping the configuration update."
        }
        else {
            try {
                $created = Update-LMFunctionConfiguration -FunctionName $FunctionName -Region $Region -ClientConfig $clientConfig @configurationParams @CredentialParameter -ErrorAction Stop
            }
            catch {
                # The code is already updated, so say what state the function is left in.
                throw [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new(
                        ("$notAppliedState The configuration update failed. Fix the cause the service reported, then run " +
                         "the deploy again. The service reported: $($_.Exception.Message)"), $_.Exception),
                    'FunctionConfigurationNotApplied', [System.Management.Automation.ErrorCategory]::WriteError, $FunctionName)
            }
        }
    }

    # Wait until the function can be invoked: a new function is Pending, and a code update is
    # InProgress, until Lambda settles it. Returning earlier hands the caller a function whose first
    # invoke is refused with ResourceConflictException.
    $null = & $waitUntilReady

    # On the update path, report the configuration Lambda returns (the code and configuration updates
    # both return it) rather than echo the requested values back.
    $live = $updated -and $null -ne $created
    $liveArchitecture = if ($live) { @($created.Architectures | ForEach-Object { [string]$_ }) | Select-Object -First 1 } else { $null }
    $liveRuntime = if ($live) { $created.RuntimeConfig } else { $null }
    [PSCustomObject]@{
        PSTypeName   = 'AWS.ToolExtras.LambdaPSRuntime.PublishedFunction'
        FunctionName = $FunctionName
        Handler      = if ($live -and $created.Handler) { $created.Handler } elseif ($updated -and -not $sendHandler) { $current.Handler } else { $Handler }
        RuntimeArn   = if ($null -ne $liveRuntime -and $liveRuntime.RuntimeArn) { [string]$liveRuntime.RuntimeArn } elseif ($updated) { $boundArn } else { $RuntimeArn }
        RuntimeAlias = if ($null -ne $liveRuntime -and $liveRuntime.RuntimeAlias) { [string]$liveRuntime.RuntimeAlias } elseif ($updated) { $boundAlias } else { $RuntimeAlias }
        Architecture = if ($liveArchitecture) { $liveArchitecture } elseif ($updated -and -not $applyArchitecture) { @($current.Architectures | ForEach-Object { [string]$_ }) | Select-Object -First 1 } else { $Architecture }
        Timeout      = if ($live -and $created.Timeout) { [int]$created.Timeout } elseif ($updated -and $UpdateSetting -notcontains 'Timeout') { [int]$current.Timeout } else { $Timeout }
        MemorySize   = if ($live -and $created.MemorySize) { [int]$created.MemorySize } elseif ($updated -and $UpdateSetting -notcontains 'MemorySize') { [int]$current.MemorySize } else { $MemorySize }
        Role         = if ($live -and $created.Role) { $created.Role } elseif ($updated -and -not $configurationParams.Role) { [string]$current.Role } else { $Role }
        Updated      = $updated
        Arn          = $created.FunctionArn
    }
}
