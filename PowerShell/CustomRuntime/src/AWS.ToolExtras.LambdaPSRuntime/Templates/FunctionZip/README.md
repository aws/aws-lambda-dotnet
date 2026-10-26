# PowerShell Lambda function package

The handler is `handler.ps1`. It receives `param($LambdaInput, $LambdaContext)`.

## Deploy

    New-LambdaPSFunctionDeployment -FunctionName <name> -ProjectPath . -RuntimeName <runtime-name> `
        -Role <exec-role-arn> -Region <region>

This packages the project, takes the handler (and the runtime alias and architecture, unless you pass
them) from manifest.json, and checks the function's architecture against the runtime alias.

## Modules

Declare modules in the handler with `#Requires -Modules` and an exact `RequiredVersion`, or pass
`-IncludeModule`. They are staged into the package with their dependencies, and downloaded if they
are not installed. Modules under ./modules are packaged as well, and take precedence over the
runtime's modules. To fetch modules ahead of time:

    Save-LambdaPSModule -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' } -Path ./modules

## Package and create the function yourself

    Compress-LambdaPSFunctionPackage -Path . -OutFile ../function.zip
    $function = @{
        FunctionName               = '<name>'
        Handler                    = 'handler.ps1'
        Runtime                    = 'custom'
        Architecture               = '__README_ARCHITECTURE__'
        Role                       = '<exec-role-arn>'
        Timeout                    = __TIMEOUT__
        MemorySize                 = __MEMORY_SIZE__
        Code_ZipFile               = [System.IO.File]::ReadAllBytes((Resolve-Path ../function.zip))
        RuntimeConfig_RuntimeArn   = '<runtime-arn>'
        RuntimeConfig_RuntimeAlias = '__RUNTIME_ALIAS__'
        Region                     = '<region>'
    }
    Publish-LMFunction @function

The architecture must match the runtime alias's: Lambda accepts a mismatch, and every invoke then
fails.
