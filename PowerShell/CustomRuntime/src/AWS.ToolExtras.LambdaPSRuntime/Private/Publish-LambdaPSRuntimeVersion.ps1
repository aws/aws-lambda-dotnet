function Publish-LambdaPSRuntimeVersion {
    <#
    .SYNOPSIS
        Registers and publishes a Lambda customer-managed runtime version.
    .DESCRIPTION
        Thin, single-purpose seam over the Lambda runtime cmdlets (AWS.Tools.LambdaCore). It looks the
        runtime up with Get-LMCRuntime and creates it with New-LMCRuntime only when it is not found
        (tolerating a create that races another), then calls New-LMCRuntimeVersion to create a new
        version from the pushed image. It exists so the orchestrator has one mockable point for runtime
        publication. It binds no AWS SDK assembly directly.

        A freshly published version starts in CREATING and settles to ACTIVE or FAILED as Lambda
        processes the image. This seam WAITS for that and then REQUIRES ACTIVE: a version that
        settled to FAILED, or that is still CREATING when the wait budget runs out, is a terminating
        error. It must not return normally, because the caller's next step is to point an alias at
        the version -- so a silent non-ACTIVE return is how a deployment reports success and then
        fails on every invoke. FAILED is easy to hit: Lambda pulls the image by digest and rejects an
        OCI manifest, which recent BuildKit produces by default.
    .PARAMETER RuntimeName
        The customer-managed runtime name to create or reuse.
    .PARAMETER ImageUri
        The fully-qualified ECR image URI to publish the runtime version from.
    .PARAMETER RuntimeVersion
        The immutable runtime version label to publish (for example 'v1').
    .PARAMETER Architecture
        The CPU architecture the image is expected to be: x86_64 or arm64. After publishing, this is
        checked against the architecture the service reports for the version, and a mismatch is a
        terminating RuntimeImageArchitectureMismatch error.

        The service reports what it reads from the image MANIFEST, not what the caller declares: a
        version published with Compatibility_Architecture arm64 from an amd64 image reports
        [x86_64]. So a mismatch means the image itself was built for the wrong platform --
        for example 'docker buildx' on a single-architecture builder that cannot emit linux/arm64.
        Failing here stops the caller from pointing an alias, and then functions, at it.
    .PARAMETER RuntimeAlias
        The alias the caller points at the version once it is ACTIVE. Optional; it only fills in the
        alias command the timeout error suggests, which otherwise shows <alias>.
    .PARAMETER Region
        The AWS region the runtime is in.
    .PARAMETER CredentialParameter
        AWS credential parameters (ProfileName, ProfileLocation, Credential) to splat onto every
        AWS.Tools call. Empty by default, so AWS.Tools uses its default credentials.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.PublishedRuntime
    .EXAMPLE
        Publish-LambdaPSRuntimeVersion -RuntimeName my-ps -ImageUri 111122223333.dkr.ecr.us-east-1.amazonaws.com/rt:v1 -RuntimeVersion v1 -Architecture x86_64 -Region us-east-1

        Creates the runtime if needed and publishes a new version from the image.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.PublishedRuntime')]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RuntimeName,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ImageUri,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RuntimeVersion,

        [Parameter(Mandatory)]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RuntimeAlias,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Region,

        [Parameter()]
        [ValidateNotNull()]
        [hashtable]$CredentialParameter = @{}
    )


    if (-not $PSCmdlet.ShouldProcess($RuntimeName, 'Publish Lambda runtime version')) {
        return
    }

    # Ensure the runtime exists: look it up first, create it only when it is not found, so a
    # re-deploy makes no failed create call. Get-LMCRuntime's not-found is a clean
    # ResourceNotFoundException. The conflict tolerance covers a create that races another.
    # New-LMCRuntime takes -Name; the other runtime cmdlets identify the runtime with -RuntimeName.
    $clientConfig = Get-LambdaPSClientConfig -Service LambdaCore
    $runtimeExists = $true
    try {
        $null = Get-LMCRuntime -RuntimeName $RuntimeName -Region $Region -ClientConfig $clientConfig @CredentialParameter -ErrorAction Stop
    }
    catch {
        if (-not (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'ResourceNotFoundException')) { throw }
        $runtimeExists = $false
    }
    if ($runtimeExists) {
        Write-Verbose "Runtime '$RuntimeName' already exists; reusing it."
    }
    else {
        try {
            $null = New-LMCRuntime -Name $RuntimeName -Region $Region -ClientConfig $clientConfig @CredentialParameter -ErrorAction Stop
        }
        catch {
            if (-not (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'ResourceConflictException')) { throw }
            Write-Verbose "Runtime '$RuntimeName' was created concurrently; reusing it."
        }
    }

    # Create an immutable version from the pushed image. -RuntimeName is the runtime and -Name is the
    # version label. Compatibility_Architecture is sent for completeness, but the architecture the
    # service reports comes from the image manifest (see -Architecture), so the check after the
    # wait -- not this declaration -- is what guarantees the architecture.
    $publishParams = @{
        RuntimeName                = $RuntimeName
        Name                       = $RuntimeVersion
        ImageUri                   = $ImageUri
        Compatibility_Architecture = @($Architecture)
        Region                     = $Region
        ClientConfig               = $clientConfig
    }
    $published = New-LMCRuntimeVersion @publishParams @CredentialParameter -ErrorAction Stop

    # A freshly published version is CREATING; wait for it to settle so an alias can point at it. A
    # version that is not readable yet counts as still being created; any other error stops the wait
    # with its real cause, rather than being read as a settled state. The version exists by then, so
    # that error (RuntimeVersionWaitFailed) says so and gives the commands to finish, as the timeout does.
    $commandArguments = "-Region $Region$(Get-LambdaPSCredentialArgumentText -CredentialParameter $CredentialParameter)"
    $aliasName = if ($RuntimeAlias) { $RuntimeAlias } else { '<alias>' }
    $finishCommands = ("check with Get-LMCRuntimeVersion -RuntimeName $RuntimeName -Name $RuntimeVersion $commandArguments. " +
        "Once it is ACTIVE and its Compatibility.Architectures is '$Architecture', point the alias at it with " +
        "Update-LMCRuntimeAlias -RuntimeName $RuntimeName -Name $aliasName -RuntimeVersion $RuntimeVersion $commandArguments " +
        '(New-LMCRuntimeAlias if the alias does not exist yet).')
    $wait = $script:Config.wait.RuntimeVersion
    $rv = $null
    for ($i = 0; $i -lt $wait.Attempts; $i++) {
        try {
            $rv = Get-LMCRuntimeVersion -RuntimeName $RuntimeName -Name $RuntimeVersion -Region $Region -ClientConfig $clientConfig @CredentialParameter -ErrorAction Stop
        }
        catch {
            if (-not (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'ResourceNotFoundException')) {
                $serviceText = $_.Exception.Message.Trim()
                if ($serviceText -and $serviceText -notmatch '[.!?]["'')\]]*\z') { $serviceText += '.' }
                $serviceDetail = if ($serviceText) { ": $serviceText" } else { '.' }
                throw [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new(
                        ("Runtime '$RuntimeName' version '$RuntimeVersion' was created, but its state could not be " +
                         "read$serviceDetail The version exists, so running the deploy again is refused; " + $finishCommands),
                        $_.Exception),
                    'RuntimeVersionWaitFailed', [System.Management.Automation.ErrorCategory]::ReadError, $RuntimeVersion)
            }
            $rv = $null
        }
        if ($null -ne $rv -and $rv.State -ne 'CREATING') { break }
        Write-Verbose "Runtime '$RuntimeName' version '$RuntimeVersion' is still being created (attempt $($i + 1)/$($wait.Attempts))."
        Start-Sleep -Seconds $wait.IntervalSeconds
    }

    # Errors are raised with `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this
    # rethrows the error with its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also
    # reach the caller's -ErrorVariable naming this internal helper.

    # Still CREATING after the wait budget: report the timeout rather than letting the caller alias a
    # version that may never become usable. Running the deploy again would be refused, because the
    # version now exists, so the message gives the alias commands to run once it is ACTIVE.
    if ($null -eq $rv -or $rv.State -eq 'CREATING') {
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.TimeoutException]::new(
                ("Runtime '$RuntimeName' version '$RuntimeVersion' was still being created after " +
                 "$($wait.Attempts * $wait.IntervalSeconds) seconds. It may still become ACTIVE; " + $finishCommands)),
            'RuntimeVersionTimeout', [System.Management.Automation.ErrorCategory]::OperationTimeout, $RuntimeVersion)
    }

    # Anything other than ACTIVE is a failed publish. Returning here would let the caller alias a
    # broken version and print a success object. Only FAILED is about the image; another state (for
    # example DELETING, from a concurrent removal) is reported as it is.
    if ($rv.State -ne 'ACTIVE') {
        # A FAILED version exists and is immutable, so a rerun under the same name is refused; say so, as
        # the architecture-mismatch error below does.
        $cause = if ($rv.State -eq 'FAILED') {
            (" Check that the image is a Docker v2 Schema 2 manifest: Lambda pulls by digest and rejects OCI " +
             "('docker buildx imagetools inspect $ImageUri' shows its media type), or Lambda could not pull it " +
             "(check that the repository policy grants lambda.amazonaws.com pull for this runtime's ARN). Version '$RuntimeVersion' " +
             'already exists and is immutable, so publish under a new -RuntimeVersion, or remove it with ' +
             "Remove-LMCRuntimeVersion -RuntimeName $RuntimeName -Name $RuntimeVersion $commandArguments.")
        }
        else { '' }
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new(
                ("Runtime '$RuntimeName' version '$RuntimeVersion' settled in state '$($rv.State)', not ACTIVE, " +
                 "so it cannot be used. Image: '$ImageUri'.$cause")),
            'RuntimeVersionNotActive', [System.Management.Automation.ErrorCategory]::InvalidResult, $RuntimeVersion)
    }

    # The architecture the service reports is read from the image manifest, so it is the image's REAL
    # architecture. If it is not the one requested, the image was built for the wrong platform:
    # returning normally would let the caller alias it, and every function adopting the alias's
    # architecture would then silently run on the wrong one. The version already exists at this
    # point (it is immutable), so the error says so -- only the alias step is prevented.
    [string[]]$reported = Get-LambdaPSReportedArchitecture -Compatibility $rv.Compatibility
    if ($reported.Count -eq 0) {
        # The service reports one for an image it accepted; if it does not, the match cannot be
        # checked here; New-LambdaPSFunctionDeployment warns on the same condition.
        Write-Warning ("Runtime '$RuntimeName' version '$RuntimeVersion' reports no architecture, so the " +
            "image could not be confirmed as '$Architecture'.")
    }
    elseif ($reported -notcontains $Architecture) {
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new(
                ("Runtime '$RuntimeName' version '$RuntimeVersion' was requested as '$Architecture', but its " +
                 "image '$ImageUri' is [$($reported -join ', ')]. The alias was NOT pointed at it. The " +
                 'image was built for the wrong platform -- check that the docker buildx builder ' +
                 "supports the target ('docker buildx ls'; arm64 on an amd64 host needs QEMU/binfmt or " +
                 "a multi-platform builder). Version '$RuntimeVersion' already exists and is immutable, so " +
                 "rebuild and publish under a new -RuntimeVersion, or remove it with Remove-LMCRuntimeVersion " +
                 "-RuntimeName $RuntimeName -Name $RuntimeVersion $commandArguments.")),
            'RuntimeImageArchitectureMismatch', [System.Management.Automation.ErrorCategory]::InvalidResult, $RuntimeVersion)
    }

    return [PSCustomObject]@{
        PSTypeName   = 'AWS.ToolExtras.LambdaPSRuntime.PublishedRuntime'
        RuntimeName  = $RuntimeName
        ImageUri     = $ImageUri
        Version      = $RuntimeVersion
        Architecture = $Architecture
        State        = $rv.State
        Arn          = $published.Arn
    }
}
