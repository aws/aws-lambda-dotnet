function Get-LambdaPSPwshArchive {
    <#
    .SYNOPSIS
        Returns a local path to a verified PowerShell Linux release tarball for the container-free
        (layer / self-contained .zip) build.
    .DESCRIPTION
        The mockable acquisition seam for the container-free package build. It never trusts an
        unverified download: for a pinned version/architecture it downloads the official PowerShell
        release tarball over HTTPS and verifies its SHA256 against a hash baked into the module,
        failing closed on any mismatch. If no pinned hash exists for the requested version, it
        refuses rather than downloading unverified, and directs the caller to supply -PwshArchivePath.

        -PwshArchivePath is the bring-your-own escape hatch for air-gapped or security-controlled
        environments: the caller supplies an already-vetted tarball. When a hash is pinned for
        -PwshVersion and -Architecture, the file must match it, so a tarball of the wrong version or
        architecture fails here rather than producing a broken package. For a version with no pinned
        hash the file is used as-is (the caller owns that file's provenance).

        The download retries a failed HTTP response (a GitHub rate limit or server error) and gives up
        on a host that does not connect and send its response headers in time. A download that stops
        part-way through the file has no time limit.
    .PARAMETER PwshVersion
        The full pwsh version (x.y.z, for example 7.4.20) to download and verify.
    .PARAMETER Architecture
        The target architecture: x86_64 (mapped to the linux-x64 tarball) or arm64 (linux-arm64).
    .PARAMETER PwshArchivePath
        A pre-downloaded, caller-vetted tarball to use instead of downloading. It is checked against
        the pinned hash for -PwshVersion and -Architecture when one exists, and used as-is otherwise.
    .PARAMETER ValidateOnly
        Run the checks that need no download or hashing (the -PwshArchivePath file exists, or a hash
        is pinned for -PwshVersion and -Architecture) and return nothing. Callers use it before their ShouldProcess,
        so -WhatIf reports the same errors a real run would.
    .OUTPUTS
        System.String
    .EXAMPLE
        Get-LambdaPSPwshArchive -PwshVersion 7.4.20 -Architecture x86_64

        Downloads powershell-7.4.20-linux-x64.tar.gz and returns its path after verifying the SHA256.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidatePattern('\A[0-9]+\.[0-9]+\.[0-9]+\z')]
        [string]$PwshVersion,

        [Parameter(Mandatory)]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture,

        [Parameter()]
        [string]$PwshArchivePath,

        [Parameter()]
        [switch]$ValidateOnly
    )

    $archToken = if ($Architecture -eq 'arm64') { 'arm64' } else { 'x64' }

    # Pinned, known-good SHA256 of the official tarball (the hash table is in Get-LambdaPSPwshArchiveHash).
    $key = "$PwshVersion/$archToken"
    $expected = Get-LambdaPSPwshArchiveHash -PwshVersion $PwshVersion -Architecture $Architecture

    # Errors are raised with `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called
    # this rethrows them with its own $PSCmdlet, so the caller's -ErrorVariable never names this helper.

    # Bring-your-own: the caller-vetted tarball (air-gapped / controlled acquisition).
    if (-not [string]::IsNullOrWhiteSpace($PwshArchivePath)) {
        if (-not (Test-Path -LiteralPath $PwshArchivePath -PathType Leaf)) {
            throw [System.Management.Automation.ErrorRecord]::new(
                    [System.IO.FileNotFoundException]::new("The -PwshArchivePath '$PwshArchivePath' was not found."),
                    'PwshArchiveNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $PwshArchivePath)
        }
        if ($ValidateOnly) { return }
        $byo = Resolve-LambdaPSPath -Path $PwshArchivePath
        # A pinned version must match its official tarball, which catches a tarball of the wrong
        # architecture or version. The file is the caller's, so it is left in place either way.
        if ($expected) {
            $actual = (Get-FileHash -LiteralPath $byo -Algorithm SHA256).Hash
            if ($actual -ne $expected) {
                throw [System.Management.Automation.ErrorRecord]::new(
                        [System.Security.SecurityException]::new(
                            ("The -PwshArchivePath '$byo' is not the official pwsh $PwshVersion linux-$archToken tarball: " +
                             "expected SHA256 $expected but got $actual. Check the file's version and architecture.")),
                        'PwshArchiveHashMismatch', [System.Management.Automation.ErrorCategory]::SecurityError, $byo)
            }
        }
        return $byo
    }

    if (-not $expected) {
        throw [System.Management.Automation.ErrorRecord]::new(
                [System.InvalidOperationException]::new(
                    ("No pinned SHA256 is known for pwsh $PwshVersion ($Architecture), so it cannot be verified. " +
                     'Use a pwsh version this module pins a hash for, or supply -PwshArchivePath with a tarball you have verified.')),
                'UnknownPwshArchiveHash', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $key)
    }
    if ($ValidateOnly) { return }

    $fileName = "powershell-$PwshVersion-linux-$archToken.tar.gz"
    $url = "$($script:Config.pwsh.ReleaseUrl)/v$PwshVersion/$fileName"
    # A random name, so another user on a shared host cannot pre-create the file and swap its
    # contents between the hash check and the extraction.
    $destination = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "lambdaps-$([System.IO.Path]::GetRandomFileName())-$fileName")

    Write-Verbose "Downloading $url"
    try {
        # Retry a failed HTTP response (a GitHub rate limit or server error), and give up on a host
        # that does not connect and send its headers in time. A dropped connection is not retried. The limits are in Config/pwsh.psd1.
        $download = $script:Config.pwsh.Download
        Invoke-WebRequest -Uri $url -OutFile $destination -UseBasicParsing -ErrorAction Stop `
            -MaximumRetryCount $download.RetryCount -RetryIntervalSec $download.RetryDelaySeconds `
            -ConnectionTimeoutSeconds $download.ConnectTimeoutSeconds
        $actual = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    }
    catch {
        # An interrupted download leaves a partial file under a name nothing will reuse.
        Remove-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue
        throw
    }
    if ($actual -ne $expected) {
        Remove-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue
        throw [System.Management.Automation.ErrorRecord]::new(
                [System.Security.SecurityException]::new(
                    "SHA256 mismatch for '$fileName': expected $expected but got $actual. Refusing to use an unverified interpreter."),
                'PwshArchiveHashMismatch', [System.Management.Automation.ErrorCategory]::SecurityError, $fileName)
    }

    return $destination
}
