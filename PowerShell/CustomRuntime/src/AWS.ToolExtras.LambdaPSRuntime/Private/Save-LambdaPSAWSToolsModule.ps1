function Save-LambdaPSAWSToolsModule {
    <#
    .SYNOPSIS
        Downloads AWS.Tools modules of one version into a folder with AWS.Tools.Installer 2.x.
    .DESCRIPTION
        The mockable seam for AWS.Tools downloads. AWS.Tools.Installer 2.x downloads the
        CloudFront-hosted, SHA512-checked AWS.Tools.zip rather than going to the PowerShell Gallery.
        Modules land in <Path>/<Name>/<Version>/; the installer always adds AWS.Tools.Common.

        Installer 2.0.5 or a later 2.x is used from the local machine when present. Otherwise it is saved with
        PSResourceGet into -InstallerPath and imported from there, so it is never installed. Because its
        code runs on this machine, it is saved only from -ModuleRepository, or from PSGallery (where AWS
        publishes it) when -ModuleRepository is omitted -- never from whichever registered repository
        has the highest priority -- and, on Windows, only when its Authenticode signature is valid. It has a
        RequiredModules entry on PowerShellGet, so importing it needs PowerShellGet present even though
        this module never calls PowerShellGet; a failed import is a terminating
        AWSToolsInstallerImportFailed error.

        Install-AWSToolsModule is always called with -Path and -Force:
          -Path   so nothing is written to a module path. Installing in place would overwrite
                  AWS.Tools.Common, which this module has loaded: the files are locked on Windows and
                  a mapped assembly is replaced under the running session elsewhere.
          -Force  because without it the installer silently skips the whole call (a Write-Host line,
                  no error) when that version of AWS.Tools.Common is already in the target folder.
        The installer's host output is suppressed. A failed download is a terminating
        ModuleDownloadFailed error wrapping the installer's exception.
    .PARAMETER Name
        Full AWS.Tools module names, all of the same version.
    .PARAMETER Version
        The exact AWS.Tools version.
    .PARAMETER Path
        The folder to download into. Created when missing.
    .PARAMETER ModuleRepository
        The PSResourceGet repositories to save AWS.Tools.Installer from when it is not installed; PSGallery
        when omitted. The AWS.Tools modules themselves always come from the AWS.Tools.zip, not from a
        repository.
    .PARAMETER InstallerPath
        Where to save AWS.Tools.Installer when it is not available locally.
    .OUTPUTS
        None.
    .EXAMPLE
        Save-LambdaPSAWSToolsModule -Name 'AWS.Tools.S3' -Version '5.0.274' -Path /tmp/d -InstallerPath /tmp/d/.installer

        Downloads AWS.Tools.S3 and AWS.Tools.Common 5.0.274 into /tmp/d.
    #>
    [CmdletBinding()]
    [OutputType([void])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string[]]$Name,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Version,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$InstallerPath,

        [Parameter()]
        [AllowNull()]
        [string[]]$ModuleRepository
    )

    # 2.0.5 is the oldest 2.x release this module supports; 3.x may change the interface.
    $minimum = [version]'2.0.5'
    $ceiling = [version]'3.0'
    $isSupported = { param($m) $m.Version -ge $minimum -and $m.Version -lt $ceiling }

    $installer = @(Get-Module -ListAvailable -Name 'AWS.Tools.Installer') |
        Where-Object { & $isSupported $_ } | Sort-Object Version -Descending | Select-Object -First 1

    # One already saved by an earlier call in this run (another AWS.Tools version in the same set).
    if ($null -eq $installer) {
        $savedRoot = [System.IO.Path]::Combine($InstallerPath, 'AWS.Tools.Installer')
        if (Test-Path -LiteralPath $savedRoot -PathType Container) {
            $installer = @(Get-ChildItem -LiteralPath $savedRoot -Directory |
                    ForEach-Object { [System.IO.Path]::Combine($_.FullName, 'AWS.Tools.Installer.psd1') } |
                    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
                    ForEach-Object { Get-Module -ListAvailable -Name $_ }) |
                Where-Object { & $isSupported $_ } | Sort-Object Version -Descending | Select-Object -First 1
        }
    }

    if ($null -eq $installer) {
        $repository = if ($ModuleRepository) { $ModuleRepository } else { @('PSGallery') }
        $isDefault = -not $ModuleRepository
        $saved = Save-LambdaPSModuleFromRepository -Name 'AWS.Tools.Installer' -VersionRange '[2.0.5,3.0)' -Path $InstallerPath `
            -ModuleRepository $repository -AuthenticodeCheck:$IsWindows -InstallerDefault:$isDefault
        $manifest = [System.IO.Path]::Combine($InstallerPath, 'AWS.Tools.Installer', $saved.Version, 'AWS.Tools.Installer.psd1')
        $installer = @(Get-Module -ListAvailable -Name $manifest) | Select-Object -First 1
        Write-Verbose "AWS.Tools.Installer $($saved.Version) was not available locally; saved it to '$InstallerPath'."
    }

    try {
        $imported = Import-Module -Name $installer.Path -PassThru -ErrorAction Stop | Where-Object { $_.Name -eq 'AWS.Tools.Installer' } | Select-Object -First 1
    }
    catch {
        $message = "Could not import AWS.Tools.Installer $($installer.Version): $($_.Exception.Message) " +
            'AWS.Tools.Installer requires the PowerShellGet module to be present.'
        # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error
        # with its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
        # caller's -ErrorVariable naming this internal helper.
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new($message, $_.Exception),
            'AWSToolsInstallerImportFailed', [System.Management.Automation.ErrorCategory]::NotInstalled, $installer.Path)
    }

    # Invoke through the imported module, not by name: an AWS.Tools.Installer 1.x already loaded in
    # the session would otherwise win command resolution.
    $install = $imported.ExportedCommands['Install-AWSToolsModule']
    $null = [System.IO.Directory]::CreateDirectory($Path)
    $installParams = @{
        Name        = $Name
        Version     = $Version
        Path        = $Path
        Force       = $true
        Confirm     = $false
        ErrorAction = 'Stop'
    }
    try {
        $null = & $install @installParams 6>$null
    }
    catch {
        $message = "Could not download $($Name -join ', ') $Version with AWS.Tools.Installer: $($_.Exception.Message)"
        # TargetObject: the 'Name Version' key, or every key when several modules were requested.
        $keys = [string[]]@($Name | ForEach-Object { "$_ $Version" })
        $target = if ($keys.Count -eq 1) { $keys[0] } else { $keys }
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new($message, $_.Exception),
            'ModuleDownloadFailed', [System.Management.Automation.ErrorCategory]::ResourceUnavailable, $target)
    }
}
