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
        [object[]]$Missing
    )

    $names = ($Missing | ForEach-Object { "'$($_.Name)' $($_.Version)" }) -join ', '
    $subject = if ($Missing.Count -eq 1) { "Module $names is" } else { "Modules $names are" }
    # Not "add them to the project's modules folder": New-LambdaPSRuntimeDeployment replaces its
    # project on every run, so modules added there would not last.
    $message = "$subject not available on this machine. Rerun with -MissingModuleAction Save " +
        '(the default) or Install to download them, or save them with Save-LambdaPSModule into a ' +
        'folder you add to PSModulePath.'
    # TargetObject: the 'Name Version' key, or every key when several are missing.
    $keys = [string[]]@($Missing | ForEach-Object { $_.Key })
    $target = if ($keys.Count -eq 1) { $keys[0] } else { $keys }
    [System.Management.Automation.ErrorRecord]::new(
        [System.IO.FileNotFoundException]::new($message),
        'ModuleNotFoundLocally', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $target)
}
