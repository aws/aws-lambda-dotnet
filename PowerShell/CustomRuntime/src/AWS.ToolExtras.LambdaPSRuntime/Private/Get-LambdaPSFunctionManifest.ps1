function Get-LambdaPSFunctionManifest {
    <#
    .SYNOPSIS
        Reads the manifest.json that New-LambdaPSFunctionProject writes, or returns $null.
    .DESCRIPTION
        The single reader of a function project's manifest.json. A folder is treated as a scaffolded
        function project only when its top-level manifest.json parses and has the scaffolder's shape
        (HandlerString and ScriptFile), so a manifest.json of the customer's own, such as an app
        config file, is never mistaken for one: for that, this returns $null (with a Verbose note),
        and the file is ordinary payload. A manifest.json that is not valid JSON is a terminating
        InvalidProjectManifest error that names the file, as an invalid lambdaps-runtime.json is for
        Get-LambdaPSProjectMarker: carrying on would package and deploy a scaffolded project without
        the handler, runtime alias and architecture it records.
    .PARAMETER Path
        The function source folder. A file, or a folder without a manifest.json, returns $null.
    .OUTPUTS
        System.Management.Automation.PSCustomObject

        ScriptFile, RuntimeAlias and Architecture as recorded (each may be $null).
    .EXAMPLE
        $manifest = Get-LambdaPSFunctionManifest -Path ./fn

        Returns the recorded handler script, runtime alias and architecture of ./fn, or $null when
        ./fn is not a scaffolded function project.
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path
    )

    # Resolved against the PowerShell location, as Test-Path does, so ReadAllText reads the same file.
    $manifestPath = [System.IO.Path]::Combine((Resolve-LambdaPSPath -Path $Path), 'manifest.json')
    if (-not (Test-Path -LiteralPath $Path -PathType Container) -or -not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        return $null
    }

    try {
        $manifest = [System.IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error with
        # its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
        # caller's -ErrorVariable naming this internal helper.
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.IO.InvalidDataException]::new(
                "The function manifest '$manifestPath' is not valid JSON: $($_.Exception.Message)", $_.Exception),
            'InvalidProjectManifest', [System.Management.Automation.ErrorCategory]::InvalidData, $manifestPath)
    }
    $names = if ($manifest -is [System.Management.Automation.PSCustomObject]) { @($manifest.PSObject.Properties.Name) } else { @() }
    if ($names -notcontains 'HandlerString' -or $names -notcontains 'ScriptFile') {
        Write-Verbose "manifest.json at '$manifestPath' was not written by New-LambdaPSFunctionProject, so it is treated as an ordinary file."
        return $null
    }

    [PSCustomObject]@{
        ScriptFile   = if ([string]::IsNullOrWhiteSpace($manifest.ScriptFile)) { $null } else { [string]$manifest.ScriptFile }
        RuntimeAlias = if ([string]::IsNullOrWhiteSpace($manifest.RuntimeAlias)) { $null } else { [string]$manifest.RuntimeAlias }
        Architecture = if ([string]::IsNullOrWhiteSpace($manifest.Architecture)) { $null } else { [string]$manifest.Architecture }
    }
}
