function Get-LambdaPSCredentialArgumentText {
    <#
    .SYNOPSIS
        Returns the credential arguments to add to an AWS.Tools command suggested in an error message.
    .DESCRIPTION
        Several errors suggest an AWS.Tools command to run next (check a version, move an alias). Run
        without the caller's credential parameters, that command would use the shell's default
        credentials, possibly in another account. This returns the -ProfileName the caller passed, their
        -ProfileLocation as an absolute path, and a -Credential placeholder when they passed an
        AWSCredentials object (which cannot be written out), each with a leading space; or an empty
        string when they passed none.
    .PARAMETER CredentialParameter
        The credential parameters from Get-LambdaPSCredentialParameter.
    .OUTPUTS
        System.String
    .EXAMPLE
        "Get-LMCRuntimeVersion -RuntimeName rt -Name v1 -Region $Region$(Get-LambdaPSCredentialArgumentText -CredentialParameter $credentialParameter)"

        Suggests the command with -ProfileName dev when the deploy ran with -ProfileName dev.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter()]
        [AllowNull()]
        [hashtable]$CredentialParameter
    )

    if ($null -eq $CredentialParameter) { return '' }
    $text = ''
    foreach ($name in 'ProfileName', 'ProfileLocation') {
        if ($CredentialParameter.ContainsKey($name)) {
            $text += " -$name '$([System.Management.Automation.Language.CodeGeneration]::EscapeSingleQuotedStringContent([string]$CredentialParameter[$name]))'"
        }
    }
    if ($CredentialParameter.ContainsKey('Credential')) { $text += ' -Credential <the credentials you deployed with>' }
    return $text
}
