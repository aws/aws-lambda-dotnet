$script:privatePath = Join-Path -Path $PSScriptRoot -ChildPath 'Private'
$script:publicPath = Join-Path -Path $PSScriptRoot -ChildPath 'Public'
$script:templatesPath = Join-Path -Path $PSScriptRoot -ChildPath 'Templates'


# Add private functions
# Hardcoded file names to ensure they load in an order to support pre-requisites
$files = @(
    '_Constants.ps1',
    '_BlueprintFunctions.ps1',
    '_ArgumentCompleters.ps1',
    '_DeploymentFunctions.ps1',
    '_ProjectCreationFunctions.ps1'
)
foreach ($file in $files)
{
    $filePath = Join-Path -Path $privatePath -ChildPath $file
    . $filePath
}

# Add public functions
foreach ($file in (Get-ChildItem -Path $publicPath -Filter '*.ps1')) {
    . $file.FullName
}
