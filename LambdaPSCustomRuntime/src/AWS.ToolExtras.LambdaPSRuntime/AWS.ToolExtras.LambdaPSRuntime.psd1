@{
    RootModule        = 'AWS.ToolExtras.LambdaPSRuntime.psm1'
    ModuleVersion     = '0.1.0'
    GUID              = '1d3067bc-4650-4c14-b36c-11563ec12158'
    Author            = 'AWS Tools for PowerShell'
    CompanyName       = 'Amazon Web Services'
    Copyright         = '(c) Amazon.com, Inc. or its affiliates. All Rights Reserved.'
    Description       = 'Value-add PowerShell tooling for running PowerShell on AWS Lambda with customer-managed runtimes and custom runtimes. Scaffolds, validates, builds, and packages runtime and function projects, and provides one-command deployments (New-LambdaPSRuntimeDeployment for a customer-managed runtime, New-LambdaPSFunctionDeployment for functions). AWS is reached by invoking the AWS.Tools cmdlets (LM*, LMC*, ECR*, STS) and by shelling out to a container CLI; no AWS SDK assembly is bound directly.'
    PowerShellVersion = '7.4'
    CompatiblePSEditions = @('Core')

    # Every AWS-calling seam needs these four loaded: Get-LambdaPSClientConfig constructs the
    # per-service SDK config TYPES (Amazon.Lambda.AmazonLambdaConfig, ...), and a type reference does
    # not trigger cmdlet auto-loading the way a command call does. Without this the first AWS call in
    # a clean session fails with "Unable to find type [Amazon.Lambda.AmazonLambdaConfig]".
    #
    # ModuleVersion is a MINIMUM, not an exact pin, so consumers can take AWS.Tools patches and newer
    # releases without this module re-pinning. An exact RequiredVersion here would recreate the
    # unified-versioning lockstep that shipping outside the AWS.Tools.* family exists to avoid.
    #
    # The floors are the first AWS.Tools builds with the cmdlets and parameters this module uses:
    # AWS.Tools.LambdaCore's Lambda customer-managed runtime cmdlets (New-LMCRuntime,
    # New-LMCRuntimeVersion, New-LMCRuntimeAlias, ...) and AWS.Tools.Lambda's runtime binding
    # (-RuntimeConfig_RuntimeArn, -RuntimeConfig_RuntimeAlias). Every AWS.Tools module requires one
    # exact AWS.Tools.Common build, so the four load together only at the same version, and
    # AWS.Tools.ECR and AWS.Tools.SecurityToken have the same floor. A manifest cannot express
    # "exports command X", so an AWS.Tools build at or above the floor that lacks one of them fails at
    # the point of call.
    RequiredModules   = @(
        @{ ModuleName = 'AWS.Tools.Lambda'; ModuleVersion = '5.0.1001'; GUID = '240bbccc-f270-44af-8e80-bb4ebe8614ab' }
        @{ ModuleName = 'AWS.Tools.LambdaCore'; ModuleVersion = '5.0.1001'; GUID = '76c3e742-7ab9-4d8d-94e8-902df9225ae3' }
        @{ ModuleName = 'AWS.Tools.ECR'; ModuleVersion = '5.0.1001'; GUID = '58bb5011-59c3-41ea-ba12-45c3813db939' }
        @{ ModuleName = 'AWS.Tools.SecurityToken'; ModuleVersion = '5.0.1001'; GUID = 'b760f2e8-291f-4df4-bd69-3a615b47c8d4' }
    )

    FunctionsToExport = @(
        'Build-LambdaPSRuntimeImage'
        'Build-LambdaPSRuntimePackage'
        'Compress-LambdaPSFunctionPackage'
        'Invoke-LambdaPSFunction'
        'New-LambdaPSFunctionDeployment'
        'New-LambdaPSFunctionProject'
        'New-LambdaPSRuntimeDeployment'
        'New-LambdaPSRuntimeProject'
        'Save-LambdaPSModule'
        'Test-LambdaPSContainerRuntime'
        'Test-LambdaPSRuntimeProject'
    )
    CmdletsToExport   = @()
    VariablesToExport = @()
    AliasesToExport   = @()
    PrivateData       = @{
        PSData = @{
            Tags         = @('AWS', 'Lambda', 'PowerShell', 'CustomRuntime', 'CustomerManagedRuntime', 'Scaffolding')
            LicenseUri   = 'https://aws.amazon.com/apache-2-0/'
            ProjectUri   = 'https://github.com/aws/aws-lambda-dotnet/tree/HEAD/LambdaPSCustomRuntime'
            ReleaseNotes = 'Initial release.'
        }
    }
}
