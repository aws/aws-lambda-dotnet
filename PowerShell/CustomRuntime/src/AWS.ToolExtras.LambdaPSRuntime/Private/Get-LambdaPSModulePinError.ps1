function Get-LambdaPSModulePinError {
    <#
    .SYNOPSIS
        Validates that a module specification pins an exact RequiredVersion.
    .DESCRIPTION
        The single source of the exact-pin rule, applied to every module set by
        Get-LambdaPSModuleClosure and, for '#Requires -Modules' (so the error can name the script),
        by the packagers (Compress-LambdaPSFunctionPackage and Build-LambdaPSRuntimePackage). A specification is acceptable only when it pins an exact
        RequiredVersion: the RequiredVersion property must be set AND both the minimum-version
        property (Version) and the MaximumVersion property must be null. Minimum-version specs and
        version-range specs are rejected; this tooling never resolves 'latest'.

        This helper does not throw. It returns a ready-to-throw ErrorRecord (ErrorId
        'ModuleVersionRequired', category InvalidArgument) when the spec is unpinned, or $null when
        the spec is acceptable. The caller invokes $PSCmdlet.ThrowTerminatingError on the returned
        record so the terminating error is attributed to the caller's cmdlet, not to this helper.
    .PARAMETER Specification
        The module specification to validate.
    .PARAMETER DeclaredIn
        The script whose #Requires -Modules declared the module, when it came from one. The message
        then names that script and shows the #Requires form, since the caller never typed the spec.
    .OUTPUTS
        System.Management.Automation.ErrorRecord
    .EXAMPLE
        $err = Get-LambdaPSModulePinError -Specification @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' }

        Returns $null because the spec pins an exact RequiredVersion.
    .EXAMPLE
        $err = Get-LambdaPSModulePinError -Specification @{ ModuleName = 'AWS.Tools.S3'; ModuleVersion = '5.0.274' }
        if ($null -ne $err) { $PSCmdlet.ThrowTerminatingError($err) }

        Returns a ModuleVersionRequired ErrorRecord because ModuleVersion is a minimum, not an exact pin.
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.ErrorRecord])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [Microsoft.PowerShell.Commands.ModuleSpecification]$Specification,

        [Parameter()]
        [string]$DeclaredIn
    )

    # RequiredVersion is the exact pin. Version is the MINIMUM-version property; MaximumVersion is
    # the range ceiling. An exact pin means RequiredVersion is set and neither of the other two is.
    $isExactPin = ($null -ne $Specification.RequiredVersion) -and
        ($null -eq $Specification.Version) -and
        ($null -eq $Specification.MaximumVersion)

    if ($isExactPin) {
        return $null
    }

    # The example pins the version the spec gave (its minimum, or its maximum), or a placeholder.
    $typed = @($Specification.RequiredVersion, $Specification.Version, $Specification.MaximumVersion |
            Where-Object { $_ }) | Select-Object -First 1
    $exampleVersion = if ($typed) { [string]$typed } else { '<version>' }
    $example = "@{ ModuleName = '$($Specification.Name)'; RequiredVersion = '$exampleVersion' }"
    $message = if ($DeclaredIn) {
        "Module '$($Specification.Name)', declared with #Requires -Modules in '$DeclaredIn', must pin an exact " +
        "RequiredVersion (for example #Requires -Modules $example). This tooling never resolves 'latest'; " +
        'minimum-version and version-range specs are rejected.'
    }
    else {
        "Module '$($Specification.Name)' must pin an exact RequiredVersion (for example $example). " +
        "This tooling never resolves 'latest'; minimum-version and version-range specs are rejected."
    }
    return [System.Management.Automation.ErrorRecord]::new(
        [System.ArgumentException]::new($message, 'Module'),
        'ModuleVersionRequired',
        [System.Management.Automation.ErrorCategory]::InvalidArgument,
        $Specification)
}
