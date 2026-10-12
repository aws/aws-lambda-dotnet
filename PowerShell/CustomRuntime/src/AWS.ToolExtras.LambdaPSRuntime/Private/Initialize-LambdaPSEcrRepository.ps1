function Initialize-LambdaPSEcrRepository {
    <#
    .SYNOPSIS
        Ensures an ECR repository exists, grants Lambda image pull on it, and logs docker in to it.
    .DESCRIPTION
        Seam over the ECR cmdlets (AWS.Tools.ECR) that prepares a repository for a runtime-image push.
        It binds no AWS SDK assembly directly. Three steps:

          1. Get-ECRRepository, then New-ECRRepository (with scan on push) only when the repository
             is absent. When that create is declined or run under -WhatIf, steps 2 and 3 are skipped:
             they need the repository.
          2. The Lambda image-pull grant, scoped to this account and -RuntimeArn. The existing policy
             is READ and the grant MERGED in (Merge-LambdaPSEcrPullPolicy); nothing is written when
             Lambda already has access for this runtime, with a warning when that existing grant
             does not pin the account.
             Set-ECRRepositoryPolicy replaces a policy wholesale, so writing a fixed policy would
             delete every other grant on an existing repository.
          3. docker login to the registry, so the following buildx push is authorized.

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

        Creates the repository if it does not exist, merges in the Lambda pull grant scoped to the
        runtime, and logs docker in to the registry. Returns nothing.
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

    $created = $false
    if (-not $existing) {
        if (-not $PSCmdlet.ShouldProcess($RepositoryName, 'Create ECR repository')) {
            # Declined or -WhatIf: the repository does not exist, so there is no policy to read or
            # write and no registry to log in to.
            Write-Verbose "ECR repository '$RepositoryName' was not created; the pull grant and docker login are skipped."
            return
        }
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

    # Authenticate the local container CLI to ECR so the subsequent build --push succeeds
    # (otherwise docker returns "denied: requested access to the resource is denied").
    # Uses the ECR cmdlet for the token and the docker CLI for the login (no SDK binding).
    if ($PSCmdlet.ShouldProcess($RepositoryName, 'docker login to ECR')) {
        $login = Get-ECRLoginCommand -Region $Region -ClientConfig $clientConfig @CredentialParameter -ErrorAction Stop
        # Log in against the BARE registry host (strip any https:// scheme). docker stores the
        # credential under the key you pass; buildx --push looks it up by bare host, so a
        # scheme-qualified login lands under a key buildx never finds -> "denied" on push.
        $registry = $login.Endpoint -replace '^https?://', ''
        # The exit code is checked below; with the user's preference on, pwsh would raise its own error
        # first and lose docker's output.
        $PSNativeCommandUseErrorActionPreference = $false
        # The password goes in on stdin, so docker's output never contains it and is safe to show.
        $loginOutput = ($login.Password | & docker login --username $login.Username --password-stdin $registry 2>&1 | Out-String).Trim()
        if ($LASTEXITCODE -ne 0) {
            # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error
            # with its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach
            # the caller's -ErrorVariable naming this internal helper.
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.InvalidOperationException]::new("docker login to ECR registry '$registry' failed (exit $LASTEXITCODE): $loginOutput"),
                'EcrDockerLoginFailed', [System.Management.Automation.ErrorCategory]::AuthenticationError, $RepositoryName)
        }
        # With no credential helper configured, docker stores the registry token (valid for about 12
        # hours) unencrypted in its config file and says so. Raise that as a warning rather than leave
        # it in the Verbose output. Docker's wording is not a contract: unmatched, it stays Verbose.
        if ("$loginOutput" -match 'stored unencrypted') {
            Write-Warning ("docker stored the ECR login token for '$registry' unencrypted in its config file " +
                '(~/.docker/config.json). The token is valid for about 12 hours. To keep it out of that file, ' +
                'configure a docker credential helper, such as amazon-ecr-credential-helper.')
        }
        if ($loginOutput) { Write-Verbose "docker login: $loginOutput" }
    }
}
