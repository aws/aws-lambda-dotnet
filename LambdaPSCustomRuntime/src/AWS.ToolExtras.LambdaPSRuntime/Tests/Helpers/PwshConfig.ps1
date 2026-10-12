# The module's pinned pwsh releases (Config/pwsh.psd1), so tests follow the data rather than repeat
# it. Dot-source it from BeforeDiscovery to build -ForEach cases, or from BeforeAll to use in a test.

$script:pwshConfig = Import-PowerShellDataFile -Path ([System.IO.Path]::Combine((Split-Path -Parent (Split-Path -Parent $PSScriptRoot)), 'Config', 'pwsh.psd1'))
