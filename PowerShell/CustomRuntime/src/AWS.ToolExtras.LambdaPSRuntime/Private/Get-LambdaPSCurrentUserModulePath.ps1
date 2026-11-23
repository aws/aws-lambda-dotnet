function Get-LambdaPSCurrentUserModulePath {
    <#
    .SYNOPSIS
        Returns the CurrentUser module folder that PSResourceGet and Install-Module install into.
    .DESCRIPTION
        <Documents>/PowerShell/Modules on Windows; elsewhere, including macOS,
        $XDG_DATA_HOME/powershell/Modules, or ~/.local/share/powershell/Modules when XDG_DATA_HOME is
        not set -- the folder PowerShell itself puts on the PSModulePath. (.NET's LocalApplicationData
        is ~/Library/Application Support on macOS, which PowerShell does not search.) This is where
        -MissingModuleAction Install puts downloaded modules. It is a seam so tests never write to the
        real user profile. Reads nothing and writes nothing.
    .OUTPUTS
        System.String
    .EXAMPLE
        Get-LambdaPSCurrentUserModulePath

        Returns, for example, '/home/me/.local/share/powershell/Modules'.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    if ($IsWindows) {
        return [System.IO.Path]::Combine([Environment]::GetFolderPath('MyDocuments'), 'PowerShell', 'Modules')
    }
    $dataHome = $env:XDG_DATA_HOME
    if ([string]::IsNullOrWhiteSpace($dataHome)) {
        $dataHome = [System.IO.Path]::Combine([Environment]::GetFolderPath('UserProfile'), '.local', 'share')
    }
    return [System.IO.Path]::Combine($dataHome, 'powershell', 'Modules')
}
