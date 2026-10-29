function Get-LambdaPSRequiredModule {
    <#
    .SYNOPSIS
        Extracts the '#Requires -Modules' module specifications declared in a handler script.
    .DESCRIPTION
        Parses a PowerShell script with the language parser and returns the module specifications
        from its '#Requires -Modules' statements (the parser exposes them as
        ScriptRequirements.RequiredModules). This lets a customer declare a function's bundled modules
        inline in the handler, as an alternative to passing them through -IncludeModule.

        It does not enforce the exact-pin rule: the packagers (Compress-LambdaPSFunctionPackage and
        Build-LambdaPSRuntimePackage) validate each returned specification with
        Get-LambdaPSModulePinError, which also decides -IncludeModule pins, so there is a single place
        that decides what an acceptable pin is. It returns an array (empty when the script declares no
        module requirements). It reads the file only and makes no AWS call.

        It stops with HandlerScriptNotFound when -Path is not a file, and with
        AWSPowerShellModuleNotSupported when the script requires AWSPowerShell or AWSPowerShell.NetCore
        (Get-LambdaPSAWSPowerShellModuleReason gives each module's reason).
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

    # AWSPowerShell and AWSPowerShell.NetCore are refused; Get-LambdaPSAWSPowerShellModuleReason gives
    # each module's reason.
    $monolithic = Get-LambdaPSAWSPowerShellModuleReason -Name @($ast.ScriptRequirements.RequiredModules.Name)
    if ($null -ne $monolithic) {
        $message = "'$fullPath' requires $(@($monolithic.Names | ForEach-Object { "'$_'" }) -join ' and ') (#Requires -Modules), " +
            "which cannot be packaged for Lambda: $($monolithic.Reason). Require the AWS.Tools.* modules for the services you " +
            "use instead (for example @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '<version>' }), each pinned with a " +
            'RequiredVersion.'
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.ArgumentException]::new($message, 'Path'),
            'AWSPowerShellModuleNotSupported', [System.Management.Automation.ErrorCategory]::InvalidArgument, $monolithic.Names[0])
    }

    # Emit the specifications individually (no comma-wrap) so the caller's @(...) counts each module.
    return ([Microsoft.PowerShell.Commands.ModuleSpecification[]]@($ast.ScriptRequirements.RequiredModules))
}
