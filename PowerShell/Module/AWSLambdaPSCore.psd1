#
# Module manifest for module 'AWSLambdaPSCore'
#

@{

  # Script module or binary module file associated with this manifest.
  RootModule = 'AWSLambdaPSCore.psm1'

  CompatiblePSEditions = @('Core')

  # Version number of this module.
  ModuleVersion = '1.0.0.0'

  # ID used to uniquely identify this module
  GUID = '79B7BFF6-B031-4D8D-B17C-E7E11F1A741F'

  # Author of this module
  Author = 'Amazon.com, Inc'

  # Company or vendor of this module
  CompanyName = 'Amazon.com, Inc'

  # Copyright statement for this module
  Copyright = 'Copyright 2012-2018 Amazon.com, Inc. or its affiliates. All Rights Reserved.'

  # Description of the functionality provided by this module
  Description = 'The AWS Lambda Tools for Powershell can be used to create and deploy AWS Lambda functions written in PowerShell.'

  # Minimum version of the PowerShell engine required by this module
  PowerShellVersion = '6.0'

  # Name of the PowerShell host required by this module
  # PowerShellHostName = ''

  # Minimum version of the PowerShell host required by this module
  # PowerShellHostVersion = ''

  # Minimum version of the .NET Framework required by this module
  # DotNetFrameworkVersion = ''

  # Minimum version of the common language runtime (CLR) required by this module
  # CLRVersion = ''

  # Processor architecture (None, X86, Amd64, IA64) required by this module
  # ProcessorArchitecture = ''

  # Modules that must be imported into the global environment prior to importing this module
  # RequiredModules = @()

  # Assemblies that must be loaded prior to importing this module.
  # We list the SDK assemblies for the convenience of PowerShell v2 users
  # who want to work with generic types when the type parameter is in an
  # external SDK assembly.
  # RequiredAssemblies = @()

  # Script files (.ps1) that are run in the caller's environment prior to importing this module
  # ScriptsToProcess = @()

  # Type files (.ps1xml) to be loaded when importing this module
  # TypesToProcess = @()

  # Format files (.ps1xml) to be loaded when importing this module
  # FormatsToProcess = @()

  # Modules to import as nested modules of the module specified in ModuleToProcess
  # NestedModules = @()

  # Functions to export from this module
  FunctionsToExport = @(
    'Get-AWSPowerShellLambdaTemplate'
    'New-AWSPowerShellLambda'
    'New-AWSPowerShellLambdaPackage'
    'Publish-AWSPowerShellLambda'
  )

  # Cmdlets to export from this module
  # CmdletsToExport = ''

  # Variables to export from this module
  # VariablesToExport = ''

  # Aliases to export from this module
  # AliasesToExport = @()

  # List of all modules packaged with this module
  # ModuleList = @()

  # List of all files packaged with this module
  # FileList = @()
}
