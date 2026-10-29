function New-LambdaPSDownloadedModule {
    <#
    .SYNOPSIS
        Builds the DownloadedModule result object for a module written to a download folder.
    .DESCRIPTION
        The one place the AWS.ToolExtras.LambdaPSRuntime.DownloadedModule shape is defined, used by
        Save-LambdaPSMissingModule (Saved and Installed modules) and Save-LambdaPSModule (Copied
        modules). Version is a [version], like StagedModule's, so results sort and compare as versions.
        Writes nothing.
    .PARAMETER Name
        The module name.
    .PARAMETER Version
        The module version.
    .PARAMETER Source
        AWS.Tools.Installer, PSResourceGet, or Local.
    .PARAMETER Repository
        The PSResourceGet repository the module came from, or $null.
    .PARAMETER Path
        The folder the module version is in.
    .PARAMETER Action
        Saved, Installed or Copied.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.DownloadedModule
    .EXAMPLE
        New-LambdaPSDownloadedModule -Name 'AWS.Tools.S3' -Version '5.0.274' -Source 'AWS.Tools.Installer' -Path ./mods/AWS.Tools.S3/5.0.274 -Action Saved

        Returns the result object for AWS.Tools.S3 5.0.274 saved into ./mods.
    #>
    [CmdletBinding()]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.DownloadedModule')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Builds an in-memory object; it changes no state.')]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Name,

        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [version]$Version,

        [Parameter(Mandatory)]
        [ValidateSet('AWS.Tools.Installer', 'PSResourceGet', 'Local')]
        [string]$Source,

        [Parameter()]
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Repository,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter(Mandatory)]
        [ValidateSet('Saved', 'Installed', 'Copied')]
        [string]$Action
    )

    [PSCustomObject]@{
        PSTypeName = 'AWS.ToolExtras.LambdaPSRuntime.DownloadedModule'
        Name       = $Name
        Version    = $Version
        Source     = $Source
        Repository = if ($Repository) { $Repository } else { $null }
        Path       = $Path
        Action     = $Action
    }
}
