function Save-LambdaPSModuleFromRepository {
    <#
    .SYNOPSIS
        Downloads one module into a folder with PSResourceGet (Save-PSResource).
    .DESCRIPTION
        The mockable seam for every non-AWS.Tools module download, and for the AWS.Tools.Installer
        bootstrap. It calls Save-PSResource with -SkipDependencyCheck (the caller walks dependencies
        itself, so each is fetched at the version the walk settles on rather than whatever
        PSResourceGet would resolve) and -IncludeXml (so a module later moved into a module path is
        still recognised by Get-InstalledPSResource). The module lands in <Path>/<Name>/<Version>/.

        Repository trust is left to the user's PSResourceGet configuration: -TrustRepository is not
        passed, so an untrusted repository (PSGallery by default) prompts as it normally would, and
        fails in a non-interactive session. Repositories are searched in their registered priority
        order, so a private repository registered ahead of PSGallery is used automatically.

        PowerShellGet is deliberately not a fallback. A missing Save-PSResource is a terminating
        ModuleInstallerNotFound error; a failed download is a terminating ModuleDownloadFailed error
        wrapping the PSResourceGet exception.
    .PARAMETER Name
        The module name.
    .PARAMETER VersionRange
        A NuGet version range, for example '[1.2.3]' for exactly 1.2.3. A bare '1.2.3' would mean 1.2.3
        or later, which this tooling never asks for.
    .PARAMETER ModuleRepository
        The PSResourceGet repositories to download from, in order. When omitted, every registered
        repository is searched in priority order. Names, or patterns whose only wildcard is '*', each
        of which must match a registered repository, or the download stops with
        ModuleRepositoryNotFound (Assert-LambdaPSModuleRepository).
    .PARAMETER Path
        The folder to save into. Created when missing (Save-PSResource requires it to exist), and
        removed again when this call created it and the download fails.
    .PARAMETER InstallerDefault
        -ModuleRepository is the AWS.Tools.Installer default (PSGallery), not names the user passed;
        a ModuleRepositoryNotFound message then says so.
    .PARAMETER AuthenticodeCheck
        Pass -AuthenticodeCheck to Save-PSResource, so a package whose files are not validly signed
        is refused. Save-PSResource supports it on Windows only.
    .OUTPUTS
        System.Management.Automation.PSCustomObject
    .EXAMPLE
        Save-LambdaPSModuleFromRepository -Name 'PSFramework' -VersionRange '[1.12.346]' -Path /tmp/downloads

        Saves PSFramework 1.12.346 to /tmp/downloads/PSFramework/1.12.346 and returns its Name, Version
        and Repository.
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Name,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$VersionRange,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter()]
        [AllowNull()]
        [string[]]$ModuleRepository,

        [Parameter()]
        [switch]$AuthenticodeCheck,

        [Parameter()]
        [switch]$InstallerDefault
    )

    if ($null -eq (Get-Command 'Save-PSResource' -ErrorAction SilentlyContinue)) {
        $message = "Downloading module '$Name' requires the Microsoft.PowerShell.PSResourceGet module " +
            "(Save-PSResource), which ships with PowerShell 7.4 and later but was not found. Install " +
            "PSResourceGet, or install the module locally so it does not have to be downloaded."
        # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error
        # with its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
        # caller's -ErrorVariable naming this internal helper.
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.Management.Automation.CommandNotFoundException]::new($message),
            'ModuleInstallerNotFound', [System.Management.Automation.ErrorCategory]::NotInstalled, $Name)
    }

    if ($ModuleRepository) {
        Assert-LambdaPSModuleRepository -Name $ModuleRepository -InstallerDefault:$InstallerDefault
    }

    # A -Path folder this call creates is removed again when the download fails.
    $createdPath = -not (Test-Path -LiteralPath $Path)
    $removeCreatedPath = {
        if ($createdPath) { Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue -WhatIf:$false -Confirm:$false }
    }
    $from = if (-not $ModuleRepository) { 'the registered repositories' }
    elseif (@($ModuleRepository).Count -eq 1) { "repository '$($ModuleRepository)'" }
    else { "repositories $(@($ModuleRepository | ForEach-Object { "'$_'" }) -join ', ')" }
    $null = [System.IO.Directory]::CreateDirectory($Path)
    # A ModuleDownloadFailed error targets 'Name Version', the key the rest of the module uses; an exact
    # '[1.2.3]' range is reported as its version.
    $target = "$Name $($VersionRange -replace '^\[([^,\]]+)\]$', '$1')"

    $saveParams = @{
        Name                = $Name
        Version             = $VersionRange
        Path                = $Path
        SkipDependencyCheck = $true
        IncludeXml          = $true
        PassThru            = $true
        Confirm             = $false
        ErrorAction         = 'Stop'
    }
    # Without -ModuleRepository, PSResourceGet searches every registered repository in priority order, so
    # a same-named package in a higher-priority feed would win. Naming the repositories pins that.
    if ($ModuleRepository) { $saveParams.Repository = $ModuleRepository }
    if ($AuthenticodeCheck) { $saveParams.AuthenticodeCheck = $true }
    try {
        $saved = @(Save-PSResource @saveParams) | Where-Object { $_.Name -eq $Name } | Select-Object -First 1
    }
    catch {
        & $removeCreatedPath
        $message = "Could not download module '$Name' $VersionRange from $from with PSResourceGet: $($_.Exception.Message) " +
            'If the repository is untrusted and this session is non-interactive, trust it with ' +
            'Set-PSResourceRepository -Name <repository> -Trusted, or install the module locally so it does ' +
            'not have to be downloaded.'
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new($message, $_.Exception),
            'ModuleDownloadFailed', [System.Management.Automation.ErrorCategory]::ResourceUnavailable, $target)
    }

    if ($null -eq $saved) {
        & $removeCreatedPath
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new("PSResourceGet reported no module '$Name' $VersionRange from $from after saving it."),
            'ModuleDownloadFailed', [System.Management.Automation.ErrorCategory]::ResourceUnavailable, $target)
    }

    [PSCustomObject]@{
        Name       = $saved.Name
        Version    = [string]$saved.Version
        Repository = [string]$saved.Repository
    }
}
