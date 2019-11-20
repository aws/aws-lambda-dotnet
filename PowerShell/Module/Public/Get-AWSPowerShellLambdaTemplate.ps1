<#
    .SYNOPSIS
    Lists the available PowerShell script templates.

    .DESCRIPTION
    Lists the available PowerShell script templates. The templates are used with the New-AWSPowerShellLambda
    cmdlet's -Template parameter to create an initial PowerShell script or project to get started with using
    PowerShell in AWS Lambda.

    .PARAMETER InstalledOnly
    Overrides default behavior to obtain the list of available templates from online sources which will include
    new and updated templates released periodically. If the switch is specified only those templates that were
    installed as part of the AWSLambdaPowerShell module are returned.

    .EXAMPLE
    Get-AWSPowerShellLambdaTemplate

    Outputs the latest available template content from online and outputs the template details to the pipeline.
    If the online content cannot be reached the cmdlet will fall back to outputting the details of templates
    installed with the module.

    .EXAMPLE
    Get-AWSPowerShellLambdaTemplate -InstalledOnly

    Outputs the details of the available templates from those installed with this module to the pipeline.
#>
function Get-AWSPowerShellLambdaTemplate
{
    [CmdletBinding()]
    param
    (
        [switch]$InstalledOnly
    )

    if ($InstalledOnly)
    {
        $manifest = _loadBlueprintManifest
    }
    else
    {
        $manifest = _loadBlueprintManifest -Online
    }

    foreach ($b in $manifest.blueprints)
    {
        [PSCustomObject]@{
            Template    = $b.name
            Description = $b.description
        }
    }
}