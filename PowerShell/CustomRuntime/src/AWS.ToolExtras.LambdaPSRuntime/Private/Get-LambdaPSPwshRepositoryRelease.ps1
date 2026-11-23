function Get-LambdaPSPwshRepositoryRelease {
    <#
    .SYNOPSIS
        Lists the pwsh releases in Microsoft's RPM repository that the amd64 runtime image installs from.
    .DESCRIPTION
        Reads the primary package list that repodata/repomd.xml names, only when it has the SHA256
        repomd.xml gives. That hash, from the same server, catches corruption, not tampering, and
        repomd.xml's signature is not checked. This only chooses a version: dnf checks the RPM's Microsoft
        signature at install. Returns the x86_64 'powershell' versions (x.y.z), oldest first. A failed
        download, unreadable document or hash mismatch throws PwshRepositoryLookupFailed.
    .PARAMETER PowerShellVersion
        The major.minor line (for example 7.6).
    .OUTPUTS
        System.String
    .EXAMPLE
        Get-LambdaPSPwshRepositoryRelease -PowerShellVersion 7.6

        Returns '7.6.0', '7.6.1', ... up to the newest 7.6 release in the repository.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidatePattern('\A[0-9]+\.[0-9]+\z')]
        [string]$PowerShellVersion
    )

    $repository = $script:Config.pwsh.PackageRepository
    $download = $script:Config.pwsh.Download
    $baseUrl = $repository.Url
    $webParams = @{
        UseBasicParsing          = $true
        ErrorAction              = 'Stop'
        MaximumRetryCount        = $download.RetryCount
        RetryIntervalSec         = $download.RetryDelaySeconds
        ConnectionTimeoutSeconds = $download.ConnectTimeoutSeconds
        OperationTimeoutSeconds  = $repository.OperationTimeoutSeconds
    }
    # No DTDs, so a document cannot pull in external entities.
    $xmlSettings = [System.Xml.XmlReaderSettings]::new()
    $xmlSettings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit

    # `throw`, not $PSCmdlet.ThrowTerminatingError: the caller reports the error as its own.
    $newLookupError = {
        param([string]$Reason, [System.Exception]$InnerException)
        [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new("Could not read the pwsh releases in '$baseUrl': $Reason", $InnerException),
            'PwshRepositoryLookupFailed', [System.Management.Automation.ErrorCategory]::ReadError, $baseUrl)
    }

    $primaryFile = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "lambdaps-$([System.IO.Path]::GetRandomFileName())-primary.xml.gz")
    try {
        try {
            $repomdText = [string](Invoke-WebRequest -Uri "$baseUrl/repodata/repomd.xml" @webParams).Content
            $repomd = [System.Xml.XmlDocument]::new()
            $reader = [System.Xml.XmlReader]::Create([System.IO.StringReader]::new($repomdText), $xmlSettings)
            try { $repomd.Load($reader) } finally { $reader.Dispose() }
        }
        catch { throw (& $newLookupError "repodata/repomd.xml: $($_.Exception.Message)" $_.Exception) }

        $namespace = [System.Xml.XmlNamespaceManager]::new($repomd.NameTable)
        $namespace.AddNamespace('repo', 'http://linux.duke.edu/metadata/repo')
        $href = [string]$repomd.SelectSingleNode("/repo:repomd/repo:data[@type='primary']/repo:location/@href", $namespace).Value
        $checksum = $repomd.SelectSingleNode("/repo:repomd/repo:data[@type='primary']/repo:checksum[@type='sha256']", $namespace)
        if ($href -notmatch '\Arepodata/[A-Za-z0-9._-]+\z' -or $null -eq $checksum) {
            throw (& $newLookupError 'repodata/repomd.xml names no primary package list in repodata/ with a SHA256.' $null)
        }

        try { $null = Invoke-WebRequest -Uri "$baseUrl/$href" -OutFile $primaryFile @webParams }
        catch { throw (& $newLookupError "${href}: $($_.Exception.Message)" $_.Exception) }
        $actual = (Get-FileHash -LiteralPath $primaryFile -Algorithm SHA256).Hash
        if ($actual -ne $checksum.InnerText.ToUpperInvariant()) {
            throw (& $newLookupError "$href has SHA256 $actual, not the $($checksum.InnerText) repodata/repomd.xml names." $null)
        }

        $versions = [System.Collections.Generic.List[string]]::new()
        $stream = $null; $reader = $null
        try {
            $stream = [System.IO.Compression.GZipStream]::new([System.IO.File]::OpenRead($primaryFile), [System.IO.Compression.CompressionMode]::Decompress)
            $reader = [System.Xml.XmlReader]::Create($stream, $xmlSettings)
            # Each <package> holds <name>, <arch> and <version ver="..."/>, in that order. Reading an
            # element's text moves past it, so the next Read() is skipped after one.
            $name = $null; $arch = $null; $advance = $true
            while (-not $reader.EOF) {
                if ($advance -and -not $reader.Read()) { break }
                $advance = $true
                if ($reader.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
                switch ($reader.LocalName) {
                    'package' { $name = $null; $arch = $null }
                    'name' { $name = $reader.ReadElementContentAsString(); $advance = $false }
                    'arch' { $arch = $reader.ReadElementContentAsString(); $advance = $false }
                    'version' {
                        $ver = $reader.GetAttribute('ver')
                        if ($name -ceq 'powershell' -and $arch -ceq 'x86_64' -and $ver -match '\A[0-9]+\.[0-9]+\.[0-9]+\z' -and
                            $ver.StartsWith("$PowerShellVersion.", [System.StringComparison]::Ordinal)) {
                            $versions.Add($ver)
                        }
                    }
                }
            }
        }
        catch { throw (& $newLookupError "${href}: $($_.Exception.Message)" $_.Exception) }
        finally {
            if ($null -ne $reader) { $reader.Dispose() }
            if ($null -ne $stream) { $stream.Dispose() }
        }
    }
    finally {
        $removeParams = @{ LiteralPath = $primaryFile; Force = $true; ErrorAction = 'SilentlyContinue'; WhatIf = $false; Confirm = $false }
        Remove-Item @removeParams
    }

    $versions | Select-Object -Unique | Sort-Object { [version]$_ }
}
