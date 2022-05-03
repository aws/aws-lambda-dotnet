function _deployProject
{
    param
    (
        [Parameter(Mandatory = $true)]
        [string]$FunctionName,

        [Parameter(Mandatory = $false)]
        [string]$FunctionHandler,

        [Parameter(Mandatory = $false)]
        [string]$PowerShellFunctionHandler,

        [Parameter(Mandatory = $false)]
        [string]$ProfileName,

        [Parameter(Mandatory = $false)]
        [string]$AWSAccessKeyId,        

        [Parameter(Mandatory = $false)]
        [string]$AWSSecretKey,

        [Parameter(Mandatory = $false)]
        [string]$AWSSessionToken,

        [Parameter(Mandatory = $false)]
        [string]$Region,

        [Parameter(Mandatory = $false)]
        [string]$FunctionRole,

        [Parameter(Mandatory = $false)]
        [int]$FunctionMemory,

        [Parameter(Mandatory = $false)]
        [string]$FunctionArchitecture,

        [Parameter(Mandatory = $false)]
        [int]$FunctionTimeout,

        [Parameter(Mandatory = $false)]
        [string[]]$FunctionLayer,        

        [Parameter(Mandatory = $false)]
        [Boolean]$PublishNewVersion,

        [Parameter(Mandatory = $false)]
        [Hashtable]$EnvironmentVariables,

        [Parameter(Mandatory = $false)]
        [string]$KmsKeyArn,

        [Parameter(Mandatory = $false)]
        [string[]]$FunctionSubnets,

        [Parameter(Mandatory = $false)]
        [string[]]$FunctionSecurityGroups,

        [Parameter(Mandatory = $false)]
        [string]$DeadLetterQueueArn,

        [Parameter(Mandatory = $false)]
        [string]$TracingMode,

        [Parameter(Mandatory = $false)]
        [string]$S3Bucket,

        [Parameter(Mandatory = $false)]
        [string]$S3KeyPrefix,

        [Parameter(Mandatory = $false)]
        [Hashtable]$Tags,

        [Parameter(Mandatory = $false)]
        [Boolean]$DisableInteractive,

        [Parameter(Mandatory = $false)]
        [string]$BuildDirectory
    )

    _validateDotnetInstall

    if ($BuildDirectory)
    {
        Push-Location $BuildDirectory
    }

    try
    {
        $arguments = '"{0}"' -f $FunctionName
        $arguments += " --configuration Release --framework $AwsPowerShellTargetFramework --function-runtime $AwsPowerShellLambdaRuntime"

        $arguments += ' '
        $arguments += _setupAWSCredentialsCliArguments -ProfileName $ProfileName -AWSAccessKeyId $AWSAccessKeyId -AWSSecretKey $AWSSecretKey -AWSSessionToken $AWSSessionToken
        $arguments += ' '
        $arguments += _setupAWSRegionCliArguments -Region $Region

        if (($FunctionHandler))
        {
            $arguments += " --function-handler $FunctionHandler"
        }

        if (($FunctionRole))
        {
            $arguments += " --function-role $FunctionRole"
        }

        if (($FunctionMemory))
        {
            $arguments += " --function-memory-size $FunctionMemory"
        }


        if (($FunctionArchitecture))
        {
            $arguments += " --function-architecture $FunctionArchitecture"
        }        

        if (($FunctionTimeout))
        {
            $arguments += " --function-timeout $FunctionTimeout"
        }

        $formattedLayers = _formatArray($FunctionLayer)
        if(($formattedLayers))
        {
            $arguments += " --function-layers $formattedLayers"
        }

        if (($PublishNewVersion))
        {
            $arguments += ' --function-publish true'
        }

        if ($PowerShellFunctionHandler)
        {
            $arguments += ' --append-environment-variables "{0}={1}"' -f $AwsPowerShellFunctionEnvName, $PowerShellFunctionHandler
            Write-Host "Setting the $AwsPowerShellFunctionEnvName environment variable to $PowerShellFunctionHandler to identify the PowerShell function to call"
        }

        $formattedEnvironmentVariables = _formatHashTable($EnvironmentVariables)
        if (($formattedEnvironmentVariables))
        {
            $arguments += ' --environment-variables "{0}"' -f $formattedEnvironmentVariables
        }

        if (($KmsKeyArn))
        {
            $arguments += " --kms-key $KmsKeyArn"
        }

        $formattedSubnets = _formatArray($FunctionSubnets)
        if (($formattedSubnets))
        {
            $arguments += " --function-subnets $formattedSubnets"
        }

        $formattedSecurityGroups = _formatArray($FunctionSecurityGroups)
        if (($formattedSecurityGroups))
        {
            $arguments += " --function-security-groups $formattedSecurityGroups"
        }

        if (($DeadLetterQueueArn))
        {
            $arguments += " --dead-letter-target-arn $DeadLetterQueueArn"
        }

        if (($TracingMode))
        {
            $arguments += " --tracing-mode $TracingMode"
        }

        if (($S3Bucket))
        {
            $arguments += " --s3-bucket $S3Bucket"
        }

        if (($S3KeyPrefix))
        {
            $arguments += " --s3-prefix $S3KeyPrefix"
        }

        $formattedTags = _formatHashTable($Tags)
        if (($formattedTags))
        {
            $arguments += ' --tags "{0}"' -f $formattedTags
        }

        if (($DisableInteractive))
        {
            $arguments += ' --disable-interactive true'
        }

        $amazonLambdaToolsPath = _configureAmazonLambdaTools

        $env:AWS_EXECUTION_ENV="AWSLambdaPSCore"
        try
        {
            if ($DisableInteractive)
            {
                Invoke-Expression "$amazonLambdaToolsPath deploy-function $arguments" | Foreach-Object {Write-Verbose -Message "$_`r"}
            }
            else
            {
                Write-Host 'Initiate deployment'
                Invoke-Expression "$amazonLambdaToolsPath deploy-function $arguments"
            }
        }
        finally
        {
            Remove-Item Env:\AWS_EXECUTION_ENV
        }

        if ($LASTEXITCODE -ne 0)
        {
            $msg = @"
Error publishing PowerShell Lambda Function: $LastExitCode
CALLSTACK:$(Get-PSCallStack | Out-String)
"@
            throw $msg
        }
    }
    finally
    {
        Pop-Location
    }
}


