<#
    .SYNOPSIS
    Create a new PowerShell script file.

    .DESCRIPTION
    Creates a new AWS Lambda function in a PowerShell script file, or a project containing a script file,
    based on a template or 'blueprint'. The script or project can later be published to AWS Lambda
    using the Publish-AWSPowerShellLambda cmdlet or compiled into a deployment package bundle using the 
    New-AWSPowerShellLambdaPackage function for publishing at a later date using services such as AWS CloudFormation.

    Using the project based workflow is for advanced use cases when you need more control over how the script is loaded
    or need to include additional files. If you just need to deploy a single script consider using the simpler script
    based workflow.

    A project will consist of the following files, some file names are based on the Name parameter.
        * <name>.ps1 - The main file to be edited. This is the PowerShell script that will be invoked for each Lambda function invocation.
        * <name>.csproj> - The .NET project file used to build the Lambda package bundle
        * Bootstrap.cs - C# Code that indicates the PowerShell script to execute during a Lambda function invocation
        * aws-lambda-tools-defaults.json - Contains default values used when publishing the project. If you rename files be sure update the function-handler field in this property to match the changes.

    .PARAMETER Directory
    The name of folder to contain the script file and other content from the template. If not specified
    a subfolder matching the template name will be created in the current working folder and the content
    from the template will be saved into that subfolder.

    .PARAMETER ProfileName
    Optional parameter to preconfigure the AWS credentials profile the project will be published with. This can be overriden when publishing.

    .PARAMETER ProjectName
    The name of the new project containing your PowerShell Lambda script. If not specified the name of the template is used. This
    value will also be used to set the output directory when -WithProject is specified if the -Directory parameter is not specified.

    .PARAMETER Region
    Optional parameter to preconfigure the AWS region the project will be published to. This can be overriden when publishing.
    template will be used.

    .PARAMETER ScriptName
    Optional basename for the generated script file containing your Lambda function. If not specified
    the original name of the script file in the selected template, which usually matches the template name, is used.

    .PARAMETER Template
    The name of the template to use for creating the PowerShell script. To see a list of available templates
    use the Get-AWSPowerShellLambdaTemplate cmdlet.

    .PARAMETER WithProject
    Optional switch that will create an AWS Lambda project for the PowerShell script. This is an advanced use case providing the
    ability to customize the files added to the Lambda deployment bundle and the PowerShell Host executing in the Lambda function.


    .EXAMPLE
    New-AWSPowerShellLambda -Template S3Event

    Creates a new script function from the template named 'S3Event' into the subfolder
    named 'S3Event' in the current working location.

    .EXAMPLE
    New-AWSPowerShellLambda -Template S3Event -Directory .\s3EventProcessor

    Creates a new script function from the template named 'S3Event' into the subfolder
    named 's3EventProcessor' in the current working location.

    .EXAMPLE
    New-AWSPowerShellLambda -Template S3Event -Directory C:\MyLambdaFunctions

    Creates a new script function from the template named 'S3Event' into the
    C:\MyLambdaFunctions folder.

    .EXAMPLE
    New-AWSPowerShellLambda -Template S3Event -ScriptName MyS3EventFunction

    Creates a new script function from the template named 'S3Event' in the current working location.
    The script file containing your new Lambda function will have the filename MyS3EventFunction.ps1.

    .EXAMPLE
    New-AWSPowerShellLambda -Template S3Event -ScriptName S3EventProcessor -WithProject -Region us-east-1

    In this example a new AWS Lambda PowerShell project is created in a directory called S3EventProcessor.
    The project will be preconfigured in the generated aws-lambda-tools-defaults.json file to deploy to the
    us-east-1 region.

    .EXAMPLE
    New-AWSPowerShellLambda -Template S3Event -ScriptName S3EventProcessor -Directory ./MyCode -WithProject

    In this example a new AWS Lambda PowerShell project is created in a directory called MyCode with the script
    and project files called S3EventProcessor.
#>
function New-AWSPowerShellLambda
{
    param
    (
        [Parameter(Mandatory = $true,
            ValueFromPipeline = $true,
            ValueFromPipelineByPropertyName = $true,
            HelpMessage = 'The template to base the initial function script on.'
        )]
        [ValidateNotNullOrEmpty()]
        [string]$Template,

        [Parameter()]
        [string]$Directory,

        [Parameter()]
        [string]$ScriptName,

        [Parameter(ParameterSetName = 'Project')]
        [switch]$WithProject,

        [Parameter(ParameterSetName = 'Project')]
        [string]$ProjectName,

        [Parameter(ParameterSetName = 'Project')]
        [string]$ProfileName,

        [Parameter(ParameterSetName = 'Project')]
        [string]$Region
    )

    if ($WithProject)
    {
        if ($ProjectName)
        {
            $_projectName = $ProjectName
        }
        else
        {
            $_projectName = $Template
        }
    }

    if ($Directory)
    {
        $_directory = $Directory
    }
    else
    {
        if ($_projectName)
        {
            $_directory = $_projectName;
        }
        elseif ($ScriptName) 
        {
            $_directory = $ScriptName
        }
        else
        {
            $_directory = Join-Path -Path (Get-Location).Path -ChildPath $Template
        }
    }

    if (!(Test-Path -Path $_directory))
    {
        New-Item -ItemType Directory -Path $_directory -Force | Out-Null
    }

    $splat = @{
        Template = $Template
        Directory = $_directory
        BaseName = $ScriptName
    }
    $_functionScriptFile = _initializeScriptFromTemplate @splat
    if ($WithProject)
    {
        $splat = @{
            ProjectName = $_projectName
            ScriptFile = $_functionScriptFile
            Directory = $_directory
            QuietMode = $false
            ProfileName = $ProfileName
            Region = $Region
        }
        _addPowerShellLambdaProjectContent @splat
        Write-Host ('New AWS lambda PowerShell project {0} from template {1} at {2}' -f $_projectName, $Template, (Resolve-Path -Path $_directory))
    }
    else
    {
        Write-Host ('Created new AWS Lambda PowerShell script {0} from template {1} at {2}' -f $_functionScriptFile, $Template, (Resolve-Path $_directory))
    }
}