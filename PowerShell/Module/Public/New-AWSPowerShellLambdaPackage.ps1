<#
    .SYNOPSIS
    Creates a deployment package bundle for a PowerShell script or script project that can be published separately to an
    AWS Lambda Function.

    .DESCRIPTION
    PowerShell scripts and script projects are deployed to AWS Lambda as a .NET Core package bundle.

    For standalone script files this cmdlet will create a package bundle using a temporary .NET Core C# project that
    will include the specified PowerShell script and any required PowerShell modules. The temporary C# project also
    includes code to bootstrap the PowerShell runtime and execute the Lambda function.

    For script projects the bundle will be created using your existing project content. Using the project based workflow
    is for advanced use cases when you need more control over how the script is loaded or need to include additional
    files. If you just need to deploy a single script consider using the simpler script based workflow.

    For both standalone scripts and script projects the package bundle (a zip file) can then be deployed separately
    to AWS Lambda.

    .PARAMETER DisableModuleRestore
    Skip restoring any required PowerShell modules when deploying a project.

    .PARAMETER ProjectDirectory
    The directory containing the AWS Lambda PowerShell project to publish.

    .PARAMETER ModuleRepository
    Custom repositories to use when downloading modules to satisfy your script's declared dependencies.

    .PARAMETER OutputPackage
    Set this parameter to zip file to generate just the deployment package that will be deployed as part of another system.

    .PARAMETER PowerShellSdkVersion
    Optional parameter to override the version of PowerShell that will execute the script. The version number 
    must match a version of the Microsoft.PowerShell.SDK NuGet package. https://www.nuget.org/packages/Microsoft.PowerShell.SDK

    .PARAMETER ScriptPath
    The path to the PowerShell script file to be published to AWS Lambda.

    .PARAMETER Architecture
    The architecture of the Lambda function. Valid values: x86_64 or arm64. Default is x86_64
        
    .PARAMETER StagingDirectory
    Optional parameter to set the directory where the AWS Lambda package bundle for a standalone script deployment
    will be created. If not set the system temp directory will be used.

    .EXAMPLE
    New-AWSPowerShellLambdaPackage -ScriptPath cleanup-s3-bucket.ps1 -OutputPackage C:\PendingDeployment\MyS3CleanupFunction.zip

    Creates the zip file C:\PendingDeployment\MyS3CleanupFunction.zip containing the packaged function from the script file
    .\cleanup-s3-bucket.ps1 that can be deployed separately to AWS Lambda.

    .EXAMPLE
    New-AWSPowerShellLambdaPackage -OutputPackage C:\PendingDeployment\MyS3CleanupProjectBundle.zip

    Creates a zip file containing the deployment bundle for a script project in the current directory.

    .EXAMPLE
    New-AWSPowerShellLambdaPackage -ProjectDirectory C:\MyLambdaProject -OutputPackage C:\PendingDeployment\MyS3CleanupProjectBundle.zip

    Creates a zip file containing the deployment bundle for the script project in the folder C:\MyLambdaProject.