function _packageProject
{
    param
    (
        [Parameter(Mandatory = $true)]
        [string]$OutputPackage,

        [Parameter(Mandatory = $false)]
        [string]$BuildDirectory,

        [Parameter(Mandatory = $false)]
        [string]$FunctionArchitecture        
    )

    _validateDotnetInstall

    if (($BuildDirectory))
    {
        Push-Location $BuildDirectory
    }
    try
    {
        $arguments = $Name
        $arguments += " --configuration Release --framework $AwsPowerShellTargetFramework --function-runtime $AwsPowerShellLambdaRuntime"

        if (($OutputPackage))
        {
            $arguments += " --output-package `"$OutputPackage`""
        }

        if($FunctionArchitecture)
        {
            $arguments += " --function-architecture $FunctionArchitecture"
        }
        $amazonLambdaToolsPath = _configureAmazonLambdaTools

        Write-Host 'Initiate packaging'

        # All output from the function deployment is sent to the verbose stream to allow user controlled access
        # to this level of detail
        Write-Verbose -Message "$amazonLambdaToolsPath package $arguments"
        Invoke-Expression "$amazonLambdaToolsPath package $arguments" | Foreach-Object {Write-Verbose -Message "$_`r"}
        if ($LASTEXITCODE -ne 0)
        {
            $msg = @"
Error publishing PowerShell Lambda Function: $LastExitCode
CALLSTACK:$(Get-PSCallStack | Out-String)
"@
            throw $msg
        }
    }
    finally
    {
        Pop-Location
    }
}

function _setupAWSCredentialsCliArguments
{
    param
    (
        [string]$ProfileName,
        [string]$AWSAccessKeyId,
        [string]$AWSSecretKey,
        [string]$AWSSessionToken
    )

    if ($ProfileName)
    {
        return "--profile $ProfileName"
    }

    if ($AWSAccessKeyId -or $AWSSecretKey -or $AWSSessionToken)
    {
        if(!($AWSAccessKeyId))
        {
            throw "The AWSAccessKeyId parameter is required when AWSSecretKey or AWSSessionToken are set."
        }

        if(!($AWSSecretKey))
        {
            throw "The AWSSecretKey parameter is required when AWSAccessKeyId is set."
        }
        
        $arguments = "--aws-access-key-id $AWSAccessKeyId --aws-secret-key $AWSSecretKey"

        if($AWSSessionToken)
        {
            $arguments += " --aws-session-token $AWSSessionToken"
        }

        return $arguments
    }

    # Look to see if the AWS module is loaded and that it was used to configure credentials for the shell.
    # If it has then pass those credentials into the Lambda dotnet CLI tool.
    if (Get-Command 'Get-AWSCredentials' -ErrorAction SilentlyContinue)
    {
        $shellCredentials = Get-AWSCredentials
        if ($shellCredentials)
        {
            $realCreds = $shellCredentials.GetCredentials()
            Write-Verbose -Message 'Using aws credentials configured for the hosting shell'
            $arguments = '--aws-access-key-id {0} --aws-secret-key {1}' -f $realCreds.AccessKey, $realCreds.SecretKey

            if ($realCreds.UseToken)
            {
                Write-Verbose -Message 'Using session token'
                $arguments += ' --aws-session-token {0}' -f $realCreds.Token
            }

            return $arguments
        }
    }

    return [String]::Empty
}

function _setupAWSRegionCliArguments
{
    param
    (
        [string]$Region
    )

    if ($Region)
    {
        return "--region $Region"
    }

    if (Get-Command 'Get-DefaultAWSRegion' -ErrorAction SilentlyContinue)
    {
        $shellRegion = Get-DefaultAWSRegion
        if ($shellRegion)
        {
            Write-Verbose -Message ('Using region {0} configured for the hosting shell' -f $shellRegion.Region)
            return '--region {0}' -f $shellRegion.Region
        }
    }

    return [String]::Empty
}

function _configureAmazonLambdaTools
{
    Write-Host 'Restoring .NET Lambda deployment tool'

    # see if tool is already installed
    $amazonLambdaToolsInstalled = & dotnet tool list -g | Select-String -Pattern Amazon.Lambda.Tools -SimpleMatch -Quiet

    # When "-Verbose" switch was used this output was not hidden.
    # Using stream redirection to force hide all output from the dotnet cli call
    if (-not $amazonLambdaToolsInstalled)
    {
        Write-Verbose -Message 'Installing .NET Global Tool Amazon.Lambda.Tools'
        & dotnet tool install -g Amazon.Lambda.Tools *>&1 | Out-Null
    }
    else
    {
        Write-Verbose -Message 'Updating .NET Global Tool Amazon.Lambda.Tools'

        # When "-Verbose" switch was used this output was not hidden.
        # Using stream redirection to force hide all output from the dotnet cli call
        & dotnet tool update -g Amazon.Lambda.Tools *>&1 | Out-Null
    }

    if ($LASTEXITCODE -ne 0) {
        $msg = @"
Error configuring .NET CLI AWS Lambda deployment tools: $LastExitCode
CALLSTACK:$(Get-PSCallStack | Out-String)
"@
        throw $msg
    }

    $toolsFolder = Join-Path -Path '~' -ChildPath '.dotnet' -AdditionalChildPath 'tools'

    $amazonLambdaToolsPath = Join-Path -Path $toolsFolder -ChildPath 'dotnet-lambda.exe'
    Write-Verbose -Message 'Looking for windows excutable for dotnet-lambda.exe'
    if (!(Test-Path -Path $amazonLambdaToolsPath))
    {
        Write-Verbose -Message 'Did not find windows executable, assuming on non windows platform and using dotnet-lambda'
        $amazonLambdaToolsPath = Join-Path -Path $toolsFolder -ChildPath 'dotnet-lambda'
    }

    return $amazonLambdaToolsPath
}

function _formatHashTable
{
    param
    (
        [Parameter(Mandatory = $false)]
        [Hashtable]$Table
    )

    if (!($Table) -or $Table.Count -eq 0)
    {
        return $null
    }

    $sb = [System.Text.StringBuilder]::new()

    $Table.Keys | ForEach-Object {
        if ($sb.Length -ne 0)
        {
            $sb.Append(";") | Out-Null
        }

        $sb.AppendFormat('{0}={1}', $_, $Table[$_]) | Out-Null
    }

    return $sb.ToString()
}

function _formatArray
{
    param
    (
        [Parameter(Mandatory = $false)]
        [string[]]$Items
    )

    if (!($Items) -or $Items.Count -eq 0)
    {
        return $null
    }

    $sb = [System.Text.StringBuilder]::new()

    $items | ForEach-Object {
        if ($sb.Length -ne 0)
        {
            $sb.Append(",") | Out-Null
        }
        $sb.Append($_) | Out-Null
    }

    return $sb.ToString()
}

function _prepareDependentPowerShellModules
{
    param
    (
        [Parameter(Mandatory = $true)]
        [string]$Script,

        [Parameter(Mandatory = $true)]
        [string]$ProjectDirectory,

        [Parameter(Mandatory = $true)]
        [bool]$ClearExisting,

        [Parameter()]
        [string[]]$ModuleRepository
    )

    $SavedModulesDirectory = Join-Path -Path $ProjectDirectory -ChildPath $ProjectModuleDirectory
    if ($ClearExisting -and (Test-Path -Path $SavedModulesDirectory))
    {
        Remove-Item -Path $SavedModulesDirectory -Recurse -Force
    }

    if (!(Test-Path -Path $SavedModulesDirectory))
    {
        New-Item -ItemType directory -Path $SavedModulesDirectory | Out-Null
    }

    ## Use the FullName property of the $Script fileinfo object, as [System.Management.Automation.Language.Parser]::ParseFile() does not succeed with PSPath values like `Microsoft.PowerShell.Core\FileSystem::\\someserver\somepath\Get-Something.ps1`. 
    ## $Script will have a PSPath value like this when the given file is at a UNC path.
    $strScriptFullname = (Get-Item -Path $Script).FullName
    ## variable in which to place any ParseFile() errors, so as to be able to check for them
    $arrErrorFromParseFile = @()
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($strScriptFullname, [ref]$null, [ref]$arrErrorFromParseFile)
    if (($arrErrorFromParseFile | Measure-Object).Count -gt 0) {
        ## Write a warning (not terminating for now)
        Write-Warning "Received error trying to parse given script file '$Script'. Resulting Lambda package might not contain required PowerShell modules needed for success"
    } ## end if
    if ($ast.ScriptRequirements.RequiredModules)
    {
        $ast.ScriptRequirements.RequiredModules | ForEach-Object -Process {

            if ($_.Name -ieq 'AWSPowerShell')
            {
                Write-Warning 'This script requires the AWSPowerShell module which is not supported. Please change the #Requires statement to use the service specifc modules like AWS.Tools.S3 which is compatible with PowerShell 6.0 and above.'

                Write-Warning 'To use the AWS CmdLets install the AWS.Tools.* module for the services needed and then update the #Requires statement to the version installed. If you are not going to use the AWS CmdLets then remove the #Requires statement from the script.'

                throw 'The AWSPowerShell Module is not supported. Change the #Requires statement to reference the service specific modules like AWS.Tools.S3 module instead.'
            }

            $localModule = _findLocalModule -Name $_.Name -Version $_.Version
            if ($localModule)
            {
                Write-Host ('Copying local module {0}({1}) from {2}' -f $localModule.Name, $localModule.Version, $localModule.ModuleBase)
                $copyPath = Join-Path -Path $SavedModulesDirectory -ChildPath $localModule.Name -AdditionalChildPath $localModule.Version.ToString()
                if (!(Test-Path -Path $copyPath))
                {
                    New-Item -ItemType directory -Path $copyPath | Out-Null
                }
                Copy-Item -Path (Join-Path -Path $localModule.ModuleBase -ChildPath '*') -Destination $copyPath -Recurse
            }
            else
            {
                $splat = @{
                    Name = $_.Name
                    Path = $SavedModulesDirectory
                    ErrorAction = 'Stop'
                }

                if ($_.Version)
                {
                    $splat.Add('RequiredVersion',$_.Version)
                }

                if ($ModuleRepository)
                {
                    $splat.Add('Repository',$ModuleRepository)
                }

                # in the Save-Module call, replace -RequiredVersion with @splat
                Write-Host ('Saving module {0}' -f $_.Name)
                Save-Module @splat
            }
        }
    }
    ## Add verbosity that no RequiredModules found
    else {Write-Verbose "No RequiredModules found for script '$Script'"}
}

function _findLocalModule
{
    param
    (
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter()]
        [Version]$Version
    )

    $loadedModule = Get-Module -Name $Name
    if ($loadedModule -and ($Version -eq $null -or $Version -eq $loadedModule.Version))
    {
        $message = 'Found imported module {0} ({1}) to save with package bundle.' -f $loadedModule.Name, $loadedModule.Version.ToString()
        Write-Verbose -Message $message
        return $loadedModule
    }

    $availableModules = Get-Module -ListAvailable -Name $Name | Sort-Object -Property Version -Descending

    # Select-Object added to ensure multiple installed copies of a specified version won't break staging folder
    # names. Before: ModuleName\System.Obejct[]\. After: Module\Version\
    $availableModules | ForEach-Object -Process {
        if ($null -eq $Version -or $_.Version -eq $Version)
        {
            $message = 'Found installed module {0} ({1}) to save with package bundle.' -f $_.Name, $_.Version.ToString()
            Write-Verbose -Message $message
            return $_
        }
    } | Select-Object -First 1

    return $null
}

