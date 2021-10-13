<#
    .SYNOPSIS
    Publishes (deploys) a PowerShell script or script project as an AWS Lambda Function.

    .DESCRIPTION
    PowerShell scripts are deployed to AWS Lambda as a .NET Core package bundle. For standalone script files this cmdlet will
    create a temporary .NET Core C# project that will include the specified PowerShell script and any required PowerShell modules.
    The C# project also includes code to bootstrap the PowerShell runtime and execute the Lambda function.

    The cmdlet can also be used to publish a script project to AWS Lambda. Using the project based workflow is for advanced use
    cases when you need more control over how the script is loaded or need to include additional files. If you just need to deploy
    a single script consider using the simpler script based workflow.

    When publishing a project-based script function many of the configuration parameters can be specified in a configuration file
    in your project named aws-lambda-tools-defaults.json. If your script project was generated using the New-AWSPowerShellLambda
    cmdlet a default version of this file will have been created for you. The following cmdlet parameters can take their values
    from the aws-lambda-tools-defaults.json file:

    * DeadLetterQueueArn
    * EnvironmentVariable
    * Handler
    * Memory
    * Name
    * Layer
    * IAMRoleArn
    * SecurityGroup
    * Subnet
    * Timeout
    * KmsKeyArn
    * Profile
    * Region
    * Tag
    * TracingMode

    .PARAMETER DeadLetterQueue
    Optional, target ARN of an SNS topic or SQS Queue for the Dead Letter Queue.

    .PARAMETER DisableInteractive
    Do not prompt for missing function configuration values required for successful deployment to AWS Lambda.

    .PARAMETER DisableModuleRestore
    Skip restoring any required PowerShell modules when deploying a project.

    .PARAMETER EnvironmentVariable
    Collection of key=value environment variables to be set for the function when it executes inside AWS Lambda.

    .PARAMETER Handler
    The string that identifies the entry point in the Lambda package bundle.

    .PARAMETER KmsKeyArn
    KMS Key ARN of a customer key used to encrypt the function's environment variables.

    .PARAMETER Memory
    The amount of memory, in MB, your Lambda function is given when it executes inside AWS Lambda.

    .PARAMETER Architecture
    The architecture of the Lambda function. Valid values: x86_64 or arm64. Default is x86_64

    .PARAMETER Layer
    The Lambda layers to include with the Lambda function.

    .PARAMETER ModuleRepository
    Custom repositories to use when downloading modules to satisfy your script's declared dependencies.

    .PARAMETER Name
    The name of the AWS Lambda function that will execute the PowerShell script.

    .PARAMETER PowerShellFunctionHandler
    The name of the specific PowerShell function to be invoked when your Lambda function executes. The default
    behavior is to use the script in its entirety as the function handler. The name of the function to run, if
    specified, is communicated to Lambda using an environment variable named AWS_POWERSHELL_FUNCTION_HANDLER.

    .PARAMETER PowerShellSdkVersion
    Optional parameter to override the version of PowerShell that will execute the script. The version number
    must match a version of the Microsoft.PowerShell.SDK NuGet package. https://www.nuget.org/packages/Microsoft.PowerShell.SDK

    .PARAMETER ProfileName
    The AWS credentials profile to use when publishing to AWS Lambda. If not set environment credentials will be used.

    .PARAMETER AWSAccessKeyId
    The AWS access key id. Used when setting credentials explicitly instead of using ProfileName.

    .PARAMETER AWSSecretKey
    The AWS secret key. Used when setting credentials explicitly instead of using ProfileName.

    .PARAMETER AWSSessionToken
    The AWS session token. Used when setting credentials explicitly instead of using ProfileName.

    .PARAMETER ProjectDirectory
    The directory containing the AWS Lambda PowerShell project to publish.

    .PARAMETER PublishNewVersion
    Publish a new version as an atomic operation.

    .PARAMETER Region
    The region to connect to AWS services, if not set region will be detected from the environment.

    .PARAMETER IAMRoleArn
    The IAM role ARN that Lambda assumes when it executes your function.

    .PARAMETER S3Bucket
    S3 bucket to upload the build output.

    .PARAMETER S3KeyPrefix
    S3 key prefix for the build output.

    .PARAMETER StagingDirectory
    Optional parameter to set the directory where the AWS Lambda package bundle for a standalone script deployment
    will be created. If not set the system temp directory will be used.

    .PARAMETER ScriptPath
    The path to the PowerShell script file to be published to AWS Lambda.

    .PARAMETER SecurityGroup
    List of security group ids if your function references resources in a VPC.

    .PARAMETER Subnet
    List of subnet ids if your function references resources in a VPC.

    .PARAMETER Timeout
    The function execution timeout in seconds.

    .PARAMETER Tag
    Tags applied to the function.

    .PARAMETER TracingMode
    Configures when AWS X-Ray should trace the function. Valid values: PassThrough or Active.

    .EXAMPLE
    Publish-AWSPowerShellLambda -Name S3CleanupFunction -ScriptPath cleanup-s3-bucket.ps1

    This example creates a package bundle with the script and any required PowerShell modules and deploys the bundle to AWS Lambda.
    AWS Credentials and region will be determined by the environment running the cmdlet.

    .EXAMPLE
    Publish-AWSPowerShellLambda -Name S3CleanupFunction -ScriptPath cleanup-s3-bucket.ps1 -Profile beta -Region us-west-2

    In this example the profile to find AWS credentials and the AWS region are set explicitly.

    .EXAMPLE
    Publish-AWSPowerShellLambda -Name SampleLambda -ScriptPath sample-lambda.ps1 -EnvironmentVariable @{'TABLE_NAME'='MyDynamoDBTable'}

    This example creates a package bundle with the script and any required PowerShell modules and deploys the bundle to AWS Lambda.
    AWS Credentials and region will be determined by the environment running the cmdlet.

    The deployed Lambda Function will include the Environment Variable "TABLE_NAME" with the value of "MyDynamoDBTable".