#>
function New-AWSPowerShellLambdaPackage
{
    [CmdletBinding(DefaultParameterSetName = 'PackageScript')]
    param
    (
        [Parameter(Mandatory = $true,
            ParameterSetName = 'PackageScript',
            HelpMessage = 'The path to the PowerShell script to be packaged for later deployment to AWS Lambda.')]
        [string]$ScriptPath,

        [Parameter(ParameterSetName = 'PackageScript')]
        [string]$StagingDirectory,

        [Parameter(ParameterSetName = 'PackageScript')]
        [string]$PowerShellSdkVersion,

        [Parameter(ParameterSetName = 'PackageProject')]
        [string]$ProjectDirectory,

        [Parameter(ParameterSetName = 'PackageProject')]
        [switch]$DisableModuleRestore,

        [Parameter(Mandatory = $true,
            HelpMessage = 'The path and name of the output package bundle (zip file) to create.')]
        [String]$OutputPackage,

        [Parameter()]
        [string[]]$ModuleRepository,  

        [Parameter()]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture      
    )

    _validateDotnetInstall

    # If staging directory is a new temp directory then delete the stage directory after publishing completes
    $deleteStagingDirectory = $false

    if ($PSCmdlet.ParameterSetName -eq 'PackageScript')
    {
        if (!(Test-Path -Path $ScriptPath))
        {
            throw "Script $ScriptPath does not exist."
        }

        if(!($StagingDirectory))
        {
            $deleteStagingDirectory = $true
        }
                
        # Creates and returns an updated StagingDirectory with the ScriptName inside it
        $_name = [System.IO.Path]::GetFileNameWithoutExtension($ScriptPath)
        $stagingSplat = @{
            Name             = $_name
            StagingDirectory = $StagingDirectory
        }
        $StagingDirectory = _createStagingDirectory @stagingSplat

        $_scriptPath = (Resolve-Path -Path $ScriptPath).Path
        $_buildDirectory = (Resolve-Path -Path $StagingDirectory).Path

        $splat = @{
            ProjectName = $_name
            ScriptFile  = [System.IO.Path]::GetFileName($_scriptPath)
            Directory   = $_buildDirectory
            QuietMode   = $false
            PowerShellSdkVersion = $PowerShellSdkVersion
        }
        _addPowerShellLambdaProjectContent @splat

        Write-Host 'Copying PowerShell script to staging directory'
        Copy-Item -Path $_scriptPath -Destination $_buildDirectory

        $splat = @{
            Script           = $_scriptPath
            ProjectDirectory = $_buildDirectory
            ClearExisting    = $true
            ModuleRepository = $ModuleRepository
        }
        _prepareDependentPowerShellModules @splat

        $namespaceName = _makeSafeNamespaceName $_name
        $_handler = "$_name::$namespaceName.Bootstrap::ExecuteFunction"
    }
    else
    {
        if (!($ProjectDirectory))
        {
            $ProjectDirectory = $pwd.Path
        }

        if (!(Test-Path -Path $ProjectDirectory))
        {
            throw "Project directory $ProjectDirectory does not exist."
        }

        if (!($DisableModuleRestore))
        {
            $clearExisting = $true

            $targetPath = Join-Path -Path $ProjectDirectory -ChildPath '*.ps1'
            Get-ChildItem -Path $targetPath | ForEach-Object {
                $splat = @{
                    Script           = $_.FullName
                    ProjectDirectory = $ProjectDirectory
                    ClearExisting    = $clearExisting
                    ModuleRepository = $ModuleRepository
                }
                _prepareDependentPowerShellModules @splat

                $clearExisting = $false
            }
        }

        $_buildDirectory = $ProjectDirectory
        $_handler = $Handler
    }

    if (!([System.IO.Path]::IsPathRooted($OutputPackage)))
    {
        $OutputPackage = Join-Path -Path $pwd -ChildPath $OutputPackage
        Write-Host "Resolved full output package path as $OutputPackage"
    }

    Write-Host "Creating deployment package at $OutputPackage"

    _packageProject -OutputPackage $OutputPackage -BuildDirectory $_buildDirectory -FunctionArchitecture $Architecture

    if ($PSCmdlet.ParameterSetName -eq 'PackageScript')
    {
        Write-Host "When deploying this package to AWS Lambda you will need to specify the function handler. The handler for this package is: $_handler. To request Lambda to invoke a specific PowerShell function in your script specify the name of the PowerShell function in the environment variable $AwsPowerShellFunctionEnvName when publishing the package."

        # Resolve-Path added to fix relative paths and improve PathToPackage output
        [PSCustomObject][ordered]@{
            LambdaHandler                   = $_handler
            PathToPackage                   = Resolve-Path -Path $OutputPackage
            PowerShellFunctionHandlerEnvVar = 'AWS_POWERSHELL_FUNCTION_HANDLER'
        }

        if($deleteStagingDirectory)
        {
            Write-Verbose -Message "Removing staging directory $_buildDirectory"
            Remove-Item -Path $_buildDirectory -Recurse -Force       
        }        
    }
}