function _validateDotnetInstall
{
    $application = Get-Command -Name dotnet
    if (!($application))
    {
        throw '.NET 6 SDK was not found which is required to build the PowerShell Lambda package bundle. Download the .NET 6 SDK from https://www.microsoft.com/net/download'
    }

    $minVersion = [System.Version]::Parse('6.0.100')
    $foundMin = $false

    $installedSDKs = & dotnet --list-sdks
    foreach ($sdk in $installedSDKs) {
        $foundVersion = $sdk.split(' ')[0]
        $version = [System.Version]::new()
        if ([System.Version]::TryParse($foundVersion, [ref]$version))
        {
            if ($minVersion -le $foundVersion)
            {
                $foundMin = $true
            }
        }
    }

    if (!($foundMin))
    {
        throw 'The installed .NET SDK does not meet the minimum requirement to build the PowerShell Lambda package bundle. Download the .NET 6 SDK from https://www.microsoft.com/net/download'
    }
}

function _createStagingDirectory
{
    param
    (
        [Parameter(Mandatory = $true)]
        [String]$Name,

        [Parameter(Mandatory = $false)]
        [String]$StagingDirectory
    )

    if ($StagingDirectory)
    {
        $NewStagingDirectory = Join-Path -Path $StagingDirectory -ChildPath $Name
    }
    else
    {
        $NewStagingDirectory = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $Name
    }

    if (Test-Path -Path $NewStagingDirectory)
    {
        Write-Verbose -Message 'Removing previous staging directory'
        Remove-Item -Path $NewStagingDirectory -Recurse -Force
    }

    Write-Host "Staging deployment at $NewStagingDirectory"
    New-Item -ItemType Directory -Path $NewStagingDirectory -Force | Out-Null

    return $NewStagingDirectory
}
