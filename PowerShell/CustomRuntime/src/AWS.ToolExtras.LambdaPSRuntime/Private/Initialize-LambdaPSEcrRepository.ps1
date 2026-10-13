function Initialize-LambdaPSEcrRepository {
    <#
    .SYNOPSIS
        Ensures an ECR repository exists, grants Lambda image pull on it, and logs docker in to it.
    .DESCRIPTION
        Seam over the ECR cmdlets (AWS.Tools.ECR) that prepares a repository for a runtime-image push.
        It binds no AWS SDK assembly directly. Three steps:

          1. Get-ECRRepository. When the repository is absent and its create is declined or run under
             -WhatIf, steps 2 and 3 are skipped, and nothing is created.
          2. docker login to the registry, so the following buildx push is authorized. It runs before
             the create, so a failed login creates no repository. It is skipped when the docker config
             ($env:DOCKER_CONFIG, or .docker in the user's home folder) hands this registry to the
             amazon-ecr-credential-helper (credHelpers, or else credsStore, set to ecr-login): that
             helper supplies ECR tokens itself and cannot store a login.
          3. New-ECRRepository (with scan on push) when the repository is absent, then the Lambda
             image-pull grant, scoped to this account and -RuntimeArn. The existing policy is READ and
             the grant MERGED in (Merge-LambdaPSEcrPullPolicy); nothing is written when Lambda already
             has access for this runtime, with a warning when that existing grant does not pin the
             account. Set-ECRRepositoryPolicy replaces a policy wholesale, so writing a fixed policy
             would delete every other grant on an existing repository.

        Known limit: ECR has no conditional policy write. Two deploys that share one -RepositoryName
        and run at the same time each read the policy, merge in their grant and write the whole
        policy, so the last write wins and the other runtime's grant can be lost. Run such deploys
        one at a time, or give each runtime its own repository.

        Not-found conditions are recognized by exception identity through Test-LambdaPSServiceError,
        which walks the InnerException chain. That walk is required: AWS.Tools wraps the SDK
        exception in an outer InvalidOperationException, so checking the outer type never matches.
    .PARAMETER RepositoryName
        The ECR repository name to confirm or create.
    .PARAMETER AccountId
        The account that owns the repository and the runtime.
    .PARAMETER RuntimeArn
        The ARN of the runtime the image is for; the pull grant is scoped to it.
    .PARAMETER Region
        The AWS region the repository lives in.
    .PARAMETER CredentialParameter
        AWS credential parameters (ProfileName, ProfileLocation, Credential) to splat onto every
        AWS.Tools call. Empty by default, so AWS.Tools uses its default credentials.
    .OUTPUTS
        None.
    .EXAMPLE
        Initialize-LambdaPSEcrRepository -RepositoryName powershell-runtime -AccountId 111122223333 -RuntimeArn arn:aws:lambda:us-east-1:111122223333:runtime/my-ps -Region us-east-1

        Logs docker in to the registry, creates the repository if it does not exist, and merges in
        the Lambda pull grant scoped to the runtime. Returns nothing.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([void])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RepositoryName,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$AccountId,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RuntimeArn,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Region,

        [Parameter()]
        [ValidateNotNull()]
        [hashtable]$CredentialParameter = @{}
    )


    # Get-ECRRepository THROWS when the repo does not exist (it does not return empty), so probe with
    # try/catch and treat only not-found as "create it"; any other error (auth, throttling, ...) is
    # real and must surface. Matched by identity through the wrapped exception chain, not message text.
    $clientConfig = Get-LambdaPSClientConfig -Service ECR
    $existing = $null
    try {
        $existing = Get-ECRRepository -RepositoryName $RepositoryName -Region $Region -ClientConfig $clientConfig @CredentialParameter -ErrorAction Stop
    }
    catch {
        if (-not (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'RepositoryNotFoundException')) { throw }
    }

    if (-not $existing -and -not $PSCmdlet.ShouldProcess($RepositoryName, 'Create ECR repository')) {
        # Declined or -WhatIf: the repository does not exist, so there is no policy to read or
        # write and nothing to push to.
        Write-Verbose "ECR repository '$RepositoryName' was not created; the pull grant and docker login are skipped."
        return
    }

    # Authenticate the local container CLI to ECR so the subsequent build --push succeeds
    # (otherwise docker returns "denied: requested access to the resource is denied"). This comes
    # before the create, so a login that fails leaves no new repository behind.
    # Uses the ECR cmdlet for the token and the docker CLI for the login (no SDK binding).
    if ($PSCmdlet.ShouldProcess($RepositoryName, 'docker login to ECR')) {
        $login = Get-ECRLoginCommand -Region $Region -ClientConfig $clientConfig @CredentialParameter -ErrorAction Stop
        # Log in against the BARE registry host (strip any https:// scheme). docker stores the
        # credential under the key you pass; buildx --push looks it up by bare host, so a
        # scheme-qualified login lands under a key buildx never finds -> "denied" on push.
        $registry = $login.Endpoint -replace '^https?://', ''

        # docker picks a registry's credential helper from credHelpers, or else credsStore. The
        # amazon-ecr-credential-helper (ecr-login) gets ECR tokens itself and cannot store one, so a
        # docker login through it fails; the push authenticates without it.
        $dockerConfigDirectory = if ($env:DOCKER_CONFIG) { $env:DOCKER_CONFIG } else {
            [System.IO.Path]::Combine([System.Environment]::GetFolderPath('UserProfile'), '.docker')
        }
        $dockerConfigPath = [System.IO.Path]::Combine($dockerConfigDirectory, 'config.json')
        $credentialHelper = $null
        if ([System.IO.File]::Exists($dockerConfigPath)) {
            # A config docker cannot read is docker's to report, at login.
            $dockerConfig = try { [System.IO.File]::ReadAllText($dockerConfigPath) | ConvertFrom-Json -AsHashtable -ErrorAction Stop } catch { $null }
            if ($dockerConfig -is [System.Collections.IDictionary]) {
                $helpers = $dockerConfig['credHelpers']
                $credentialHelper = if ($helpers -is [System.Collections.IDictionary] -and $helpers.Contains($registry)) {
                    $helpers[$registry]
                }
                else { $dockerConfig['credsStore'] }
            }
        }

        if ([string]$credentialHelper -eq 'ecr-login') {
            Write-Verbose "docker login skipped: the docker config at '$dockerConfigPath' hands '$registry' to the ECR credential helper."
        }
        else {
            # The exit code is checked below; with the user's preference on, pwsh would raise its own
            # error first and lose docker's output.
            $PSNativeCommandUseErrorActionPreference = $false
            # The password goes in on stdin, so docker's output never contains it and is safe to show.
            $loginOutput = ($login.Password | & docker login --username $login.Username --password-stdin $registry 2>&1 | Out-String).Trim()
            if ($LASTEXITCODE -ne 0) {
                # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the
                # error with its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would
                # also reach the caller's -ErrorVariable naming this internal helper.
                throw [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new("docker login to ECR registry '$registry' failed (exit $LASTEXITCODE): $loginOutput"),
                    'EcrDockerLoginFailed', [System.Management.Automation.ErrorCategory]::AuthenticationError, $RepositoryName)
            }
            # With no credential helper configured, docker stores the registry token (valid for about 12
            # hours) unencrypted in its config file and says so, naming the file. Raise that as a warning
            # rather than leave it in the Verbose output. Docker's wording is not a contract: unmatched,
            # it stays Verbose.
            if ("$loginOutput" -match 'stored unencrypted') {
                $tokenFile = if ("$loginOutput" -match 'stored unencrypted in (?<path>\S.*?)\.?\s*(?:\r?\n|$)') { $Matches['path'] } else { $dockerConfigPath }
                Write-Warning ("docker stored the ECR login token for '$registry' unencrypted in '$tokenFile'. The " +
                    'token is valid for about 12 hours. To keep it out of that file, configure a docker ' +
                    'credential store (osxkeychain, wincred, pass or secretservice), or the ' +
                    'amazon-ecr-credential-helper, which gets ECR tokens itself so no login is stored.')
            }
            if ($loginOutput) { Write-Verbose "docker login: $loginOutput" }
        }
    }

    $created = $false
    if (-not $existing) {
        try {
            # Scan on push: the repository holds the image every function on the runtime runs.
            $null = New-ECRRepository -RepositoryName $RepositoryName -ImageScanningConfiguration_ScanOnPush $true -Region $Region -ClientConfig $clientConfig @CredentialParameter -ErrorAction Stop
            $created = $true
        }
        catch {
            # A concurrent deploy may have created it between the probe and here; that is fine.
            if (-not (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'RepositoryAlreadyExistsException')) { throw }
        }
    }

    # Grant Lambda permission to pull the image, or New-LMCRuntimeVersion fails with
    # "Lambda does not have permission to access the ECR image". MERGE into the existing policy rather
    # than replace it: Set-ECRRepositoryPolicy is a full replace, and this may be a repository the
    # customer already manages (a customer-managed repository is supported) with other grants on it.
    $currentPolicy = $null
    if (-not $created) {
        try {
            $currentPolicy = (Get-ECRRepositoryPolicy -RepositoryName $RepositoryName -Region $Region -ClientConfig $clientConfig @CredentialParameter -ErrorAction Stop).PolicyText
        }
        catch {
            # No policy at all is normal for a repository; anything else must surface.
            if (-not (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'RepositoryPolicyNotFoundException')) { throw }
        }
    }

    $mergedPolicy = Merge-LambdaPSEcrPullPolicy -PolicyText $currentPolicy -AccountId $AccountId -RuntimeArn $RuntimeArn
    if ($null -eq $mergedPolicy) {
        Write-Verbose "ECR repository '$RepositoryName' already grants Lambda image pull; policy left unchanged."
    }
    else {
        $policyAction = if ([string]::IsNullOrWhiteSpace($currentPolicy)) {
            'Set ECR repository policy granting Lambda image pull'
        }
        else {
            'Add a Lambda image-pull statement to the existing ECR repository policy (existing statements are kept)'
        }
        if ($PSCmdlet.ShouldProcess($RepositoryName, $policyAction)) {
            $null = Set-ECRRepositoryPolicy -RepositoryName $RepositoryName -PolicyText $mergedPolicy -Region $Region -ClientConfig $clientConfig @CredentialParameter -ErrorAction Stop
        }
    }
}
