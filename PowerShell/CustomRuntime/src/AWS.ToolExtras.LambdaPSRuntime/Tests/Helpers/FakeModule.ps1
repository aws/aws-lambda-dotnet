# Nothing in the module-acquisition tests touches the network or the real user module folder: every
# download seam is mocked, and a mocked download writes a fake <Name>/<Version>/<Name>.psd1 the way
# the real tools do. Module names and versions are fictitious so a real installed module can never
# satisfy a lookup by accident.

# Writes a minimal module laid out as Save-PSResource / Install-AWSToolsModule -Path leave it.
function New-FakeModule {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Test helper: it writes only under the folder a test passes in.')]
    param([string]$Root, [string]$Name, [string]$Version, [string]$Requires = '@()', [switch]$WithHelpAndSymbols)
    $dir = [System.IO.Path]::Combine($Root, $Name, $Version)
    $null = New-Item -ItemType Directory -Path $dir -Force
    # -WithHelpAndSymbols adds what an AWS.Tools module ships beside its manifest: cmdlet help (.xml),
    # debug symbols (.pdb) and format data (.ps1xml) that the manifest loads.
    $formats = '@()'
    if ($WithHelpAndSymbols) {
        Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, "$Name.dll-Help.xml")) -Value ('<helpItems>' + ('x' * 8192) + '</helpItems>')
        Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, "$Name.pdb")) -Value ('x' * 8192)
        Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, "$Name.Format.ps1xml")) -Value '<Configuration><ViewDefinitions /></Configuration>'
        $formats = "@('$Name.Format.ps1xml')"
    }
    Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, "$Name.psd1")) -Value (
        "@{ ModuleVersion = '$Version'; GUID = '$([guid]::NewGuid())'; RequiredModules = $Requires; FormatsToProcess = $formats }")
    $dir
}
