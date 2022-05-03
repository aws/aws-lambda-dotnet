$_blueprintsOnlinePath = '/LambdaSampleFunctions/powershell/v3/'
$_modulePath = Split-Path -Path $PSScriptRoot -Parent
$_blueprintsModulePath = Join-Path -Path $_modulePath -ChildPath 'Templates' -AdditionalChildPath 'Blueprints'
$_manifestFilename = 'ps-lambda-blueprint-manifest.json'

<#
    .DESCRIPTION
    Probes the default or custom location to find the requested blueprint content
#>
function _getHostedBlueprintsContent
{
    param
    (
        # the relative path and filename to the content to load
        [Parameter(Mandatory = $true)]
        [string]$Contentpath
    )

    $_defaultOrigins = @(
        'https://d3rrggjwfhwld2.cloudfront.net'
        'https://aws-vs-toolkit.s3.amazonaws.com'
    )

    $_altOrigin = $env:PSLAMBDA_BLUEPRINTS_ORIGIN

    if ($_altOrigin)
    {
        $_contentOrigins = @($_altOrigin)
    }
    else
    {
        $_contentOrigins = $_defaultOrigins
    }

    foreach ($_origin in $_contentOrigins)
    {
        try
        {
            $_contentLocation = $_origin + $_blueprintsOnlinePath + $ContentPath
            Write-Debug "Attempting to load blueprints content at $_contentLocation"
            if ($_contentLocation.StartsWith('http'))
            {
                # Preventing "-Verbose" from displaying Invoke-WebRequest verbose output
                $response = Invoke-WebRequest -Uri $_contentLocation -Verbose:$false
                $_content = [System.Text.Encoding]::UTF8.GetString($response.Content)
                return $_content
            }
            else
            {
                $_content = [System.IO.File]::ReadAllText($_contentLocation)
                return $_content
            }
        }
        catch
        {
        }
    }
}

<#
    .DESCRIPTION
    Reads blueprint content from that installed with the module
#>
function _getLocalBlueprintsContent
{
    param
    (
        # The relative path and filename to the content to load
        [Parameter(Mandatory = $true)]
        [string]$ContentPath
    )

    $_localContentPath = Join-Path -Path $_blueprintsModulePath -ChildPath $ContentPath
    Write-Debug "Attempting load of blueprint content from $_localContentPath"
    if (Test-Path -Path $_localContentPath -PathType Leaf)
    {
        return [System.IO.File]::ReadAllText($_localContentPath)
    }
    else
    {
        throw "$_localContentPath does not exist!"
    }
}

<#
    .DESCRIPTION
    Composite helper to obtain content from blueprints; if not available online
    it attempts to try from the local module install
#>
function _getBlueprintsContent
{
    param
    (
        # the relative path and filename to the content to load
        [Parameter(Mandatory = $true)]
        [string]$ContentPath,

        # Defines whether to load Blueprints from online data sources
        [switch]$Online
    )

    if ($Online)
    {
        $private:_content = _getHostedBlueprintsContent -Contentpath $ContentPath
    }

    if (!($private:_content))
    {
        $private:_content = _getLocalBlueprintsContent -ContentPath $ContentPath
    }

    return $private:_content
}

<#
    .DESCRIPTION
    Loads the manifest and returns a PSObject of the content. Default
    behavior is to download from one of the online sources detailed in
    $_defaultOrigins. An alternative origin can be used by setting
    the PSLAMBDA_BLUEPRINTS_ORIGIN environment variable to a web or
    file-based location.
#>
function _loadBlueprintManifest
{
    param
    (
        # Defines whether to load Blueprints from online data sources
        [switch]$Online
    )

    $_getBlueprintsContent = @{
        ContentPath = $_manifestFilename
    }

    if ($Online)
    {
        $_getBlueprintsContent.Add('Online', $true)
    }

    return ConvertFrom-Json -InputObject (_getBlueprintsContent @_getBlueprintsContent)
}

<#
    .DESCRIPTION
    Unpacks one or more content files for a blueprint into the user-specified
    location. Returns the name of the script file containing the actual
    Lambda function to be run.
#>
function _unpackBlueprintContents
{
    param
    (
        # The name of the template containing the content to be unpacked.
        [Parameter(Mandatory = $true)]
        [string]$Template,

        # The output folder to hold the content. It is created if necessary.
        [Parameter(Mandatory = $true)]
        [string]$Directory,

        # Basename to use for files in the blueprint that can be customized.
        # If not specified, the template name is used.
        [Parameter()]
        [string]$BaseName,

        # Optional script block that the unpacked script content will be passed to
        # prior to output to disk, allowing for template substitution etc.
        [Parameter()]
        [scriptblock]$ContentProcessor
    )

    $_manifest = _loadBlueprintManifest

    $_template = $_manifest.blueprints | Where-Object -Property name -EQ -Value $Template
    if (!($_template))
    {
        throw "Failed to find blueprint details for blueprint named $Template"
    }

    if (!(Test-Path $Directory))
    {
        New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    }

    $_lambdaFunctionFile = ''

    # unpack the contents; for each file 'source' is the name of the content in the online or installed
    # payload, 'output' is the name we want the content to have on disk
    foreach ($_contentFile in $_template.content)
    {
        $_sourcePath = Join-Path -Path $Template -ChildPath $_contentFile.source
        $_outputFilename = $_contentFile.output.Replace('{basename}', $BaseName)
        $_outputPath = Join-Path -Path $Directory -ChildPath $_outputFilename

        Write-Debug "Loading blueprint content at $_sourcePath"
        $_content = _getBlueprintsContent -ContentPath $_sourcePath

        if ($ContentProcessor)
        {
            $fileExt = [System.IO.Path]::GetExtension($_outputFilename)
            $_content = Invoke-Command -ScriptBlock $ContentProcessor -ArgumentList $fileExt, $_content
        }

        Write-Debug "Writing output file $_outputPath"
        New-Item -ItemType File -Path $_outputPath -Force | Out-Null
        Out-File -FilePath $_outputPath -InputObject $_content -Encoding utf8NoBOM -Force | Out-Null

        if ($_contentFile.fileType -eq "lambdaFunction")
        {
            $_lambdaFunctionFile = $_outputFilename
        }
    }

    return $_lambdaFunctionFile
}