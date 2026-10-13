function Get-LambdaPSRequiredModule {
    <#
    .SYNOPSIS
        Extracts the '#Requires -Modules' module specifications declared in a handler script.
    .DESCRIPTION
        Parses a PowerShell script with the language parser and returns the module specifications
        from its '#Requires -Modules' statements (the parser exposes them as
        ScriptRequirements.RequiredModules). This lets a customer declare a function's bundled modules
        inline in the handler, as an alternative to passing them through -IncludeModule.

        This helper only extracts; it does not enforce the exact-pin rule. Compress-LambdaPSFunctionPackage
        validates each returned specification with Get-LambdaPSModulePinError, which also decides
        -IncludeModule pins, so there is a single place that decides what an acceptable pin is. It returns an array (empty
        when the script declares no module requirements). It reads the file only and makes no AWS
        call.
    .PARAMETER Path
        The handler script (.ps1) to read the '#Requires -Modules' statements from.
    .OUTPUTS
        Microsoft.PowerShell.Commands.ModuleSpecification
    .EXAMPLE
        Get-LambdaPSRequiredModule -Path ./handler.ps1

        Returns the module specifications from the handler's '#Requires -Modules' lines, or an empty
        array when it declares none.
    #>
    [CmdletBinding()]
    [OutputType([Microsoft.PowerShell.Commands.ModuleSpecification])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error
        # with its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
        # caller's -ErrorVariable naming this internal helper.
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.IO.FileNotFoundException]::new("Handler script '$Path' was not found."),
            'HandlerScriptNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $Path)
    }

    $fullPath = Resolve-LambdaPSPath -Path $Path
    $tokens = $null
    $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($fullPath, [ref]$tokens, [ref]$errors)

    # ScriptRequirements is null when the script has no #Requires at all; RequiredModules is null when
    # it has #Requires statements but none name a module. Emit nothing in either case so the caller's
    # @(...) sees an empty set rather than a single $null element.
    if ($null -eq $ast.ScriptRequirements -or $null -eq $ast.ScriptRequirements.RequiredModules) {
        return
    }

    # AWSPowerShell is the Windows PowerShell (Desktop edition) module and does not run in PowerShell 7
    # on Lambda. AWSPowerShell.NetCore holds every AWS service (over 600 MB installed), which is more
    # than Lambda's 250 MB limit for a .zip package and its layers, and far more than a container image
    # needs. The AWS.Tools.* modules split the same cmdlets by service.
    $monolithic = @($ast.ScriptRequirements.RequiredModules | Where-Object { $_.Name -in 'AWSPowerShell', 'AWSPowerShell.NetCore' })
    if ($monolithic.Count -gt 0) {
        $reasons = [ordered]@{
            'AWSPowerShell'         = 'AWSPowerShell is the Windows PowerShell (Desktop edition) module and does not run in PowerShell 7 on Lambda'
            'AWSPowerShell.NetCore' = "AWSPowerShell.NetCore holds every AWS service (over 600 MB installed), which is more than Lambda's " +
                '250 MB limit for a .zip package and its layers, and far more than a container image needs'
        }
        # -contains ignores case, so 'awspowershell.netcore' gets the AWSPowerShell.NetCore reason.
        $why = @($reasons.Keys | Where-Object { @($monolithic.Name) -contains $_ } | ForEach-Object { $reasons[$_] }) -join '; '
        $message = "'$fullPath' requires $(@($monolithic | ForEach-Object { "'$($_.Name)'" }) -join ' and ') (#Requires -Modules), " +
            "which cannot be packaged for Lambda: $why. Require the AWS.Tools.* modules for the services you use instead " +
            "(for example @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '<version>' }), each pinned with a RequiredVersion."
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.ArgumentException]::new($message, 'Path'),
            'AWSPowerShellModuleNotSupported', [System.Management.Automation.ErrorCategory]::InvalidArgument, $monolithic[0].Name)
    }

    # Emit the specifications individually (no comma-wrap) so the caller's @(...) counts each module.
    return ([Microsoft.PowerShell.Commands.ModuleSpecification[]]@($ast.ScriptRequirements.RequiredModules))
}
