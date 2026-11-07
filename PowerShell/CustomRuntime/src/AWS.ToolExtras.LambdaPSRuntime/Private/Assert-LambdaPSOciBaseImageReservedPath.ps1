function Assert-LambdaPSOciBaseImageReservedPath {
    <#
    .SYNOPSIS
        Stops a no-Docker runtime image build when its base image has files in /opt, /var/task or /tmp.
    .DESCRIPTION
        The no-Docker counterpart of the Docker build's ReservedPathNotEmpty check. Its own layers add
        files under /var/runtime only, so the base image is the one place such files can come from. It
        reads each base layer in order, applying whiteouts. A Container function image is not checked, as
        on the Docker build. When the layers cannot be read, it warns and the build goes on.
    .PARAMETER Plan
        The recipe from Get-LambdaPSOciImagePlan.
    .PARAMETER ManifestDigest
        The digest of the base image's platform manifest the build uses (OciImageBuilder.BaseImageDigest).
    .EXAMPLE
        Assert-LambdaPSOciBaseImageReservedPath -Plan $plan -ManifestDigest $builder.BaseImageDigest

        Throws ReservedPathNotEmpty when the base image has files in a folder Lambda hides.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [PSTypeName('AWS.ToolExtras.LambdaPSRuntime.OciImagePlan')]
        [PSCustomObject]$Plan,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ManifestDigest
    )

    if ($Plan.Target -ne 'CMR') { return }
    $registry, $repository = $Plan.BaseImage -replace '[:@].*\z', '' -split '/', 2
    $download = $script:Config.pwsh.Download
    $web = @{
        UseBasicParsing          = $true
        ErrorAction              = 'Stop'
        ConnectionTimeoutSeconds = $download.ConnectTimeoutSeconds
        OperationTimeoutSeconds  = $download.OperationTimeoutSeconds
    }
    # Not on the first request: its expected 401 would be retried too.
    $retry = @{ MaximumRetryCount = $download.RetryCount; RetryIntervalSec = $download.RetryDelaySeconds }
    $layerFile = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "lambdaps-base-layer-$([System.IO.Path]::GetRandomFileName())")
    $present = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::Ordinal)
    try {
        $manifestUri = "https://$registry/v2/$repository/manifests/$ManifestDigest"
        $accept = @{ Accept = 'application/vnd.docker.distribution.manifest.v2+json, application/vnd.oci.image.manifest.v1+json' }
        $challenge = Invoke-WebRequest -Uri $manifestUri -Method Head -Headers $accept -SkipHttpErrorCheck @web
        $headers = $accept.Clone()
        if ($challenge.StatusCode -eq 401) {
            $bearer = @{}
            foreach ($match in [regex]::Matches([string]$challenge.Headers['WWW-Authenticate'], '(\w+)="([^"]*)"')) {
                $bearer[$match.Groups[1].Value] = $match.Groups[2].Value
            }
            $tokenUri = "$($bearer.realm)?service=$([uri]::EscapeDataString($bearer.service))&scope=$([uri]::EscapeDataString("repository:${repository}:pull"))"
            $token = Invoke-RestMethod -Uri $tokenUri @web @retry
            $headers.Authorization = "Bearer $($token.token)"
        }
        $manifest = Invoke-RestMethod -Uri $manifestUri -Headers $headers @web @retry

        foreach ($layer in $manifest.layers) {
            Invoke-WebRequest -Uri "https://$registry/v2/$repository/blobs/$($layer.digest)" -Headers $headers -OutFile $layerFile @web @retry
            if ("sha256:$((Get-FileHash -LiteralPath $layerFile -Algorithm SHA256).Hash)" -ne $layer.digest) {
                throw "layer $($layer.digest) does not match its digest."
            }
            $stream = [System.IO.File]::OpenRead($layerFile)
            try {
                $reader = [System.Formats.Tar.TarReader]::new([System.IO.Compression.GZipStream]::new($stream, [System.IO.Compression.CompressionMode]::Decompress))
                while ($null -ne ($entry = $reader.GetNextEntry())) {
                    $path = '/' + ($entry.Name -replace '\A(\./|/)+', '').TrimEnd('/')
                    $leaf = $path.Substring($path.LastIndexOf('/') + 1)
                    $removed = if ($leaf -ceq '.wh..wh..opq') { $path.Substring(0, $path.Length - $leaf.Length) }
                    elseif ($leaf.StartsWith('.wh.', [System.StringComparison]::Ordinal)) { $path.Remove($path.Length - $leaf.Length, 4) }
                    if ($null -ne $removed) {
                        $null = $present.RemoveWhere({ param($p) $p -ceq $removed -or $p.StartsWith($removed.TrimEnd('/') + '/', [System.StringComparison]::Ordinal) })
                    }
                    elseif ($path -cmatch '\A/(opt|var/task|tmp)/.') { $null = $present.Add($path) }
                }
            }
            finally { $stream.Dispose() }
        }
    }
    catch {
        Write-Warning "Could not list /opt, /var/task and /tmp in base image '$($Plan.BaseImage)' to check that they are empty: $($_.Exception.Message)"
        return
    }
    finally {
        $removeParams = @{ LiteralPath = $layerFile; Force = $true; ErrorAction = 'SilentlyContinue'; WhatIf = $false; Confirm = $false }
        Remove-Item @removeParams
    }

    # Reported as the Docker build's 'ls -A' does: each folder's direct entries.
    $found = foreach ($folder in '/opt', '/var/task', '/tmp') {
        $names = @($present | Where-Object { $_.StartsWith("$folder/", [System.StringComparison]::Ordinal) } |
                ForEach-Object { $_.Substring($folder.Length + 1).Split('/')[0] } | Select-Object -Unique)
        if ($names.Count -gt 0) { "$folder ($($names.Count) entries): $(($names | Select-Object -First 5) -join ' ')" }
    }
    if ($found) {
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new(
                ("Base image '$($Plan.BaseImage)' has files in $($found -join '; '). Lambda mounts the function's code over /var/task " +
                 'and its layers over /opt, and starts each execution environment with an empty /tmp, so the runtime ' +
                 "can't use them. Build with -BuildMethod Docker and remove them in the Dockerfile.")),
            'ReservedPathNotEmpty', [System.Management.Automation.ErrorCategory]::InvalidData, $Plan.BaseImage)
    }
}