#>
function Publish-AWSPowerShellLambda
{
    [CmdletBinding(DefaultParameterSetName = 'DeployScript')]
    param
    (
        [Parameter(Mandatory = $true,
            ParameterSetName = 'DeployScript',
            HelpMessage = 'The name of the AWS Lambda function that will execute the PowerShell script.')]
        [Parameter(ParameterSetName = 'DeployProject')]
        [string]$Name,

        [Parameter(Mandatory = $true,
            ParameterSetName = 'DeployScript',
            HelpMessage = 'The path to the PowerShell script to be published to AWS Lambda.')]
        [string]$ScriptPath,

        [Parameter(ParameterSetName = 'DeployScript')]
        [string]$StagingDirectory,

        [Parameter(Mandatory = $true,
            ParameterSetName = 'DeployProject',
            HelpMessage = 'The path to the PowerShell project to be published to AWS Lambda.')]
        [string]$ProjectDirectory,

        [Parameter(ParameterSetName = 'DeployProject')]
        [string]$Handler,

        [Parameter(ParameterSetName = 'DeployProject')]
        [switch]$DisableModuleRestore,

        [Parameter()]
        [string]$PowerShellFunctionHandler,

        [Parameter()]
        [string]$ProfileName,

        [Parameter()]
        [string]$AWSAccessKeyId,

        [Parameter()]
        [string]$AWSSecretKey,

        [Parameter()]
        [string]$AWSSessionToken,

        [Parameter()]
        [string]$Region,

        [Parameter()]
        [string]$IAMRoleArn,

        [Parameter()]
        [int]$Memory,

        [Parameter()]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture,

        [Parameter()]
        [int]$Timeout,

        [Parameter()]
        [Switch]$PublishNewVersion,

        [Parameter()]
        [Hashtable]$EnvironmentVariable,

        [Parameter()]
        [string]$KmsKeyArn,

        [Parameter()]
        [string[]]$Layer,

        [Parameter()]
        [string[]]$Subnet,

        [Parameter()]
        [string[]]$SecurityGroup,

        [Parameter()]
        [string]$DeadLetterQueueArn,

        [Parameter()]
        [ValidateSet('Active', 'PassThrough')]
        [string]$TracingMode,

        [Parameter()]
        [string]$S3Bucket,

        [Parameter()]
        [string]$S3KeyPrefix,

        [Parameter()]
        [Hashtable]$Tag,

        [Parameter()]
        [string[]]$ModuleRepository,

        [Parameter(ParameterSetName = 'DeployScript')]
        [string]$PowerShellSdkVersion,

        [Parameter()]
        [Switch]$DisableInteractive
    )

    _validateDotnetInstall

    # If staging directory is a new temp directory then delete the stage directory after publishing completes
    $deleteStagingDirectory = $false

    if ($PSCmdlet.ParameterSetName -eq 'DeployScript')
    {
        if (!(Test-Path -Path $ScriptPath))
        {
            throw "Script $ScriptPath does not exist."
        }

        if (!($StagingDirectory))
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
            ProjectName = $Name
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

        $namespaceName = _makeSafeNamespaceName $Name
        $_handler = "$Name::$namespaceName.Bootstrap::ExecuteFunction"
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
            Get-ChildItem -Path $ProjectDirectory\*.ps1 | ForEach-Object {
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

    Write-Host "Deploying to AWS Lambda"
    $splat = @{
        FunctionName           = $Name
        FunctionHandler        = $_handler
        PowerShellFunction     = $PowerShellFunctionHandler
        Profile                = $ProfileName
        AWSAccessKeyId         = $AWSAccessKeyId
        AWSSecretKey           = $AWSSecretKey
        AWSSessionToken        = $AWSSessionToken
        Region                 = $Region
        FunctionRole           = $IAMRoleArn
        FunctionMemory         = $Memory
        FunctionArchitecture   = $Architecture
        FunctionLayer          = $Layer
        FunctionTimeout        = $Timeout
        PublishNewVersion      = $PublishNewVersion
        EnvironmentVariables   = $EnvironmentVariable
        KmsKeyArn              = $KmsKeyArn
        FunctionSubnets        = $Subnet
        FunctionSecurityGroups = $SecurityGroup
        DeadLetterQueueArn     = $DeadLetterQueueArn
        TracingMode            = $TracingMode
        S3Bucket               = $S3Bucket
        S3KeyPrefix            = $S3KeyPrefix
        Tags                   = $Tag
        DisableInteractive     = $DisableInteractive
        BuildDirectory         = $_buildDirectory
    }
    _deployProject @splat

    if($deleteStagingDirectory)
    {
        Write-Verbose -Message "Removing staging directory $_buildDirectory"
        Remove-Item -Path $_buildDirectory -Recurse -Force
    }
}