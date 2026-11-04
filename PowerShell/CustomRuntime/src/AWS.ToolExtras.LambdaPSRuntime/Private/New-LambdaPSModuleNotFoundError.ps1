function New-LambdaPSModuleNotFoundError {
    <#
    .SYNOPSIS
        Builds the ModuleNotFoundLocally error for modules that are not available on this machine.
    .DESCRIPTION
        The ModuleNotFoundLocally error that Get-LambdaPSModuleClosure -RequireAll returns, naming
        every missing module at once.

        This helper does not throw. It returns a ready-to-throw ErrorRecord (ErrorId
        'ModuleNotFoundLocally', category ObjectNotFound); the caller invokes
        $PSCmdlet.ThrowTerminatingError on it so the error is attributed to the caller's cmdlet.
    .PARAMETER Missing
        The Missing entries of a Get-LambdaPSModuleClosure result (objects with Name, Version, Key).
    .PARAMETER SourceModulesPath
        The function source's own modules/ folder, which is never searched. A missing module whose
        version is in it is named in the ModuleNotFoundLocally message.
    .OUTPUTS
        System.Management.Automation.ErrorRecord
    .EXAMPLE
        New-LambdaPSModuleNotFoundError -Missing $closure.Missing

        Returns one error naming every module that is not installed.
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.ErrorRecord])]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Builds an in-memory ErrorRecord; changes no state.')]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [object[]]$Missing,

        [Parameter()]
        [string]$SourceModulesPath
    )

    $names = ($Missing | ForEach-Object { "'$($_.Name)' $($_.Version)" }) -join ', '
    $subject, $them = if ($Missing.Count -eq 1) { "Module $names is", 'it' } else { "Modules $names are", 'them' }
    # Not "add them to the project's modules folder": New-LambdaPSRuntimeDeployment replaces its
    # project on every run, so modules added there would not last.
    $message = "$subject not available on this machine. Rerun with -MissingModuleAction Save " +
        "(the default) or Install to download $them, or save $them with Save-LambdaPSModule into a " +
        'folder you add to PSModulePath.'
    # A module kept in the source's modules/ folder is not found there: say so, or the user is left
    # wondering why the copy they ship does not count.
    $shipped = @(if (-not [string]::IsNullOrWhiteSpace($SourceModulesPath)) {
            Get-ChildItem -LiteralPath $SourceModulesPath -Directory -ErrorAction SilentlyContinue
        })
    foreach ($entry in $Missing) {
        foreach ($folder in @($shipped | Where-Object Name -EQ $entry.Name)) {
            if (Test-Path -LiteralPath ([System.IO.Path]::Combine($folder.FullName, $entry.Version))) {
                $message += " '$($entry.Name)' $($entry.Version) is in '$($folder.FullName)', which is packaged as it is " +
                    'but not searched for the modules to stage.'
            }
        }
    }
    # TargetObject: the 'Name Version' key, or every key when several are missing.
    $keys = [string[]]@($Missing | ForEach-Object { $_.Key })
    $target = if ($keys.Count -eq 1) { $keys[0] } else { $keys }
    [System.Management.Automation.ErrorRecord]::new(
        [System.IO.FileNotFoundException]::new($message),
        'ModuleNotFoundLocally', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $target)
}
