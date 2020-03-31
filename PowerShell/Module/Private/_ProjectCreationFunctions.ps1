<#
    .DESCRIPTION
    Initializes the PowerShell script file containing our Lambda function from the
    blueprint contents. The name of the script file containing the function is
    returned to the caller, to be used in further initialization of the unpacked
    blueprint (eg when adding the wrapper project files).
#>
function _initializeScriptFromTemplate
{
    param
    (
        # The name of the template to source the new script or project based Lambda function from.
        [Parameter(Mandatory = $true)]
        [string]$Template,

        # The output folder to place the generated content into.
        [Parameter(Mandatory = $true)]
        [string]$Directory,

        # Optional basename to use for the Lambda function script file. If not specified the
        # name of the template is used.
        [Parameter()]
        [string]$BaseName
    )

    if ($BaseName)
    {
        $_baseName = $BaseName
    }
    else
    {
        $_baseName = $Template
    }

    $_lambdaFunctionFile = _unpackBlueprintContents -Template $Template -Directory $Directory -BaseName $_baseName -ContentProcessor {

        param([string]$fileExt, [string]$content)

        if (($fileExt -ieq '.ps1') -Or ($fileExt -ieq '.psm1'))
        {
            # Check to see if the script has stated a requirement on AWSPowerShell.NetCore or AWS.Tools.* modules and if so make the
            # version referenced match the version installed locally.
            $ast = [System.Management.Automation.Language.Parser]::ParseInput($content, [ref]$null, [ref]$null)
            if ($ast.ScriptRequirements.RequiredModules)
            {
                $ast.ScriptRequirements.RequiredModules | ForEach-Object  -Process {

                    if (!($_.Name -ieq 'AWSPowerShell.NetCore') -and !($_.Name.StartsWith("AWS.Tools.")))
                    {
                        return
                    }

                    $installedVersion = _getVersionOfLocalInstalledModule -Module $_.Name
                    if (!($installedVersion))
                    {
                        Write-Warning "This script requires the $_.Name module which is not installed locally."
                        Write-Warning "To use the AWS CmdLets execute ""Install-Module $_.Name"" and then update the #Requires statement to the version installed. If you are not going to use the AWS CmdLets then remove the #Requires statement from the script."
                    }
                    elseif ($installedVersion -ne $_.Version.ToString())
                    {
                        $lines = $content.Split('\n')
                        for ($i = 0; $i -lt $lines.Length; $i++ )
                        {
                            if ($lines[$i].Contains('#Requires') -and $lines[$i].Contains($_.Name) -and $lines[$i].Contains($_.Version.ToString()) )
                            {
                                $lines[$i] = $lines.Replace($_.Version.ToString(), $installedVersion)
                                Write-Host ("Configuring script to use installed version $installedVersion of ($_.Name)")
                                break
                            }
                        }

                        $content = [System.String]::Join('\n', $lines)
                    }
                }
            }
        }

        return $content
    }

    return $_lambdaFunctionFile
}

# Checks for an already installed dependency module and returns the version if found.
function _getVersionOfLocalInstalledModule
{
    param
    (
        [Parameter(Mandatory = $true)]
        [string]$Module
    )

    $localModule = Get-Module -ListAvailable -Name $Module | Sort-Object -Property Version -Descending | Select-Object -First 1
    if ($localModule)
    {
        $localModule.Version.ToString()
    }
}

function _addPowerShellLambdaProjectContent
{
    param
    (
        # The name the user selected for the project, or the template name if the user
        # did not supply a value.
        [Parameter()]
        [string]$ProjectName,

        # The name of the generated file containing the actual Lambda function.
        [Parameter()]
        [string]$ScriptFile,

        # The output folder containing the generated project.
        [Parameter()]
        [string]$Directory,

        # User-specified credential profile name to seed into the generated defaults file to be used on deployment.
        [Parameter()]
        [string]$ProfileName,

        # User-specified region to seed into the generated defaults file to be used on deployment.
        [Parameter()]
        [string]$Region,

        # The version of Microsoft.PowerShell.SDK to configure for the project
        [Parameter()]
        [string]$PowerShellSdkVersion,

        [Parameter(Mandatory = $true)]
        [bool]$QuietMode
    )

    if(!($PowerShellSdkVersion)) 
    {
        $PowerShellSdkVersion = $AwsPowerShellDefaultSdkVersion
    }

    # Setup project file
    $csprojContent = _getBlueprintsContent 'projectfile.csproj.txt'
    $csprojContent = $csprojContent.Replace('SCRIPT_FILE', $ScriptFile)
    $csprojContent = $csprojContent.Replace('POWERSHELL_SDK_VERSION', $PowerShellSdkVersion)

    Write-Host "Configuring PowerShell to version $PowerShellSdkVersion"

    $filename = Join-Path -Path $Directory -ChildPath "$ProjectName.csproj"
    if (!$QuietMode)
    {
        Write-Host "Generating C# project $filename used to create Lambda function bundle."
    }
    Out-File -FilePath $filename -InputObject $csprojContent -Encoding UTF8 -Force

    # Setup bootstrap code
    $namespaceName = _makeSafeNamespaceName ([System.IO.Path]::GetFileName($ProjectName))

    $bootstrapContent = _getBlueprintsContent 'bootstrap.cs.txt'
    $bootstrapContent = $bootstrapContent.Replace('NAMESPACE_NAME', $namespaceName)
    $bootstrapContent = $bootstrapContent.Replace('SCRIPT_FILE', $ScriptFile)

    $filename = Join-Path -Path $Directory -ChildPath 'Bootstrap.cs'
    if (!$QuietMode)
    {
        Write-Host "Generating $filename to load PowerShell script and required modules in Lambda environment."
    }
    Out-File -FilePath $filename -InputObject $bootstrapContent -Encoding UTF8 -Force

    if (!$QuietMode)
    {
        Write-Host 'Generating aws-lambda-tools-defaults.json config file with default values used when publishing project.'
    }

    $defaultsContent = _getBlueprintsContent 'aws-lambda-tools-defaults.txt'
    $defaultsContent = $defaultsContent.Replace('CONFIGURED_PROFILE', $ProfileName)
    $defaultsContent = $defaultsContent.Replace('CONFIGURED_REGION', $Region)
    $defaultsContent = $defaultsContent.Replace('PROJECT_NAME', $ProjectName)
    $defaultsContent = $defaultsContent.Replace('DEFAULT_MEMORY', $DefaultFunctionMemory)
    $defaultsContent = $defaultsContent.Replace('DEFAULT_TIMEOUT', $DefaultFunctionTimeout)

    $filename = Join-Path -Path $Directory -ChildPath 'aws-lambda-tools-defaults.json'
    Out-File -FilePath $filename -InputObject $defaultsContent -Encoding UTF8 -Force

    $newDirectory = Join-Path -Path $Directory -ChildPath $ProjectModuleDirectory
    New-Item -ItemType directory -Path $newDirectory -Force | Out-Null
}


function _makeSafeNamespaceName
{
    param
    (
        [Parameter(Mandatory = $true)]
        [string]$OriginalName
    )    

    $namespaceName = $OriginalName
    $namespaceName = $namespaceName -replace '-', '_'
    $namespaceName = $namespaceName -replace ' ', '_'
    $namespaceName = $namespaceName -replace '\.', '_'
    if([Char]::IsDigit($namespaceName[0]))
    {
        $namespaceName = "PS_$namespaceName"
    }    

    return $namespaceName
}