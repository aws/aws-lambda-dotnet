# Auto-generated argument completers for parameters of SDK ConstantClass-derived type used in cmdlets.
# Do not modify this file; it may be overwritten during version upgrades.

$psMajorVersion = $PSVersionTable.PSVersion.Major
if ($psMajorVersion -eq 2) 
{ 
	Write-Verbose "Dynamic argument completion not supported in PowerShell version 2; skipping load."
	return 
}

# PowerShell's native Register-ArgumentCompleter cmdlet is available on v5.0 or higher. For lower
# version, we can use the version in the TabExpansion++ module if installed.
$registrationCmdletAvailable = ($psMajorVersion -ge 5) -Or !((Get-Command Register-ArgumentCompleter -ea Ignore) -eq $null)

# internal function to perform the registration using either cmdlet or manipulation
# of the options table
function _awsArgumentCompleterRegistration()
{
    param
    (
        [scriptblock]$scriptBlock,
        [hashtable]$param2CmdletsMap
    )

    if ($registrationCmdletAvailable)
    {
        foreach ($paramName in $param2CmdletsMap.Keys)
        {
             $args = @{
                "ScriptBlock" = $scriptBlock
                "Parameter" = $paramName
            }

            $cmdletNames = $param2CmdletsMap[$paramName]
            if ($cmdletNames -And $cmdletNames.Length -gt 0)
            {
                $args["Command"] = $cmdletNames
            }

            Register-ArgumentCompleter @args
        }
    }
    else
    {
        if (-not $global:options) { $global:options = @{ CustomArgumentCompleters = @{ }; NativeArgumentCompleters = @{ } } }

        foreach ($paramName in $param2CmdletsMap.Keys)
        {
            $cmdletNames = $param2CmdletsMap[$paramName]

            if ($cmdletNames -And $cmdletNames.Length -gt 0)
            {
                foreach ($cn in $cmdletNames)
                {
                    $fqn =  [string]::Concat($cn, ":", $paramName)
                    $global:options['CustomArgumentCompleters'][$fqn] = $scriptBlock
                }
            }
            else
            {
                $global:options['CustomArgumentCompleters'][$paramName] = $scriptBlock
            }
        }

        $function:tabexpansion2 = $function:tabexpansion2 -replace 'End\r\n{', 'End { if ($null -ne $options) { $options += $global:options} else {$options = $global:options}'
    }
}

# To allow for same-name parameters of different ConstantClass-derived types 
# each completer function checks on command name concatenated with parameter name.
# Additionally, the standard code pattern for completers is to pipe through 
# sort-object after filtering against $wordToComplete but we omit this as our members 
# are already sorted.

$AWS_RegionCompleter = {
	param ($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)

	$regionHash = @{ }

    # Similar to Get-AWSRegion
	$regions = [Amazon.RegionEndpoint]::EnumerableAllRegions
	foreach ($r in $regions)
	{
		$regionHash.Add($r.SystemName, $r.DisplayName)
	}

	$regionHash.Keys |
	Sort-Object |
	Where-Object { $_ -like "$wordToComplete*" } |
	ForEach-Object {
		New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $regionHash[$_]
	}
}

_awsArgumentCompleterRegistration $AWS_RegionCompleter @{ "Region"=@() }

$AWS_EC2ImageByNameCompleter = {
	param ($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)

	$keys = [Amazon.EC2.Util.ImageUtilities]::ImageKeys

	$keys |
	Sort-Object -Descending |
	Where-Object { $_ -like "$wordToComplete*" } |
	ForEach-Object {
		New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_
	}
}

_awsArgumentCompleterRegistration $AWS_EC2ImageByNameCompleter @{ "Name"=@("Get-EC2ImageByName") }

$AWS_ProfileNameCompleter = {
	param ($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)

	# allow for new user with no profiles set up yet
	$profiles = Get-AWSCredentials -ListProfileDetail | select -expandproperty ProfileName
	if ($profiles)
	{
		$profiles |
		Sort-Object |
		Where-Object { $_ -like "$wordToComplete*" } |
		ForEach-Object {
			New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_
		}
	}
}

_awsArgumentCompleterRegistration $AWS_ProfileNameCompleter @{ "ProfileName"=@() }

# The attribute name parameter for EC2 apis such as ModifyImageAttribute is modeled as a string
# in the service model rather than an enum type, which means by default we cannot auto-generate
# an argument completer. Api's use as DescribeImageAttribute do use an enum type (ImageAttributeName)
# and so don't have this problem.
$AWS_EC2ImageAttributeCompleter = {
	param ($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)

    switch ($("$commandName/$parameterName"))
    {
        # Taken from Amazon.EC2.ImageAttributeName
        "Edit-EC2ImageAttribute/Attribute"
        {
            $v = "description","kernel","ramdisk","launchPermission","productCodes","blockDeviceMapping","sriovNetSupport"
            break
        }
    }

    $v |
    Where-Object { $_ -like "$wordToComplete*" } |
    ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

_awsArgumentCompleterRegistration $AWS_EC2ImageAttributeCompleter @{ "Attribute"=@("Edit-EC2ImageAttribute") }

# begin auto-generated service completers
# Argument completions for service AWS Certificate Manager Private Certificate Authority
$PCA_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.ACMPCA.AuditReportResponseFormat
        "New-PCACertificateAuthorityAuditReport/AuditReportResponseFormat"
        {
            $v = "CSV","JSON"
            break
        }
        
        # Amazon.ACMPCA.CertificateAuthorityStatus
        "Update-PCACertificateAuthority/Status"
        {
            $v = "ACTIVE","CREATING","DELETED","DISABLED","EXPIRED","FAILED","PENDING_CERTIFICATE"
            break
        }
        
        # Amazon.ACMPCA.CertificateAuthorityType
        "New-PCACertificateAuthority/CertificateAuthorityType"
        {
            $v = "SUBORDINATE"
            break
        }
        
        # Amazon.ACMPCA.RevocationReason
        "Revoke-PCACertificate/RevocationReason"
        {
            $v = "AFFILIATION_CHANGED","A_A_COMPROMISE","CERTIFICATE_AUTHORITY_COMPROMISE","CESSATION_OF_OPERATION","KEY_COMPROMISE","PRIVILEGE_WITHDRAWN","SUPERSEDED","UNSPECIFIED"
            break
        }
        
        # Amazon.ACMPCA.SigningAlgorithm
        "New-PCACertificate/SigningAlgorithm"
        {
            $v = "SHA256WITHECDSA","SHA256WITHRSA","SHA384WITHECDSA","SHA384WITHRSA","SHA512WITHECDSA","SHA512WITHRSA"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$PCA_map = @{
    "AuditReportResponseFormat"=@("New-PCACertificateAuthorityAuditReport")
    "CertificateAuthorityType"=@("New-PCACertificateAuthority")
    "RevocationReason"=@("Revoke-PCACertificate")
    "SigningAlgorithm"=@("New-PCACertificate")
    "Status"=@("Update-PCACertificateAuthority")
}

_awsArgumentCompleterRegistration $PCA_Completers $PCA_map


# Argument completions for service Alexa For Business
$ALXB_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.AlexaForBusiness.DeviceEventType
        "Get-ALXBDeviceEventList/EventType"
        {
            $v = "CONNECTION_STATUS","DEVICE_STATUS"
            break
        }
        
        # Amazon.AlexaForBusiness.DistanceUnit
        {
            ($_ -eq "New-ALXBProfile/DistanceUnit") -Or
            ($_ -eq "Update-ALXBProfile/DistanceUnit")
        }
        {
            $v = "IMPERIAL","METRIC"
            break
        }
        
        # Amazon.AlexaForBusiness.TemperatureUnit
        {
            ($_ -eq "New-ALXBProfile/TemperatureUnit") -Or
            ($_ -eq "Update-ALXBProfile/TemperatureUnit")
        }
        {
            $v = "CELSIUS","FAHRENHEIT"
            break
        }
        
        # Amazon.AlexaForBusiness.WakeWord
        {
            ($_ -eq "New-ALXBProfile/WakeWord") -Or
            ($_ -eq "Update-ALXBProfile/WakeWord")
        }
        {
            $v = "ALEXA","AMAZON","COMPUTER","ECHO"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$ALXB_map = @{
    "DistanceUnit"=@("New-ALXBProfile","Update-ALXBProfile")
    "EventType"=@("Get-ALXBDeviceEventList")
    "TemperatureUnit"=@("New-ALXBProfile","Update-ALXBProfile")
    "WakeWord"=@("New-ALXBProfile","Update-ALXBProfile")
}

_awsArgumentCompleterRegistration $ALXB_Completers $ALXB_map


# Argument completions for service Amazon API Gateway
$AG_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.APIGateway.ApiKeysFormat
        "Import-AGApiKey/Format"
        {
            $v = "csv"
            break
        }
        
        # Amazon.APIGateway.ApiKeySourceType
        "New-AGRestApi/ApiKeySource"
        {
            $v = "AUTHORIZER","HEADER"
            break
        }
        
        # Amazon.APIGateway.AuthorizerType
        "New-AGAuthorizer/Type"
        {
            $v = "COGNITO_USER_POOLS","REQUEST","TOKEN"
            break
        }
        
        # Amazon.APIGateway.CacheClusterSize
        {
            ($_ -eq "New-AGDeployment/CacheClusterSize") -Or
            ($_ -eq "New-AGStage/CacheClusterSize")
        }
        {
            $v = "0.5","1.6","118","13.5","237","28.4","58.2","6.1"
            break
        }
        
        # Amazon.APIGateway.ConnectionType
        "Write-AGIntegration/ConnectionType"
        {
            $v = "INTERNET","VPC_LINK"
            break
        }
        
        # Amazon.APIGateway.ContentHandlingStrategy
        {
            ($_ -eq "Write-AGIntegration/ContentHandling") -Or
            ($_ -eq "Write-AGIntegrationResponse/ContentHandling")
        }
        {
            $v = "CONVERT_TO_BINARY","CONVERT_TO_TEXT"
            break
        }
        
        # Amazon.APIGateway.DocumentationPartType
        {
            ($_ -eq "New-AGDocumentationPart/Location_Type") -Or
            ($_ -eq "Get-AGDocumentationPartList/Type")
        }
        {
            $v = "API","AUTHORIZER","METHOD","MODEL","PATH_PARAMETER","QUERY_PARAMETER","REQUEST_BODY","REQUEST_HEADER","RESOURCE","RESPONSE","RESPONSE_BODY","RESPONSE_HEADER"
            break
        }
        
        # Amazon.APIGateway.GatewayResponseType
        {
            ($_ -eq "Get-AGGatewayResponse/ResponseType") -Or
            ($_ -eq "Remove-AGGatewayResponse/ResponseType") -Or
            ($_ -eq "Update-AGGatewayResponse/ResponseType") -Or
            ($_ -eq "Write-AGGatewayResponse/ResponseType")
        }
        {
            $v = "ACCESS_DENIED","API_CONFIGURATION_ERROR","AUTHORIZER_CONFIGURATION_ERROR","AUTHORIZER_FAILURE","BAD_REQUEST_BODY","BAD_REQUEST_PARAMETERS","DEFAULT_4XX","DEFAULT_5XX","EXPIRED_TOKEN","INTEGRATION_FAILURE","INTEGRATION_TIMEOUT","INVALID_API_KEY","INVALID_SIGNATURE","MISSING_AUTHENTICATION_TOKEN","QUOTA_EXCEEDED","REQUEST_TOO_LARGE","RESOURCE_NOT_FOUND","THROTTLED","UNAUTHORIZED","UNSUPPORTED_MEDIA_TYPE"
            break
        }
        
        # Amazon.APIGateway.IntegrationType
        "Write-AGIntegration/Type"
        {
            $v = "AWS","AWS_PROXY","HTTP","HTTP_PROXY","MOCK"
            break
        }
        
        # Amazon.APIGateway.LocationStatusType
        "Get-AGDocumentationPartList/LocationStatus"
        {
            $v = "DOCUMENTED","UNDOCUMENTED"
            break
        }
        
        # Amazon.APIGateway.PutMode
        {
            ($_ -eq "Import-AGDocumentationPartList/Mode") -Or
            ($_ -eq "Write-AGRestApi/Mode")
        }
        {
            $v = "merge","overwrite"
            break
        }
        
        # Amazon.APIGateway.QuotaPeriodType
        "New-AGUsagePlan/Quota_Period"
        {
            $v = "DAY","MONTH","WEEK"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$AG_map = @{
    "ApiKeySource"=@("New-AGRestApi")
    "CacheClusterSize"=@("New-AGDeployment","New-AGStage")
    "ConnectionType"=@("Write-AGIntegration")
    "ContentHandling"=@("Write-AGIntegration","Write-AGIntegrationResponse")
    "Format"=@("Import-AGApiKey")
    "Location_Type"=@("New-AGDocumentationPart")
    "LocationStatus"=@("Get-AGDocumentationPartList")
    "Mode"=@("Import-AGDocumentationPartList","Write-AGRestApi")
    "Quota_Period"=@("New-AGUsagePlan")
    "ResponseType"=@("Get-AGGatewayResponse","Remove-AGGatewayResponse","Update-AGGatewayResponse","Write-AGGatewayResponse")
    "Type"=@("Get-AGDocumentationPartList","New-AGAuthorizer","Write-AGIntegration")
}

_awsArgumentCompleterRegistration $AG_Completers $AG_map


# Argument completions for service Application Auto Scaling
$AAS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.ApplicationAutoScaling.AdjustmentType
        "Set-AASScalingPolicy/StepScalingPolicyConfiguration_AdjustmentType"
        {
            $v = "ChangeInCapacity","ExactCapacity","PercentChangeInCapacity"
            break
        }
        
        # Amazon.ApplicationAutoScaling.MetricAggregationType
        "Set-AASScalingPolicy/StepScalingPolicyConfiguration_MetricAggregationType"
        {
            $v = "Average","Maximum","Minimum"
            break
        }
        
        # Amazon.ApplicationAutoScaling.MetricStatistic
        "Set-AASScalingPolicy/TargetTrackingScalingPolicyConfiguration_CustomizedMetricSpecification_Statistic"
        {
            $v = "Average","Maximum","Minimum","SampleCount","Sum"
            break
        }
        
        # Amazon.ApplicationAutoScaling.MetricType
        "Set-AASScalingPolicy/TargetTrackingScalingPolicyConfiguration_PredefinedMetricSpecification_PredefinedMetricType"
        {
            $v = "ALBRequestCountPerTarget","DynamoDBReadCapacityUtilization","DynamoDBWriteCapacityUtilization","EC2SpotFleetRequestAverageCPUUtilization","EC2SpotFleetRequestAverageNetworkIn","EC2SpotFleetRequestAverageNetworkOut","ECSServiceAverageCPUUtilization","ECSServiceAverageMemoryUtilization","RDSReaderAverageCPUUtilization","RDSReaderAverageDatabaseConnections","SageMakerVariantInvocationsPerInstance"
            break
        }
        
        # Amazon.ApplicationAutoScaling.PolicyType
        "Set-AASScalingPolicy/PolicyType"
        {
            $v = "StepScaling","TargetTrackingScaling"
            break
        }
        
        # Amazon.ApplicationAutoScaling.ScalableDimension
        {
            ($_ -eq "Add-AASScalableTarget/ScalableDimension") -Or
            ($_ -eq "Get-AASScalableTarget/ScalableDimension") -Or
            ($_ -eq "Get-AASScalingActivity/ScalableDimension") -Or
            ($_ -eq "Get-AASScalingPolicy/ScalableDimension") -Or
            ($_ -eq "Get-AASScheduledAction/ScalableDimension") -Or
            ($_ -eq "Remove-AASScalableTarget/ScalableDimension") -Or
            ($_ -eq "Remove-AASScalingPolicy/ScalableDimension") -Or
            ($_ -eq "Remove-AASScheduledAction/ScalableDimension") -Or
            ($_ -eq "Set-AASScalingPolicy/ScalableDimension") -Or
            ($_ -eq "Set-AASScheduledAction/ScalableDimension")
        }
        {
            $v = "appstream:fleet:DesiredCapacity","dynamodb:index:ReadCapacityUnits","dynamodb:index:WriteCapacityUnits","dynamodb:table:ReadCapacityUnits","dynamodb:table:WriteCapacityUnits","ec2:spot-fleet-request:TargetCapacity","ecs:service:DesiredCount","elasticmapreduce:instancegroup:InstanceCount","rds:cluster:ReadReplicaCount","sagemaker:variant:DesiredInstanceCount"
            break
        }
        
        # Amazon.ApplicationAutoScaling.ServiceNamespace
        {
            ($_ -eq "Add-AASScalableTarget/ServiceNamespace") -Or
            ($_ -eq "Get-AASScalableTarget/ServiceNamespace") -Or
            ($_ -eq "Get-AASScalingActivity/ServiceNamespace") -Or
            ($_ -eq "Get-AASScalingPolicy/ServiceNamespace") -Or
            ($_ -eq "Get-AASScheduledAction/ServiceNamespace") -Or
            ($_ -eq "Remove-AASScalableTarget/ServiceNamespace") -Or
            ($_ -eq "Remove-AASScalingPolicy/ServiceNamespace") -Or
            ($_ -eq "Remove-AASScheduledAction/ServiceNamespace") -Or
            ($_ -eq "Set-AASScalingPolicy/ServiceNamespace") -Or
            ($_ -eq "Set-AASScheduledAction/ServiceNamespace")
        }
        {
            $v = "appstream","dynamodb","ec2","ecs","elasticmapreduce","rds","sagemaker"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$AAS_map = @{
    "PolicyType"=@("Set-AASScalingPolicy")
    "ScalableDimension"=@("Add-AASScalableTarget","Get-AASScalableTarget","Get-AASScalingActivity","Get-AASScalingPolicy","Get-AASScheduledAction","Remove-AASScalableTarget","Remove-AASScalingPolicy","Remove-AASScheduledAction","Set-AASScalingPolicy","Set-AASScheduledAction")
    "ServiceNamespace"=@("Add-AASScalableTarget","Get-AASScalableTarget","Get-AASScalingActivity","Get-AASScalingPolicy","Get-AASScheduledAction","Remove-AASScalableTarget","Remove-AASScalingPolicy","Remove-AASScheduledAction","Set-AASScalingPolicy","Set-AASScheduledAction")
    "StepScalingPolicyConfiguration_AdjustmentType"=@("Set-AASScalingPolicy")
    "StepScalingPolicyConfiguration_MetricAggregationType"=@("Set-AASScalingPolicy")
    "TargetTrackingScalingPolicyConfiguration_CustomizedMetricSpecification_Statistic"=@("Set-AASScalingPolicy")
    "TargetTrackingScalingPolicyConfiguration_PredefinedMetricSpecification_PredefinedMetricType"=@("Set-AASScalingPolicy")
}

_awsArgumentCompleterRegistration $AAS_Completers $AAS_map


# Argument completions for service Application Discovery Service
$ADS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.ApplicationDiscoveryService.ConfigurationItemType
        "Get-ADSConfigurationList/ConfigurationType"
        {
            $v = "APPLICATION","CONNECTION","PROCESS","SERVER"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$ADS_map = @{
    "ConfigurationType"=@("Get-ADSConfigurationList")
}

_awsArgumentCompleterRegistration $ADS_Completers $ADS_map


# Argument completions for service AWS AppStream
$APS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.AppStream.AuthenticationType
        "Get-APSSessionList/AuthenticationType"
        {
            $v = "API","SAML","USERPOOL"
            break
        }
        
        # Amazon.AppStream.FleetType
        "New-APSFleet/FleetType"
        {
            $v = "ALWAYS_ON","ON_DEMAND"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$APS_map = @{
    "AuthenticationType"=@("Get-APSSessionList")
    "FleetType"=@("New-APSFleet")
}

_awsArgumentCompleterRegistration $APS_Completers $APS_map


# Argument completions for service AWS AppSync
$ASYN_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.AppSync.AuthenticationType
        {
            ($_ -eq "New-ASYNGraphqlApi/AuthenticationType") -Or
            ($_ -eq "Update-ASYNGraphqlApi/AuthenticationType")
        }
        {
            $v = "AMAZON_COGNITO_USER_POOLS","API_KEY","AWS_IAM","OPENID_CONNECT"
            break
        }
        
        # Amazon.AppSync.DataSourceType
        {
            ($_ -eq "New-ASYNDataSource/Type") -Or
            ($_ -eq "Update-ASYNDataSource/Type")
        }
        {
            $v = "AMAZON_DYNAMODB","AMAZON_ELASTICSEARCH","AWS_LAMBDA","NONE"
            break
        }
        
        # Amazon.AppSync.FieldLogLevel
        {
            ($_ -eq "New-ASYNGraphqlApi/LogConfig_FieldLogLevel") -Or
            ($_ -eq "Update-ASYNGraphqlApi/LogConfig_FieldLogLevel")
        }
        {
            $v = "ALL","ERROR","NONE"
            break
        }
        
        # Amazon.AppSync.OutputType
        "Get-ASYNIntrospectionSchema/Format"
        {
            $v = "JSON","SDL"
            break
        }
        
        # Amazon.AppSync.TypeDefinitionFormat
        {
            ($_ -eq "Get-ASYNType/Format") -Or
            ($_ -eq "Get-ASYNTypeList/Format") -Or
            ($_ -eq "New-ASYNType/Format") -Or
            ($_ -eq "Update-ASYNType/Format")
        }
        {
            $v = "JSON","SDL"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$ASYN_map = @{
    "AuthenticationType"=@("New-ASYNGraphqlApi","Update-ASYNGraphqlApi")
    "Format"=@("Get-ASYNIntrospectionSchema","Get-ASYNType","Get-ASYNTypeList","New-ASYNType","Update-ASYNType")
    "LogConfig_FieldLogLevel"=@("New-ASYNGraphqlApi","Update-ASYNGraphqlApi")
    "Type"=@("New-ASYNDataSource","Update-ASYNDataSource")
}

_awsArgumentCompleterRegistration $ASYN_Completers $ASYN_map


# Argument completions for service Amazon Athena
$ATH_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Athena.EncryptionOption
        "Start-ATHQueryExecution/ResultConfiguration_EncryptionConfiguration_EncryptionOption"
        {
            $v = "CSE_KMS","SSE_KMS","SSE_S3"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$ATH_map = @{
    "ResultConfiguration_EncryptionConfiguration_EncryptionOption"=@("Start-ATHQueryExecution")
}

_awsArgumentCompleterRegistration $ATH_Completers $ATH_map


# Argument completions for service Auto Scaling
$AS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.AutoScaling.MetricStatistic
        "Write-ASScalingPolicy/TargetTrackingConfiguration_CustomizedMetricSpecification_Statistic"
        {
            $v = "Average","Maximum","Minimum","SampleCount","Sum"
            break
        }
        
        # Amazon.AutoScaling.MetricType
        "Write-ASScalingPolicy/TargetTrackingConfiguration_PredefinedMetricSpecification_PredefinedMetricType"
        {
            $v = "ALBRequestCountPerTarget","ASGAverageCPUUtilization","ASGAverageNetworkIn","ASGAverageNetworkOut"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$AS_map = @{
    "TargetTrackingConfiguration_CustomizedMetricSpecification_Statistic"=@("Write-ASScalingPolicy")
    "TargetTrackingConfiguration_PredefinedMetricSpecification_PredefinedMetricType"=@("Write-ASScalingPolicy")
}

_awsArgumentCompleterRegistration $AS_Completers $AS_map


# Argument completions for service AWS Batch
$BAT_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Batch.CEState
        {
            ($_ -eq "New-BATComputeEnvironment/State") -Or
            ($_ -eq "Update-BATComputeEnvironment/State")
        }
        {
            $v = "DISABLED","ENABLED"
            break
        }
        
        # Amazon.Batch.CEType
        "New-BATComputeEnvironment/Type"
        {
            $v = "MANAGED","UNMANAGED"
            break
        }
        
        # Amazon.Batch.CRType
        "New-BATComputeEnvironment/ComputeResources_Type"
        {
            $v = "EC2","SPOT"
            break
        }
        
        # Amazon.Batch.JobDefinitionType
        "Register-BATJobDefinition/Type"
        {
            $v = "container"
            break
        }
        
        # Amazon.Batch.JobStatus
        "Get-BATJobsList/JobStatus"
        {
            $v = "FAILED","PENDING","RUNNABLE","RUNNING","STARTING","SUBMITTED","SUCCEEDED"
            break
        }
        
        # Amazon.Batch.JQState
        {
            ($_ -eq "New-BATJobQueue/State") -Or
            ($_ -eq "Update-BATJobQueue/State")
        }
        {
            $v = "DISABLED","ENABLED"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$BAT_map = @{
    "ComputeResources_Type"=@("New-BATComputeEnvironment")
    "JobStatus"=@("Get-BATJobsList")
    "State"=@("New-BATComputeEnvironment","New-BATJobQueue","Update-BATComputeEnvironment","Update-BATJobQueue")
    "Type"=@("New-BATComputeEnvironment","Register-BATJobDefinition")
}

_awsArgumentCompleterRegistration $BAT_Completers $BAT_map


# Argument completions for service AWS Elastic Beanstalk
$EB_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.ElasticBeanstalk.ActionStatus
        "Get-EBEnvironmentManagedAction/Status"
        {
            $v = "Pending","Running","Scheduled","Unknown"
            break
        }
        
        # Amazon.ElasticBeanstalk.ComputeType
        "New-EBApplicationVersion/BuildConfiguration_ComputeType"
        {
            $v = "BUILD_GENERAL1_LARGE","BUILD_GENERAL1_MEDIUM","BUILD_GENERAL1_SMALL"
            break
        }
        
        # Amazon.ElasticBeanstalk.EnvironmentInfoType
        {
            ($_ -eq "Get-EBEnvironmentInfo/InfoType") -Or
            ($_ -eq "Request-EBEnvironmentInfo/InfoType")
        }
        {
            $v = "bundle","tail"
            break
        }
        
        # Amazon.ElasticBeanstalk.EventSeverity
        "Get-EBEvent/Severity"
        {
            $v = "DEBUG","ERROR","FATAL","INFO","TRACE","WARN"
            break
        }
        
        # Amazon.ElasticBeanstalk.SourceRepository
        "New-EBApplicationVersion/SourceBuildInformation_SourceRepository"
        {
            $v = "CodeCommit","S3"
            break
        }
        
        # Amazon.ElasticBeanstalk.SourceType
        "New-EBApplicationVersion/SourceBuildInformation_SourceType"
        {
            $v = "Git","Zip"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$EB_map = @{
    "BuildConfiguration_ComputeType"=@("New-EBApplicationVersion")
    "InfoType"=@("Get-EBEnvironmentInfo","Request-EBEnvironmentInfo")
    "Severity"=@("Get-EBEvent")
    "SourceBuildInformation_SourceRepository"=@("New-EBApplicationVersion")
    "SourceBuildInformation_SourceType"=@("New-EBApplicationVersion")
    "Status"=@("Get-EBEnvironmentManagedAction")
}

_awsArgumentCompleterRegistration $EB_Completers $EB_map


# Argument completions for service AWS Budgets
$BGT_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Budgets.BudgetType
        {
            ($_ -eq "New-BGTBudget/Budget_BudgetType") -Or
            ($_ -eq "Update-BGTBudget/NewBudget_BudgetType")
        }
        {
            $v = "COST","RI_COVERAGE","RI_UTILIZATION","USAGE"
            break
        }
        
        # Amazon.Budgets.ComparisonOperator
        {
            ($_ -eq "Update-BGTNotification/NewNotification_ComparisonOperator") -Or
            ($_ -eq "Get-BGTSubscribersForNotification/Notification_ComparisonOperator") -Or
            ($_ -eq "New-BGTNotification/Notification_ComparisonOperator") -Or
            ($_ -eq "New-BGTSubscriber/Notification_ComparisonOperator") -Or
            ($_ -eq "Remove-BGTNotification/Notification_ComparisonOperator") -Or
            ($_ -eq "Remove-BGTSubscriber/Notification_ComparisonOperator") -Or
            ($_ -eq "Update-BGTSubscriber/Notification_ComparisonOperator") -Or
            ($_ -eq "Update-BGTNotification/OldNotification_ComparisonOperator")
        }
        {
            $v = "EQUAL_TO","GREATER_THAN","LESS_THAN"
            break
        }
        
        # Amazon.Budgets.NotificationType
        {
            ($_ -eq "Update-BGTNotification/NewNotification_NotificationType") -Or
            ($_ -eq "Get-BGTSubscribersForNotification/Notification_NotificationType") -Or
            ($_ -eq "New-BGTNotification/Notification_NotificationType") -Or
            ($_ -eq "New-BGTSubscriber/Notification_NotificationType") -Or
            ($_ -eq "Remove-BGTNotification/Notification_NotificationType") -Or
            ($_ -eq "Remove-BGTSubscriber/Notification_NotificationType") -Or
            ($_ -eq "Update-BGTSubscriber/Notification_NotificationType") -Or
            ($_ -eq "Update-BGTNotification/OldNotification_NotificationType")
        }
        {
            $v = "ACTUAL","FORECASTED"
            break
        }
        
        # Amazon.Budgets.SubscriptionType
        {
            ($_ -eq "Update-BGTSubscriber/NewSubscriber_SubscriptionType") -Or
            ($_ -eq "Update-BGTSubscriber/OldSubscriber_SubscriptionType") -Or
            ($_ -eq "New-BGTSubscriber/Subscriber_SubscriptionType") -Or
            ($_ -eq "Remove-BGTSubscriber/Subscriber_SubscriptionType")
        }
        {
            $v = "EMAIL","SNS"
            break
        }
        
        # Amazon.Budgets.ThresholdType
        {
            ($_ -eq "Update-BGTNotification/NewNotification_ThresholdType") -Or
            ($_ -eq "Get-BGTSubscribersForNotification/Notification_ThresholdType") -Or
            ($_ -eq "New-BGTNotification/Notification_ThresholdType") -Or
            ($_ -eq "New-BGTSubscriber/Notification_ThresholdType") -Or
            ($_ -eq "Remove-BGTNotification/Notification_ThresholdType") -Or
            ($_ -eq "Remove-BGTSubscriber/Notification_ThresholdType") -Or
            ($_ -eq "Update-BGTSubscriber/Notification_ThresholdType") -Or
            ($_ -eq "Update-BGTNotification/OldNotification_ThresholdType")
        }
        {
            $v = "ABSOLUTE_VALUE","PERCENTAGE"
            break
        }
        
        # Amazon.Budgets.TimeUnit
        {
            ($_ -eq "New-BGTBudget/Budget_TimeUnit") -Or
            ($_ -eq "Update-BGTBudget/NewBudget_TimeUnit")
        }
        {
            $v = "ANNUALLY","DAILY","MONTHLY","QUARTERLY"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$BGT_map = @{
    "Budget_BudgetType"=@("New-BGTBudget")
    "Budget_TimeUnit"=@("New-BGTBudget")
    "NewBudget_BudgetType"=@("Update-BGTBudget")
    "NewBudget_TimeUnit"=@("Update-BGTBudget")
    "NewNotification_ComparisonOperator"=@("Update-BGTNotification")
    "NewNotification_NotificationType"=@("Update-BGTNotification")
    "NewNotification_ThresholdType"=@("Update-BGTNotification")
    "NewSubscriber_SubscriptionType"=@("Update-BGTSubscriber")
    "Notification_ComparisonOperator"=@("Get-BGTSubscribersForNotification","New-BGTNotification","New-BGTSubscriber","Remove-BGTNotification","Remove-BGTSubscriber","Update-BGTSubscriber")
    "Notification_NotificationType"=@("Get-BGTSubscribersForNotification","New-BGTNotification","New-BGTSubscriber","Remove-BGTNotification","Remove-BGTSubscriber","Update-BGTSubscriber")
    "Notification_ThresholdType"=@("Get-BGTSubscribersForNotification","New-BGTNotification","New-BGTSubscriber","Remove-BGTNotification","Remove-BGTSubscriber","Update-BGTSubscriber")
    "OldNotification_ComparisonOperator"=@("Update-BGTNotification")
    "OldNotification_NotificationType"=@("Update-BGTNotification")
    "OldNotification_ThresholdType"=@("Update-BGTNotification")
    "OldSubscriber_SubscriptionType"=@("Update-BGTSubscriber")
    "Subscriber_SubscriptionType"=@("New-BGTSubscriber","Remove-BGTSubscriber")
}

_awsArgumentCompleterRegistration $BGT_Completers $BGT_map


# Argument completions for service AWS Certificate Manager
$ACM_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CertificateManager.CertificateTransparencyLoggingPreference
        {
            ($_ -eq "New-ACMCertificate/Options_CertificateTransparencyLoggingPreference") -Or
            ($_ -eq "Update-ACMCertificateOption/Options_CertificateTransparencyLoggingPreference")
        }
        {
            $v = "DISABLED","ENABLED"
            break
        }
        
        # Amazon.CertificateManager.ValidationMethod
        "New-ACMCertificate/ValidationMethod"
        {
            $v = "DNS","EMAIL"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$ACM_map = @{
    "Options_CertificateTransparencyLoggingPreference"=@("New-ACMCertificate","Update-ACMCertificateOption")
    "ValidationMethod"=@("New-ACMCertificate")
}

_awsArgumentCompleterRegistration $ACM_Completers $ACM_map


# Argument completions for service AWS Cloud9
$C9_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Cloud9.MemberPermissions
        {
            ($_ -eq "New-C9EnvironmentMembership/Permissions") -Or
            ($_ -eq "Update-C9EnvironmentMembership/Permissions")
        }
        {
            $v = "read-only","read-write"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$C9_map = @{
    "Permissions"=@("New-C9EnvironmentMembership","Update-C9EnvironmentMembership")
}

_awsArgumentCompleterRegistration $C9_Completers $C9_map


# Argument completions for service AWS Cloud Directory
$CDIR_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CloudDirectory.ConsistencyLevel
        {
            ($_ -eq "Get-CDIRIncomingTypedLink/ConsistencyLevel") -Or
            ($_ -eq "Get-CDIRIndex/ConsistencyLevel") -Or
            ($_ -eq "Get-CDIRLinkAttribute/ConsistencyLevel") -Or
            ($_ -eq "Get-CDIRObjectAttribute/ConsistencyLevel") -Or
            ($_ -eq "Get-CDIRObjectAttributeList/ConsistencyLevel") -Or
            ($_ -eq "Get-CDIRObjectChild/ConsistencyLevel") -Or
            ($_ -eq "Get-CDIRObjectIndex/ConsistencyLevel") -Or
            ($_ -eq "Get-CDIRObjectInformation/ConsistencyLevel") -Or
            ($_ -eq "Get-CDIRObjectParent/ConsistencyLevel") -Or
            ($_ -eq "Get-CDIRObjectPolicy/ConsistencyLevel") -Or
            ($_ -eq "Get-CDIROutgoingTypedLink/ConsistencyLevel") -Or
            ($_ -eq "Get-CDIRPolicyAttachment/ConsistencyLevel") -Or
            ($_ -eq "Read-CDIRDirectoryBatch/ConsistencyLevel")
        }
        {
            $v = "EVENTUAL","SERIALIZABLE"
            break
        }
        
        # Amazon.CloudDirectory.DirectoryState
        "Get-CDIRDirectory/State"
        {
            $v = "DELETED","DISABLED","ENABLED"
            break
        }
        
        # Amazon.CloudDirectory.FacetStyle
        "New-CDIRFacet/FacetStyle"
        {
            $v = "DYNAMIC","STATIC"
            break
        }
        
        # Amazon.CloudDirectory.ObjectType
        {
            ($_ -eq "New-CDIRFacet/ObjectType") -Or
            ($_ -eq "Update-CDIRFacet/ObjectType")
        }
        {
            $v = "INDEX","LEAF_NODE","NODE","POLICY"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CDIR_map = @{
    "ConsistencyLevel"=@("Get-CDIRIncomingTypedLink","Get-CDIRIndex","Get-CDIRLinkAttribute","Get-CDIRObjectAttribute","Get-CDIRObjectAttributeList","Get-CDIRObjectChild","Get-CDIRObjectIndex","Get-CDIRObjectInformation","Get-CDIRObjectParent","Get-CDIRObjectPolicy","Get-CDIROutgoingTypedLink","Get-CDIRPolicyAttachment","Read-CDIRDirectoryBatch")
    "FacetStyle"=@("New-CDIRFacet")
    "ObjectType"=@("New-CDIRFacet","Update-CDIRFacet")
    "State"=@("Get-CDIRDirectory")
}

_awsArgumentCompleterRegistration $CDIR_Completers $CDIR_map


# Argument completions for service AWS CloudFormation
$CFN_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CloudFormation.OnFailure
        "New-CFNStack/OnFailure"
        {
            $v = "DELETE","DO_NOTHING","ROLLBACK"
            break
        }
        
        # Amazon.CloudFormation.ResourceSignalStatus
        "Send-CFNResourceSignal/Status"
        {
            $v = "FAILURE","SUCCESS"
            break
        }
        
        # Amazon.CloudFormation.StackSetStatus
        "Get-CFNStackSetList/Status"
        {
            $v = "ACTIVE","DELETED"
            break
        }
        
        # Amazon.CloudFormation.StackStatus
        {
            ($_ -eq "Test-CFNStack/Status") -Or
            ($_ -eq "Wait-CFNStack/Status") -Or
            ($_ -eq "Watch-CFNStackEvent/Status")
        }
        {
            $v = "CREATE_COMPLETE","CREATE_FAILED","CREATE_IN_PROGRESS","DELETE_COMPLETE","DELETE_FAILED","DELETE_IN_PROGRESS","REVIEW_IN_PROGRESS","ROLLBACK_COMPLETE","ROLLBACK_FAILED","ROLLBACK_IN_PROGRESS","UPDATE_COMPLETE","UPDATE_COMPLETE_CLEANUP_IN_PROGRESS","UPDATE_IN_PROGRESS","UPDATE_ROLLBACK_COMPLETE","UPDATE_ROLLBACK_COMPLETE_CLEANUP_IN_PROGRESS","UPDATE_ROLLBACK_FAILED","UPDATE_ROLLBACK_IN_PROGRESS"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CFN_map = @{
    "OnFailure"=@("New-CFNStack")
    "Status"=@("Get-CFNStackSetList","Send-CFNResourceSignal","Test-CFNStack","Wait-CFNStack","Watch-CFNStackEvent")
}

_awsArgumentCompleterRegistration $CFN_Completers $CFN_map


# Argument completions for service Amazon CloudFront
$CF_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CloudFront.CertificateSource
        {
            ($_ -eq "New-CFDistribution/DistributionConfig_ViewerCertificate_CertificateSource") -Or
            ($_ -eq "Update-CFDistribution/DistributionConfig_ViewerCertificate_CertificateSource") -Or
            ($_ -eq "New-CFDistributionWithTag/DistributionConfigWithTags_DistributionConfig_ViewerCertificate_CertificateSource")
        }
        {
            $v = "acm","cloudfront","iam"
            break
        }
        
        # Amazon.CloudFront.GeoRestrictionType
        {
            ($_ -eq "New-CFDistribution/DistributionConfig_Restrictions_GeoRestriction_RestrictionType") -Or
            ($_ -eq "Update-CFDistribution/DistributionConfig_Restrictions_GeoRestriction_RestrictionType") -Or
            ($_ -eq "New-CFDistributionWithTag/DistributionConfigWithTags_DistributionConfig_Restrictions_GeoRestriction_RestrictionType")
        }
        {
            $v = "blacklist","none","whitelist"
            break
        }
        
        # Amazon.CloudFront.HttpVersion
        {
            ($_ -eq "New-CFDistribution/DistributionConfig_HttpVersion") -Or
            ($_ -eq "Update-CFDistribution/DistributionConfig_HttpVersion") -Or
            ($_ -eq "New-CFDistributionWithTag/DistributionConfigWithTags_DistributionConfig_HttpVersion")
        }
        {
            $v = "http1.1","http2"
            break
        }
        
        # Amazon.CloudFront.ItemSelection
        {
            ($_ -eq "New-CFDistribution/DistributionConfig_DefaultCacheBehavior_ForwardedValues_Cookies_Forward") -Or
            ($_ -eq "Update-CFDistribution/DistributionConfig_DefaultCacheBehavior_ForwardedValues_Cookies_Forward") -Or
            ($_ -eq "New-CFDistributionWithTag/DistributionConfigWithTags_DistributionConfig_DefaultCacheBehavior_ForwardedValues_Cookies_Forward")
        }
        {
            $v = "all","none","whitelist"
            break
        }
        
        # Amazon.CloudFront.MinimumProtocolVersion
        {
            ($_ -eq "New-CFDistribution/DistributionConfig_ViewerCertificate_MinimumProtocolVersion") -Or
            ($_ -eq "Update-CFDistribution/DistributionConfig_ViewerCertificate_MinimumProtocolVersion") -Or
            ($_ -eq "New-CFDistributionWithTag/DistributionConfigWithTags_DistributionConfig_ViewerCertificate_MinimumProtocolVersion")
        }
        {
            $v = "SSLv3","TLSv1","TLSv1.1_2016","TLSv1.2_2018","TLSv1_2016"
            break
        }
        
        # Amazon.CloudFront.PriceClass
        {
            ($_ -eq "New-CFDistribution/DistributionConfig_PriceClass") -Or
            ($_ -eq "Update-CFDistribution/DistributionConfig_PriceClass") -Or
            ($_ -eq "New-CFDistributionWithTag/DistributionConfigWithTags_DistributionConfig_PriceClass") -Or
            ($_ -eq "New-CFStreamingDistribution/StreamingDistributionConfig_PriceClass") -Or
            ($_ -eq "Update-CFStreamingDistribution/StreamingDistributionConfig_PriceClass") -Or
            ($_ -eq "New-CFStreamingDistributionWithTag/StreamingDistributionConfigWithTags_StreamingDistributionConfig_PriceClass")
        }
        {
            $v = "PriceClass_100","PriceClass_200","PriceClass_All"
            break
        }
        
        # Amazon.CloudFront.SSLSupportMethod
        {
            ($_ -eq "New-CFDistribution/DistributionConfig_ViewerCertificate_SSLSupportMethod") -Or
            ($_ -eq "Update-CFDistribution/DistributionConfig_ViewerCertificate_SSLSupportMethod") -Or
            ($_ -eq "New-CFDistributionWithTag/DistributionConfigWithTags_DistributionConfig_ViewerCertificate_SSLSupportMethod")
        }
        {
            $v = "sni-only","vip"
            break
        }
        
        # Amazon.CloudFront.ViewerProtocolPolicy
        {
            ($_ -eq "New-CFDistribution/DistributionConfig_DefaultCacheBehavior_ViewerProtocolPolicy") -Or
            ($_ -eq "Update-CFDistribution/DistributionConfig_DefaultCacheBehavior_ViewerProtocolPolicy") -Or
            ($_ -eq "New-CFDistributionWithTag/DistributionConfigWithTags_DistributionConfig_DefaultCacheBehavior_ViewerProtocolPolicy")
        }
        {
            $v = "allow-all","https-only","redirect-to-https"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CF_map = @{
    "DistributionConfig_DefaultCacheBehavior_ForwardedValues_Cookies_Forward"=@("New-CFDistribution","Update-CFDistribution")
    "DistributionConfig_DefaultCacheBehavior_ViewerProtocolPolicy"=@("New-CFDistribution","Update-CFDistribution")
    "DistributionConfig_HttpVersion"=@("New-CFDistribution","Update-CFDistribution")
    "DistributionConfig_PriceClass"=@("New-CFDistribution","Update-CFDistribution")
    "DistributionConfig_Restrictions_GeoRestriction_RestrictionType"=@("New-CFDistribution","Update-CFDistribution")
    "DistributionConfig_ViewerCertificate_CertificateSource"=@("New-CFDistribution","Update-CFDistribution")
    "DistributionConfig_ViewerCertificate_MinimumProtocolVersion"=@("New-CFDistribution","Update-CFDistribution")
    "DistributionConfig_ViewerCertificate_SSLSupportMethod"=@("New-CFDistribution","Update-CFDistribution")
    "DistributionConfigWithTags_DistributionConfig_DefaultCacheBehavior_ForwardedValues_Cookies_Forward"=@("New-CFDistributionWithTag")
    "DistributionConfigWithTags_DistributionConfig_DefaultCacheBehavior_ViewerProtocolPolicy"=@("New-CFDistributionWithTag")
    "DistributionConfigWithTags_DistributionConfig_HttpVersion"=@("New-CFDistributionWithTag")
    "DistributionConfigWithTags_DistributionConfig_PriceClass"=@("New-CFDistributionWithTag")
    "DistributionConfigWithTags_DistributionConfig_Restrictions_GeoRestriction_RestrictionType"=@("New-CFDistributionWithTag")
    "DistributionConfigWithTags_DistributionConfig_ViewerCertificate_CertificateSource"=@("New-CFDistributionWithTag")
    "DistributionConfigWithTags_DistributionConfig_ViewerCertificate_MinimumProtocolVersion"=@("New-CFDistributionWithTag")
    "DistributionConfigWithTags_DistributionConfig_ViewerCertificate_SSLSupportMethod"=@("New-CFDistributionWithTag")
    "StreamingDistributionConfig_PriceClass"=@("New-CFStreamingDistribution","Update-CFStreamingDistribution")
    "StreamingDistributionConfigWithTags_StreamingDistributionConfig_PriceClass"=@("New-CFStreamingDistributionWithTag")
}

_awsArgumentCompleterRegistration $CF_Completers $CF_map


# Argument completions for service Amazon CloudSearch
$CS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CloudSearch.AlgorithmicStemming
        "Set-CSAnalysisScheme/AnalysisScheme_AnalysisOptions_AlgorithmicStemming"
        {
            $v = "full","light","minimal","none"
            break
        }
        
        # Amazon.CloudSearch.AnalysisSchemeLanguage
        "Set-CSAnalysisScheme/AnalysisScheme_AnalysisSchemeLanguage"
        {
            $v = "ar","bg","ca","cs","da","de","el","en","es","eu","fa","fi","fr","ga","gl","he","hi","hu","hy","id","it","ja","ko","lv","mul","nl","no","pt","ro","ru","sv","th","tr","zh-Hans","zh-Hant"
            break
        }
        
        # Amazon.CloudSearch.IndexFieldType
        "Set-CSIndexField/IndexField_IndexFieldType"
        {
            $v = "date","date-array","double","double-array","int","int-array","latlon","literal","literal-array","text","text-array"
            break
        }
        
        # Amazon.CloudSearch.PartitionInstanceType
        "Update-CSScalingParameter/ScalingParameters_DesiredInstanceType"
        {
            $v = "search.m1.large","search.m1.small","search.m2.2xlarge","search.m2.xlarge","search.m3.2xlarge","search.m3.large","search.m3.medium","search.m3.xlarge"
            break
        }
        
        # Amazon.CloudSearch.SuggesterFuzzyMatching
        "Set-CSSuggester/Suggester_DocumentSuggesterOptions_FuzzyMatching"
        {
            $v = "high","low","none"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CS_map = @{
    "AnalysisScheme_AnalysisOptions_AlgorithmicStemming"=@("Set-CSAnalysisScheme")
    "AnalysisScheme_AnalysisSchemeLanguage"=@("Set-CSAnalysisScheme")
    "IndexField_IndexFieldType"=@("Set-CSIndexField")
    "ScalingParameters_DesiredInstanceType"=@("Update-CSScalingParameter")
    "Suggester_DocumentSuggesterOptions_FuzzyMatching"=@("Set-CSSuggester")
}

_awsArgumentCompleterRegistration $CS_Completers $CS_map


# Argument completions for service Amazon CloudSearchDomain
$CSD_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CloudSearchDomain.ContentType
        "Write-CSDDocument/ContentType"
        {
            $v = "application/json","application/xml"
            break
        }
        
        # Amazon.CloudSearchDomain.QueryParser
        "Search-CSDDocument/QueryParser"
        {
            $v = "dismax","lucene","simple","structured"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CSD_map = @{
    "ContentType"=@("Write-CSDDocument")
    "QueryParser"=@("Search-CSDDocument")
}

_awsArgumentCompleterRegistration $CSD_Completers $CSD_map


# Argument completions for service Amazon CloudWatch
$CW_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CloudWatch.ComparisonOperator
        "Write-CWMetricAlarm/ComparisonOperator"
        {
            $v = "GreaterThanOrEqualToThreshold","GreaterThanThreshold","LessThanOrEqualToThreshold","LessThanThreshold"
            break
        }
        
        # Amazon.CloudWatch.HistoryItemType
        "Get-CWAlarmHistory/HistoryItemType"
        {
            $v = "Action","ConfigurationUpdate","StateUpdate"
            break
        }
        
        # Amazon.CloudWatch.ScanBy
        "Get-CWMetricData/ScanBy"
        {
            $v = "TimestampAscending","TimestampDescending"
            break
        }
        
        # Amazon.CloudWatch.StandardUnit
        {
            ($_ -eq "Get-CWAlarmForMetric/Unit") -Or
            ($_ -eq "Get-CWMetricStatistic/Unit") -Or
            ($_ -eq "Write-CWMetricAlarm/Unit")
        }
        {
            $v = "Bits","Bits/Second","Bytes","Bytes/Second","Count","Count/Second","Gigabits","Gigabits/Second","Gigabytes","Gigabytes/Second","Kilobits","Kilobits/Second","Kilobytes","Kilobytes/Second","Megabits","Megabits/Second","Megabytes","Megabytes/Second","Microseconds","Milliseconds","None","Percent","Seconds","Terabits","Terabits/Second","Terabytes","Terabytes/Second"
            break
        }
        
        # Amazon.CloudWatch.StateValue
        {
            ($_ -eq "Get-CWAlarm/StateValue") -Or
            ($_ -eq "Set-CWAlarmState/StateValue")
        }
        {
            $v = "ALARM","INSUFFICIENT_DATA","OK"
            break
        }
        
        # Amazon.CloudWatch.Statistic
        {
            ($_ -eq "Get-CWAlarmForMetric/Statistic") -Or
            ($_ -eq "Write-CWMetricAlarm/Statistic")
        }
        {
            $v = "Average","Maximum","Minimum","SampleCount","Sum"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CW_map = @{
    "ComparisonOperator"=@("Write-CWMetricAlarm")
    "HistoryItemType"=@("Get-CWAlarmHistory")
    "ScanBy"=@("Get-CWMetricData")
    "StateValue"=@("Get-CWAlarm","Set-CWAlarmState")
    "Statistic"=@("Get-CWAlarmForMetric","Write-CWMetricAlarm")
    "Unit"=@("Get-CWAlarmForMetric","Get-CWMetricStatistic","Write-CWMetricAlarm")
}

_awsArgumentCompleterRegistration $CW_Completers $CW_map


# Argument completions for service Amazon CloudWatch Events
$CWE_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CloudWatchEvents.RuleState
        "Write-CWERule/State"
        {
            $v = "DISABLED","ENABLED"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CWE_map = @{
    "State"=@("Write-CWERule")
}

_awsArgumentCompleterRegistration $CWE_Completers $CWE_map


# Argument completions for service Amazon CloudWatch Logs
$CWL_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CloudWatchLogs.Distribution
        "Write-CWLSubscriptionFilter/Distribution"
        {
            $v = "ByLogStream","Random"
            break
        }
        
        # Amazon.CloudWatchLogs.ExportTaskStatusCode
        "Get-CWLExportTask/StatusCode"
        {
            $v = "CANCELLED","COMPLETED","FAILED","PENDING","PENDING_CANCEL","RUNNING"
            break
        }
        
        # Amazon.CloudWatchLogs.OrderBy
        "Get-CWLLogStream/OrderBy"
        {
            $v = "LastEventTime","LogStreamName"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CWL_map = @{
    "Distribution"=@("Write-CWLSubscriptionFilter")
    "OrderBy"=@("Get-CWLLogStream")
    "StatusCode"=@("Get-CWLExportTask")
}

_awsArgumentCompleterRegistration $CWL_Completers $CWL_map


# Argument completions for service AWS CodeBuild
$CB_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CodeBuild.ArtifactNamespace
        {
            ($_ -eq "New-CBProject/Artifacts_NamespaceType") -Or
            ($_ -eq "Update-CBProject/Artifacts_NamespaceType") -Or
            ($_ -eq "Start-CBBuild/ArtifactsOverride_NamespaceType")
        }
        {
            $v = "BUILD_ID","NONE"
            break
        }
        
        # Amazon.CodeBuild.ArtifactPackaging
        {
            ($_ -eq "New-CBProject/Artifacts_Packaging") -Or
            ($_ -eq "Update-CBProject/Artifacts_Packaging") -Or
            ($_ -eq "Start-CBBuild/ArtifactsOverride_Packaging")
        }
        {
            $v = "NONE","ZIP"
            break
        }
        
        # Amazon.CodeBuild.ArtifactsType
        {
            ($_ -eq "New-CBProject/Artifacts_Type") -Or
            ($_ -eq "Update-CBProject/Artifacts_Type") -Or
            ($_ -eq "Start-CBBuild/ArtifactsOverride_Type")
        }
        {
            $v = "CODEPIPELINE","NO_ARTIFACTS","S3"
            break
        }
        
        # Amazon.CodeBuild.CacheType
        {
            ($_ -eq "New-CBProject/Cache_Type") -Or
            ($_ -eq "Update-CBProject/Cache_Type") -Or
            ($_ -eq "Start-CBBuild/CacheOverride_Type")
        }
        {
            $v = "NO_CACHE","S3"
            break
        }
        
        # Amazon.CodeBuild.ComputeType
        {
            ($_ -eq "Start-CBBuild/ComputeTypeOverride") -Or
            ($_ -eq "New-CBProject/Environment_ComputeType") -Or
            ($_ -eq "Update-CBProject/Environment_ComputeType")
        }
        {
            $v = "BUILD_GENERAL1_LARGE","BUILD_GENERAL1_MEDIUM","BUILD_GENERAL1_SMALL"
            break
        }
        
        # Amazon.CodeBuild.EnvironmentType
        {
            ($_ -eq "New-CBProject/Environment_Type") -Or
            ($_ -eq "Update-CBProject/Environment_Type") -Or
            ($_ -eq "Start-CBBuild/EnvironmentTypeOverride")
        }
        {
            $v = "LINUX_CONTAINER","WINDOWS_CONTAINER"
            break
        }
        
        # Amazon.CodeBuild.ProjectSortByType
        "Get-CBProjectList/SortBy"
        {
            $v = "CREATED_TIME","LAST_MODIFIED_TIME","NAME"
            break
        }
        
        # Amazon.CodeBuild.SortOrderType
        {
            ($_ -eq "Get-CBBuildIdList/SortOrder") -Or
            ($_ -eq "Get-CBBuildIdListForProject/SortOrder") -Or
            ($_ -eq "Get-CBProjectList/SortOrder")
        }
        {
            $v = "ASCENDING","DESCENDING"
            break
        }
        
        # Amazon.CodeBuild.SourceAuthType
        {
            ($_ -eq "New-CBProject/Source_Auth_Type") -Or
            ($_ -eq "Update-CBProject/Source_Auth_Type") -Or
            ($_ -eq "Start-CBBuild/SourceAuthOverride_Type")
        }
        {
            $v = "OAUTH"
            break
        }
        
        # Amazon.CodeBuild.SourceType
        {
            ($_ -eq "New-CBProject/Source_Type") -Or
            ($_ -eq "Update-CBProject/Source_Type") -Or
            ($_ -eq "Start-CBBuild/SourceTypeOverride")
        }
        {
            $v = "BITBUCKET","CODECOMMIT","CODEPIPELINE","GITHUB","GITHUB_ENTERPRISE","S3"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CB_map = @{
    "Artifacts_NamespaceType"=@("New-CBProject","Update-CBProject")
    "Artifacts_Packaging"=@("New-CBProject","Update-CBProject")
    "Artifacts_Type"=@("New-CBProject","Update-CBProject")
    "ArtifactsOverride_NamespaceType"=@("Start-CBBuild")
    "ArtifactsOverride_Packaging"=@("Start-CBBuild")
    "ArtifactsOverride_Type"=@("Start-CBBuild")
    "Cache_Type"=@("New-CBProject","Update-CBProject")
    "CacheOverride_Type"=@("Start-CBBuild")
    "ComputeTypeOverride"=@("Start-CBBuild")
    "Environment_ComputeType"=@("New-CBProject","Update-CBProject")
    "Environment_Type"=@("New-CBProject","Update-CBProject")
    "EnvironmentTypeOverride"=@("Start-CBBuild")
    "SortBy"=@("Get-CBProjectList")
    "SortOrder"=@("Get-CBBuildIdList","Get-CBBuildIdListForProject","Get-CBProjectList")
    "Source_Auth_Type"=@("New-CBProject","Update-CBProject")
    "Source_Type"=@("New-CBProject","Update-CBProject")
    "SourceAuthOverride_Type"=@("Start-CBBuild")
    "SourceTypeOverride"=@("Start-CBBuild")
}

_awsArgumentCompleterRegistration $CB_Completers $CB_map


# Argument completions for service AWS CodeCommit
$CC_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CodeCommit.FileModeTypeEnum
        "Write-CCFile/FileMode"
        {
            $v = "EXECUTABLE","NORMAL","SYMLINK"
            break
        }
        
        # Amazon.CodeCommit.MergeOptionTypeEnum
        "Get-CCMergeConflict/MergeOption"
        {
            $v = "FAST_FORWARD_MERGE"
            break
        }
        
        # Amazon.CodeCommit.OrderEnum
        "Get-CCRepositoryList/Order"
        {
            $v = "ascending","descending"
            break
        }
        
        # Amazon.CodeCommit.PullRequestEventType
        "Get-CCPullRequestEvent/PullRequestEventType"
        {
            $v = "PULL_REQUEST_CREATED","PULL_REQUEST_MERGE_STATE_CHANGED","PULL_REQUEST_SOURCE_REFERENCE_UPDATED","PULL_REQUEST_STATUS_CHANGED"
            break
        }
        
        # Amazon.CodeCommit.PullRequestStatusEnum
        {
            ($_ -eq "Get-CCPullRequestList/PullRequestStatus") -Or
            ($_ -eq "Update-CCPullRequestStatus/PullRequestStatus")
        }
        {
            $v = "CLOSED","OPEN"
            break
        }
        
        # Amazon.CodeCommit.RelativeFileVersionEnum
        {
            ($_ -eq "Send-CCCommentForComparedCommit/Location_RelativeFileVersion") -Or
            ($_ -eq "Send-CCCommentForPullRequest/Location_RelativeFileVersion")
        }
        {
            $v = "AFTER","BEFORE"
            break
        }
        
        # Amazon.CodeCommit.SortByEnum
        "Get-CCRepositoryList/SortBy"
        {
            $v = "lastModifiedDate","repositoryName"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CC_map = @{
    "FileMode"=@("Write-CCFile")
    "Location_RelativeFileVersion"=@("Send-CCCommentForComparedCommit","Send-CCCommentForPullRequest")
    "MergeOption"=@("Get-CCMergeConflict")
    "Order"=@("Get-CCRepositoryList")
    "PullRequestEventType"=@("Get-CCPullRequestEvent")
    "PullRequestStatus"=@("Get-CCPullRequestList","Update-CCPullRequestStatus")
    "SortBy"=@("Get-CCRepositoryList")
}

_awsArgumentCompleterRegistration $CC_Completers $CC_map


# Argument completions for service AWS CodeDeploy
$CD_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CodeDeploy.ApplicationRevisionSortBy
        "Get-CDApplicationRevisionList/SortBy"
        {
            $v = "firstUsedTime","lastUsedTime","registerTime"
            break
        }
        
        # Amazon.CodeDeploy.BundleType
        {
            ($_ -eq "Get-CDApplicationRevision/Revision_S3Location_BundleType") -Or
            ($_ -eq "New-CDDeployment/Revision_S3Location_BundleType") -Or
            ($_ -eq "Register-CDApplicationRevision/Revision_S3Location_BundleType")
        }
        {
            $v = "JSON","tar","tgz","YAML","zip"
            break
        }
        
        # Amazon.CodeDeploy.ComputePlatform
        {
            ($_ -eq "New-CDApplication/ComputePlatform") -Or
            ($_ -eq "New-CDDeploymentConfig/ComputePlatform")
        }
        {
            $v = "Lambda","Server"
            break
        }
        
        # Amazon.CodeDeploy.DeploymentOption
        {
            ($_ -eq "New-CDDeploymentGroup/DeploymentStyle_DeploymentOption") -Or
            ($_ -eq "Update-CDDeploymentGroup/DeploymentStyle_DeploymentOption")
        }
        {
            $v = "WITHOUT_TRAFFIC_CONTROL","WITH_TRAFFIC_CONTROL"
            break
        }
        
        # Amazon.CodeDeploy.DeploymentReadyAction
        {
            ($_ -eq "New-CDDeploymentGroup/BlueGreenDeploymentConfiguration_DeploymentReadyOption_ActionOnTimeout") -Or
            ($_ -eq "Update-CDDeploymentGroup/BlueGreenDeploymentConfiguration_DeploymentReadyOption_ActionOnTimeout")
        }
        {
            $v = "CONTINUE_DEPLOYMENT","STOP_DEPLOYMENT"
            break
        }
        
        # Amazon.CodeDeploy.DeploymentType
        {
            ($_ -eq "New-CDDeploymentGroup/DeploymentStyle_DeploymentType") -Or
            ($_ -eq "Update-CDDeploymentGroup/DeploymentStyle_DeploymentType")
        }
        {
            $v = "BLUE_GREEN","IN_PLACE"
            break
        }
        
        # Amazon.CodeDeploy.FileExistsBehavior
        "New-CDDeployment/FileExistsBehavior"
        {
            $v = "DISALLOW","OVERWRITE","RETAIN"
            break
        }
        
        # Amazon.CodeDeploy.GreenFleetProvisioningAction
        {
            ($_ -eq "New-CDDeploymentGroup/BlueGreenDeploymentConfiguration_GreenFleetProvisioningOption_Action") -Or
            ($_ -eq "Update-CDDeploymentGroup/BlueGreenDeploymentConfiguration_GreenFleetProvisioningOption_Action")
        }
        {
            $v = "COPY_AUTO_SCALING_GROUP","DISCOVER_EXISTING"
            break
        }
        
        # Amazon.CodeDeploy.InstanceAction
        {
            ($_ -eq "New-CDDeploymentGroup/BlueGreenDeploymentConfiguration_TerminateBlueInstancesOnDeploymentSuccess_Action") -Or
            ($_ -eq "Update-CDDeploymentGroup/BlueGreenDeploymentConfiguration_TerminateBlueInstancesOnDeploymentSuccess_Action")
        }
        {
            $v = "KEEP_ALIVE","TERMINATE"
            break
        }
        
        # Amazon.CodeDeploy.LifecycleEventStatus
        "Write-CDLifecycleEventHookExecutionStatus/Status"
        {
            $v = "Failed","InProgress","Pending","Skipped","Succeeded","Unknown"
            break
        }
        
        # Amazon.CodeDeploy.ListStateFilterAction
        "Get-CDApplicationRevisionList/Deployed"
        {
            $v = "exclude","ignore","include"
            break
        }
        
        # Amazon.CodeDeploy.MinimumHealthyHostsType
        "New-CDDeploymentConfig/MinimumHealthyHosts_Type"
        {
            $v = "FLEET_PERCENT","HOST_COUNT"
            break
        }
        
        # Amazon.CodeDeploy.RegistrationStatus
        "Get-CDOnPremiseInstanceList/RegistrationStatus"
        {
            $v = "Deregistered","Registered"
            break
        }
        
        # Amazon.CodeDeploy.RevisionLocationType
        {
            ($_ -eq "Get-CDApplicationRevision/Revision_RevisionType") -Or
            ($_ -eq "New-CDDeployment/Revision_RevisionType") -Or
            ($_ -eq "Register-CDApplicationRevision/Revision_RevisionType")
        }
        {
            $v = "GitHub","S3","String"
            break
        }
        
        # Amazon.CodeDeploy.SortOrder
        "Get-CDApplicationRevisionList/SortOrder"
        {
            $v = "ascending","descending"
            break
        }
        
        # Amazon.CodeDeploy.TrafficRoutingType
        "New-CDDeploymentConfig/TrafficRoutingConfig_Type"
        {
            $v = "AllAtOnce","TimeBasedCanary","TimeBasedLinear"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CD_map = @{
    "BlueGreenDeploymentConfiguration_DeploymentReadyOption_ActionOnTimeout"=@("New-CDDeploymentGroup","Update-CDDeploymentGroup")
    "BlueGreenDeploymentConfiguration_GreenFleetProvisioningOption_Action"=@("New-CDDeploymentGroup","Update-CDDeploymentGroup")
    "BlueGreenDeploymentConfiguration_TerminateBlueInstancesOnDeploymentSuccess_Action"=@("New-CDDeploymentGroup","Update-CDDeploymentGroup")
    "ComputePlatform"=@("New-CDApplication","New-CDDeploymentConfig")
    "Deployed"=@("Get-CDApplicationRevisionList")
    "DeploymentStyle_DeploymentOption"=@("New-CDDeploymentGroup","Update-CDDeploymentGroup")
    "DeploymentStyle_DeploymentType"=@("New-CDDeploymentGroup","Update-CDDeploymentGroup")
    "FileExistsBehavior"=@("New-CDDeployment")
    "MinimumHealthyHosts_Type"=@("New-CDDeploymentConfig")
    "RegistrationStatus"=@("Get-CDOnPremiseInstanceList")
    "Revision_RevisionType"=@("Get-CDApplicationRevision","New-CDDeployment","Register-CDApplicationRevision")
    "Revision_S3Location_BundleType"=@("Get-CDApplicationRevision","New-CDDeployment","Register-CDApplicationRevision")
    "SortBy"=@("Get-CDApplicationRevisionList")
    "SortOrder"=@("Get-CDApplicationRevisionList")
    "Status"=@("Write-CDLifecycleEventHookExecutionStatus")
    "TrafficRoutingConfig_Type"=@("New-CDDeploymentConfig")
}

_awsArgumentCompleterRegistration $CD_Completers $CD_map


# Argument completions for service AWS CodePipeline
$CP_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CodePipeline.ActionCategory
        {
            ($_ -eq "Get-CPActionableJobList/ActionTypeId_Category") -Or
            ($_ -eq "Get-CPActionableThirdPartyJobList/ActionTypeId_Category") -Or
            ($_ -eq "New-CPCustomActionType/Category") -Or
            ($_ -eq "Remove-CPCustomActionType/Category")
        }
        {
            $v = "Approval","Build","Deploy","Invoke","Source","Test"
            break
        }
        
        # Amazon.CodePipeline.ActionOwner
        {
            ($_ -eq "Get-CPActionType/ActionOwnerFilter") -Or
            ($_ -eq "Get-CPActionableJobList/ActionTypeId_Owner") -Or
            ($_ -eq "Get-CPActionableThirdPartyJobList/ActionTypeId_Owner")
        }
        {
            $v = "AWS","Custom","ThirdParty"
            break
        }
        
        # Amazon.CodePipeline.ApprovalStatus
        "Write-CPApprovalResult/Result_Status"
        {
            $v = "Approved","Rejected"
            break
        }
        
        # Amazon.CodePipeline.FailureType
        {
            ($_ -eq "Write-CPJobFailureResult/FailureDetails_Type") -Or
            ($_ -eq "Write-CPThirdPartyJobFailureResult/FailureDetails_Type")
        }
        {
            $v = "ConfigurationError","JobFailed","PermissionError","RevisionOutOfSync","RevisionUnavailable","SystemUnavailable"
            break
        }
        
        # Amazon.CodePipeline.StageRetryMode
        "Redo-CPStageExecution/RetryMode"
        {
            $v = "FAILED_ACTIONS"
            break
        }
        
        # Amazon.CodePipeline.StageTransitionType
        {
            ($_ -eq "Disable-CPStageTransition/TransitionType") -Or
            ($_ -eq "Enable-CPStageTransition/TransitionType")
        }
        {
            $v = "Inbound","Outbound"
            break
        }
        
        # Amazon.CodePipeline.WebhookAuthenticationType
        "Write-CPWebhook/Webhook_Authentication"
        {
            $v = "GITHUB_HMAC","IP","UNAUTHENTICATED"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CP_map = @{
    "ActionOwnerFilter"=@("Get-CPActionType")
    "ActionTypeId_Category"=@("Get-CPActionableJobList","Get-CPActionableThirdPartyJobList")
    "ActionTypeId_Owner"=@("Get-CPActionableJobList","Get-CPActionableThirdPartyJobList")
    "Category"=@("New-CPCustomActionType","Remove-CPCustomActionType")
    "FailureDetails_Type"=@("Write-CPJobFailureResult","Write-CPThirdPartyJobFailureResult")
    "Result_Status"=@("Write-CPApprovalResult")
    "RetryMode"=@("Redo-CPStageExecution")
    "TransitionType"=@("Disable-CPStageTransition","Enable-CPStageTransition")
    "Webhook_Authentication"=@("Write-CPWebhook")
}

_awsArgumentCompleterRegistration $CP_Completers $CP_map


# Argument completions for service Amazon Cognito Identity Provider
$CGIP_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CognitoIdentityProvider.AccountTakeoverEventActionType
        {
            ($_ -eq "Set-CGIPRiskConfiguration/AccountTakeoverRiskConfiguration_Actions_HighAction_EventAction") -Or
            ($_ -eq "Set-CGIPRiskConfiguration/AccountTakeoverRiskConfiguration_Actions_LowAction_EventAction") -Or
            ($_ -eq "Set-CGIPRiskConfiguration/AccountTakeoverRiskConfiguration_Actions_MediumAction_EventAction")
        }
        {
            $v = "BLOCK","MFA_IF_CONFIGURED","MFA_REQUIRED","NO_ACTION"
            break
        }
        
        # Amazon.CognitoIdentityProvider.AdvancedSecurityModeType
        {
            ($_ -eq "New-CGIPUserPool/UserPoolAddOns_AdvancedSecurityMode") -Or
            ($_ -eq "Update-CGIPUserPool/UserPoolAddOns_AdvancedSecurityMode")
        }
        {
            $v = "AUDIT","ENFORCED","OFF"
            break
        }
        
        # Amazon.CognitoIdentityProvider.AuthFlowType
        {
            ($_ -eq "Start-CGIPAuth/AuthFlow") -Or
            ($_ -eq "Start-CGIPAuthAdmin/AuthFlow")
        }
        {
            $v = "ADMIN_NO_SRP_AUTH","CUSTOM_AUTH","REFRESH_TOKEN","REFRESH_TOKEN_AUTH","USER_PASSWORD_AUTH","USER_SRP_AUTH"
            break
        }
        
        # Amazon.CognitoIdentityProvider.ChallengeNameType
        {
            ($_ -eq "Send-CGIPAuthChallengeResponse/ChallengeName") -Or
            ($_ -eq "Send-CGIPAuthChallengeResponseAdmin/ChallengeName")
        }
        {
            $v = "ADMIN_NO_SRP_AUTH","CUSTOM_CHALLENGE","DEVICE_PASSWORD_VERIFIER","DEVICE_SRP_AUTH","MFA_SETUP","NEW_PASSWORD_REQUIRED","PASSWORD_VERIFIER","SELECT_MFA_TYPE","SMS_MFA","SOFTWARE_TOKEN_MFA"
            break
        }
        
        # Amazon.CognitoIdentityProvider.CompromisedCredentialsEventActionType
        "Set-CGIPRiskConfiguration/CompromisedCredentialsRiskConfiguration_Actions_EventAction"
        {
            $v = "BLOCK","NO_ACTION"
            break
        }
        
        # Amazon.CognitoIdentityProvider.DefaultEmailOptionType
        {
            ($_ -eq "New-CGIPUserPool/VerificationMessageTemplate_DefaultEmailOption") -Or
            ($_ -eq "Update-CGIPUserPool/VerificationMessageTemplate_DefaultEmailOption")
        }
        {
            $v = "CONFIRM_WITH_CODE","CONFIRM_WITH_LINK"
            break
        }
        
        # Amazon.CognitoIdentityProvider.DeviceRememberedStatusType
        {
            ($_ -eq "Edit-CGIPDeviceStatus/DeviceRememberedStatus") -Or
            ($_ -eq "Edit-CGIPDeviceStatusAdmin/DeviceRememberedStatus")
        }
        {
            $v = "not_remembered","remembered"
            break
        }
        
        # Amazon.CognitoIdentityProvider.FeedbackValueType
        {
            ($_ -eq "Update-CGIPAuthEventFeedback/FeedbackValue") -Or
            ($_ -eq "Update-CGIPAuthEventFeedbackAdmin/FeedbackValue")
        }
        {
            $v = "Invalid","Valid"
            break
        }
        
        # Amazon.CognitoIdentityProvider.IdentityProviderTypeType
        "New-CGIPIdentityProvider/ProviderType"
        {
            $v = "Facebook","Google","LoginWithAmazon","OIDC","SAML"
            break
        }
        
        # Amazon.CognitoIdentityProvider.MessageActionType
        "New-CGIPUserAdmin/MessageAction"
        {
            $v = "RESEND","SUPPRESS"
            break
        }
        
        # Amazon.CognitoIdentityProvider.UserPoolMfaType
        {
            ($_ -eq "New-CGIPUserPool/MfaConfiguration") -Or
            ($_ -eq "Set-CGIPUserPoolMfaConfig/MfaConfiguration") -Or
            ($_ -eq "Update-CGIPUserPool/MfaConfiguration")
        }
        {
            $v = "OFF","ON","OPTIONAL"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CGIP_map = @{
    "AccountTakeoverRiskConfiguration_Actions_HighAction_EventAction"=@("Set-CGIPRiskConfiguration")
    "AccountTakeoverRiskConfiguration_Actions_LowAction_EventAction"=@("Set-CGIPRiskConfiguration")
    "AccountTakeoverRiskConfiguration_Actions_MediumAction_EventAction"=@("Set-CGIPRiskConfiguration")
    "AuthFlow"=@("Start-CGIPAuth","Start-CGIPAuthAdmin")
    "ChallengeName"=@("Send-CGIPAuthChallengeResponse","Send-CGIPAuthChallengeResponseAdmin")
    "CompromisedCredentialsRiskConfiguration_Actions_EventAction"=@("Set-CGIPRiskConfiguration")
    "DeviceRememberedStatus"=@("Edit-CGIPDeviceStatus","Edit-CGIPDeviceStatusAdmin")
    "FeedbackValue"=@("Update-CGIPAuthEventFeedback","Update-CGIPAuthEventFeedbackAdmin")
    "MessageAction"=@("New-CGIPUserAdmin")
    "MfaConfiguration"=@("New-CGIPUserPool","Set-CGIPUserPoolMfaConfig","Update-CGIPUserPool")
    "ProviderType"=@("New-CGIPIdentityProvider")
    "UserPoolAddOns_AdvancedSecurityMode"=@("New-CGIPUserPool","Update-CGIPUserPool")
    "VerificationMessageTemplate_DefaultEmailOption"=@("New-CGIPUserPool","Update-CGIPUserPool")
}

_awsArgumentCompleterRegistration $CGIP_Completers $CGIP_map


# Argument completions for service Amazon Comprehend
$COMP_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Comprehend.LanguageCode
        {
            ($_ -eq "Find-COMPEntity/LanguageCode") -Or
            ($_ -eq "Find-COMPEntityBatch/LanguageCode") -Or
            ($_ -eq "Find-COMPKeyPhrase/LanguageCode") -Or
            ($_ -eq "Find-COMPKeyPhrasesBatch/LanguageCode") -Or
            ($_ -eq "Find-COMPSentiment/LanguageCode") -Or
            ($_ -eq "Find-COMPSentimentBatch/LanguageCode") -Or
            ($_ -eq "Start-COMPEntitiesDetectionJob/LanguageCode") -Or
            ($_ -eq "Start-COMPKeyPhrasesDetectionJob/LanguageCode") -Or
            ($_ -eq "Start-COMPSentimentDetectionJob/LanguageCode")
        }
        {
            $v = "en","es"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$COMP_map = @{
    "LanguageCode"=@("Find-COMPEntity","Find-COMPEntityBatch","Find-COMPKeyPhrase","Find-COMPKeyPhrasesBatch","Find-COMPSentiment","Find-COMPSentimentBatch","Start-COMPEntitiesDetectionJob","Start-COMPKeyPhrasesDetectionJob","Start-COMPSentimentDetectionJob")
}

_awsArgumentCompleterRegistration $COMP_Completers $COMP_map


# Argument completions for service AWS Config
$CFG_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.ConfigService.ChronologicalOrder
        "Get-CFGResourceConfigHistory/ChronologicalOrder"
        {
            $v = "Forward","Reverse"
            break
        }
        
        # Amazon.ConfigService.ComplianceType
        {
            ($_ -eq "Get-CFGAggregateComplianceDetailsByConfigRule/ComplianceType") -Or
            ($_ -eq "Get-CFGAggregateComplianceByConfigRuleList/Filters_ComplianceType")
        }
        {
            $v = "COMPLIANT","INSUFFICIENT_DATA","NON_COMPLIANT","NOT_APPLICABLE"
            break
        }
        
        # Amazon.ConfigService.ConfigRuleComplianceSummaryGroupKey
        "Get-CFGAggregateConfigRuleComplianceSummary/GroupByKey"
        {
            $v = "ACCOUNT_ID","AWS_REGION"
            break
        }
        
        # Amazon.ConfigService.ConfigRuleState
        "Write-CFGConfigRule/ConfigRule_ConfigRuleState"
        {
            $v = "ACTIVE","DELETING","DELETING_RESULTS","EVALUATING"
            break
        }
        
        # Amazon.ConfigService.MaximumExecutionFrequency
        {
            ($_ -eq "Write-CFGConfigRule/ConfigRule_MaximumExecutionFrequency") -Or
            ($_ -eq "Write-CFGDeliveryChannel/DeliveryChannel_ConfigSnapshotDeliveryProperties_DeliveryFrequency")
        }
        {
            $v = "One_Hour","Six_Hours","Three_Hours","Twelve_Hours","TwentyFour_Hours"
            break
        }
        
        # Amazon.ConfigService.Owner
        "Write-CFGConfigRule/ConfigRule_Source_Owner"
        {
            $v = "AWS","CUSTOM_LAMBDA"
            break
        }
        
        # Amazon.ConfigService.ResourceType
        {
            ($_ -eq "Get-CFGDiscoveredResource/ResourceType") -Or
            ($_ -eq "Get-CFGResourceConfigHistory/ResourceType")
        }
        {
            $v = "AWS::ACM::Certificate","AWS::AutoScaling::AutoScalingGroup","AWS::AutoScaling::LaunchConfiguration","AWS::AutoScaling::ScalingPolicy","AWS::AutoScaling::ScheduledAction","AWS::CloudFormation::Stack","AWS::CloudFront::Distribution","AWS::CloudFront::StreamingDistribution","AWS::CloudTrail::Trail","AWS::CloudWatch::Alarm","AWS::CodeBuild::Project","AWS::DynamoDB::Table","AWS::EC2::CustomerGateway","AWS::EC2::EIP","AWS::EC2::Host","AWS::EC2::Instance","AWS::EC2::InternetGateway","AWS::EC2::NetworkAcl","AWS::EC2::NetworkInterface","AWS::EC2::RouteTable","AWS::EC2::SecurityGroup","AWS::EC2::Subnet","AWS::EC2::Volume","AWS::EC2::VPC","AWS::EC2::VPNConnection","AWS::EC2::VPNGateway","AWS::ElasticBeanstalk::Application","AWS::ElasticBeanstalk::ApplicationVersion","AWS::ElasticBeanstalk::Environment","AWS::ElasticLoadBalancing::LoadBalancer","AWS::ElasticLoadBalancingV2::LoadBalancer","AWS::IAM::Group","AWS::IAM::Policy","AWS::IAM::Role","AWS::IAM::User","AWS::Lambda::Function","AWS::RDS::DBInstance","AWS::RDS::DBSecurityGroup","AWS::RDS::DBSnapshot","AWS::RDS::DBSubnetGroup","AWS::RDS::EventSubscription","AWS::Redshift::Cluster","AWS::Redshift::ClusterParameterGroup","AWS::Redshift::ClusterSecurityGroup","AWS::Redshift::ClusterSnapshot","AWS::Redshift::ClusterSubnetGroup","AWS::Redshift::EventSubscription","AWS::S3::Bucket","AWS::SSM::ManagedInstanceInventory","AWS::WAF::RateBasedRule","AWS::WAF::Rule","AWS::WAF::RuleGroup","AWS::WAF::WebACL","AWS::WAFRegional::RateBasedRule","AWS::WAFRegional::Rule","AWS::WAFRegional::RuleGroup","AWS::WAFRegional::WebACL","AWS::XRay::EncryptionConfig"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CFG_map = @{
    "ChronologicalOrder"=@("Get-CFGResourceConfigHistory")
    "ComplianceType"=@("Get-CFGAggregateComplianceDetailsByConfigRule")
    "ConfigRule_ConfigRuleState"=@("Write-CFGConfigRule")
    "ConfigRule_MaximumExecutionFrequency"=@("Write-CFGConfigRule")
    "ConfigRule_Source_Owner"=@("Write-CFGConfigRule")
    "DeliveryChannel_ConfigSnapshotDeliveryProperties_DeliveryFrequency"=@("Write-CFGDeliveryChannel")
    "Filters_ComplianceType"=@("Get-CFGAggregateComplianceByConfigRuleList")
    "GroupByKey"=@("Get-CFGAggregateConfigRuleComplianceSummary")
    "ResourceType"=@("Get-CFGDiscoveredResource","Get-CFGResourceConfigHistory")
}

_awsArgumentCompleterRegistration $CFG_Completers $CFG_map


# Argument completions for service AWS Cost and Usage Report
$CUR_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CostAndUsageReport.AWSRegion
        "Write-CURReportDefinition/ReportDefinition_S3Region"
        {
            $v = "ap-northeast-1","ap-southeast-1","ap-southeast-2","eu-central-1","eu-west-1","us-east-1","us-west-1","us-west-2"
            break
        }
        
        # Amazon.CostAndUsageReport.CompressionFormat
        "Write-CURReportDefinition/ReportDefinition_Compression"
        {
            $v = "GZIP","ZIP"
            break
        }
        
        # Amazon.CostAndUsageReport.ReportFormat
        "Write-CURReportDefinition/ReportDefinition_Format"
        {
            $v = "textORcsv"
            break
        }
        
        # Amazon.CostAndUsageReport.TimeUnit
        "Write-CURReportDefinition/ReportDefinition_TimeUnit"
        {
            $v = "DAILY","HOURLY"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CUR_map = @{
    "ReportDefinition_Compression"=@("Write-CURReportDefinition")
    "ReportDefinition_Format"=@("Write-CURReportDefinition")
    "ReportDefinition_S3Region"=@("Write-CURReportDefinition")
    "ReportDefinition_TimeUnit"=@("Write-CURReportDefinition")
}

_awsArgumentCompleterRegistration $CUR_Completers $CUR_map


# Argument completions for service AWS Cost Explorer
$CE_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CostExplorer.AccountScope
        "Get-CEReservationPurchaseRecommendation/AccountScope"
        {
            $v = "PAYER"
            break
        }
        
        # Amazon.CostExplorer.Context
        "Get-CEDimensionValue/Context"
        {
            $v = "COST_AND_USAGE","RESERVATIONS"
            break
        }
        
        # Amazon.CostExplorer.Dimension
        "Get-CEDimensionValue/Dimension"
        {
            $v = "AZ","CACHE_ENGINE","DATABASE_ENGINE","DEPLOYMENT_OPTION","INSTANCE_TYPE","INSTANCE_TYPE_FAMILY","LEGAL_ENTITY_NAME","LINKED_ACCOUNT","OPERATING_SYSTEM","OPERATION","PLATFORM","PURCHASE_TYPE","RECORD_TYPE","REGION","SCOPE","SERVICE","SUBSCRIPTION_ID","TENANCY","USAGE_TYPE","USAGE_TYPE_GROUP"
            break
        }
        
        # Amazon.CostExplorer.Granularity
        {
            ($_ -eq "Get-CECostAndUsage/Granularity") -Or
            ($_ -eq "Get-CEReservationCoverage/Granularity") -Or
            ($_ -eq "Get-CEReservationUtilization/Granularity")
        }
        {
            $v = "DAILY","MONTHLY"
            break
        }
        
        # Amazon.CostExplorer.LookbackPeriodInDays
        "Get-CEReservationPurchaseRecommendation/LookbackPeriodInDays"
        {
            $v = "SEVEN_DAYS","SIXTY_DAYS","THIRTY_DAYS"
            break
        }
        
        # Amazon.CostExplorer.OfferingClass
        "Get-CEReservationPurchaseRecommendation/ServiceSpecification_EC2Specification_OfferingClass"
        {
            $v = "CONVERTIBLE","STANDARD"
            break
        }
        
        # Amazon.CostExplorer.PaymentOption
        "Get-CEReservationPurchaseRecommendation/PaymentOption"
        {
            $v = "ALL_UPFRONT","NO_UPFRONT","PARTIAL_UPFRONT"
            break
        }
        
        # Amazon.CostExplorer.TermInYears
        "Get-CEReservationPurchaseRecommendation/TermInYears"
        {
            $v = "ONE_YEAR","THREE_YEARS"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$CE_map = @{
    "AccountScope"=@("Get-CEReservationPurchaseRecommendation")
    "Context"=@("Get-CEDimensionValue")
    "Dimension"=@("Get-CEDimensionValue")
    "Granularity"=@("Get-CECostAndUsage","Get-CEReservationCoverage","Get-CEReservationUtilization")
    "LookbackPeriodInDays"=@("Get-CEReservationPurchaseRecommendation")
    "PaymentOption"=@("Get-CEReservationPurchaseRecommendation")
    "ServiceSpecification_EC2Specification_OfferingClass"=@("Get-CEReservationPurchaseRecommendation")
    "TermInYears"=@("Get-CEReservationPurchaseRecommendation")
}

_awsArgumentCompleterRegistration $CE_Completers $CE_map


# Argument completions for service AWS Database Migration Service
$DMS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.DatabaseMigrationService.AuthMechanismValue
        {
            ($_ -eq "Edit-DMSEndpoint/MongoDbSettings_AuthMechanism") -Or
            ($_ -eq "New-DMSEndpoint/MongoDbSettings_AuthMechanism")
        }
        {
            $v = "default","mongodb_cr","scram_sha_1"
            break
        }
        
        # Amazon.DatabaseMigrationService.AuthTypeValue
        {
            ($_ -eq "Edit-DMSEndpoint/MongoDbSettings_AuthType") -Or
            ($_ -eq "New-DMSEndpoint/MongoDbSettings_AuthType")
        }
        {
            $v = "no","password"
            break
        }
        
        # Amazon.DatabaseMigrationService.CompressionTypeValue
        {
            ($_ -eq "Edit-DMSEndpoint/S3Settings_CompressionType") -Or
            ($_ -eq "New-DMSEndpoint/S3Settings_CompressionType")
        }
        {
            $v = "gzip","none"
            break
        }
        
        # Amazon.DatabaseMigrationService.DmsSslModeValue
        {
            ($_ -eq "Edit-DMSEndpoint/SslMode") -Or
            ($_ -eq "New-DMSEndpoint/SslMode")
        }
        {
            $v = "none","require","verify-ca","verify-full"
            break
        }
        
        # Amazon.DatabaseMigrationService.MigrationTypeValue
        {
            ($_ -eq "Edit-DMSReplicationTask/MigrationType") -Or
            ($_ -eq "New-DMSReplicationTask/MigrationType")
        }
        {
            $v = "cdc","full-load","full-load-and-cdc"
            break
        }
        
        # Amazon.DatabaseMigrationService.NestingLevelValue
        {
            ($_ -eq "Edit-DMSEndpoint/MongoDbSettings_NestingLevel") -Or
            ($_ -eq "New-DMSEndpoint/MongoDbSettings_NestingLevel")
        }
        {
            $v = "none","one"
            break
        }
        
        # Amazon.DatabaseMigrationService.ReplicationEndpointTypeValue
        {
            ($_ -eq "Edit-DMSEndpoint/EndpointType") -Or
            ($_ -eq "New-DMSEndpoint/EndpointType")
        }
        {
            $v = "source","target"
            break
        }
        
        # Amazon.DatabaseMigrationService.SourceType
        "Get-DMSEvent/SourceType"
        {
            $v = "replication-instance"
            break
        }
        
        # Amazon.DatabaseMigrationService.StartReplicationTaskTypeValue
        "Start-DMSReplicationTask/StartReplicationTaskType"
        {
            $v = "reload-target","resume-processing","start-replication"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$DMS_map = @{
    "EndpointType"=@("Edit-DMSEndpoint","New-DMSEndpoint")
    "MigrationType"=@("Edit-DMSReplicationTask","New-DMSReplicationTask")
    "MongoDbSettings_AuthMechanism"=@("Edit-DMSEndpoint","New-DMSEndpoint")
    "MongoDbSettings_AuthType"=@("Edit-DMSEndpoint","New-DMSEndpoint")
    "MongoDbSettings_NestingLevel"=@("Edit-DMSEndpoint","New-DMSEndpoint")
    "S3Settings_CompressionType"=@("Edit-DMSEndpoint","New-DMSEndpoint")
    "SourceType"=@("Get-DMSEvent")
    "SslMode"=@("Edit-DMSEndpoint","New-DMSEndpoint")
    "StartReplicationTaskType"=@("Start-DMSReplicationTask")
}

_awsArgumentCompleterRegistration $DMS_Completers $DMS_map


# Argument completions for service AWS Data Pipeline
$DP_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.DataPipeline.TaskStatus
        "Set-DPTaskStatus/TaskStatus"
        {
            $v = "FAILED","FALSE","FINISHED"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$DP_map = @{
    "TaskStatus"=@("Set-DPTaskStatus")
}

_awsArgumentCompleterRegistration $DP_Completers $DP_map


# Argument completions for service Amazon DynamoDB Accelerator (DAX)
$DAX_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.DAX.SourceType
        "Get-DAXEvent/SourceType"
        {
            $v = "CLUSTER","PARAMETER_GROUP","SUBNET_GROUP"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$DAX_map = @{
    "SourceType"=@("Get-DAXEvent")
}

_awsArgumentCompleterRegistration $DAX_Completers $DAX_map


# Argument completions for service AWS Device Farm
$DF_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.DeviceFarm.ArtifactCategory
        "Get-DFArtifactList/Type"
        {
            $v = "FILE","LOG","SCREENSHOT"
            break
        }
        
        # Amazon.DeviceFarm.BillingMethod
        {
            ($_ -eq "Get-DFDevicePoolCompatibility/Configuration_BillingMethod") -Or
            ($_ -eq "New-DFRemoteAccessSession/Configuration_BillingMethod") -Or
            ($_ -eq "Submit-DFTestRun/Configuration_BillingMethod")
        }
        {
            $v = "METERED","UNMETERED"
            break
        }
        
        # Amazon.DeviceFarm.DevicePoolType
        "Get-DFDevicePoolList/Type"
        {
            $v = "CURATED","PRIVATE"
            break
        }
        
        # Amazon.DeviceFarm.InteractionMode
        "New-DFRemoteAccessSession/InteractionMode"
        {
            $v = "INTERACTIVE","NO_VIDEO","VIDEO_ONLY"
            break
        }
        
        # Amazon.DeviceFarm.NetworkProfileType
        {
            ($_ -eq "Get-DFNetworkProfileList/Type") -Or
            ($_ -eq "New-DFNetworkProfile/Type") -Or
            ($_ -eq "Update-DFNetworkProfile/Type")
        }
        {
            $v = "CURATED","PRIVATE"
            break
        }
        
        # Amazon.DeviceFarm.TestType
        {
            ($_ -eq "Get-DFDevicePoolCompatibility/Test_Type") -Or
            ($_ -eq "Submit-DFTestRun/Test_Type") -Or
            ($_ -eq "Get-DFDevicePoolCompatibility/TestType")
        }
        {
            $v = "APPIUM_JAVA_JUNIT","APPIUM_JAVA_TESTNG","APPIUM_PYTHON","APPIUM_WEB_JAVA_JUNIT","APPIUM_WEB_JAVA_TESTNG","APPIUM_WEB_PYTHON","BUILTIN_EXPLORER","BUILTIN_FUZZ","CALABASH","INSTRUMENTATION","REMOTE_ACCESS_RECORD","REMOTE_ACCESS_REPLAY","UIAUTOMATION","UIAUTOMATOR","WEB_PERFORMANCE_PROFILE","XCTEST","XCTEST_UI"
            break
        }
        
        # Amazon.DeviceFarm.UploadType
        "New-DFUpload/Type"
        {
            $v = "ANDROID_APP","APPIUM_JAVA_JUNIT_TEST_PACKAGE","APPIUM_JAVA_TESTNG_TEST_PACKAGE","APPIUM_PYTHON_TEST_PACKAGE","APPIUM_WEB_JAVA_JUNIT_TEST_PACKAGE","APPIUM_WEB_JAVA_TESTNG_TEST_PACKAGE","APPIUM_WEB_PYTHON_TEST_PACKAGE","CALABASH_TEST_PACKAGE","EXTERNAL_DATA","INSTRUMENTATION_TEST_PACKAGE","IOS_APP","UIAUTOMATION_TEST_PACKAGE","UIAUTOMATOR_TEST_PACKAGE","WEB_APP","XCTEST_TEST_PACKAGE","XCTEST_UI_TEST_PACKAGE"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$DF_map = @{
    "Configuration_BillingMethod"=@("Get-DFDevicePoolCompatibility","New-DFRemoteAccessSession","Submit-DFTestRun")
    "InteractionMode"=@("New-DFRemoteAccessSession")
    "Test_Type"=@("Get-DFDevicePoolCompatibility","Submit-DFTestRun")
    "TestType"=@("Get-DFDevicePoolCompatibility")
    "Type"=@("Get-DFArtifactList","Get-DFDevicePoolList","Get-DFNetworkProfileList","New-DFNetworkProfile","New-DFUpload","Update-DFNetworkProfile")
}

_awsArgumentCompleterRegistration $DF_Completers $DF_map


# Argument completions for service AWS Direct Connect
$DC_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.DirectConnect.AddressFamily
        {
            ($_ -eq "New-DCBGPPeer/NewBGPPeer_AddressFamily") -Or
            ($_ -eq "New-DCPrivateVirtualInterface/NewPrivateVirtualInterface_AddressFamily") -Or
            ($_ -eq "Enable-DCPrivateVirtualInterface/NewPrivateVirtualInterfaceAllocation_AddressFamily") -Or
            ($_ -eq "New-DCPublicVirtualInterface/NewPublicVirtualInterface_AddressFamily") -Or
            ($_ -eq "Enable-DCPublicVirtualInterface/NewPublicVirtualInterfaceAllocation_AddressFamily")
        }
        {
            $v = "ipv4","ipv6"
            break
        }
        
        # Amazon.DirectConnect.LoaContentType
        {
            ($_ -eq "Get-DCConnectionLoa/LoaContentType") -Or
            ($_ -eq "Get-DCInterconnectLoa/LoaContentType") -Or
            ($_ -eq "Get-DCLoa/LoaContentType")
        }
        {
            $v = "application/pdf"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$DC_map = @{
    "LoaContentType"=@("Get-DCConnectionLoa","Get-DCInterconnectLoa","Get-DCLoa")
    "NewBGPPeer_AddressFamily"=@("New-DCBGPPeer")
    "NewPrivateVirtualInterface_AddressFamily"=@("New-DCPrivateVirtualInterface")
    "NewPrivateVirtualInterfaceAllocation_AddressFamily"=@("Enable-DCPrivateVirtualInterface")
    "NewPublicVirtualInterface_AddressFamily"=@("New-DCPublicVirtualInterface")
    "NewPublicVirtualInterfaceAllocation_AddressFamily"=@("Enable-DCPublicVirtualInterface")
}

_awsArgumentCompleterRegistration $DC_Completers $DC_map


# Argument completions for service AWS Directory Service
$DS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.DirectoryService.DirectoryEdition
        "New-DSMicrosoftAD/Edition"
        {
            $v = "Enterprise","Standard"
            break
        }
        
        # Amazon.DirectoryService.DirectorySize
        {
            ($_ -eq "Connect-DSDirectory/Size") -Or
            ($_ -eq "New-DSDirectory/Size")
        }
        {
            $v = "Large","Small"
            break
        }
        
        # Amazon.DirectoryService.RadiusAuthenticationProtocol
        {
            ($_ -eq "Enable-DSRadius/RadiusSettings_AuthenticationProtocol") -Or
            ($_ -eq "Update-DSRadius/RadiusSettings_AuthenticationProtocol")
        }
        {
            $v = "CHAP","MS-CHAPv1","MS-CHAPv2","PAP"
            break
        }
        
        # Amazon.DirectoryService.TrustDirection
        "New-DSTrust/TrustDirection"
        {
            $v = "One-Way: Incoming","One-Way: Outgoing","Two-Way"
            break
        }
        
        # Amazon.DirectoryService.TrustType
        "New-DSTrust/TrustType"
        {
            $v = "Forest"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$DS_map = @{
    "Edition"=@("New-DSMicrosoftAD")
    "RadiusSettings_AuthenticationProtocol"=@("Enable-DSRadius","Update-DSRadius")
    "Size"=@("Connect-DSDirectory","New-DSDirectory")
    "TrustDirection"=@("New-DSTrust")
    "TrustType"=@("New-DSTrust")
}

_awsArgumentCompleterRegistration $DS_Completers $DS_map


# Argument completions for service Amazon DynamoDB
$DDB_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.DynamoDBv2.KeyType
        "Add-DDBKeySchema/KeyType"
        {
            $v = "HASH","RANGE"
            break
        }
        
        # Amazon.DynamoDBv2.ProjectionType
        "Add-DDBIndexSchema/ProjectionType"
        {
            $v = "ALL","INCLUDE","KEYS_ONLY"
            break
        }
        
        # Amazon.DynamoDBv2.ScalarAttributeType
        {
            ($_ -eq "Add-DDBIndexSchema/HashKeyDataType") -Or
            ($_ -eq "Add-DDBKeySchema/KeyDataType") -Or
            ($_ -eq "Add-DDBIndexSchema/RangeKeyDataType")
        }
        {
            $v = "B","N","S"
            break
        }
        
        # Amazon.DynamoDBv2.StreamViewType
        "Update-DDBTable/StreamSpecification_StreamViewType"
        {
            $v = "KEYS_ONLY","NEW_AND_OLD_IMAGES","NEW_IMAGE","OLD_IMAGE"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$DDB_map = @{
    "HashKeyDataType"=@("Add-DDBIndexSchema")
    "KeyDataType"=@("Add-DDBKeySchema")
    "KeyType"=@("Add-DDBKeySchema")
    "ProjectionType"=@("Add-DDBIndexSchema")
    "RangeKeyDataType"=@("Add-DDBIndexSchema")
    "StreamSpecification_StreamViewType"=@("Update-DDBTable")
}

_awsArgumentCompleterRegistration $DDB_Completers $DDB_map


# Argument completions for service Amazon Elastic Compute Cloud
$EC2_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.EC2.Affinity
        "Edit-EC2InstancePlacement/Affinity"
        {
            $v = "default","host"
            break
        }
        
        # Amazon.EC2.AllocationStrategy
        "Request-EC2SpotFleet/SpotFleetRequestConfig_AllocationStrategy"
        {
            $v = "diversified","lowestPrice"
            break
        }
        
        # Amazon.EC2.ArchitectureValues
        "Register-EC2Image/Architecture"
        {
            $v = "i386","x86_64"
            break
        }
        
        # Amazon.EC2.AutoPlacement
        {
            ($_ -eq "Edit-EC2Host/AutoPlacement") -Or
            ($_ -eq "New-EC2Host/AutoPlacement")
        }
        {
            $v = "off","on"
            break
        }
        
        # Amazon.EC2.ContainerFormat
        "New-EC2InstanceExportTask/ExportToS3Task_ContainerFormat"
        {
            $v = "ova"
            break
        }
        
        # Amazon.EC2.CurrencyCodeValues
        {
            ($_ -eq "New-EC2HostReservation/CurrencyCode") -Or
            ($_ -eq "New-EC2ReservedInstance/LimitPrice_CurrencyCode")
        }
        {
            $v = "USD"
            break
        }
        
        # Amazon.EC2.DefaultTargetCapacityType
        {
            ($_ -eq "Edit-EC2Fleet/TargetCapacitySpecification_DefaultTargetCapacityType") -Or
            ($_ -eq "New-EC2Fleet/TargetCapacitySpecification_DefaultTargetCapacityType")
        }
        {
            $v = "on-demand","spot"
            break
        }
        
        # Amazon.EC2.DiskImageFormat
        "New-EC2InstanceExportTask/ExportToS3Task_DiskImageFormat"
        {
            $v = "RAW","VHD","VMDK"
            break
        }
        
        # Amazon.EC2.DomainType
        "New-EC2Address/Domain"
        {
            $v = "standard","vpc"
            break
        }
        
        # Amazon.EC2.EventType
        "Get-EC2SpotFleetRequestHistory/EventType"
        {
            $v = "error","fleetRequestChange","instanceChange"
            break
        }
        
        # Amazon.EC2.ExcessCapacityTerminationPolicy
        {
            ($_ -eq "Edit-EC2SpotFleetRequest/ExcessCapacityTerminationPolicy") -Or
            ($_ -eq "Request-EC2SpotFleet/SpotFleetRequestConfig_ExcessCapacityTerminationPolicy")
        }
        {
            $v = "default","noTermination"
            break
        }
        
        # Amazon.EC2.ExportEnvironment
        "New-EC2InstanceExportTask/TargetEnvironment"
        {
            $v = "citrix","microsoft","vmware"
            break
        }
        
        # Amazon.EC2.FleetEventType
        "Get-EC2FleetHistory/EventType"
        {
            $v = "fleet-change","instance-change","service-error"
            break
        }
        
        # Amazon.EC2.FleetExcessCapacityTerminationPolicy
        {
            ($_ -eq "Edit-EC2Fleet/ExcessCapacityTerminationPolicy") -Or
            ($_ -eq "New-EC2Fleet/ExcessCapacityTerminationPolicy")
        }
        {
            $v = "no-termination","termination"
            break
        }
        
        # Amazon.EC2.FleetType
        {
            ($_ -eq "Request-EC2SpotFleet/SpotFleetRequestConfig_Type") -Or
            ($_ -eq "New-EC2Fleet/Type")
        }
        {
            $v = "maintain","request"
            break
        }
        
        # Amazon.EC2.FlowLogsResourceType
        "New-EC2FlowLog/ResourceType"
        {
            $v = "NetworkInterface","Subnet","VPC"
            break
        }
        
        # Amazon.EC2.FpgaImageAttributeName
        {
            ($_ -eq "Edit-EC2FpgaImageAttribute/Attribute") -Or
            ($_ -eq "Get-EC2FpgaImageAttribute/Attribute")
        }
        {
            $v = "description","loadPermission","name","productCodes"
            break
        }
        
        # Amazon.EC2.GatewayType
        {
            ($_ -eq "New-EC2CustomerGateway/Type") -Or
            ($_ -eq "New-EC2VpnGateway/Type")
        }
        {
            $v = "ipsec.1"
            break
        }
        
        # Amazon.EC2.HostTenancy
        "Edit-EC2InstancePlacement/Tenancy"
        {
            $v = "dedicated","host"
            break
        }
        
        # Amazon.EC2.ImageAttributeName
        "Get-EC2ImageAttribute/Attribute"
        {
            $v = "blockDeviceMapping","description","kernel","launchPermission","productCodes","ramdisk","sriovNetSupport"
            break
        }
        
        # Amazon.EC2.InstanceAttributeName
        {
            ($_ -eq "Edit-EC2InstanceAttribute/Attribute") -Or
            ($_ -eq "Get-EC2InstanceAttribute/Attribute") -Or
            ($_ -eq "Reset-EC2InstanceAttribute/Attribute")
        }
        {
            $v = "blockDeviceMapping","disableApiTermination","ebsOptimized","enaSupport","groupSet","instanceInitiatedShutdownBehavior","instanceType","kernel","productCodes","ramdisk","rootDeviceName","sourceDestCheck","sriovNetSupport","userData"
            break
        }
        
        # Amazon.EC2.InstanceInterruptionBehavior
        {
            ($_ -eq "Request-EC2SpotInstance/InstanceInterruptionBehavior") -Or
            ($_ -eq "Request-EC2SpotFleet/SpotFleetRequestConfig_InstanceInterruptionBehavior")
        }
        {
            $v = "hibernate","stop","terminate"
            break
        }
        
        # Amazon.EC2.InstanceType
        {
            ($_ -eq "Get-EC2ReservedInstancesOffering/InstanceType") -Or
            ($_ -eq "New-EC2Instance/InstanceType") -Or
            ($_ -eq "Request-EC2SpotInstance/LaunchSpecification_InstanceType")
        }
        {
            $v = "c1.medium","c1.xlarge","c3.2xlarge","c3.4xlarge","c3.8xlarge","c3.large","c3.xlarge","c4.2xlarge","c4.4xlarge","c4.8xlarge","c4.large","c4.xlarge","c5.18xlarge","c5.2xlarge","c5.4xlarge","c5.9xlarge","c5.large","c5.xlarge","c5d.18xlarge","c5d.2xlarge","c5d.4xlarge","c5d.9xlarge","c5d.large","c5d.xlarge","cc1.4xlarge","cc2.8xlarge","cg1.4xlarge","cr1.8xlarge","d2.2xlarge","d2.4xlarge","d2.8xlarge","d2.xlarge","f1.16xlarge","f1.2xlarge","g2.2xlarge","g2.8xlarge","g3.16xlarge","g3.4xlarge","g3.8xlarge","h1.16xlarge","h1.2xlarge","h1.4xlarge","h1.8xlarge","hi1.4xlarge","hs1.8xlarge","i2.2xlarge","i2.4xlarge","i2.8xlarge","i2.xlarge","i3.16xlarge","i3.2xlarge","i3.4xlarge","i3.8xlarge","i3.large","i3.metal","i3.xlarge","m1.large","m1.medium","m1.small","m1.xlarge","m2.2xlarge","m2.4xlarge","m2.xlarge","m3.2xlarge","m3.large","m3.medium","m3.xlarge","m4.10xlarge","m4.16xlarge","m4.2xlarge","m4.4xlarge","m4.large","m4.xlarge","m5.12xlarge","m5.24xlarge","m5.2xlarge","m5.4xlarge","m5.large","m5.xlarge","m5d.12xlarge","m5d.24xlarge","m5d.2xlarge","m5d.4xlarge","m5d.large","m5d.xlarge","p2.16xlarge","p2.8xlarge","p2.xlarge","p3.16xlarge","p3.2xlarge","p3.8xlarge","r3.2xlarge","r3.4xlarge","r3.8xlarge","r3.large","r3.xlarge","r4.16xlarge","r4.2xlarge","r4.4xlarge","r4.8xlarge","r4.large","r4.xlarge","t1.micro","t2.2xlarge","t2.large","t2.medium","t2.micro","t2.nano","t2.small","t2.xlarge","x1.16xlarge","x1.32xlarge","x1e.16xlarge","x1e.2xlarge","x1e.32xlarge","x1e.4xlarge","x1e.8xlarge","x1e.xlarge"
            break
        }
        
        # Amazon.EC2.InterfacePermissionType
        "New-EC2NetworkInterfacePermission/Permission"
        {
            $v = "EIP-ASSOCIATE","INSTANCE-ATTACH"
            break
        }
        
        # Amazon.EC2.NetworkInterfaceAttribute
        "Get-EC2NetworkInterfaceAttribute/Attribute"
        {
            $v = "attachment","description","groupSet","sourceDestCheck"
            break
        }
        
        # Amazon.EC2.OfferingClassType
        {
            ($_ -eq "Get-EC2ReservedInstance/OfferingClass") -Or
            ($_ -eq "Get-EC2ReservedInstancesOffering/OfferingClass")
        }
        {
            $v = "convertible","standard"
            break
        }
        
        # Amazon.EC2.OfferingTypeValues
        {
            ($_ -eq "Get-EC2ReservedInstance/OfferingType") -Or
            ($_ -eq "Get-EC2ReservedInstancesOffering/OfferingType")
        }
        {
            $v = "All Upfront","Heavy Utilization","Light Utilization","Medium Utilization","No Upfront","Partial Upfront"
            break
        }
        
        # Amazon.EC2.OperationType
        {
            ($_ -eq "Edit-EC2FpgaImageAttribute/OperationType") -Or
            ($_ -eq "Edit-EC2ImageAttribute/OperationType") -Or
            ($_ -eq "Edit-EC2SnapshotAttribute/OperationType")
        }
        {
            $v = "add","remove"
            break
        }
        
        # Amazon.EC2.PlacementStrategy
        "New-EC2PlacementGroup/Strategy"
        {
            $v = "cluster","spread"
            break
        }
        
        # Amazon.EC2.ReportStatusType
        "Send-EC2InstanceStatus/Status"
        {
            $v = "impaired","ok"
            break
        }
        
        # Amazon.EC2.ResetFpgaImageAttributeName
        "Reset-EC2FpgaImageAttribute/Attribute"
        {
            $v = "loadPermission"
            break
        }
        
        # Amazon.EC2.ResetImageAttributeName
        "Reset-EC2ImageAttribute/Attribute"
        {
            $v = "launchPermission"
            break
        }
        
        # Amazon.EC2.RIProductDescription
        "Get-EC2ReservedInstancesOffering/ProductDescription"
        {
            $v = "Linux/UNIX","Linux/UNIX (Amazon VPC)","Windows","Windows (Amazon VPC)"
            break
        }
        
        # Amazon.EC2.RuleAction
        {
            ($_ -eq "New-EC2NetworkAclEntry/RuleAction") -Or
            ($_ -eq "Set-EC2NetworkAclEntry/RuleAction")
        }
        {
            $v = "allow","deny"
            break
        }
        
        # Amazon.EC2.ShutdownBehavior
        "New-EC2Instance/InstanceInitiatedShutdownBehavior"
        {
            $v = "stop","terminate"
            break
        }
        
        # Amazon.EC2.SnapshotAttributeName
        {
            ($_ -eq "Edit-EC2SnapshotAttribute/Attribute") -Or
            ($_ -eq "Get-EC2SnapshotAttribute/Attribute") -Or
            ($_ -eq "Reset-EC2SnapshotAttribute/Attribute")
        }
        {
            $v = "createVolumePermission","productCodes"
            break
        }
        
        # Amazon.EC2.SpotAllocationStrategy
        "New-EC2Fleet/SpotOptions_AllocationStrategy"
        {
            $v = "diversified","lowest-price"
            break
        }
        
        # Amazon.EC2.SpotInstanceInterruptionBehavior
        "New-EC2Fleet/SpotOptions_InstanceInterruptionBehavior"
        {
            $v = "hibernate","stop","terminate"
            break
        }
        
        # Amazon.EC2.SpotInstanceType
        "Request-EC2SpotInstance/Type"
        {
            $v = "one-time","persistent"
            break
        }
        
        # Amazon.EC2.Tenancy
        {
            ($_ -eq "Get-EC2ReservedInstancesOffering/InstanceTenancy") -Or
            ($_ -eq "New-EC2Vpc/InstanceTenancy") -Or
            ($_ -eq "Request-EC2SpotInstance/LaunchSpecification_Placement_Tenancy") -Or
            ($_ -eq "New-EC2Instance/Tenancy")
        }
        {
            $v = "dedicated","default","host"
            break
        }
        
        # Amazon.EC2.TrafficType
        "New-EC2FlowLog/TrafficType"
        {
            $v = "ACCEPT","ALL","REJECT"
            break
        }
        
        # Amazon.EC2.VolumeAttributeName
        "Get-EC2VolumeAttribute/Attribute"
        {
            $v = "autoEnableIO","productCodes"
            break
        }
        
        # Amazon.EC2.VolumeType
        {
            ($_ -eq "Edit-EC2Volume/VolumeType") -Or
            ($_ -eq "New-EC2Volume/VolumeType")
        }
        {
            $v = "gp2","io1","sc1","st1","standard"
            break
        }
        
        # Amazon.EC2.VpcAttributeName
        "Get-EC2VpcAttribute/Attribute"
        {
            $v = "enableDnsHostnames","enableDnsSupport"
            break
        }
        
        # Amazon.EC2.VpcEndpointType
        "New-EC2VpcEndpoint/VpcEndpointType"
        {
            $v = "Gateway","Interface"
            break
        }
        
        # Amazon.EC2.VpcTenancy
        "Edit-EC2VpcTenancy/InstanceTenancy"
        {
            $v = "default"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$EC2_map = @{
    "Affinity"=@("Edit-EC2InstancePlacement")
    "Architecture"=@("Register-EC2Image")
    "Attribute"=@("Edit-EC2FpgaImageAttribute","Edit-EC2InstanceAttribute","Edit-EC2SnapshotAttribute","Get-EC2FpgaImageAttribute","Get-EC2ImageAttribute","Get-EC2InstanceAttribute","Get-EC2NetworkInterfaceAttribute","Get-EC2SnapshotAttribute","Get-EC2VolumeAttribute","Get-EC2VpcAttribute","Reset-EC2FpgaImageAttribute","Reset-EC2ImageAttribute","Reset-EC2InstanceAttribute","Reset-EC2SnapshotAttribute")
    "AutoPlacement"=@("Edit-EC2Host","New-EC2Host")
    "CurrencyCode"=@("New-EC2HostReservation")
    "Domain"=@("New-EC2Address")
    "EventType"=@("Get-EC2FleetHistory","Get-EC2SpotFleetRequestHistory")
    "ExcessCapacityTerminationPolicy"=@("Edit-EC2Fleet","Edit-EC2SpotFleetRequest","New-EC2Fleet")
    "ExportToS3Task_ContainerFormat"=@("New-EC2InstanceExportTask")
    "ExportToS3Task_DiskImageFormat"=@("New-EC2InstanceExportTask")
    "InstanceInitiatedShutdownBehavior"=@("New-EC2Instance")
    "InstanceInterruptionBehavior"=@("Request-EC2SpotInstance")
    "InstanceTenancy"=@("Edit-EC2VpcTenancy","Get-EC2ReservedInstancesOffering","New-EC2Vpc")
    "InstanceType"=@("Get-EC2ReservedInstancesOffering","New-EC2Instance")
    "LaunchSpecification_InstanceType"=@("Request-EC2SpotInstance")
    "LaunchSpecification_Placement_Tenancy"=@("Request-EC2SpotInstance")
    "LimitPrice_CurrencyCode"=@("New-EC2ReservedInstance")
    "OfferingClass"=@("Get-EC2ReservedInstance","Get-EC2ReservedInstancesOffering")
    "OfferingType"=@("Get-EC2ReservedInstance","Get-EC2ReservedInstancesOffering")
    "OperationType"=@("Edit-EC2FpgaImageAttribute","Edit-EC2ImageAttribute","Edit-EC2SnapshotAttribute")
    "Permission"=@("New-EC2NetworkInterfacePermission")
    "ProductDescription"=@("Get-EC2ReservedInstancesOffering")
    "ResourceType"=@("New-EC2FlowLog")
    "RuleAction"=@("New-EC2NetworkAclEntry","Set-EC2NetworkAclEntry")
    "SpotFleetRequestConfig_AllocationStrategy"=@("Request-EC2SpotFleet")
    "SpotFleetRequestConfig_ExcessCapacityTerminationPolicy"=@("Request-EC2SpotFleet")
    "SpotFleetRequestConfig_InstanceInterruptionBehavior"=@("Request-EC2SpotFleet")
    "SpotFleetRequestConfig_Type"=@("Request-EC2SpotFleet")
    "SpotOptions_AllocationStrategy"=@("New-EC2Fleet")
    "SpotOptions_InstanceInterruptionBehavior"=@("New-EC2Fleet")
    "Status"=@("Send-EC2InstanceStatus")
    "Strategy"=@("New-EC2PlacementGroup")
    "TargetCapacitySpecification_DefaultTargetCapacityType"=@("Edit-EC2Fleet","New-EC2Fleet")
    "TargetEnvironment"=@("New-EC2InstanceExportTask")
    "Tenancy"=@("Edit-EC2InstancePlacement","New-EC2Instance")
    "TrafficType"=@("New-EC2FlowLog")
    "Type"=@("New-EC2CustomerGateway","New-EC2Fleet","New-EC2VpnGateway","Request-EC2SpotInstance")
    "VolumeType"=@("Edit-EC2Volume","New-EC2Volume")
    "VpcEndpointType"=@("New-EC2VpcEndpoint")
}

_awsArgumentCompleterRegistration $EC2_Completers $EC2_map


# Argument completions for service Amazon EC2 Container Registry
$ECR_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.ECR.TagStatus
        {
            ($_ -eq "Get-ECRImage/Filter_TagStatus") -Or
            ($_ -eq "Get-ECRImageMetadata/Filter_TagStatus") -Or
            ($_ -eq "Get-ECRLifecyclePolicyPreview/Filter_TagStatus")
        }
        {
            $v = "TAGGED","UNTAGGED"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$ECR_map = @{
    "Filter_TagStatus"=@("Get-ECRImage","Get-ECRImageMetadata","Get-ECRLifecyclePolicyPreview")
}

_awsArgumentCompleterRegistration $ECR_Completers $ECR_map


# Argument completions for service Amazon EC2 Container Service
$ECS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.ECS.AssignPublicIp
        {
            ($_ -eq "New-ECSService/NetworkConfiguration_AwsvpcConfiguration_AssignPublicIp") -Or
            ($_ -eq "New-ECSTask/NetworkConfiguration_AwsvpcConfiguration_AssignPublicIp") -Or
            ($_ -eq "Start-ECSTask/NetworkConfiguration_AwsvpcConfiguration_AssignPublicIp") -Or
            ($_ -eq "Update-ECSService/NetworkConfiguration_AwsvpcConfiguration_AssignPublicIp")
        }
        {
            $v = "DISABLED","ENABLED"
            break
        }
        
        # Amazon.ECS.ContainerInstanceStatus
        {
            ($_ -eq "Get-ECSContainerInstanceList/Status") -Or
            ($_ -eq "Update-ECSContainerInstancesState/Status")
        }
        {
            $v = "ACTIVE","DRAINING"
            break
        }
        
        # Amazon.ECS.DesiredStatus
        "Get-ECSTaskList/DesiredStatus"
        {
            $v = "PENDING","RUNNING","STOPPED"
            break
        }
        
        # Amazon.ECS.LaunchType
        {
            ($_ -eq "Get-ECSClusterService/LaunchType") -Or
            ($_ -eq "Get-ECSTaskList/LaunchType") -Or
            ($_ -eq "New-ECSService/LaunchType") -Or
            ($_ -eq "New-ECSTask/LaunchType")
        }
        {
            $v = "EC2","FARGATE"
            break
        }
        
        # Amazon.ECS.NetworkMode
        "Register-ECSTaskDefinition/NetworkMode"
        {
            $v = "awsvpc","bridge","host","none"
            break
        }
        
        # Amazon.ECS.SchedulingStrategy
        {
            ($_ -eq "Get-ECSClusterService/SchedulingStrategy") -Or
            ($_ -eq "New-ECSService/SchedulingStrategy")
        }
        {
            $v = "DAEMON","REPLICA"
            break
        }
        
        # Amazon.ECS.SortOrder
        "Get-ECSTaskDefinitionList/Sort"
        {
            $v = "ASC","DESC"
            break
        }
        
        # Amazon.ECS.TargetType
        "Get-ECSAttributeList/TargetType"
        {
            $v = "container-instance"
            break
        }
        
        # Amazon.ECS.TaskDefinitionFamilyStatus
        "Get-ECSTaskDefinitionFamilyList/Status"
        {
            $v = "ACTIVE","ALL","INACTIVE"
            break
        }
        
        # Amazon.ECS.TaskDefinitionStatus
        "Get-ECSTaskDefinitionList/Status"
        {
            $v = "ACTIVE","INACTIVE"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$ECS_map = @{
    "DesiredStatus"=@("Get-ECSTaskList")
    "LaunchType"=@("Get-ECSClusterService","Get-ECSTaskList","New-ECSService","New-ECSTask")
    "NetworkConfiguration_AwsvpcConfiguration_AssignPublicIp"=@("New-ECSService","New-ECSTask","Start-ECSTask","Update-ECSService")
    "NetworkMode"=@("Register-ECSTaskDefinition")
    "SchedulingStrategy"=@("Get-ECSClusterService","New-ECSService")
    "Sort"=@("Get-ECSTaskDefinitionList")
    "Status"=@("Get-ECSContainerInstanceList","Get-ECSTaskDefinitionFamilyList","Get-ECSTaskDefinitionList","Update-ECSContainerInstancesState")
    "TargetType"=@("Get-ECSAttributeList")
}

_awsArgumentCompleterRegistration $ECS_Completers $ECS_map


# Argument completions for service Amazon Elastic File System
$EFS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.ElasticFileSystem.PerformanceMode
        "New-EFSFileSystem/PerformanceMode"
        {
            $v = "generalPurpose","maxIO"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$EFS_map = @{
    "PerformanceMode"=@("New-EFSFileSystem")
}

_awsArgumentCompleterRegistration $EFS_Completers $EFS_map


# Argument completions for service Amazon ElastiCache
$EC_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.ElastiCache.AZMode
        {
            ($_ -eq "Edit-ECCacheCluster/AZMode") -Or
            ($_ -eq "New-ECCacheCluster/AZMode")
        }
        {
            $v = "cross-az","single-az"
            break
        }
        
        # Amazon.ElastiCache.SourceType
        "Get-ECEvent/SourceType"
        {
            $v = "cache-cluster","cache-parameter-group","cache-security-group","cache-subnet-group","replication-group"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$EC_map = @{
    "AZMode"=@("Edit-ECCacheCluster","New-ECCacheCluster")
    "SourceType"=@("Get-ECEvent")
}

_awsArgumentCompleterRegistration $EC_Completers $EC_map


# Argument completions for service Elastic Load Balancing V2
$ELB2_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.ElasticLoadBalancingV2.IpAddressType
        {
            ($_ -eq "New-ELB2LoadBalancer/IpAddressType") -Or
            ($_ -eq "Set-ELB2IpAddressType/IpAddressType")
        }
        {
            $v = "dualstack","ipv4"
            break
        }
        
        # Amazon.ElasticLoadBalancingV2.LoadBalancerSchemeEnum
        "New-ELB2LoadBalancer/Scheme"
        {
            $v = "internal","internet-facing"
            break
        }
        
        # Amazon.ElasticLoadBalancingV2.LoadBalancerTypeEnum
        "New-ELB2LoadBalancer/Type"
        {
            $v = "application","network"
            break
        }
        
        # Amazon.ElasticLoadBalancingV2.ProtocolEnum
        {
            ($_ -eq "Edit-ELB2TargetGroup/HealthCheckProtocol") -Or
            ($_ -eq "New-ELB2TargetGroup/HealthCheckProtocol") -Or
            ($_ -eq "Edit-ELB2Listener/Protocol") -Or
            ($_ -eq "New-ELB2Listener/Protocol") -Or
            ($_ -eq "New-ELB2TargetGroup/Protocol")
        }
        {
            $v = "HTTP","HTTPS","TCP"
            break
        }
        
        # Amazon.ElasticLoadBalancingV2.TargetTypeEnum
        "New-ELB2TargetGroup/TargetType"
        {
            $v = "instance","ip"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$ELB2_map = @{
    "HealthCheckProtocol"=@("Edit-ELB2TargetGroup","New-ELB2TargetGroup")
    "IpAddressType"=@("New-ELB2LoadBalancer","Set-ELB2IpAddressType")
    "Protocol"=@("Edit-ELB2Listener","New-ELB2Listener","New-ELB2TargetGroup")
    "Scheme"=@("New-ELB2LoadBalancer")
    "TargetType"=@("New-ELB2TargetGroup")
    "Type"=@("New-ELB2LoadBalancer")
}

_awsArgumentCompleterRegistration $ELB2_Completers $ELB2_map


# Argument completions for service Amazon Elastic MapReduce
$EMR_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.ElasticMapReduce.InstanceFleetType
        {
            ($_ -eq "Add-EMRInstanceFleet/InstanceFleet_InstanceFleetType") -Or
            ($_ -eq "Get-EMRInstanceList/InstanceFleetType")
        }
        {
            $v = "CORE","MASTER","TASK"
            break
        }
        
        # Amazon.ElasticMapReduce.RepoUpgradeOnBoot
        "Start-EMRJobFlow/RepoUpgradeOnBoot"
        {
            $v = "NONE","SECURITY"
            break
        }
        
        # Amazon.ElasticMapReduce.ScaleDownBehavior
        "Start-EMRJobFlow/ScaleDownBehavior"
        {
            $v = "TERMINATE_AT_INSTANCE_HOUR","TERMINATE_AT_TASK_COMPLETION"
            break
        }
        
        # Amazon.ElasticMapReduce.SpotProvisioningTimeoutAction
        "Add-EMRInstanceFleet/InstanceFleet_LaunchSpecifications_SpotSpecification_TimeoutAction"
        {
            $v = "SWITCH_TO_ON_DEMAND","TERMINATE_CLUSTER"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$EMR_map = @{
    "InstanceFleet_InstanceFleetType"=@("Add-EMRInstanceFleet")
    "InstanceFleet_LaunchSpecifications_SpotSpecification_TimeoutAction"=@("Add-EMRInstanceFleet")
    "InstanceFleetType"=@("Get-EMRInstanceList")
    "RepoUpgradeOnBoot"=@("Start-EMRJobFlow")
    "ScaleDownBehavior"=@("Start-EMRJobFlow")
}

_awsArgumentCompleterRegistration $EMR_Completers $EMR_map


# Argument completions for service Amazon Elasticsearch
$ES_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Elasticsearch.ESPartitionInstanceType
        {
            ($_ -eq "New-ESDomain/ElasticsearchClusterConfig_DedicatedMasterType") -Or
            ($_ -eq "Update-ESDomainConfig/ElasticsearchClusterConfig_DedicatedMasterType") -Or
            ($_ -eq "New-ESDomain/ElasticsearchClusterConfig_InstanceType") -Or
            ($_ -eq "Update-ESDomainConfig/ElasticsearchClusterConfig_InstanceType") -Or
            ($_ -eq "Get-ESInstanceTypeLimit/InstanceType")
        }
        {
            $v = "c4.2xlarge.elasticsearch","c4.4xlarge.elasticsearch","c4.8xlarge.elasticsearch","c4.large.elasticsearch","c4.xlarge.elasticsearch","d2.2xlarge.elasticsearch","d2.4xlarge.elasticsearch","d2.8xlarge.elasticsearch","d2.xlarge.elasticsearch","i2.2xlarge.elasticsearch","i2.xlarge.elasticsearch","i3.16xlarge.elasticsearch","i3.2xlarge.elasticsearch","i3.4xlarge.elasticsearch","i3.8xlarge.elasticsearch","i3.large.elasticsearch","i3.xlarge.elasticsearch","m3.2xlarge.elasticsearch","m3.large.elasticsearch","m3.medium.elasticsearch","m3.xlarge.elasticsearch","m4.10xlarge.elasticsearch","m4.2xlarge.elasticsearch","m4.4xlarge.elasticsearch","m4.large.elasticsearch","m4.xlarge.elasticsearch","r3.2xlarge.elasticsearch","r3.4xlarge.elasticsearch","r3.8xlarge.elasticsearch","r3.large.elasticsearch","r3.xlarge.elasticsearch","r4.16xlarge.elasticsearch","r4.2xlarge.elasticsearch","r4.4xlarge.elasticsearch","r4.8xlarge.elasticsearch","r4.large.elasticsearch","r4.xlarge.elasticsearch","t2.medium.elasticsearch","t2.micro.elasticsearch","t2.small.elasticsearch"
            break
        }
        
        # Amazon.Elasticsearch.VolumeType
        {
            ($_ -eq "New-ESDomain/EBSOptions_VolumeType") -Or
            ($_ -eq "Update-ESDomainConfig/EBSOptions_VolumeType")
        }
        {
            $v = "gp2","io1","standard"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$ES_map = @{
    "EBSOptions_VolumeType"=@("New-ESDomain","Update-ESDomainConfig")
    "ElasticsearchClusterConfig_DedicatedMasterType"=@("New-ESDomain","Update-ESDomainConfig")
    "ElasticsearchClusterConfig_InstanceType"=@("New-ESDomain","Update-ESDomainConfig")
    "InstanceType"=@("Get-ESInstanceTypeLimit")
}

_awsArgumentCompleterRegistration $ES_Completers $ES_map


# Argument completions for service AWS Glue
$GLUE_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Glue.Language
        {
            ($_ -eq "Get-GLUEPlan/Language") -Or
            ($_ -eq "New-GLUEScript/Language")
        }
        {
            $v = "PYTHON","SCALA"
            break
        }
        
        # Amazon.Glue.TriggerType
        "New-GLUETrigger/Type"
        {
            $v = "CONDITIONAL","ON_DEMAND","SCHEDULED"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$GLUE_map = @{
    "Language"=@("Get-GLUEPlan","New-GLUEScript")
    "Type"=@("New-GLUETrigger")
}

_awsArgumentCompleterRegistration $GLUE_Completers $GLUE_map


# Argument completions for service Amazon GameLift Service
$GML_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.GameLift.AcceptanceType
        "Confirm-GMLMatch/AcceptanceType"
        {
            $v = "ACCEPT","REJECT"
            break
        }
        
        # Amazon.GameLift.BuildStatus
        "Get-GMLBuild/Status"
        {
            $v = "FAILED","INITIALIZED","READY"
            break
        }
        
        # Amazon.GameLift.ComparisonOperatorType
        "Write-GMLScalingPolicy/ComparisonOperator"
        {
            $v = "GreaterThanOrEqualToThreshold","GreaterThanThreshold","LessThanOrEqualToThreshold","LessThanThreshold"
            break
        }
        
        # Amazon.GameLift.EC2InstanceType
        {
            ($_ -eq "Get-GMLEC2InstanceLimit/EC2InstanceType") -Or
            ($_ -eq "New-GMLFleet/EC2InstanceType")
        }
        {
            $v = "c3.2xlarge","c3.4xlarge","c3.8xlarge","c3.large","c3.xlarge","c4.2xlarge","c4.4xlarge","c4.8xlarge","c4.large","c4.xlarge","m3.2xlarge","m3.large","m3.medium","m3.xlarge","m4.10xlarge","m4.2xlarge","m4.4xlarge","m4.large","m4.xlarge","r3.2xlarge","r3.4xlarge","r3.8xlarge","r3.large","r3.xlarge","r4.16xlarge","r4.2xlarge","r4.4xlarge","r4.8xlarge","r4.large","r4.xlarge","t2.large","t2.medium","t2.micro","t2.small"
            break
        }
        
        # Amazon.GameLift.FleetType
        "New-GMLFleet/FleetType"
        {
            $v = "ON_DEMAND","SPOT"
            break
        }
        
        # Amazon.GameLift.MetricName
        "Write-GMLScalingPolicy/MetricName"
        {
            $v = "ActivatingGameSessions","ActiveGameSessions","ActiveInstances","AvailableGameSessions","AvailablePlayerSessions","CurrentPlayerSessions","IdleInstances","PercentAvailableGameSessions","PercentIdleInstances","QueueDepth","WaitTime"
            break
        }
        
        # Amazon.GameLift.OperatingSystem
        "New-GMLBuild/OperatingSystem"
        {
            $v = "AMAZON_LINUX","WINDOWS_2012"
            break
        }
        
        # Amazon.GameLift.PlayerSessionCreationPolicy
        "Update-GMLGameSession/PlayerSessionCreationPolicy"
        {
            $v = "ACCEPT_ALL","DENY_ALL"
            break
        }
        
        # Amazon.GameLift.PolicyType
        "Write-GMLScalingPolicy/PolicyType"
        {
            $v = "RuleBased","TargetBased"
            break
        }
        
        # Amazon.GameLift.ProtectionPolicy
        {
            ($_ -eq "New-GMLFleet/NewGameSessionProtectionPolicy") -Or
            ($_ -eq "Update-GMLFleetAttribute/NewGameSessionProtectionPolicy") -Or
            ($_ -eq "Update-GMLGameSession/ProtectionPolicy")
        }
        {
            $v = "FullProtection","NoProtection"
            break
        }
        
        # Amazon.GameLift.RoutingStrategyType
        {
            ($_ -eq "New-GMLAlias/RoutingStrategy_Type") -Or
            ($_ -eq "Update-GMLAlias/RoutingStrategy_Type") -Or
            ($_ -eq "Get-GMLAlias/RoutingStrategyType")
        }
        {
            $v = "SIMPLE","TERMINAL"
            break
        }
        
        # Amazon.GameLift.ScalingAdjustmentType
        "Write-GMLScalingPolicy/ScalingAdjustmentType"
        {
            $v = "ChangeInCapacity","ExactCapacity","PercentChangeInCapacity"
            break
        }
        
        # Amazon.GameLift.ScalingStatusType
        "Get-GMLScalingPolicy/StatusFilter"
        {
            $v = "ACTIVE","DELETED","DELETE_REQUESTED","DELETING","ERROR","UPDATE_REQUESTED","UPDATING"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$GML_map = @{
    "AcceptanceType"=@("Confirm-GMLMatch")
    "ComparisonOperator"=@("Write-GMLScalingPolicy")
    "EC2InstanceType"=@("Get-GMLEC2InstanceLimit","New-GMLFleet")
    "FleetType"=@("New-GMLFleet")
    "MetricName"=@("Write-GMLScalingPolicy")
    "NewGameSessionProtectionPolicy"=@("New-GMLFleet","Update-GMLFleetAttribute")
    "OperatingSystem"=@("New-GMLBuild")
    "PlayerSessionCreationPolicy"=@("Update-GMLGameSession")
    "PolicyType"=@("Write-GMLScalingPolicy")
    "ProtectionPolicy"=@("Update-GMLGameSession")
    "RoutingStrategy_Type"=@("New-GMLAlias","Update-GMLAlias")
    "RoutingStrategyType"=@("Get-GMLAlias")
    "ScalingAdjustmentType"=@("Write-GMLScalingPolicy")
    "Status"=@("Get-GMLBuild")
    "StatusFilter"=@("Get-GMLScalingPolicy")
}

_awsArgumentCompleterRegistration $GML_Completers $GML_map


# Argument completions for service AWS Greengrass
$GG_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Greengrass.DeploymentType
        "New-GGDeployment/DeploymentType"
        {
            $v = "ForceResetDeployment","NewDeployment","Redeployment","ResetDeployment"
            break
        }
        
        # Amazon.Greengrass.SoftwareToUpdate
        "New-GGSoftwareUpdateJob/SoftwareToUpdate"
        {
            $v = "core","ota_agent"
            break
        }
        
        # Amazon.Greengrass.UpdateAgentLogLevel
        "New-GGSoftwareUpdateJob/UpdateAgentLogLevel"
        {
            $v = "DEBUG","ERROR","FATAL","INFO","NONE","TRACE","VERBOSE","WARN"
            break
        }
        
        # Amazon.Greengrass.UpdateTargetsArchitecture
        "New-GGSoftwareUpdateJob/UpdateTargetsArchitecture"
        {
            $v = "aarch64","armv7l","x86_64"
            break
        }
        
        # Amazon.Greengrass.UpdateTargetsOperatingSystem
        "New-GGSoftwareUpdateJob/UpdateTargetsOperatingSystem"
        {
            $v = "amazon_linux","raspbian","ubuntu"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$GG_map = @{
    "DeploymentType"=@("New-GGDeployment")
    "SoftwareToUpdate"=@("New-GGSoftwareUpdateJob")
    "UpdateAgentLogLevel"=@("New-GGSoftwareUpdateJob")
    "UpdateTargetsArchitecture"=@("New-GGSoftwareUpdateJob")
    "UpdateTargetsOperatingSystem"=@("New-GGSoftwareUpdateJob")
}

_awsArgumentCompleterRegistration $GG_Completers $GG_map


# Argument completions for service Amazon GuardDuty
$GD_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.GuardDuty.Feedback
        "Update-GDFindingFeedback/Feedback"
        {
            $v = "NOT_USEFUL","USEFUL"
            break
        }
        
        # Amazon.GuardDuty.FilterAction
        {
            ($_ -eq "New-GDFilter/Action") -Or
            ($_ -eq "Update-GDFilter/Action")
        }
        {
            $v = "ARCHIVE","NOOP"
            break
        }
        
        # Amazon.GuardDuty.IpSetFormat
        "New-GDIPSet/Format"
        {
            $v = "ALIEN_VAULT","FIRE_EYE","OTX_CSV","PROOF_POINT","STIX","TXT"
            break
        }
        
        # Amazon.GuardDuty.ThreatIntelSetFormat
        "New-GDThreatIntelSet/Format"
        {
            $v = "ALIEN_VAULT","FIRE_EYE","OTX_CSV","PROOF_POINT","STIX","TXT"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$GD_map = @{
    "Action"=@("New-GDFilter","Update-GDFilter")
    "Feedback"=@("Update-GDFindingFeedback")
    "Format"=@("New-GDIPSet","New-GDThreatIntelSet")
}

_awsArgumentCompleterRegistration $GD_Completers $GD_map


# Argument completions for service AWS Health
$HLTH_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.AWSHealth.EventAggregateField
        "Get-HLTHEventAggregate/AggregateField"
        {
            $v = "eventTypeCategory"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$HLTH_map = @{
    "AggregateField"=@("Get-HLTHEventAggregate")
}

_awsArgumentCompleterRegistration $HLTH_Completers $HLTH_map


# Argument completions for service AWS Cloud HSM
$HSM_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.CloudHSM.ClientVersion
        "Get-HSMConfig/ClientVersion"
        {
            $v = "5.1","5.3"
            break
        }
        
        # Amazon.CloudHSM.SubscriptionType
        "New-HSMItem/SubscriptionType"
        {
            $v = "PRODUCTION"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$HSM_map = @{
    "ClientVersion"=@("Get-HSMConfig")
    "SubscriptionType"=@("New-HSMItem")
}

_awsArgumentCompleterRegistration $HSM_Completers $HSM_map


# Argument completions for service AWS Identity and Access Management
$IAM_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.IdentityManagement.AssignmentStatusType
        "Get-IAMVirtualMFADevice/AssignmentStatus"
        {
            $v = "Any","Assigned","Unassigned"
            break
        }
        
        # Amazon.IdentityManagement.EncodingType
        "Get-IAMSSHPublicKey/Encoding"
        {
            $v = "PEM","SSH"
            break
        }
        
        # Amazon.IdentityManagement.EntityType
        "Get-IAMEntitiesForPolicy/EntityFilter"
        {
            $v = "AWSManagedPolicy","Group","LocalManagedPolicy","Role","User"
            break
        }
        
        # Amazon.IdentityManagement.PolicyScopeType
        "Get-IAMPolicyList/Scope"
        {
            $v = "All","AWS","Local"
            break
        }
        
        # Amazon.IdentityManagement.StatusType
        {
            ($_ -eq "Update-IAMAccessKey/Status") -Or
            ($_ -eq "Update-IAMServiceSpecificCredential/Status") -Or
            ($_ -eq "Update-IAMSigningCertificate/Status") -Or
            ($_ -eq "Update-IAMSSHPublicKey/Status")
        }
        {
            $v = "Active","Inactive"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$IAM_map = @{
    "AssignmentStatus"=@("Get-IAMVirtualMFADevice")
    "Encoding"=@("Get-IAMSSHPublicKey")
    "EntityFilter"=@("Get-IAMEntitiesForPolicy")
    "Scope"=@("Get-IAMPolicyList")
    "Status"=@("Update-IAMAccessKey","Update-IAMServiceSpecificCredential","Update-IAMSigningCertificate","Update-IAMSSHPublicKey")
}

_awsArgumentCompleterRegistration $IAM_Completers $IAM_map


# Argument completions for service AWS Import/Export
$IE_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.ImportExport.JobType
        {
            ($_ -eq "New-IEJob/JobType") -Or
            ($_ -eq "Update-IEJob/JobType")
        }
        {
            $v = "Export","Import"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$IE_map = @{
    "JobType"=@("New-IEJob","Update-IEJob")
}

_awsArgumentCompleterRegistration $IE_Completers $IE_map


# Argument completions for service Amazon Inspector
$INS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Inspector.InspectorEvent
        {
            ($_ -eq "Add-INSEventSubscription/Event") -Or
            ($_ -eq "Remove-INSEventSubscription/Event")
        }
        {
            $v = "ASSESSMENT_RUN_COMPLETED","ASSESSMENT_RUN_STARTED","ASSESSMENT_RUN_STATE_CHANGED","FINDING_REPORTED","OTHER"
            break
        }
        
        # Amazon.Inspector.Locale
        {
            ($_ -eq "Get-INSExclusion/Locale") -Or
            ($_ -eq "Get-INSExclusionsPreview/Locale") -Or
            ($_ -eq "Get-INSFinding/Locale") -Or
            ($_ -eq "Get-INSRulesPackage/Locale")
        }
        {
            $v = "EN_US"
            break
        }
        
        # Amazon.Inspector.ReportFileFormat
        "Get-INSAssessmentReport/ReportFileFormat"
        {
            $v = "HTML","PDF"
            break
        }
        
        # Amazon.Inspector.ReportType
        "Get-INSAssessmentReport/ReportType"
        {
            $v = "FINDING","FULL"
            break
        }
        
        # Amazon.Inspector.StopAction
        "Stop-INSAssessmentRun/StopAction"
        {
            $v = "SKIP_EVALUATION","START_EVALUATION"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$INS_map = @{
    "Event"=@("Add-INSEventSubscription","Remove-INSEventSubscription")
    "Locale"=@("Get-INSExclusion","Get-INSExclusionsPreview","Get-INSFinding","Get-INSRulesPackage")
    "ReportFileFormat"=@("Get-INSAssessmentReport")
    "ReportType"=@("Get-INSAssessmentReport")
    "StopAction"=@("Stop-INSAssessmentRun")
}

_awsArgumentCompleterRegistration $INS_Completers $INS_map


# Argument completions for service AWS IoT
$IOT_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.IoT.AuthorizerStatus
        {
            ($_ -eq "Get-IOTAuthorizersList/Status") -Or
            ($_ -eq "New-IOTAuthorizer/Status") -Or
            ($_ -eq "Update-IOTAuthorizer/Status")
        }
        {
            $v = "ACTIVE","INACTIVE"
            break
        }
        
        # Amazon.IoT.AutoRegistrationStatus
        "Update-IOTCACertificate/NewAutoRegistrationStatus"
        {
            $v = "DISABLE","ENABLE"
            break
        }
        
        # Amazon.IoT.CACertificateStatus
        "Update-IOTCACertificate/NewStatus"
        {
            $v = "ACTIVE","INACTIVE"
            break
        }
        
        # Amazon.IoT.CannedAccessControlList
        {
            ($_ -eq "New-IOTTopicRule/TopicRulePayload_ErrorAction_S3_CannedAcl") -Or
            ($_ -eq "Set-IOTTopicRule/TopicRulePayload_ErrorAction_S3_CannedAcl")
        }
        {
            $v = "authenticated-read","aws-exec-read","bucket-owner-full-control","bucket-owner-read","log-delivery-write","private","public-read","public-read-write"
            break
        }
        
        # Amazon.IoT.CertificateStatus
        {
            ($_ -eq "Update-IOTCertificate/NewStatus") -Or
            ($_ -eq "Register-IOTCertificate/Status")
        }
        {
            $v = "ACTIVE","INACTIVE","PENDING_ACTIVATION","PENDING_TRANSFER","REGISTER_INACTIVE","REVOKED"
            break
        }
        
        # Amazon.IoT.DynamoKeyType
        {
            ($_ -eq "New-IOTTopicRule/TopicRulePayload_ErrorAction_DynamoDB_HashKeyType") -Or
            ($_ -eq "Set-IOTTopicRule/TopicRulePayload_ErrorAction_DynamoDB_HashKeyType") -Or
            ($_ -eq "New-IOTTopicRule/TopicRulePayload_ErrorAction_DynamoDB_RangeKeyType") -Or
            ($_ -eq "Set-IOTTopicRule/TopicRulePayload_ErrorAction_DynamoDB_RangeKeyType")
        }
        {
            $v = "NUMBER","STRING"
            break
        }
        
        # Amazon.IoT.JobExecutionStatus
        {
            ($_ -eq "Get-IOTJobExecutionsForJobList/Status") -Or
            ($_ -eq "Get-IOTJobExecutionsForThingList/Status")
        }
        {
            $v = "CANCELED","FAILED","IN_PROGRESS","QUEUED","REJECTED","REMOVED","SUCCEEDED"
            break
        }
        
        # Amazon.IoT.JobStatus
        "Get-IOTJobsList/Status"
        {
            $v = "CANCELED","COMPLETED","DELETION_IN_PROGRESS","IN_PROGRESS"
            break
        }
        
        # Amazon.IoT.LogLevel
        {
            ($_ -eq "Set-IOTV2LoggingOption/DefaultLogLevel") -Or
            ($_ -eq "Set-IOTLoggingOption/LoggingOptionsPayload_LogLevel") -Or
            ($_ -eq "Set-IOTV2LoggingLevel/LogLevel")
        }
        {
            $v = "DEBUG","DISABLED","ERROR","INFO","WARN"
            break
        }
        
        # Amazon.IoT.LogTargetType
        {
            ($_ -eq "Set-IOTV2LoggingLevel/LogTarget_TargetType") -Or
            ($_ -eq "Get-IOTV2LoggingLevelsList/TargetType") -Or
            ($_ -eq "Remove-IOTV2LoggingLevel/TargetType")
        }
        {
            $v = "DEFAULT","THING_GROUP"
            break
        }
        
        # Amazon.IoT.MessageFormat
        {
            ($_ -eq "New-IOTTopicRule/TopicRulePayload_ErrorAction_Sns_MessageFormat") -Or
            ($_ -eq "Set-IOTTopicRule/TopicRulePayload_ErrorAction_Sns_MessageFormat")
        }
        {
            $v = "JSON","RAW"
            break
        }
        
        # Amazon.IoT.OTAUpdateStatus
        "Get-IOTOTAUpdateList/OtaUpdateStatus"
        {
            $v = "CREATE_COMPLETE","CREATE_FAILED","CREATE_IN_PROGRESS","CREATE_PENDING"
            break
        }
        
        # Amazon.IoT.ReportType
        "Get-IOTThingRegistrationTaskReportsList/ReportType"
        {
            $v = "ERRORS","RESULTS"
            break
        }
        
        # Amazon.IoT.Status
        "Get-IOTThingRegistrationTasksList/Status"
        {
            $v = "Cancelled","Cancelling","Completed","Failed","InProgress"
            break
        }
        
        # Amazon.IoT.TargetSelection
        {
            ($_ -eq "Get-IOTJobsList/TargetSelection") -Or
            ($_ -eq "New-IOTJob/TargetSelection") -Or
            ($_ -eq "New-IOTOTAUpdate/TargetSelection")
        }
        {
            $v = "CONTINUOUS","SNAPSHOT"
            break
        }
        
        # Amazon.IoT.ThingIndexingMode
        "Update-IOTIndexingConfiguration/ThingIndexingConfiguration_ThingIndexingMode"
        {
            $v = "OFF","REGISTRY","REGISTRY_AND_SHADOW"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$IOT_map = @{
    "DefaultLogLevel"=@("Set-IOTV2LoggingOption")
    "LoggingOptionsPayload_LogLevel"=@("Set-IOTLoggingOption")
    "LogLevel"=@("Set-IOTV2LoggingLevel")
    "LogTarget_TargetType"=@("Set-IOTV2LoggingLevel")
    "NewAutoRegistrationStatus"=@("Update-IOTCACertificate")
    "NewStatus"=@("Update-IOTCACertificate","Update-IOTCertificate")
    "OtaUpdateStatus"=@("Get-IOTOTAUpdateList")
    "ReportType"=@("Get-IOTThingRegistrationTaskReportsList")
    "Status"=@("Get-IOTAuthorizersList","Get-IOTJobExecutionsForJobList","Get-IOTJobExecutionsForThingList","Get-IOTJobsList","Get-IOTThingRegistrationTasksList","New-IOTAuthorizer","Register-IOTCertificate","Update-IOTAuthorizer")
    "TargetSelection"=@("Get-IOTJobsList","New-IOTJob","New-IOTOTAUpdate")
    "TargetType"=@("Get-IOTV2LoggingLevelsList","Remove-IOTV2LoggingLevel")
    "ThingIndexingConfiguration_ThingIndexingMode"=@("Update-IOTIndexingConfiguration")
    "TopicRulePayload_ErrorAction_DynamoDB_HashKeyType"=@("New-IOTTopicRule","Set-IOTTopicRule")
    "TopicRulePayload_ErrorAction_DynamoDB_RangeKeyType"=@("New-IOTTopicRule","Set-IOTTopicRule")
    "TopicRulePayload_ErrorAction_S3_CannedAcl"=@("New-IOTTopicRule","Set-IOTTopicRule")
    "TopicRulePayload_ErrorAction_Sns_MessageFormat"=@("New-IOTTopicRule","Set-IOTTopicRule")
}

_awsArgumentCompleterRegistration $IOT_Completers $IOT_map


# Argument completions for service AWS IoT Jobs Data Plane
$IOTJ_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.IoTJobsDataPlane.JobExecutionStatus
        "Update-IOTJJobExecution/Status"
        {
            $v = "CANCELED","FAILED","IN_PROGRESS","QUEUED","REJECTED","REMOVED","SUCCEEDED"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$IOTJ_map = @{
    "Status"=@("Update-IOTJJobExecution")
}

_awsArgumentCompleterRegistration $IOTJ_Completers $IOTJ_map


# Argument completions for service Amazon Kinesis
$KIN_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Kinesis.EncryptionType
        {
            ($_ -eq "Start-KINStreamEncryption/EncryptionType") -Or
            ($_ -eq "Stop-KINStreamEncryption/EncryptionType")
        }
        {
            $v = "KMS","NONE"
            break
        }
        
        # Amazon.Kinesis.ScalingType
        "Update-KINShardCount/ScalingType"
        {
            $v = "UNIFORM_SCALING"
            break
        }
        
        # Amazon.Kinesis.ShardIteratorType
        "Get-KINShardIterator/ShardIteratorType"
        {
            $v = "AFTER_SEQUENCE_NUMBER","AT_SEQUENCE_NUMBER","AT_TIMESTAMP","LATEST","TRIM_HORIZON"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$KIN_map = @{
    "EncryptionType"=@("Start-KINStreamEncryption","Stop-KINStreamEncryption")
    "ScalingType"=@("Update-KINShardCount")
    "ShardIteratorType"=@("Get-KINShardIterator")
}

_awsArgumentCompleterRegistration $KIN_Completers $KIN_map


# Argument completions for service Amazon Kinesis Analytics
$KINA_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.KinesisAnalytics.InputStartingPosition
        "Find-KINAInputSchema/InputStartingPositionConfiguration_InputStartingPosition"
        {
            $v = "LAST_STOPPED_POINT","NOW","TRIM_HORIZON"
            break
        }
        
        # Amazon.KinesisAnalytics.RecordFormatType
        {
            ($_ -eq "Add-KINAApplicationInput/Input_InputSchema_RecordFormat_RecordFormatType") -Or
            ($_ -eq "Add-KINAApplicationOutput/Output_DestinationSchema_RecordFormatType") -Or
            ($_ -eq "Add-KINAApplicationReferenceDataSource/ReferenceDataSource_ReferenceSchema_RecordFormat_RecordFormatType")
        }
        {
            $v = "CSV","JSON"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$KINA_map = @{
    "Input_InputSchema_RecordFormat_RecordFormatType"=@("Add-KINAApplicationInput")
    "InputStartingPositionConfiguration_InputStartingPosition"=@("Find-KINAInputSchema")
    "Output_DestinationSchema_RecordFormatType"=@("Add-KINAApplicationOutput")
    "ReferenceDataSource_ReferenceSchema_RecordFormat_RecordFormatType"=@("Add-KINAApplicationReferenceDataSource")
}

_awsArgumentCompleterRegistration $KINA_Completers $KINA_map


# Argument completions for service Amazon Kinesis Firehose
$KINF_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.KinesisFirehose.DeliveryStreamType
        {
            ($_ -eq "Get-KINFDeliveryStreamList/DeliveryStreamType") -Or
            ($_ -eq "New-KINFDeliveryStream/DeliveryStreamType")
        }
        {
            $v = "DirectPut","KinesisStreamAsSource"
            break
        }
        
        # Amazon.KinesisFirehose.ElasticsearchIndexRotationPeriod
        {
            ($_ -eq "New-KINFDeliveryStream/ElasticsearchDestinationConfiguration_IndexRotationPeriod") -Or
            ($_ -eq "Update-KINFDestination/ElasticsearchDestinationUpdate_IndexRotationPeriod")
        }
        {
            $v = "NoRotation","OneDay","OneHour","OneMonth","OneWeek"
            break
        }
        
        # Amazon.KinesisFirehose.ElasticsearchS3BackupMode
        "New-KINFDeliveryStream/ElasticsearchDestinationConfiguration_S3BackupMode"
        {
            $v = "AllDocuments","FailedDocumentsOnly"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$KINF_map = @{
    "DeliveryStreamType"=@("Get-KINFDeliveryStreamList","New-KINFDeliveryStream")
    "ElasticsearchDestinationConfiguration_IndexRotationPeriod"=@("New-KINFDeliveryStream")
    "ElasticsearchDestinationConfiguration_S3BackupMode"=@("New-KINFDeliveryStream")
    "ElasticsearchDestinationUpdate_IndexRotationPeriod"=@("Update-KINFDestination")
}

_awsArgumentCompleterRegistration $KINF_Completers $KINF_map


# Argument completions for service Amazon Kinesis Video Streams
$KV_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.KinesisVideo.APIName
        "Get-KVDataEndpoint/APIName"
        {
            $v = "GET_MEDIA","GET_MEDIA_FOR_FRAGMENT_LIST","LIST_FRAGMENTS","PUT_MEDIA"
            break
        }
        
        # Amazon.KinesisVideo.UpdateDataRetentionOperation
        "Update-KVDataRetention/Operation"
        {
            $v = "DECREASE_DATA_RETENTION","INCREASE_DATA_RETENTION"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$KV_map = @{
    "APIName"=@("Get-KVDataEndpoint")
    "Operation"=@("Update-KVDataRetention")
}

_awsArgumentCompleterRegistration $KV_Completers $KV_map


# Argument completions for service AWS Key Management Service
$KMS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.KeyManagementService.AlgorithmSpec
        "Get-KMSParametersForImport/WrappingAlgorithm"
        {
            $v = "RSAES_OAEP_SHA_1","RSAES_OAEP_SHA_256","RSAES_PKCS1_V1_5"
            break
        }
        
        # Amazon.KeyManagementService.DataKeySpec
        {
            ($_ -eq "New-KMSDataKey/KeySpec") -Or
            ($_ -eq "New-KMSDataKeyWithoutPlaintext/KeySpec")
        }
        {
            $v = "AES_128","AES_256"
            break
        }
        
        # Amazon.KeyManagementService.ExpirationModelType
        "Import-KMSKeyMaterial/ExpirationModel"
        {
            $v = "KEY_MATERIAL_DOES_NOT_EXPIRE","KEY_MATERIAL_EXPIRES"
            break
        }
        
        # Amazon.KeyManagementService.KeyUsageType
        "New-KMSKey/KeyUsage"
        {
            $v = "ENCRYPT_DECRYPT"
            break
        }
        
        # Amazon.KeyManagementService.OriginType
        "New-KMSKey/Origin"
        {
            $v = "AWS_KMS","EXTERNAL"
            break
        }
        
        # Amazon.KeyManagementService.WrappingKeySpec
        "Get-KMSParametersForImport/WrappingKeySpec"
        {
            $v = "RSA_2048"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$KMS_map = @{
    "ExpirationModel"=@("Import-KMSKeyMaterial")
    "KeySpec"=@("New-KMSDataKey","New-KMSDataKeyWithoutPlaintext")
    "KeyUsage"=@("New-KMSKey")
    "Origin"=@("New-KMSKey")
    "WrappingAlgorithm"=@("Get-KMSParametersForImport")
    "WrappingKeySpec"=@("Get-KMSParametersForImport")
}

_awsArgumentCompleterRegistration $KMS_Completers $KMS_map


# Argument completions for service AWS Lambda
$LM_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Lambda.EventSourcePosition
        "New-LMEventSourceMapping/StartingPosition"
        {
            $v = "AT_TIMESTAMP","LATEST","TRIM_HORIZON"
            break
        }
        
        # Amazon.Lambda.FunctionVersion
        "Get-LMFunctionList/FunctionVersion"
        {
            $v = "ALL"
            break
        }
        
        # Amazon.Lambda.InvocationType
        "Invoke-LMFunction/InvocationType"
        {
            $v = "DryRun","Event","RequestResponse"
            break
        }
        
        # Amazon.Lambda.LogType
        "Invoke-LMFunction/LogType"
        {
            $v = "None","Tail"
            break
        }
        
        # Amazon.Lambda.Runtime
        {
            ($_ -eq "Publish-LMFunction/Runtime") -Or
            ($_ -eq "Update-LMFunctionConfiguration/Runtime")
        }
        {
            $v = "dotnetcore1.0","dotnetcore2.0","go1.x","java8","nodejs","nodejs4.3","nodejs4.3-edge","nodejs6.10","nodejs8.10","python2.7","python3.6"
            break
        }
        
        # Amazon.Lambda.TracingMode
        "Update-LMFunctionConfiguration/TracingConfig_Mode"
        {
            $v = "Active","PassThrough"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$LM_map = @{
    "FunctionVersion"=@("Get-LMFunctionList")
    "InvocationType"=@("Invoke-LMFunction")
    "LogType"=@("Invoke-LMFunction")
    "Runtime"=@("Publish-LMFunction","Update-LMFunctionConfiguration")
    "StartingPosition"=@("New-LMEventSourceMapping")
    "TracingConfig_Mode"=@("Update-LMFunctionConfiguration")
}

_awsArgumentCompleterRegistration $LM_Completers $LM_map


# Argument completions for service Amazon Lex Model Building Service
$LMB_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.LexModelBuildingService.ExportType
        "Get-LMBExport/ExportType"
        {
            $v = "ALEXA_SKILLS_KIT","LEX"
            break
        }
        
        # Amazon.LexModelBuildingService.FulfillmentActivityType
        "Write-LMBIntent/FulfillmentActivity_Type"
        {
            $v = "CodeHook","ReturnIntent"
            break
        }
        
        # Amazon.LexModelBuildingService.Locale
        {
            ($_ -eq "Get-LMBBuiltinIntentList/Locale") -Or
            ($_ -eq "Get-LMBBuiltinSlotType/Locale") -Or
            ($_ -eq "Write-LMBBot/Locale")
        }
        {
            $v = "de-DE","en-GB","en-US"
            break
        }
        
        # Amazon.LexModelBuildingService.MergeStrategy
        "Start-LMBImport/MergeStrategy"
        {
            $v = "FAIL_ON_CONFLICT","OVERWRITE_LATEST"
            break
        }
        
        # Amazon.LexModelBuildingService.ProcessBehavior
        "Write-LMBBot/ProcessBehavior"
        {
            $v = "BUILD","SAVE"
            break
        }
        
        # Amazon.LexModelBuildingService.ResourceType
        {
            ($_ -eq "Get-LMBExport/ResourceType") -Or
            ($_ -eq "Start-LMBImport/ResourceType")
        }
        {
            $v = "BOT","INTENT","SLOT_TYPE"
            break
        }
        
        # Amazon.LexModelBuildingService.SlotValueSelectionStrategy
        "Write-LMBSlotType/ValueSelectionStrategy"
        {
            $v = "ORIGINAL_VALUE","TOP_RESOLUTION"
            break
        }
        
        # Amazon.LexModelBuildingService.StatusType
        "Get-LMBUtterancesView/StatusType"
        {
            $v = "Detected","Missed"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$LMB_map = @{
    "ExportType"=@("Get-LMBExport")
    "FulfillmentActivity_Type"=@("Write-LMBIntent")
    "Locale"=@("Get-LMBBuiltinIntentList","Get-LMBBuiltinSlotType","Write-LMBBot")
    "MergeStrategy"=@("Start-LMBImport")
    "ProcessBehavior"=@("Write-LMBBot")
    "ResourceType"=@("Get-LMBExport","Start-LMBImport")
    "StatusType"=@("Get-LMBUtterancesView")
    "ValueSelectionStrategy"=@("Write-LMBSlotType")
}

_awsArgumentCompleterRegistration $LMB_Completers $LMB_map


# Argument completions for service Amazon Lightsail
$LS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Lightsail.InstanceAccessProtocol
        "Get-LSInstanceAccessDetail/Protocol"
        {
            $v = "rdp","ssh"
            break
        }
        
        # Amazon.Lightsail.InstanceMetricName
        "Get-LSInstanceMetricData/MetricName"
        {
            $v = "CPUUtilization","NetworkIn","NetworkOut","StatusCheckFailed","StatusCheckFailed_Instance","StatusCheckFailed_System"
            break
        }
        
        # Amazon.Lightsail.LoadBalancerAttributeName
        "Update-LSLoadBalancerAttribute/AttributeName"
        {
            $v = "HealthCheckPath","SessionStickinessEnabled","SessionStickiness_LB_CookieDurationSeconds"
            break
        }
        
        # Amazon.Lightsail.LoadBalancerMetricName
        "Get-LSLoadBalancerMetricData/MetricName"
        {
            $v = "ClientTLSNegotiationErrorCount","HealthyHostCount","HTTPCode_Instance_2XX_Count","HTTPCode_Instance_3XX_Count","HTTPCode_Instance_4XX_Count","HTTPCode_Instance_5XX_Count","HTTPCode_LB_4XX_Count","HTTPCode_LB_5XX_Count","InstanceResponseTime","RejectedConnectionCount","RequestCount","UnhealthyHostCount"
            break
        }
        
        # Amazon.Lightsail.MetricUnit
        {
            ($_ -eq "Get-LSInstanceMetricData/Unit") -Or
            ($_ -eq "Get-LSLoadBalancerMetricData/Unit")
        }
        {
            $v = "Bits","Bits/Second","Bytes","Bytes/Second","Count","Count/Second","Gigabits","Gigabits/Second","Gigabytes","Gigabytes/Second","Kilobits","Kilobits/Second","Kilobytes","Kilobytes/Second","Megabits","Megabits/Second","Megabytes","Megabytes/Second","Microseconds","Milliseconds","None","Percent","Seconds","Terabits","Terabits/Second","Terabytes","Terabytes/Second"
            break
        }
        
        # Amazon.Lightsail.NetworkProtocol
        {
            ($_ -eq "Close-LSInstancePublicPort/PortInfo_Protocol") -Or
            ($_ -eq "Open-LSInstancePublicPort/PortInfo_Protocol")
        }
        {
            $v = "all","tcp","udp"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$LS_map = @{
    "AttributeName"=@("Update-LSLoadBalancerAttribute")
    "MetricName"=@("Get-LSInstanceMetricData","Get-LSLoadBalancerMetricData")
    "PortInfo_Protocol"=@("Close-LSInstancePublicPort","Open-LSInstancePublicPort")
    "Protocol"=@("Get-LSInstanceAccessDetail")
    "Unit"=@("Get-LSInstanceMetricData","Get-LSLoadBalancerMetricData")
}

_awsArgumentCompleterRegistration $LS_Completers $LS_map


# Argument completions for service AWS Marketplace Commerce Analytics
$MCA_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.AWSMarketplaceCommerceAnalytics.DataSetType
        "New-MCADataSet/DataSetType"
        {
            $v = "customer_profile_by_geography","customer_profile_by_industry","customer_profile_by_revenue","customer_subscriber_annual_subscriptions","customer_subscriber_hourly_monthly_subscriptions","daily_business_canceled_product_subscribers","daily_business_fees","daily_business_free_trial_conversions","daily_business_new_instances","daily_business_new_product_subscribers","daily_business_usage_by_instance_type","disbursed_amount_by_age_of_disbursed_funds","disbursed_amount_by_age_of_uncollected_funds","disbursed_amount_by_customer_geo","disbursed_amount_by_instance_hours","disbursed_amount_by_product","disbursed_amount_by_product_with_uncollected_funds","monthly_revenue_annual_subscriptions","monthly_revenue_billing_and_revenue_data","sales_compensation_billed_revenue","us_sales_and_use_tax_records"
            break
        }
        
        # Amazon.AWSMarketplaceCommerceAnalytics.SupportDataSetType
        "Start-MCASupportDataExport/DataSetType"
        {
            $v = "customer_support_contacts_data","test_customer_support_contacts_data"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$MCA_map = @{
    "DataSetType"=@("New-MCADataSet","Start-MCASupportDataExport")
}

_awsArgumentCompleterRegistration $MCA_Completers $MCA_map


# Argument completions for service AWS Elemental MediaConvert
$EMC_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.MediaConvert.JobStatus
        "Get-EMCJobList/Status"
        {
            $v = "CANCELED","COMPLETE","ERROR","PROGRESSING","SUBMITTED"
            break
        }
        
        # Amazon.MediaConvert.JobTemplateListBy
        "Get-EMCJobTemplateList/ListBy"
        {
            $v = "CREATION_DATE","NAME","SYSTEM"
            break
        }
        
        # Amazon.MediaConvert.Order
        {
            ($_ -eq "Get-EMCJobList/Order") -Or
            ($_ -eq "Get-EMCJobTemplateList/Order") -Or
            ($_ -eq "Get-EMCPresetList/Order") -Or
            ($_ -eq "Get-EMCQueueList/Order")
        }
        {
            $v = "ASCENDING","DESCENDING"
            break
        }
        
        # Amazon.MediaConvert.PresetListBy
        "Get-EMCPresetList/ListBy"
        {
            $v = "CREATION_DATE","NAME","SYSTEM"
            break
        }
        
        # Amazon.MediaConvert.QueueListBy
        "Get-EMCQueueList/ListBy"
        {
            $v = "CREATION_DATE","NAME"
            break
        }
        
        # Amazon.MediaConvert.QueueStatus
        "Update-EMCQueue/Status"
        {
            $v = "ACTIVE","PAUSED"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$EMC_map = @{
    "ListBy"=@("Get-EMCJobTemplateList","Get-EMCPresetList","Get-EMCQueueList")
    "Order"=@("Get-EMCJobList","Get-EMCJobTemplateList","Get-EMCPresetList","Get-EMCQueueList")
    "Status"=@("Get-EMCJobList","Update-EMCQueue")
}

_awsArgumentCompleterRegistration $EMC_Completers $EMC_map


# Argument completions for service AWS Elemental MediaLive
$EML_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.MediaLive.InputCodec
        {
            ($_ -eq "New-EMLChannel/InputSpecification_Codec") -Or
            ($_ -eq "Update-EMLChannel/InputSpecification_Codec")
        }
        {
            $v = "AVC","HEVC","MPEG2"
            break
        }
        
        # Amazon.MediaLive.InputMaximumBitrate
        {
            ($_ -eq "New-EMLChannel/InputSpecification_MaximumBitrate") -Or
            ($_ -eq "Update-EMLChannel/InputSpecification_MaximumBitrate")
        }
        {
            $v = "MAX_10_MBPS","MAX_20_MBPS","MAX_50_MBPS"
            break
        }
        
        # Amazon.MediaLive.InputResolution
        {
            ($_ -eq "New-EMLChannel/InputSpecification_Resolution") -Or
            ($_ -eq "Update-EMLChannel/InputSpecification_Resolution")
        }
        {
            $v = "HD","SD","UHD"
            break
        }
        
        # Amazon.MediaLive.InputType
        "New-EMLInput/Type"
        {
            $v = "RTMP_PULL","RTMP_PUSH","RTP_PUSH","UDP_PUSH","URL_PULL"
            break
        }
        
        # Amazon.MediaLive.LogLevel
        {
            ($_ -eq "New-EMLChannel/LogLevel") -Or
            ($_ -eq "Update-EMLChannel/LogLevel")
        }
        {
            $v = "DEBUG","DISABLED","ERROR","INFO","WARNING"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$EML_map = @{
    "InputSpecification_Codec"=@("New-EMLChannel","Update-EMLChannel")
    "InputSpecification_MaximumBitrate"=@("New-EMLChannel","Update-EMLChannel")
    "InputSpecification_Resolution"=@("New-EMLChannel","Update-EMLChannel")
    "LogLevel"=@("New-EMLChannel","Update-EMLChannel")
    "Type"=@("New-EMLInput")
}

_awsArgumentCompleterRegistration $EML_Completers $EML_map


# Argument completions for service AWS Elemental MediaPackage
$EMP_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.MediaPackage.StreamOrder
        {
            ($_ -eq "New-EMPOriginEndpoint/CmafPackage_StreamSelection_StreamOrder") -Or
            ($_ -eq "Update-EMPOriginEndpoint/CmafPackage_StreamSelection_StreamOrder")
        }
        {
            $v = "ORIGINAL","VIDEO_BITRATE_ASCENDING","VIDEO_BITRATE_DESCENDING"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$EMP_map = @{
    "CmafPackage_StreamSelection_StreamOrder"=@("New-EMPOriginEndpoint","Update-EMPOriginEndpoint")
}

_awsArgumentCompleterRegistration $EMP_Completers $EMP_map


# Argument completions for service AWS Elemental MediaStore Data Plane
$EMSD_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.MediaStoreData.StorageClass
        "Write-EMSDObject/StorageClass"
        {
            $v = "TEMPORAL"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$EMSD_map = @{
    "StorageClass"=@("Write-EMSDObject")
}

_awsArgumentCompleterRegistration $EMSD_Completers $EMSD_map


# Argument completions for service AWS Migration Hub
$MH_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.MigrationHub.ApplicationStatus
        "Send-MHApplicationStateNotification/Status"
        {
            $v = "COMPLETED","IN_PROGRESS","NOT_STARTED"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$MH_map = @{
    "Status"=@("Send-MHApplicationStateNotification")
}

_awsArgumentCompleterRegistration $MH_Completers $MH_map


# Argument completions for service Amazon Machine Learning
$ML_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.MachineLearning.BatchPredictionFilterVariable
        "Get-MLBatchPredictionList/FilterVariable"
        {
            $v = "CreatedAt","DataSourceId","DataURI","IAMUser","LastUpdatedAt","MLModelId","Name","Status"
            break
        }
        
        # Amazon.MachineLearning.DataSourceFilterVariable
        "Get-MLDataSourceList/FilterVariable"
        {
            $v = "CreatedAt","DataLocationS3","IAMUser","LastUpdatedAt","Name","Status"
            break
        }
        
        # Amazon.MachineLearning.EvaluationFilterVariable
        "Get-MLEvaluationList/FilterVariable"
        {
            $v = "CreatedAt","DataSourceId","DataURI","IAMUser","LastUpdatedAt","MLModelId","Name","Status"
            break
        }
        
        # Amazon.MachineLearning.MLModelFilterVariable
        "Get-MLModelList/FilterVariable"
        {
            $v = "Algorithm","CreatedAt","IAMUser","LastUpdatedAt","MLModelType","Name","RealtimeEndpointStatus","Status","TrainingDataSourceId","TrainingDataURI"
            break
        }
        
        # Amazon.MachineLearning.MLModelType
        "New-MLModel/MLModelType"
        {
            $v = "BINARY","MULTICLASS","REGRESSION"
            break
        }
        
        # Amazon.MachineLearning.SortOrder
        {
            ($_ -eq "Get-MLBatchPredictionList/SortOrder") -Or
            ($_ -eq "Get-MLDataSourceList/SortOrder") -Or
            ($_ -eq "Get-MLEvaluationList/SortOrder") -Or
            ($_ -eq "Get-MLModelList/SortOrder")
        }
        {
            $v = "asc","dsc"
            break
        }
        
        # Amazon.MachineLearning.TaggableResourceType
        {
            ($_ -eq "Add-MLResourceTag/ResourceType") -Or
            ($_ -eq "Get-MLResourceTag/ResourceType") -Or
            ($_ -eq "Remove-MLResourceTag/ResourceType")
        }
        {
            $v = "BatchPrediction","DataSource","Evaluation","MLModel"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$ML_map = @{
    "FilterVariable"=@("Get-MLBatchPredictionList","Get-MLDataSourceList","Get-MLEvaluationList","Get-MLModelList")
    "MLModelType"=@("New-MLModel")
    "ResourceType"=@("Add-MLResourceTag","Get-MLResourceTag","Remove-MLResourceTag")
    "SortOrder"=@("Get-MLBatchPredictionList","Get-MLDataSourceList","Get-MLEvaluationList","Get-MLModelList")
}

_awsArgumentCompleterRegistration $ML_Completers $ML_map


# Argument completions for service Amazon MQ
$MQ_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.MQ.DeploymentMode
        "New-MQBroker/DeploymentMode"
        {
            $v = "ACTIVE_STANDBY_MULTI_AZ","SINGLE_INSTANCE"
            break
        }
        
        # Amazon.MQ.EngineType
        {
            ($_ -eq "New-MQBroker/EngineType") -Or
            ($_ -eq "New-MQConfiguration/EngineType")
        }
        {
            $v = "ACTIVEMQ"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$MQ_map = @{
    "DeploymentMode"=@("New-MQBroker")
    "EngineType"=@("New-MQBroker","New-MQConfiguration")
}

_awsArgumentCompleterRegistration $MQ_Completers $MQ_map


# Argument completions for service Amazon MTurk Service
$MTR_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.MTurk.EventType
        "Send-MTRTestEventNotification/TestEventType"
        {
            $v = "AssignmentAbandoned","AssignmentAccepted","AssignmentApproved","AssignmentRejected","AssignmentReturned","AssignmentSubmitted","HITCreated","HITDisposed","HITExpired","HITExtended","HITReviewable","Ping"
            break
        }
        
        # Amazon.MTurk.NotificationTransport
        {
            ($_ -eq "Send-MTRTestEventNotification/Notification_Transport") -Or
            ($_ -eq "Update-MTRNotificationSetting/Notification_Transport")
        }
        {
            $v = "Email","SNS","SQS"
            break
        }
        
        # Amazon.MTurk.QualificationStatus
        "Get-MTRWorkersWithQualificationType/Status"
        {
            $v = "Granted","Revoked"
            break
        }
        
        # Amazon.MTurk.QualificationTypeStatus
        {
            ($_ -eq "New-MTRQualificationType/QualificationTypeStatus") -Or
            ($_ -eq "Update-MTRQualificationType/QualificationTypeStatus")
        }
        {
            $v = "Active","Inactive"
            break
        }
        
        # Amazon.MTurk.ReviewableHITStatus
        "Get-MTRReviewableHITList/Status"
        {
            $v = "Reviewable","Reviewing"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$MTR_map = @{
    "Notification_Transport"=@("Send-MTRTestEventNotification","Update-MTRNotificationSetting")
    "QualificationTypeStatus"=@("New-MTRQualificationType","Update-MTRQualificationType")
    "Status"=@("Get-MTRReviewableHITList","Get-MTRWorkersWithQualificationType")
    "TestEventType"=@("Send-MTRTestEventNotification")
}

_awsArgumentCompleterRegistration $MTR_Completers $MTR_map


# Argument completions for service AWS OpsWorks
$OPS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.OpsWorks.AppType
        {
            ($_ -eq "New-OPSApp/Type") -Or
            ($_ -eq "Update-OPSApp/Type")
        }
        {
            $v = "aws-flow-ruby","java","nodejs","other","php","rails","static"
            break
        }
        
        # Amazon.OpsWorks.Architecture
        {
            ($_ -eq "New-OPSInstance/Architecture") -Or
            ($_ -eq "Update-OPSInstance/Architecture")
        }
        {
            $v = "i386","x86_64"
            break
        }
        
        # Amazon.OpsWorks.AutoScalingType
        {
            ($_ -eq "New-OPSInstance/AutoScalingType") -Or
            ($_ -eq "Update-OPSInstance/AutoScalingType")
        }
        {
            $v = "load","timer"
            break
        }
        
        # Amazon.OpsWorks.DeploymentCommandName
        "New-OPSDeployment/Command_Name"
        {
            $v = "configure","deploy","execute_recipes","install_dependencies","restart","rollback","setup","start","stop","undeploy","update_custom_cookbooks","update_dependencies"
            break
        }
        
        # Amazon.OpsWorks.LayerType
        "New-OPSLayer/Type"
        {
            $v = "aws-flow-ruby","custom","db-master","ecs-cluster","java-app","lb","memcached","monitoring-master","nodejs-app","php-app","rails-app","web"
            break
        }
        
        # Amazon.OpsWorks.RootDeviceType
        {
            ($_ -eq "Copy-OPSStack/DefaultRootDeviceType") -Or
            ($_ -eq "New-OPSStack/DefaultRootDeviceType") -Or
            ($_ -eq "Update-OPSStack/DefaultRootDeviceType") -Or
            ($_ -eq "New-OPSInstance/RootDeviceType")
        }
        {
            $v = "ebs","instance-store"
            break
        }
        
        # Amazon.OpsWorks.SourceType
        {
            ($_ -eq "New-OPSApp/AppSource_Type") -Or
            ($_ -eq "Update-OPSApp/AppSource_Type") -Or
            ($_ -eq "Copy-OPSStack/CustomCookbooksSource_Type") -Or
            ($_ -eq "New-OPSStack/CustomCookbooksSource_Type") -Or
            ($_ -eq "Update-OPSStack/CustomCookbooksSource_Type")
        }
        {
            $v = "archive","git","s3","svn"
            break
        }
        
        # Amazon.OpsWorks.VirtualizationType
        "New-OPSInstance/VirtualizationType"
        {
            $v = "hvm","paravirtual"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$OPS_map = @{
    "AppSource_Type"=@("New-OPSApp","Update-OPSApp")
    "Architecture"=@("New-OPSInstance","Update-OPSInstance")
    "AutoScalingType"=@("New-OPSInstance","Update-OPSInstance")
    "Command_Name"=@("New-OPSDeployment")
    "CustomCookbooksSource_Type"=@("Copy-OPSStack","New-OPSStack","Update-OPSStack")
    "DefaultRootDeviceType"=@("Copy-OPSStack","New-OPSStack","Update-OPSStack")
    "RootDeviceType"=@("New-OPSInstance")
    "Type"=@("New-OPSApp","New-OPSLayer","Update-OPSApp")
    "VirtualizationType"=@("New-OPSInstance")
}

_awsArgumentCompleterRegistration $OPS_Completers $OPS_map


# Argument completions for service AWS Organizations
$ORG_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Organizations.ActionType
        {
            ($_ -eq "Get-ORGAccountHandshakeList/Filter_ActionType") -Or
            ($_ -eq "Get-ORGOrganizationHandshakeList/Filter_ActionType")
        }
        {
            $v = "ADD_ORGANIZATIONS_SERVICE_LINKED_ROLE","APPROVE_ALL_FEATURES","ENABLE_ALL_FEATURES","INVITE"
            break
        }
        
        # Amazon.Organizations.ChildType
        "Get-ORGChild/ChildType"
        {
            $v = "ACCOUNT","ORGANIZATIONAL_UNIT"
            break
        }
        
        # Amazon.Organizations.HandshakePartyType
        "New-ORGAccountInvitation/Target_Type"
        {
            $v = "ACCOUNT","EMAIL","ORGANIZATION"
            break
        }
        
        # Amazon.Organizations.IAMUserAccessToBilling
        "New-ORGAccount/IamUserAccessToBilling"
        {
            $v = "ALLOW","DENY"
            break
        }
        
        # Amazon.Organizations.OrganizationFeatureSet
        "New-ORGOrganization/FeatureSet"
        {
            $v = "ALL","CONSOLIDATED_BILLING"
            break
        }
        
        # Amazon.Organizations.PolicyType
        {
            ($_ -eq "Get-ORGPolicyForTarget/Filter") -Or
            ($_ -eq "Get-ORGPolicyList/Filter") -Or
            ($_ -eq "Disable-ORGPolicyType/PolicyType") -Or
            ($_ -eq "Enable-ORGPolicyType/PolicyType") -Or
            ($_ -eq "New-ORGPolicy/Type")
        }
        {
            $v = "SERVICE_CONTROL_POLICY"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$ORG_map = @{
    "ChildType"=@("Get-ORGChild")
    "FeatureSet"=@("New-ORGOrganization")
    "Filter"=@("Get-ORGPolicyForTarget","Get-ORGPolicyList")
    "Filter_ActionType"=@("Get-ORGAccountHandshakeList","Get-ORGOrganizationHandshakeList")
    "IamUserAccessToBilling"=@("New-ORGAccount")
    "PolicyType"=@("Disable-ORGPolicyType","Enable-ORGPolicyType")
    "Target_Type"=@("New-ORGAccountInvitation")
    "Type"=@("New-ORGPolicy")
}

_awsArgumentCompleterRegistration $ORG_Completers $ORG_map


# Argument completions for service Amazon Pinpoint
$PIN_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Pinpoint.Action
        {
            ($_ -eq "Send-PINMessage/MessageRequest_MessageConfiguration_ADMMessage_Action") -Or
            ($_ -eq "Send-PINMessage/MessageRequest_MessageConfiguration_APNSMessage_Action") -Or
            ($_ -eq "Send-PINMessage/MessageRequest_MessageConfiguration_BaiduMessage_Action") -Or
            ($_ -eq "Send-PINMessage/MessageRequest_MessageConfiguration_DefaultPushNotificationMessage_Action") -Or
            ($_ -eq "Send-PINMessage/MessageRequest_MessageConfiguration_GCMMessage_Action") -Or
            ($_ -eq "Send-PINUserMessageBatch/SendUsersMessageRequest_MessageConfiguration_ADMMessage_Action") -Or
            ($_ -eq "Send-PINUserMessageBatch/SendUsersMessageRequest_MessageConfiguration_APNSMessage_Action") -Or
            ($_ -eq "Send-PINUserMessageBatch/SendUsersMessageRequest_MessageConfiguration_BaiduMessage_Action") -Or
            ($_ -eq "Send-PINUserMessageBatch/SendUsersMessageRequest_MessageConfiguration_DefaultPushNotificationMessage_Action") -Or
            ($_ -eq "Send-PINUserMessageBatch/SendUsersMessageRequest_MessageConfiguration_GCMMessage_Action") -Or
            ($_ -eq "New-PINCampaign/WriteCampaignRequest_MessageConfiguration_ADMMessage_Action") -Or
            ($_ -eq "Update-PINCampaign/WriteCampaignRequest_MessageConfiguration_ADMMessage_Action") -Or
            ($_ -eq "New-PINCampaign/WriteCampaignRequest_MessageConfiguration_APNSMessage_Action") -Or
            ($_ -eq "Update-PINCampaign/WriteCampaignRequest_MessageConfiguration_APNSMessage_Action") -Or
            ($_ -eq "New-PINCampaign/WriteCampaignRequest_MessageConfiguration_BaiduMessage_Action") -Or
            ($_ -eq "Update-PINCampaign/WriteCampaignRequest_MessageConfiguration_BaiduMessage_Action") -Or
            ($_ -eq "New-PINCampaign/WriteCampaignRequest_MessageConfiguration_DefaultMessage_Action") -Or
            ($_ -eq "Update-PINCampaign/WriteCampaignRequest_MessageConfiguration_DefaultMessage_Action") -Or
            ($_ -eq "New-PINCampaign/WriteCampaignRequest_MessageConfiguration_GCMMessage_Action") -Or
            ($_ -eq "Update-PINCampaign/WriteCampaignRequest_MessageConfiguration_GCMMessage_Action")
        }
        {
            $v = "DEEP_LINK","OPEN_APP","URL"
            break
        }
        
        # Amazon.Pinpoint.ChannelType
        "Update-PINEndpoint/EndpointRequest_ChannelType"
        {
            $v = "ADM","APNS","APNS_SANDBOX","APNS_VOIP","APNS_VOIP_SANDBOX","BAIDU","CUSTOM","EMAIL","GCM","SMS"
            break
        }
        
        # Amazon.Pinpoint.DimensionType
        {
            ($_ -eq "New-PINSegment/WriteSegmentRequest_Dimensions_Demographic_AppVersion_DimensionType") -Or
            ($_ -eq "Update-PINSegment/WriteSegmentRequest_Dimensions_Demographic_AppVersion_DimensionType") -Or
            ($_ -eq "New-PINSegment/WriteSegmentRequest_Dimensions_Demographic_Channel_DimensionType") -Or
            ($_ -eq "Update-PINSegment/WriteSegmentRequest_Dimensions_Demographic_Channel_DimensionType") -Or
            ($_ -eq "New-PINSegment/WriteSegmentRequest_Dimensions_Demographic_DeviceType_DimensionType") -Or
            ($_ -eq "Update-PINSegment/WriteSegmentRequest_Dimensions_Demographic_DeviceType_DimensionType") -Or
            ($_ -eq "New-PINSegment/WriteSegmentRequest_Dimensions_Demographic_Make_DimensionType") -Or
            ($_ -eq "Update-PINSegment/WriteSegmentRequest_Dimensions_Demographic_Make_DimensionType") -Or
            ($_ -eq "New-PINSegment/WriteSegmentRequest_Dimensions_Demographic_Model_DimensionType") -Or
            ($_ -eq "Update-PINSegment/WriteSegmentRequest_Dimensions_Demographic_Model_DimensionType") -Or
            ($_ -eq "New-PINSegment/WriteSegmentRequest_Dimensions_Demographic_Platform_DimensionType") -Or
            ($_ -eq "Update-PINSegment/WriteSegmentRequest_Dimensions_Demographic_Platform_DimensionType") -Or
            ($_ -eq "New-PINSegment/WriteSegmentRequest_Dimensions_Location_Country_DimensionType") -Or
            ($_ -eq "Update-PINSegment/WriteSegmentRequest_Dimensions_Location_Country_DimensionType")
        }
        {
            $v = "EXCLUSIVE","INCLUSIVE"
            break
        }
        
        # Amazon.Pinpoint.Duration
        {
            ($_ -eq "New-PINSegment/WriteSegmentRequest_Dimensions_Behavior_Recency_Duration") -Or
            ($_ -eq "Update-PINSegment/WriteSegmentRequest_Dimensions_Behavior_Recency_Duration")
        }
        {
            $v = "DAY_14","DAY_30","DAY_7","HR_24"
            break
        }
        
        # Amazon.Pinpoint.Format
        "New-PINImportJob/ImportJobRequest_Format"
        {
            $v = "CSV","JSON"
            break
        }
        
        # Amazon.Pinpoint.Frequency
        {
            ($_ -eq "New-PINCampaign/WriteCampaignRequest_Schedule_Frequency") -Or
            ($_ -eq "Update-PINCampaign/WriteCampaignRequest_Schedule_Frequency")
        }
        {
            $v = "DAILY","HOURLY","MONTHLY","ONCE","WEEKLY"
            break
        }
        
        # Amazon.Pinpoint.Include
        {
            ($_ -eq "New-PINSegment/WriteSegmentRequest_SegmentGroups_Include") -Or
            ($_ -eq "Update-PINSegment/WriteSegmentRequest_SegmentGroups_Include")
        }
        {
            $v = "ALL","ANY","NONE"
            break
        }
        
        # Amazon.Pinpoint.MessageType
        {
            ($_ -eq "Send-PINMessage/MessageRequest_MessageConfiguration_SMSMessage_MessageType") -Or
            ($_ -eq "Send-PINUserMessageBatch/SendUsersMessageRequest_MessageConfiguration_SMSMessage_MessageType") -Or
            ($_ -eq "New-PINCampaign/WriteCampaignRequest_MessageConfiguration_SMSMessage_MessageType") -Or
            ($_ -eq "Update-PINCampaign/WriteCampaignRequest_MessageConfiguration_SMSMessage_MessageType")
        }
        {
            $v = "PROMOTIONAL","TRANSACTIONAL"
            break
        }
        
        # Amazon.Pinpoint.Mode
        {
            ($_ -eq "Update-PINApplicationSettingList/WriteApplicationSettingsRequest_CampaignHook_Mode") -Or
            ($_ -eq "New-PINCampaign/WriteCampaignRequest_Hook_Mode") -Or
            ($_ -eq "Update-PINCampaign/WriteCampaignRequest_Hook_Mode")
        }
        {
            $v = "DELIVERY","FILTER"
            break
        }
        
        # Amazon.Pinpoint.RecencyType
        {
            ($_ -eq "New-PINSegment/WriteSegmentRequest_Dimensions_Behavior_Recency_RecencyType") -Or
            ($_ -eq "Update-PINSegment/WriteSegmentRequest_Dimensions_Behavior_Recency_RecencyType")
        }
        {
            $v = "ACTIVE","INACTIVE"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$PIN_map = @{
    "EndpointRequest_ChannelType"=@("Update-PINEndpoint")
    "ImportJobRequest_Format"=@("New-PINImportJob")
    "MessageRequest_MessageConfiguration_ADMMessage_Action"=@("Send-PINMessage")
    "MessageRequest_MessageConfiguration_APNSMessage_Action"=@("Send-PINMessage")
    "MessageRequest_MessageConfiguration_BaiduMessage_Action"=@("Send-PINMessage")
    "MessageRequest_MessageConfiguration_DefaultPushNotificationMessage_Action"=@("Send-PINMessage")
    "MessageRequest_MessageConfiguration_GCMMessage_Action"=@("Send-PINMessage")
    "MessageRequest_MessageConfiguration_SMSMessage_MessageType"=@("Send-PINMessage")
    "SendUsersMessageRequest_MessageConfiguration_ADMMessage_Action"=@("Send-PINUserMessageBatch")
    "SendUsersMessageRequest_MessageConfiguration_APNSMessage_Action"=@("Send-PINUserMessageBatch")
    "SendUsersMessageRequest_MessageConfiguration_BaiduMessage_Action"=@("Send-PINUserMessageBatch")
    "SendUsersMessageRequest_MessageConfiguration_DefaultPushNotificationMessage_Action"=@("Send-PINUserMessageBatch")
    "SendUsersMessageRequest_MessageConfiguration_GCMMessage_Action"=@("Send-PINUserMessageBatch")
    "SendUsersMessageRequest_MessageConfiguration_SMSMessage_MessageType"=@("Send-PINUserMessageBatch")
    "WriteApplicationSettingsRequest_CampaignHook_Mode"=@("Update-PINApplicationSettingList")
    "WriteCampaignRequest_Hook_Mode"=@("New-PINCampaign","Update-PINCampaign")
    "WriteCampaignRequest_MessageConfiguration_ADMMessage_Action"=@("New-PINCampaign","Update-PINCampaign")
    "WriteCampaignRequest_MessageConfiguration_APNSMessage_Action"=@("New-PINCampaign","Update-PINCampaign")
    "WriteCampaignRequest_MessageConfiguration_BaiduMessage_Action"=@("New-PINCampaign","Update-PINCampaign")
    "WriteCampaignRequest_MessageConfiguration_DefaultMessage_Action"=@("New-PINCampaign","Update-PINCampaign")
    "WriteCampaignRequest_MessageConfiguration_GCMMessage_Action"=@("New-PINCampaign","Update-PINCampaign")
    "WriteCampaignRequest_MessageConfiguration_SMSMessage_MessageType"=@("New-PINCampaign","Update-PINCampaign")
    "WriteCampaignRequest_Schedule_Frequency"=@("New-PINCampaign","Update-PINCampaign")
    "WriteSegmentRequest_Dimensions_Behavior_Recency_Duration"=@("New-PINSegment","Update-PINSegment")
    "WriteSegmentRequest_Dimensions_Behavior_Recency_RecencyType"=@("New-PINSegment","Update-PINSegment")
    "WriteSegmentRequest_Dimensions_Demographic_AppVersion_DimensionType"=@("New-PINSegment","Update-PINSegment")
    "WriteSegmentRequest_Dimensions_Demographic_Channel_DimensionType"=@("New-PINSegment","Update-PINSegment")
    "WriteSegmentRequest_Dimensions_Demographic_DeviceType_DimensionType"=@("New-PINSegment","Update-PINSegment")
    "WriteSegmentRequest_Dimensions_Demographic_Make_DimensionType"=@("New-PINSegment","Update-PINSegment")
    "WriteSegmentRequest_Dimensions_Demographic_Model_DimensionType"=@("New-PINSegment","Update-PINSegment")
    "WriteSegmentRequest_Dimensions_Demographic_Platform_DimensionType"=@("New-PINSegment","Update-PINSegment")
    "WriteSegmentRequest_Dimensions_Location_Country_DimensionType"=@("New-PINSegment","Update-PINSegment")
    "WriteSegmentRequest_SegmentGroups_Include"=@("New-PINSegment","Update-PINSegment")
}

_awsArgumentCompleterRegistration $PIN_Completers $PIN_map


# Argument completions for service Amazon Polly
$POL_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Polly.LanguageCode
        "Get-POLVoice/LanguageCode"
        {
            $v = "cy-GB","da-DK","de-DE","en-AU","en-GB","en-GB-WLS","en-IN","en-US","es-ES","es-US","fr-CA","fr-FR","is-IS","it-IT","ja-JP","ko-KR","nb-NO","nl-NL","pl-PL","pt-BR","pt-PT","ro-RO","ru-RU","sv-SE","tr-TR"
            break
        }
        
        # Amazon.Polly.OutputFormat
        "Get-POLSpeech/OutputFormat"
        {
            $v = "json","mp3","ogg_vorbis","pcm"
            break
        }
        
        # Amazon.Polly.TextType
        "Get-POLSpeech/TextType"
        {
            $v = "ssml","text"
            break
        }
        
        # Amazon.Polly.VoiceId
        "Get-POLSpeech/VoiceId"
        {
            $v = "Aditi","Amy","Astrid","Brian","Carla","Carmen","Celine","Chantal","Conchita","Cristiano","Dora","Emma","Enrique","Ewa","Filiz","Geraint","Giorgio","Gwyneth","Hans","Ines","Ivy","Jacek","Jan","Joanna","Joey","Justin","Karl","Kendra","Kimberly","Lea","Liv","Lotte","Mads","Maja","Marlene","Mathieu","Matthew","Maxim","Miguel","Mizuki","Naja","Nicole","Penelope","Raveena","Ricardo","Ruben","Russell","Salli","Seoyeon","Takumi","Tatyana","Vicki","Vitoria"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$POL_map = @{
    "LanguageCode"=@("Get-POLVoice")
    "OutputFormat"=@("Get-POLSpeech")
    "TextType"=@("Get-POLSpeech")
    "VoiceId"=@("Get-POLSpeech")
}

_awsArgumentCompleterRegistration $POL_Completers $POL_map


# Argument completions for service Amazon Relational Database Service
$RDS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.RDS.SourceType
        "Get-RDSEvent/SourceType"
        {
            $v = "db-cluster","db-cluster-snapshot","db-instance","db-parameter-group","db-security-group","db-snapshot"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$RDS_map = @{
    "SourceType"=@("Get-RDSEvent")
}

_awsArgumentCompleterRegistration $RDS_Completers $RDS_map


# Argument completions for service Amazon Redshift
$RS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Redshift.SourceType
        "Get-RSEvent/SourceType"
        {
            $v = "cluster","cluster-parameter-group","cluster-security-group","cluster-snapshot"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$RS_map = @{
    "SourceType"=@("Get-RSEvent")
}

_awsArgumentCompleterRegistration $RS_Completers $RS_map


# Argument completions for service Amazon Rekognition
$REK_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Rekognition.CelebrityRecognitionSortBy
        "Get-REKCelebrityRecognition/SortBy"
        {
            $v = "ID","TIMESTAMP"
            break
        }
        
        # Amazon.Rekognition.ContentModerationSortBy
        "Get-REKContentModeration/SortBy"
        {
            $v = "NAME","TIMESTAMP"
            break
        }
        
        # Amazon.Rekognition.FaceAttributes
        "Start-REKFaceDetection/FaceAttributes"
        {
            $v = "ALL","DEFAULT"
            break
        }
        
        # Amazon.Rekognition.FaceSearchSortBy
        "Get-REKFaceSearch/SortBy"
        {
            $v = "INDEX","TIMESTAMP"
            break
        }
        
        # Amazon.Rekognition.LabelDetectionSortBy
        "Get-REKLabelDetection/SortBy"
        {
            $v = "NAME","TIMESTAMP"
            break
        }
        
        # Amazon.Rekognition.PersonTrackingSortBy
        "Get-REKPersonTracking/SortBy"
        {
            $v = "INDEX","TIMESTAMP"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$REK_map = @{
    "FaceAttributes"=@("Start-REKFaceDetection")
    "SortBy"=@("Get-REKCelebrityRecognition","Get-REKContentModeration","Get-REKFaceSearch","Get-REKLabelDetection","Get-REKPersonTracking")
}

_awsArgumentCompleterRegistration $REK_Completers $REK_map


# Argument completions for service Amazon Route 53
$R53_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Route53.AccountLimitType
        "Get-R53AccountLimit/Type"
        {
            $v = "MAX_HEALTH_CHECKS_BY_OWNER","MAX_HOSTED_ZONES_BY_OWNER","MAX_REUSABLE_DELEGATION_SETS_BY_OWNER","MAX_TRAFFIC_POLICIES_BY_OWNER","MAX_TRAFFIC_POLICY_INSTANCES_BY_OWNER"
            break
        }
        
        # Amazon.Route53.CloudWatchRegion
        {
            ($_ -eq "Update-R53HealthCheck/AlarmIdentifier_Region") -Or
            ($_ -eq "New-R53HealthCheck/HealthCheckConfig_AlarmIdentifier_Region")
        }
        {
            $v = "ap-northeast-1","ap-northeast-2","ap-northeast-3","ap-south-1","ap-southeast-1","ap-southeast-2","ca-central-1","eu-central-1","eu-west-1","eu-west-2","eu-west-3","sa-east-1","us-east-1","us-east-2","us-west-1","us-west-2"
            break
        }
        
        # Amazon.Route53.HealthCheckType
        "New-R53HealthCheck/HealthCheckConfig_Type"
        {
            $v = "CALCULATED","CLOUDWATCH_METRIC","HTTP","HTTPS","HTTPS_STR_MATCH","HTTP_STR_MATCH","TCP"
            break
        }
        
        # Amazon.Route53.HostedZoneLimitType
        "Get-R53HostedZoneLimit/Type"
        {
            $v = "MAX_RRSETS_BY_ZONE","MAX_VPCS_ASSOCIATED_BY_ZONE"
            break
        }
        
        # Amazon.Route53.InsufficientDataHealthStatus
        {
            ($_ -eq "New-R53HealthCheck/HealthCheckConfig_InsufficientDataHealthStatus") -Or
            ($_ -eq "Update-R53HealthCheck/InsufficientDataHealthStatus")
        }
        {
            $v = "Healthy","LastKnownStatus","Unhealthy"
            break
        }
        
        # Amazon.Route53.ReusableDelegationSetLimitType
        "Get-R53ReusableDelegationSetLimit/Type"
        {
            $v = "MAX_ZONES_BY_REUSABLE_DELEGATION_SET"
            break
        }
        
        # Amazon.Route53.RRType
        {
            ($_ -eq "Test-R53DNSAnswer/RecordType") -Or
            ($_ -eq "Get-R53ResourceRecordSet/StartRecordType") -Or
            ($_ -eq "Get-R53TrafficPolicyInstanceList/TrafficPolicyInstanceTypeMarker") -Or
            ($_ -eq "Get-R53TrafficPolicyInstancesByHostedZone/TrafficPolicyInstanceTypeMarker") -Or
            ($_ -eq "Get-R53TrafficPolicyInstancesByPolicy/TrafficPolicyInstanceTypeMarker")
        }
        {
            $v = "A","AAAA","CAA","CNAME","MX","NAPTR","NS","PTR","SOA","SPF","SRV","TXT"
            break
        }
        
        # Amazon.Route53.TagResourceType
        {
            ($_ -eq "Edit-R53TagsForResource/ResourceType") -Or
            ($_ -eq "Get-R53TagsForResource/ResourceType") -Or
            ($_ -eq "Get-R53TagsForResourceList/ResourceType")
        }
        {
            $v = "healthcheck","hostedzone"
            break
        }
        
        # Amazon.Route53.VPCRegion
        {
            ($_ -eq "New-R53HostedZone/VPC_VPCRegion") -Or
            ($_ -eq "New-R53VPCAssociationAuthorization/VPC_VPCRegion") -Or
            ($_ -eq "Register-R53VPCWithHostedZone/VPC_VPCRegion") -Or
            ($_ -eq "Remove-R53VPCAssociationAuthorization/VPC_VPCRegion") -Or
            ($_ -eq "Unregister-R53VPCFromHostedZone/VPC_VPCRegion")
        }
        {
            $v = "ap-northeast-1","ap-northeast-2","ap-northeast-3","ap-south-1","ap-southeast-1","ap-southeast-2","ca-central-1","cn-north-1","eu-central-1","eu-west-1","eu-west-2","eu-west-3","sa-east-1","us-east-1","us-east-2","us-west-1","us-west-2"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$R53_map = @{
    "AlarmIdentifier_Region"=@("Update-R53HealthCheck")
    "HealthCheckConfig_AlarmIdentifier_Region"=@("New-R53HealthCheck")
    "HealthCheckConfig_InsufficientDataHealthStatus"=@("New-R53HealthCheck")
    "HealthCheckConfig_Type"=@("New-R53HealthCheck")
    "InsufficientDataHealthStatus"=@("Update-R53HealthCheck")
    "RecordType"=@("Test-R53DNSAnswer")
    "ResourceType"=@("Edit-R53TagsForResource","Get-R53TagsForResource","Get-R53TagsForResourceList")
    "StartRecordType"=@("Get-R53ResourceRecordSet")
    "TrafficPolicyInstanceTypeMarker"=@("Get-R53TrafficPolicyInstanceList","Get-R53TrafficPolicyInstancesByHostedZone","Get-R53TrafficPolicyInstancesByPolicy")
    "Type"=@("Get-R53AccountLimit","Get-R53HostedZoneLimit","Get-R53ReusableDelegationSetLimit")
    "VPC_VPCRegion"=@("New-R53HostedZone","New-R53VPCAssociationAuthorization","Register-R53VPCWithHostedZone","Remove-R53VPCAssociationAuthorization","Unregister-R53VPCFromHostedZone")
}

_awsArgumentCompleterRegistration $R53_Completers $R53_map


# Argument completions for service Amazon Route 53 Domains
$R53D_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Route53Domains.ContactType
        {
            ($_ -eq "Invoke-R53DDomainTransfer/AdminContact_ContactType") -Or
            ($_ -eq "Register-R53DDomain/AdminContact_ContactType") -Or
            ($_ -eq "Update-R53DDomainContact/AdminContact_ContactType") -Or
            ($_ -eq "Invoke-R53DDomainTransfer/RegistrantContact_ContactType") -Or
            ($_ -eq "Register-R53DDomain/RegistrantContact_ContactType") -Or
            ($_ -eq "Update-R53DDomainContact/RegistrantContact_ContactType") -Or
            ($_ -eq "Invoke-R53DDomainTransfer/TechContact_ContactType") -Or
            ($_ -eq "Register-R53DDomain/TechContact_ContactType") -Or
            ($_ -eq "Update-R53DDomainContact/TechContact_ContactType")
        }
        {
            $v = "ASSOCIATION","COMPANY","PERSON","PUBLIC_BODY","RESELLER"
            break
        }
        
        # Amazon.Route53Domains.CountryCode
        {
            ($_ -eq "Invoke-R53DDomainTransfer/AdminContact_CountryCode") -Or
            ($_ -eq "Register-R53DDomain/AdminContact_CountryCode") -Or
            ($_ -eq "Update-R53DDomainContact/AdminContact_CountryCode") -Or
            ($_ -eq "Invoke-R53DDomainTransfer/RegistrantContact_CountryCode") -Or
            ($_ -eq "Register-R53DDomain/RegistrantContact_CountryCode") -Or
            ($_ -eq "Update-R53DDomainContact/RegistrantContact_CountryCode") -Or
            ($_ -eq "Invoke-R53DDomainTransfer/TechContact_CountryCode") -Or
            ($_ -eq "Register-R53DDomain/TechContact_CountryCode") -Or
            ($_ -eq "Update-R53DDomainContact/TechContact_CountryCode")
        }
        {
            $v = "AD","AE","AF","AG","AI","AL","AM","AN","AO","AQ","AR","AS","AT","AU","AW","AZ","BA","BB","BD","BE","BF","BG","BH","BI","BJ","BL","BM","BN","BO","BR","BS","BT","BW","BY","BZ","CA","CC","CD","CF","CG","CH","CI","CK","CL","CM","CN","CO","CR","CU","CV","CX","CY","CZ","DE","DJ","DK","DM","DO","DZ","EC","EE","EG","ER","ES","ET","FI","FJ","FK","FM","FO","FR","GA","GB","GD","GE","GH","GI","GL","GM","GN","GQ","GR","GT","GU","GW","GY","HK","HN","HR","HT","HU","ID","IE","IL","IM","IN","IQ","IR","IS","IT","JM","JO","JP","KE","KG","KH","KI","KM","KN","KP","KR","KW","KY","KZ","LA","LB","LC","LI","LK","LR","LS","LT","LU","LV","LY","MA","MC","MD","ME","MF","MG","MH","MK","ML","MM","MN","MO","MP","MR","MS","MT","MU","MV","MW","MX","MY","MZ","NA","NC","NE","NG","NI","NL","NO","NP","NR","NU","NZ","OM","PA","PE","PF","PG","PH","PK","PL","PM","PN","PR","PT","PW","PY","QA","RO","RS","RU","RW","SA","SB","SC","SD","SE","SG","SH","SI","SK","SL","SM","SN","SO","SR","ST","SV","SY","SZ","TC","TD","TG","TH","TJ","TK","TL","TM","TN","TO","TR","TT","TV","TW","TZ","UA","UG","US","UY","UZ","VA","VC","VE","VG","VI","VN","VU","WF","WS","YE","YT","ZA","ZM","ZW"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$R53D_map = @{
    "AdminContact_ContactType"=@("Invoke-R53DDomainTransfer","Register-R53DDomain","Update-R53DDomainContact")
    "AdminContact_CountryCode"=@("Invoke-R53DDomainTransfer","Register-R53DDomain","Update-R53DDomainContact")
    "RegistrantContact_ContactType"=@("Invoke-R53DDomainTransfer","Register-R53DDomain","Update-R53DDomainContact")
    "RegistrantContact_CountryCode"=@("Invoke-R53DDomainTransfer","Register-R53DDomain","Update-R53DDomainContact")
    "TechContact_ContactType"=@("Invoke-R53DDomainTransfer","Register-R53DDomain","Update-R53DDomainContact")
    "TechContact_CountryCode"=@("Invoke-R53DDomainTransfer","Register-R53DDomain","Update-R53DDomainContact")
}

_awsArgumentCompleterRegistration $R53D_Completers $R53D_map


# Argument completions for service Amazon Simple Storage Service
$S3_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.S3.BucketAccelerateStatus
        "Write-S3BucketAccelerateConfiguration/AccelerateConfiguration_Status"
        {
            $v = "Enabled","Suspended"
            break
        }
        
        # Amazon.S3.EncodingType
        {
            ($_ -eq "Get-S3Object/Encoding") -Or
            ($_ -eq "Get-S3Version/Encoding")
        }
        {
            $v = "Url"
            break
        }
        
        # Amazon.S3.GlacierJobTier
        {
            ($_ -eq "Restore-S3Object/RetrievalTier") -Or
            ($_ -eq "Restore-S3Object/Tier")
        }
        {
            $v = "Bulk","Expedited","Standard"
            break
        }
        
        # Amazon.S3.InventoryFormat
        "Write-S3BucketInventoryConfiguration/InventoryConfiguration_Destination_S3BucketDestination_InventoryFormat"
        {
            $v = "CSV","ORC"
            break
        }
        
        # Amazon.S3.InventoryFrequency
        "Write-S3BucketInventoryConfiguration/InventoryConfiguration_Schedule_Frequency"
        {
            $v = "Daily","Weekly"
            break
        }
        
        # Amazon.S3.InventoryIncludedObjectVersions
        "Write-S3BucketInventoryConfiguration/InventoryConfiguration_IncludedObjectVersions"
        {
            $v = "All","Current"
            break
        }
        
        # Amazon.S3.RequestPayer
        {
            ($_ -eq "Get-S3ObjectMetadata/RequestPayer") -Or
            ($_ -eq "Restore-S3Object/RequestPayer")
        }
        {
            $v = "requester"
            break
        }
        
        # Amazon.S3.RestoreRequestType
        "Restore-S3Object/RestoreRequestType"
        {
            $v = "SELECT"
            break
        }
        
        # Amazon.S3.S3CannedACL
        {
            ($_ -eq "Copy-S3Object/CannedACLName") -Or
            ($_ -eq "New-S3Bucket/CannedACLName") -Or
            ($_ -eq "Set-S3ACL/CannedACLName") -Or
            ($_ -eq "Write-S3Object/CannedACLName") -Or
            ($_ -eq "Restore-S3Object/OutputLocation_S3_CannedACL")
        }
        {
            $v = "authenticated-read","aws-exec-read","bucket-owner-full-control","bucket-owner-read","log-delivery-write","NoACL","private","public-read","public-read-write"
            break
        }
        
        # Amazon.S3.S3StorageClass
        "Restore-S3Object/OutputLocation_S3_StorageClass"
        {
            $v = "GLACIER","ONEZONE_IA","REDUCED_REDUNDANCY","STANDARD","STANDARD_IA"
            break
        }
        
        # Amazon.S3.ServerSideEncryptionCustomerMethod
        {
            ($_ -eq "Copy-S3Object/CopySourceServerSideEncryptionCustomerMethod") -Or
            ($_ -eq "Copy-S3Object/ServerSideEncryptionCustomerMethod") -Or
            ($_ -eq "Get-S3ObjectMetadata/ServerSideEncryptionCustomerMethod") -Or
            ($_ -eq "Get-S3PreSignedURL/ServerSideEncryptionCustomerMethod") -Or
            ($_ -eq "Read-S3Object/ServerSideEncryptionCustomerMethod") -Or
            ($_ -eq "Write-S3Object/ServerSideEncryptionCustomerMethod")
        }
        {
            $v = "","AES256"
            break
        }
        
        # Amazon.S3.ServerSideEncryptionMethod
        {
            ($_ -eq "Restore-S3Object/OutputLocation_S3_Encryption_EncryptionType") -Or
            ($_ -eq "Copy-S3Object/ServerSideEncryption") -Or
            ($_ -eq "Write-S3Object/ServerSideEncryption") -Or
            ($_ -eq "Get-S3PreSignedURL/ServerSideEncryptionMethod")
        }
        {
            $v = "","AES256","aws:kms"
            break
        }
        
        # Amazon.S3.StorageClassAnalysisSchemaVersion
        "Write-S3BucketAnalyticsConfiguration/AnalyticsConfiguration_StorageClassAnalysis_DataExport_OutputSchemaVersion"
        {
            $v = "V_1"
            break
        }
        
        # Amazon.S3.VersionStatus
        "Write-S3BucketVersioning/VersioningConfig_Status"
        {
            $v = "Enabled","Off","Suspended"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$S3_map = @{
    "AccelerateConfiguration_Status"=@("Write-S3BucketAccelerateConfiguration")
    "AnalyticsConfiguration_StorageClassAnalysis_DataExport_OutputSchemaVersion"=@("Write-S3BucketAnalyticsConfiguration")
    "CannedACLName"=@("Copy-S3Object","New-S3Bucket","Set-S3ACL","Write-S3Object")
    "CopySourceServerSideEncryptionCustomerMethod"=@("Copy-S3Object")
    "Encoding"=@("Get-S3Object","Get-S3Version")
    "InventoryConfiguration_Destination_S3BucketDestination_InventoryFormat"=@("Write-S3BucketInventoryConfiguration")
    "InventoryConfiguration_IncludedObjectVersions"=@("Write-S3BucketInventoryConfiguration")
    "InventoryConfiguration_Schedule_Frequency"=@("Write-S3BucketInventoryConfiguration")
    "OutputLocation_S3_CannedACL"=@("Restore-S3Object")
    "OutputLocation_S3_Encryption_EncryptionType"=@("Restore-S3Object")
    "OutputLocation_S3_StorageClass"=@("Restore-S3Object")
    "RequestPayer"=@("Get-S3ObjectMetadata","Restore-S3Object")
    "RestoreRequestType"=@("Restore-S3Object")
    "RetrievalTier"=@("Restore-S3Object")
    "ServerSideEncryption"=@("Copy-S3Object","Write-S3Object")
    "ServerSideEncryptionCustomerMethod"=@("Copy-S3Object","Get-S3ObjectMetadata","Get-S3PreSignedURL","Read-S3Object","Write-S3Object")
    "ServerSideEncryptionMethod"=@("Get-S3PreSignedURL")
    "Tier"=@("Restore-S3Object")
    "VersioningConfig_Status"=@("Write-S3BucketVersioning")
}

_awsArgumentCompleterRegistration $S3_Completers $S3_map


# Argument completions for service Amazon SageMaker Service
$SM_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.SageMaker.DirectInternetAccess
        "New-SMNotebookInstance/DirectInternetAccess"
        {
            $v = "Disabled","Enabled"
            break
        }
        
        # Amazon.SageMaker.EndpointConfigSortKey
        "Get-SMConfigList/SortBy"
        {
            $v = "CreationTime","Name"
            break
        }
        
        # Amazon.SageMaker.EndpointSortKey
        "Get-SMEndpointList/SortBy"
        {
            $v = "CreationTime","Name","Status"
            break
        }
        
        # Amazon.SageMaker.EndpointStatus
        "Get-SMEndpointList/StatusEquals"
        {
            $v = "Creating","Deleting","Failed","InService","OutOfService","RollingBack","Updating"
            break
        }
        
        # Amazon.SageMaker.HyperParameterTuningJobObjectiveType
        "New-SMHyperParameterTuningJob/HyperParameterTuningJobConfig_HyperParameterTuningJobObjective_Type"
        {
            $v = "Maximize","Minimize"
            break
        }
        
        # Amazon.SageMaker.HyperParameterTuningJobSortByOptions
        "Get-SMHyperParameterTuningJobList/SortBy"
        {
            $v = "CreationTime","Name","Status"
            break
        }
        
        # Amazon.SageMaker.HyperParameterTuningJobStatus
        "Get-SMHyperParameterTuningJobList/StatusEquals"
        {
            $v = "Completed","Failed","InProgress","Stopped","Stopping"
            break
        }
        
        # Amazon.SageMaker.HyperParameterTuningJobStrategyType
        "New-SMHyperParameterTuningJob/HyperParameterTuningJobConfig_Strategy"
        {
            $v = "Bayesian"
            break
        }
        
        # Amazon.SageMaker.InstanceType
        {
            ($_ -eq "New-SMNotebookInstance/InstanceType") -Or
            ($_ -eq "Update-SMNotebookInstance/InstanceType")
        }
        {
            $v = "ml.m4.10xlarge","ml.m4.16xlarge","ml.m4.2xlarge","ml.m4.4xlarge","ml.m4.xlarge","ml.p2.16xlarge","ml.p2.8xlarge","ml.p2.xlarge","ml.p3.16xlarge","ml.p3.2xlarge","ml.p3.8xlarge","ml.t2.2xlarge","ml.t2.large","ml.t2.medium","ml.t2.xlarge"
            break
        }
        
        # Amazon.SageMaker.ModelSortKey
        "Get-SMModelList/SortBy"
        {
            $v = "CreationTime","Name"
            break
        }
        
        # Amazon.SageMaker.NotebookInstanceLifecycleConfigSortKey
        "Get-SMNotebookInstanceLifecycleConfigList/SortBy"
        {
            $v = "CreationTime","LastModifiedTime","Name"
            break
        }
        
        # Amazon.SageMaker.NotebookInstanceLifecycleConfigSortOrder
        "Get-SMNotebookInstanceLifecycleConfigList/SortOrder"
        {
            $v = "Ascending","Descending"
            break
        }
        
        # Amazon.SageMaker.NotebookInstanceSortKey
        "Get-SMNotebookInstanceList/SortBy"
        {
            $v = "CreationTime","Name","Status"
            break
        }
        
        # Amazon.SageMaker.NotebookInstanceSortOrder
        "Get-SMNotebookInstanceList/SortOrder"
        {
            $v = "Ascending","Descending"
            break
        }
        
        # Amazon.SageMaker.NotebookInstanceStatus
        "Get-SMNotebookInstanceList/StatusEquals"
        {
            $v = "Deleting","Failed","InService","Pending","Stopped","Stopping","Updating"
            break
        }
        
        # Amazon.SageMaker.OrderKey
        {
            ($_ -eq "Get-SMConfigList/SortOrder") -Or
            ($_ -eq "Get-SMEndpointList/SortOrder") -Or
            ($_ -eq "Get-SMModelList/SortOrder")
        }
        {
            $v = "Ascending","Descending"
            break
        }
        
        # Amazon.SageMaker.SortBy
        "Get-SMTrainingJobList/SortBy"
        {
            $v = "CreationTime","Name","Status"
            break
        }
        
        # Amazon.SageMaker.SortOrder
        {
            ($_ -eq "Get-SMHyperParameterTuningJobList/SortOrder") -Or
            ($_ -eq "Get-SMTrainingJobList/SortOrder") -Or
            ($_ -eq "Get-SMTrainingJobsForHyperParameterTuningJobList/SortOrder")
        }
        {
            $v = "Ascending","Descending"
            break
        }
        
        # Amazon.SageMaker.TrainingInputMode
        "New-SMHyperParameterTuningJob/TrainingJobDefinition_AlgorithmSpecification_TrainingInputMode"
        {
            $v = "File","Pipe"
            break
        }
        
        # Amazon.SageMaker.TrainingJobSortByOptions
        "Get-SMTrainingJobsForHyperParameterTuningJobList/SortBy"
        {
            $v = "CreationTime","FinalObjectiveMetricValue","Name","Status"
            break
        }
        
        # Amazon.SageMaker.TrainingJobStatus
        {
            ($_ -eq "Get-SMTrainingJobList/StatusEquals") -Or
            ($_ -eq "Get-SMTrainingJobsForHyperParameterTuningJobList/StatusEquals")
        }
        {
            $v = "Completed","Failed","InProgress","Stopped","Stopping"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$SM_map = @{
    "DirectInternetAccess"=@("New-SMNotebookInstance")
    "HyperParameterTuningJobConfig_HyperParameterTuningJobObjective_Type"=@("New-SMHyperParameterTuningJob")
    "HyperParameterTuningJobConfig_Strategy"=@("New-SMHyperParameterTuningJob")
    "InstanceType"=@("New-SMNotebookInstance","Update-SMNotebookInstance")
    "SortBy"=@("Get-SMConfigList","Get-SMEndpointList","Get-SMHyperParameterTuningJobList","Get-SMModelList","Get-SMNotebookInstanceLifecycleConfigList","Get-SMNotebookInstanceList","Get-SMTrainingJobList","Get-SMTrainingJobsForHyperParameterTuningJobList")
    "SortOrder"=@("Get-SMConfigList","Get-SMEndpointList","Get-SMHyperParameterTuningJobList","Get-SMModelList","Get-SMNotebookInstanceLifecycleConfigList","Get-SMNotebookInstanceList","Get-SMTrainingJobList","Get-SMTrainingJobsForHyperParameterTuningJobList")
    "StatusEquals"=@("Get-SMEndpointList","Get-SMHyperParameterTuningJobList","Get-SMNotebookInstanceList","Get-SMTrainingJobList","Get-SMTrainingJobsForHyperParameterTuningJobList")
    "TrainingJobDefinition_AlgorithmSpecification_TrainingInputMode"=@("New-SMHyperParameterTuningJob")
}

_awsArgumentCompleterRegistration $SM_Completers $SM_map


# Argument completions for service AWS Service Catalog
$SC_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.ServiceCatalog.AccessLevelFilterKey
        {
            ($_ -eq "Find-SCProvisionedProduct/AccessLevelFilter_Key") -Or
            ($_ -eq "Get-SCProvisionedProduct/AccessLevelFilter_Key") -Or
            ($_ -eq "Get-SCProvisionedProductPlanList/AccessLevelFilter_Key") -Or
            ($_ -eq "Get-SCRecordHistory/AccessLevelFilter_Key")
        }
        {
            $v = "Account","Role","User"
            break
        }
        
        # Amazon.ServiceCatalog.PortfolioShareType
        "Get-SCAcceptedPortfolioSharesList/PortfolioShareType"
        {
            $v = "AWS_SERVICECATALOG","IMPORTED"
            break
        }
        
        # Amazon.ServiceCatalog.PrincipalType
        "Register-SCPrincipalWithPortfolio/PrincipalType"
        {
            $v = "IAM"
            break
        }
        
        # Amazon.ServiceCatalog.ProductSource
        "Find-SCProductsAsAdmin/ProductSource"
        {
            $v = "ACCOUNT"
            break
        }
        
        # Amazon.ServiceCatalog.ProductType
        "New-SCProduct/ProductType"
        {
            $v = "CLOUD_FORMATION_TEMPLATE","MARKETPLACE"
            break
        }
        
        # Amazon.ServiceCatalog.ProductViewSortBy
        {
            ($_ -eq "Find-SCProduct/SortBy") -Or
            ($_ -eq "Find-SCProductsAsAdmin/SortBy")
        }
        {
            $v = "CreationDate","Title","VersionCount"
            break
        }
        
        # Amazon.ServiceCatalog.ProvisionedProductPlanType
        "New-SCProvisionedProductPlan/PlanType"
        {
            $v = "CLOUDFORMATION"
            break
        }
        
        # Amazon.ServiceCatalog.ProvisioningArtifactType
        {
            ($_ -eq "New-SCProvisioningArtifact/Parameters_Type") -Or
            ($_ -eq "New-SCProduct/ProvisioningArtifactParameters_Type")
        }
        {
            $v = "CLOUD_FORMATION_TEMPLATE","MARKETPLACE_AMI","MARKETPLACE_CAR"
            break
        }
        
        # Amazon.ServiceCatalog.SortOrder
        {
            ($_ -eq "Find-SCProduct/SortOrder") -Or
            ($_ -eq "Find-SCProductsAsAdmin/SortOrder") -Or
            ($_ -eq "Find-SCProvisionedProduct/SortOrder")
        }
        {
            $v = "ASCENDING","DESCENDING"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$SC_map = @{
    "AccessLevelFilter_Key"=@("Find-SCProvisionedProduct","Get-SCProvisionedProduct","Get-SCProvisionedProductPlanList","Get-SCRecordHistory")
    "Parameters_Type"=@("New-SCProvisioningArtifact")
    "PlanType"=@("New-SCProvisionedProductPlan")
    "PortfolioShareType"=@("Get-SCAcceptedPortfolioSharesList")
    "PrincipalType"=@("Register-SCPrincipalWithPortfolio")
    "ProductSource"=@("Find-SCProductsAsAdmin")
    "ProductType"=@("New-SCProduct")
    "ProvisioningArtifactParameters_Type"=@("New-SCProduct")
    "SortBy"=@("Find-SCProduct","Find-SCProductsAsAdmin")
    "SortOrder"=@("Find-SCProduct","Find-SCProductsAsAdmin","Find-SCProvisionedProduct")
}

_awsArgumentCompleterRegistration $SC_Completers $SC_map


# Argument completions for service Amazon Route 53 Auto Naming
$SD_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.ServiceDiscovery.CustomHealthStatus
        "Update-SDInstanceCustomHealthStatus/Status"
        {
            $v = "HEALTHY","UNHEALTHY"
            break
        }
        
        # Amazon.ServiceDiscovery.HealthCheckType
        "New-SDService/HealthCheckConfig_Type"
        {
            $v = "HTTP","HTTPS","TCP"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$SD_map = @{
    "HealthCheckConfig_Type"=@("New-SDService")
    "Status"=@("Update-SDInstanceCustomHealthStatus")
}

_awsArgumentCompleterRegistration $SD_Completers $SD_map


# Argument completions for service Amazon Simple Email Service
$SES_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.SimpleEmail.BehaviorOnMXFailure
        "Set-SESIdentityMailFromDomain/BehaviorOnMXFailure"
        {
            $v = "RejectMessage","UseDefaultValue"
            break
        }
        
        # Amazon.SimpleEmail.IdentityType
        "Get-SESIdentity/IdentityType"
        {
            $v = "Domain","EmailAddress"
            break
        }
        
        # Amazon.SimpleEmail.NotificationType
        {
            ($_ -eq "Set-SESIdentityHeadersInNotificationsEnabled/NotificationType") -Or
            ($_ -eq "Set-SESIdentityNotificationTopic/NotificationType")
        }
        {
            $v = "Bounce","Complaint","Delivery"
            break
        }
        
        # Amazon.SimpleEmail.ReceiptFilterPolicy
        "New-SESReceiptFilter/Filter_IpFilter_Policy"
        {
            $v = "Allow","Block"
            break
        }
        
        # Amazon.SimpleEmail.TlsPolicy
        {
            ($_ -eq "New-SESReceiptRule/Rule_TlsPolicy") -Or
            ($_ -eq "Update-SESReceiptRule/Rule_TlsPolicy")
        }
        {
            $v = "Optional","Require"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$SES_map = @{
    "BehaviorOnMXFailure"=@("Set-SESIdentityMailFromDomain")
    "Filter_IpFilter_Policy"=@("New-SESReceiptFilter")
    "IdentityType"=@("Get-SESIdentity")
    "NotificationType"=@("Set-SESIdentityHeadersInNotificationsEnabled","Set-SESIdentityNotificationTopic")
    "Rule_TlsPolicy"=@("New-SESReceiptRule","Update-SESReceiptRule")
}

_awsArgumentCompleterRegistration $SES_Completers $SES_map


# Argument completions for service AWS Step Functions
$SFN_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.StepFunctions.ExecutionStatus
        "Get-SFNExecutionList/StatusFilter"
        {
            $v = "ABORTED","FAILED","RUNNING","SUCCEEDED","TIMED_OUT"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$SFN_map = @{
    "StatusFilter"=@("Get-SFNExecutionList")
}

_awsArgumentCompleterRegistration $SFN_Completers $SFN_map


# Argument completions for service AWS Shield
$SHLD_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Shield.AutoRenew
        "Update-SHLDSubscription/AutoRenew"
        {
            $v = "DISABLED","ENABLED"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$SHLD_map = @{
    "AutoRenew"=@("Update-SHLDSubscription")
}

_awsArgumentCompleterRegistration $SHLD_Completers $SHLD_map


# Argument completions for service Amazon Server Migration Service
$SMS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.ServerMigrationService.LicenseType
        {
            ($_ -eq "New-SMSReplicationJob/LicenseType") -Or
            ($_ -eq "Update-SMSReplicationJob/LicenseType")
        }
        {
            $v = "AWS","BYOL"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$SMS_map = @{
    "LicenseType"=@("New-SMSReplicationJob","Update-SMSReplicationJob")
}

_awsArgumentCompleterRegistration $SMS_Completers $SMS_map


# Argument completions for service AWS Import/Export Snowball
$SNOW_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.Snowball.JobType
        {
            ($_ -eq "New-SNOWCluster/JobType") -Or
            ($_ -eq "New-SNOWJob/JobType")
        }
        {
            $v = "EXPORT","IMPORT","LOCAL_USE"
            break
        }
        
        # Amazon.Snowball.ShippingOption
        {
            ($_ -eq "New-SNOWCluster/ShippingOption") -Or
            ($_ -eq "New-SNOWJob/ShippingOption") -Or
            ($_ -eq "Update-SNOWCluster/ShippingOption") -Or
            ($_ -eq "Update-SNOWJob/ShippingOption")
        }
        {
            $v = "EXPRESS","NEXT_DAY","SECOND_DAY","STANDARD"
            break
        }
        
        # Amazon.Snowball.SnowballCapacity
        {
            ($_ -eq "New-SNOWJob/SnowballCapacityPreference") -Or
            ($_ -eq "Update-SNOWJob/SnowballCapacityPreference")
        }
        {
            $v = "NoPreference","T100","T50","T80"
            break
        }
        
        # Amazon.Snowball.SnowballType
        {
            ($_ -eq "New-SNOWCluster/SnowballType") -Or
            ($_ -eq "New-SNOWJob/SnowballType")
        }
        {
            $v = "EDGE","STANDARD"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$SNOW_map = @{
    "JobType"=@("New-SNOWCluster","New-SNOWJob")
    "ShippingOption"=@("New-SNOWCluster","New-SNOWJob","Update-SNOWCluster","Update-SNOWJob")
    "SnowballCapacityPreference"=@("New-SNOWJob","Update-SNOWJob")
    "SnowballType"=@("New-SNOWCluster","New-SNOWJob")
}

_awsArgumentCompleterRegistration $SNOW_Completers $SNOW_map


# Argument completions for service AWS Systems Manager
$SSM_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.SimpleSystemsManagement.AssociationStatusName
        "Update-SSMAssociationStatus/AssociationStatus_Name"
        {
            $v = "Failed","Pending","Success"
            break
        }
        
        # Amazon.SimpleSystemsManagement.DocumentFormat
        {
            ($_ -eq "Get-SSMDocument/DocumentFormat") -Or
            ($_ -eq "New-SSMDocument/DocumentFormat") -Or
            ($_ -eq "Update-SSMDocument/DocumentFormat")
        }
        {
            $v = "JSON","YAML"
            break
        }
        
        # Amazon.SimpleSystemsManagement.DocumentHashType
        {
            ($_ -eq "Send-SSMCommand/DocumentHashType") -Or
            ($_ -eq "Register-SSMTaskWithMaintenanceWindow/TaskInvocationParameters_RunCommand_DocumentHashType") -Or
            ($_ -eq "Update-SSMMaintenanceWindowTask/TaskInvocationParameters_RunCommand_DocumentHashType")
        }
        {
            $v = "Sha1","Sha256"
            break
        }
        
        # Amazon.SimpleSystemsManagement.DocumentPermissionType
        {
            ($_ -eq "Edit-SSMDocumentPermission/PermissionType") -Or
            ($_ -eq "Get-SSMDocumentPermission/PermissionType")
        }
        {
            $v = "Share"
            break
        }
        
        # Amazon.SimpleSystemsManagement.DocumentType
        "New-SSMDocument/DocumentType"
        {
            $v = "Automation","Command","Policy"
            break
        }
        
        # Amazon.SimpleSystemsManagement.ExecutionMode
        "Start-SSMAutomationExecution/Mode"
        {
            $v = "Auto","Interactive"
            break
        }
        
        # Amazon.SimpleSystemsManagement.InventorySchemaDeleteOption
        "Remove-SSMInventory/SchemaDeleteOption"
        {
            $v = "DeleteSchema","DisableSchema"
            break
        }
        
        # Amazon.SimpleSystemsManagement.MaintenanceWindowResourceType
        "Register-SSMTargetWithMaintenanceWindow/ResourceType"
        {
            $v = "INSTANCE"
            break
        }
        
        # Amazon.SimpleSystemsManagement.MaintenanceWindowTaskType
        "Register-SSMTaskWithMaintenanceWindow/TaskType"
        {
            $v = "AUTOMATION","LAMBDA","RUN_COMMAND","STEP_FUNCTIONS"
            break
        }
        
        # Amazon.SimpleSystemsManagement.NotificationType
        {
            ($_ -eq "Send-SSMCommand/NotificationConfig_NotificationType") -Or
            ($_ -eq "Register-SSMTaskWithMaintenanceWindow/TaskInvocationParameters_RunCommand_NotificationConfig_NotificationType") -Or
            ($_ -eq "Update-SSMMaintenanceWindowTask/TaskInvocationParameters_RunCommand_NotificationConfig_NotificationType")
        }
        {
            $v = "Command","Invocation"
            break
        }
        
        # Amazon.SimpleSystemsManagement.OperatingSystem
        {
            ($_ -eq "Get-SSMDefaultPatchBaseline/OperatingSystem") -Or
            ($_ -eq "Get-SSMPatchBaselineForPatchGroup/OperatingSystem") -Or
            ($_ -eq "New-SSMPatchBaseline/OperatingSystem")
        }
        {
            $v = "AMAZON_LINUX","AMAZON_LINUX_2","CENTOS","REDHAT_ENTERPRISE_LINUX","SUSE","UBUNTU","WINDOWS"
            break
        }
        
        # Amazon.SimpleSystemsManagement.ParameterType
        "Write-SSMParameter/Type"
        {
            $v = "SecureString","String","StringList"
            break
        }
        
        # Amazon.SimpleSystemsManagement.PatchComplianceLevel
        {
            ($_ -eq "New-SSMPatchBaseline/ApprovedPatchesComplianceLevel") -Or
            ($_ -eq "Update-SSMPatchBaseline/ApprovedPatchesComplianceLevel")
        }
        {
            $v = "CRITICAL","HIGH","INFORMATIONAL","LOW","MEDIUM","UNSPECIFIED"
            break
        }
        
        # Amazon.SimpleSystemsManagement.ResourceDataSyncS3Format
        "New-SSMResourceDataSync/S3Destination_SyncFormat"
        {
            $v = "JsonSerDe"
            break
        }
        
        # Amazon.SimpleSystemsManagement.ResourceTypeForTagging
        {
            ($_ -eq "Add-SSMResourceTag/ResourceType") -Or
            ($_ -eq "Get-SSMResourceTag/ResourceType") -Or
            ($_ -eq "Remove-SSMResourceTag/ResourceType")
        }
        {
            $v = "Document","MaintenanceWindow","ManagedInstance","Parameter","PatchBaseline"
            break
        }
        
        # Amazon.SimpleSystemsManagement.SignalType
        "Send-SSMAutomationSignal/SignalType"
        {
            $v = "Approve","Reject","Resume","StartStep","StopStep"
            break
        }
        
        # Amazon.SimpleSystemsManagement.StopType
        "Stop-SSMAutomationExecution/Type"
        {
            $v = "Cancel","Complete"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$SSM_map = @{
    "ApprovedPatchesComplianceLevel"=@("New-SSMPatchBaseline","Update-SSMPatchBaseline")
    "AssociationStatus_Name"=@("Update-SSMAssociationStatus")
    "DocumentFormat"=@("Get-SSMDocument","New-SSMDocument","Update-SSMDocument")
    "DocumentHashType"=@("Send-SSMCommand")
    "DocumentType"=@("New-SSMDocument")
    "Mode"=@("Start-SSMAutomationExecution")
    "NotificationConfig_NotificationType"=@("Send-SSMCommand")
    "OperatingSystem"=@("Get-SSMDefaultPatchBaseline","Get-SSMPatchBaselineForPatchGroup","New-SSMPatchBaseline")
    "PermissionType"=@("Edit-SSMDocumentPermission","Get-SSMDocumentPermission")
    "ResourceType"=@("Add-SSMResourceTag","Get-SSMResourceTag","Register-SSMTargetWithMaintenanceWindow","Remove-SSMResourceTag")
    "S3Destination_SyncFormat"=@("New-SSMResourceDataSync")
    "SchemaDeleteOption"=@("Remove-SSMInventory")
    "SignalType"=@("Send-SSMAutomationSignal")
    "TaskInvocationParameters_RunCommand_DocumentHashType"=@("Register-SSMTaskWithMaintenanceWindow","Update-SSMMaintenanceWindowTask")
    "TaskInvocationParameters_RunCommand_NotificationConfig_NotificationType"=@("Register-SSMTaskWithMaintenanceWindow","Update-SSMMaintenanceWindowTask")
    "TaskType"=@("Register-SSMTaskWithMaintenanceWindow")
    "Type"=@("Stop-SSMAutomationExecution","Write-SSMParameter")
}

_awsArgumentCompleterRegistration $SSM_Completers $SSM_map


# Argument completions for service AWS Storage Gateway
$SG_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.StorageGateway.ObjectACL
        {
            ($_ -eq "New-SGNFSFileShare/ObjectACL") -Or
            ($_ -eq "New-SGSMBFileShare/ObjectACL") -Or
            ($_ -eq "Update-SGNFSFileShare/ObjectACL") -Or
            ($_ -eq "Update-SGSMBFileShare/ObjectACL")
        }
        {
            $v = "authenticated-read","aws-exec-read","bucket-owner-full-control","bucket-owner-read","private","public-read","public-read-write"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$SG_map = @{
    "ObjectACL"=@("New-SGNFSFileShare","New-SGSMBFileShare","Update-SGNFSFileShare","Update-SGSMBFileShare")
}

_awsArgumentCompleterRegistration $SG_Completers $SG_map


# Argument completions for service Amazon Transcribe Service
$TRS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.TranscribeService.LanguageCode
        {
            ($_ -eq "New-TRSVocabulary/LanguageCode") -Or
            ($_ -eq "Start-TRSTranscriptionJob/LanguageCode") -Or
            ($_ -eq "Update-TRSVocabulary/LanguageCode")
        }
        {
            $v = "en-US","es-US"
            break
        }
        
        # Amazon.TranscribeService.MediaFormat
        "Start-TRSTranscriptionJob/MediaFormat"
        {
            $v = "flac","mp3","mp4","wav"
            break
        }
        
        # Amazon.TranscribeService.TranscriptionJobStatus
        "Get-TRSTranscriptionJobList/Status"
        {
            $v = "COMPLETED","FAILED","IN_PROGRESS"
            break
        }
        
        # Amazon.TranscribeService.VocabularyState
        "Get-TRSVocabularyList/StateEquals"
        {
            $v = "FAILED","PENDING","READY"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$TRS_map = @{
    "LanguageCode"=@("New-TRSVocabulary","Start-TRSTranscriptionJob","Update-TRSVocabulary")
    "MediaFormat"=@("Start-TRSTranscriptionJob")
    "StateEquals"=@("Get-TRSVocabularyList")
    "Status"=@("Get-TRSTranscriptionJobList")
}

_awsArgumentCompleterRegistration $TRS_Completers $TRS_map


# Argument completions for service AWS WAF
$WAF_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.WAF.RateKey
        "New-WAFRateBasedRule/RateKey"
        {
            $v = "IP"
            break
        }
        
        # Amazon.WAF.WafActionType
        {
            ($_ -eq "New-WAFWebACL/DefaultAction_Type") -Or
            ($_ -eq "Update-WAFWebACL/DefaultAction_Type")
        }
        {
            $v = "ALLOW","BLOCK","COUNT"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$WAF_map = @{
    "DefaultAction_Type"=@("New-WAFWebACL","Update-WAFWebACL")
    "RateKey"=@("New-WAFRateBasedRule")
}

_awsArgumentCompleterRegistration $WAF_Completers $WAF_map


# Argument completions for service AWS WAF Regional
$WAFR_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.WAFRegional.RateKey
        "New-WAFRRateBasedRule/RateKey"
        {
            $v = "IP"
            break
        }
        
        # Amazon.WAFRegional.WafActionType
        {
            ($_ -eq "New-WAFRWebACL/DefaultAction_Type") -Or
            ($_ -eq "Update-WAFRWebACL/DefaultAction_Type")
        }
        {
            $v = "ALLOW","BLOCK","COUNT"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$WAFR_map = @{
    "DefaultAction_Type"=@("New-WAFRWebACL","Update-WAFRWebACL")
    "RateKey"=@("New-WAFRRateBasedRule")
}

_awsArgumentCompleterRegistration $WAFR_Completers $WAFR_map


# Argument completions for service Amazon WorkDocs
$WD_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.WorkDocs.BooleanEnumType
        "Update-WDUser/GrantPoweruserPrivileges"
        {
            $v = "FALSE","TRUE"
            break
        }
        
        # Amazon.WorkDocs.CommentVisibilityType
        "New-WDComment/Visibility"
        {
            $v = "PRIVATE","PUBLIC"
            break
        }
        
        # Amazon.WorkDocs.DocumentVersionStatus
        "Update-WDDocumentVersion/VersionStatus"
        {
            $v = "ACTIVE"
            break
        }
        
        # Amazon.WorkDocs.FolderContentType
        "Get-WDFolderContent/Type"
        {
            $v = "ALL","DOCUMENT","FOLDER"
            break
        }
        
        # Amazon.WorkDocs.LocaleType
        "Update-WDUser/Locale"
        {
            $v = "de","default","en","es","fr","ja","ko","pt_BR","ru","zh_CN","zh_TW"
            break
        }
        
        # Amazon.WorkDocs.OrderType
        {
            ($_ -eq "Get-WDFolderContent/Order") -Or
            ($_ -eq "Get-WDUserList/Order")
        }
        {
            $v = "ASCENDING","DESCENDING"
            break
        }
        
        # Amazon.WorkDocs.PrincipalType
        "Remove-WDResourcePermission/PrincipalType"
        {
            $v = "ANONYMOUS","GROUP","INVITE","ORGANIZATION","USER"
            break
        }
        
        # Amazon.WorkDocs.ResourceSortType
        "Get-WDFolderContent/Sort"
        {
            $v = "DATE","NAME"
            break
        }
        
        # Amazon.WorkDocs.ResourceStateType
        {
            ($_ -eq "Update-WDDocument/ResourceState") -Or
            ($_ -eq "Update-WDFolder/ResourceState")
        }
        {
            $v = "ACTIVE","RECYCLED","RECYCLING","RESTORING"
            break
        }
        
        # Amazon.WorkDocs.StorageType
        {
            ($_ -eq "New-WDUser/StorageRule_StorageType") -Or
            ($_ -eq "Update-WDUser/StorageRule_StorageType")
        }
        {
            $v = "QUOTA","UNLIMITED"
            break
        }
        
        # Amazon.WorkDocs.SubscriptionProtocolType
        "New-WDNotificationSubscription/Protocol"
        {
            $v = "HTTPS"
            break
        }
        
        # Amazon.WorkDocs.SubscriptionType
        "New-WDNotificationSubscription/SubscriptionType"
        {
            $v = "ALL"
            break
        }
        
        # Amazon.WorkDocs.UserFilterType
        "Get-WDUserList/Include"
        {
            $v = "ACTIVE_PENDING","ALL"
            break
        }
        
        # Amazon.WorkDocs.UserSortType
        "Get-WDUserList/Sort"
        {
            $v = "FULL_NAME","STORAGE_LIMIT","STORAGE_USED","USER_NAME","USER_STATUS"
            break
        }
        
        # Amazon.WorkDocs.UserType
        "Update-WDUser/Type"
        {
            $v = "ADMIN","MINIMALUSER","POWERUSER","USER","WORKSPACESUSER"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$WD_map = @{
    "GrantPoweruserPrivileges"=@("Update-WDUser")
    "Include"=@("Get-WDUserList")
    "Locale"=@("Update-WDUser")
    "Order"=@("Get-WDFolderContent","Get-WDUserList")
    "PrincipalType"=@("Remove-WDResourcePermission")
    "Protocol"=@("New-WDNotificationSubscription")
    "ResourceState"=@("Update-WDDocument","Update-WDFolder")
    "Sort"=@("Get-WDFolderContent","Get-WDUserList")
    "StorageRule_StorageType"=@("New-WDUser","Update-WDUser")
    "SubscriptionType"=@("New-WDNotificationSubscription")
    "Type"=@("Get-WDFolderContent","Update-WDUser")
    "VersionStatus"=@("Update-WDDocumentVersion")
    "Visibility"=@("New-WDComment")
}

_awsArgumentCompleterRegistration $WD_Completers $WD_map


# Argument completions for service Amazon WorkSpaces
$WKS_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.WorkSpaces.Compute
        "Edit-WKSWorkspaceProperty/WorkspaceProperties_ComputeTypeName"
        {
            $v = "GRAPHICS","PERFORMANCE","POWER","STANDARD","VALUE"
            break
        }
        
        # Amazon.WorkSpaces.RunningMode
        "Edit-WKSWorkspaceProperty/WorkspaceProperties_RunningMode"
        {
            $v = "ALWAYS_ON","AUTO_STOP"
            break
        }
        
        # Amazon.WorkSpaces.TargetWorkspaceState
        "Edit-WKSWorkspaceState/WorkspaceState"
        {
            $v = "ADMIN_MAINTENANCE","AVAILABLE"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$WKS_map = @{
    "WorkspaceProperties_ComputeTypeName"=@("Edit-WKSWorkspaceProperty")
    "WorkspaceProperties_RunningMode"=@("Edit-WKSWorkspaceProperty")
    "WorkspaceState"=@("Edit-WKSWorkspaceState")
}

_awsArgumentCompleterRegistration $WKS_Completers $WKS_map


# Argument completions for service Amazon WorkMail
$WM_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.WorkMail.ResourceType
        "New-WMResource/Type"
        {
            $v = "EQUIPMENT","ROOM"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$WM_map = @{
    "Type"=@("New-WMResource")
}

_awsArgumentCompleterRegistration $WM_Completers $WM_map


# Argument completions for service AWS X-Ray
$XR_Completers = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    
    switch ($("$commandName/$parameterName"))
    {
        # Amazon.XRay.EncryptionType
        "Write-XREncryptionConfig/Type"
        {
            $v = "KMS","NONE"
            break
        }
        
    }
    
    $v |
        Where-Object { $_ -like "$wordToComplete*" } |
        ForEach-Object { New-Object System.Management.Automation.CompletionResult $_, $_, 'ParameterValue', $_ }
}

$XR_map = @{
    "Type"=@("Write-XREncryptionConfig")
}

_awsArgumentCompleterRegistration $XR_Completers $XR_map

# end auto-generated service completers

# SIG # Begin signature block
# MIIcUAYJKoZIhvcNAQcCoIIcQTCCHD0CAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCCyWXGk9Pc7QZ8R
# uQi23isH08c3uuDjklJTfJF+qMe0bKCCF0cwggS5MIIDoaADAgECAhArd4OFAE9M
# ppHAfzWQwHt/MA0GCSqGSIb3DQEBCwUAMIGEMQswCQYDVQQGEwJVUzEdMBsGA1UE
# ChMUU3ltYW50ZWMgQ29ycG9yYXRpb24xHzAdBgNVBAsTFlN5bWFudGVjIFRydXN0
# IE5ldHdvcmsxNTAzBgNVBAMTLFN5bWFudGVjIENsYXNzIDMgU0hBMjU2IENvZGUg
# U2lnbmluZyBDQSAtIEcyMB4XDTE3MDcxMDAwMDAwMFoXDTIwMDcxMDIzNTk1OVow
# ajELMAkGA1UEBhMCVVMxEzARBgNVBAgMCldhc2hpbmd0b24xEDAOBgNVBAcMB1Nl
# YXR0bGUxGTAXBgNVBAoMEEFtYXpvbi5jb20sIEluYy4xGTAXBgNVBAMMEEFtYXpv
# bi5jb20sIEluYy4wggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQC18TJW
# m2/aGpvb+TP2CZMg49X/5bjDAgPxWlAs7bqDyZnRYJork4rLmejpeJu+2BhRjZeK
# OirT4XveddBsdL1/TR+FKp8BXEsrm0wcR4yT6mNHJ9yCgC1YBNG91bZ75kRIT+46
# chbC7eNI5703wi8ejxe2KvvOERppBTaFStVJuAHab69dvFma8qE3s7wbqPdQ5eTI
# +Xm0bXp8cObS+vj+hf3N2pfDNWM8ITva3kbowGoCW0rKzpf7fBGtBOKnOCCSL0yC
# AOwLlFkslemVyrT1/HTDjOTKCro016HxOPddA4cefvr2ZhGlRZQQHg7wMdG7TpZX
# ueQ6LoS9UxlzCYHFAgMBAAGjggE+MIIBOjAJBgNVHRMEAjAAMA4GA1UdDwEB/wQE
# AwIHgDATBgNVHSUEDDAKBggrBgEFBQcDAzBhBgNVHSAEWjBYMFYGBmeBDAEEATBM
# MCMGCCsGAQUFBwIBFhdodHRwczovL2Quc3ltY2IuY29tL2NwczAlBggrBgEFBQcC
# AjAZDBdodHRwczovL2Quc3ltY2IuY29tL3JwYTAfBgNVHSMEGDAWgBTUwAYiSes5
# S92T4lyhuEd2CXIDWDArBgNVHR8EJDAiMCCgHqAchhpodHRwOi8vcmIuc3ltY2Iu
# Y29tL3JiLmNybDBXBggrBgEFBQcBAQRLMEkwHwYIKwYBBQUHMAGGE2h0dHA6Ly9y
# Yi5zeW1jZC5jb20wJgYIKwYBBQUHMAKGGmh0dHA6Ly9yYi5zeW1jYi5jb20vcmIu
# Y3J0MA0GCSqGSIb3DQEBCwUAA4IBAQC+C4TcK44ph2QQK/1f65jOR23DtSBC3y9a
# bzRHdo4qxmcAW5ot69os7GJfzgVsA5lh1IT4+aMuGYA4GTcF6iTSOMgFSRwP8urI
# N2BprsWuMJsQ7+Flo3PBRN3dU6idOlFKOfuRxgIHRn47t2yRan6XTNhfiWl84DrD
# NjSTnk4c72Gzu0hiwQB9OTsf8CQP3Shb3ZzcAOmeUB01TFoJU34PfJpKlKQZeQIi
# W5WdPPr1G/0cAHgejDHtdNYcSqIWfoGeYgCxUg1IFpp1VmPlqb/de8QKONzPDK6/
# 5hulSGqGgpRmEkwGGJiQeOB51GxYZRCPq3hN3UJ6N0A+hYzj7yspMIIFRzCCBC+g
# AwIBAgIQfBs1NUrn23TnQV8RacprqDANBgkqhkiG9w0BAQsFADCBvTELMAkGA1UE
# BhMCVVMxFzAVBgNVBAoTDlZlcmlTaWduLCBJbmMuMR8wHQYDVQQLExZWZXJpU2ln
# biBUcnVzdCBOZXR3b3JrMTowOAYDVQQLEzEoYykgMjAwOCBWZXJpU2lnbiwgSW5j
# LiAtIEZvciBhdXRob3JpemVkIHVzZSBvbmx5MTgwNgYDVQQDEy9WZXJpU2lnbiBV
# bml2ZXJzYWwgUm9vdCBDZXJ0aWZpY2F0aW9uIEF1dGhvcml0eTAeFw0xNDA3MjIw
# MDAwMDBaFw0yNDA3MjEyMzU5NTlaMIGEMQswCQYDVQQGEwJVUzEdMBsGA1UEChMU
# U3ltYW50ZWMgQ29ycG9yYXRpb24xHzAdBgNVBAsTFlN5bWFudGVjIFRydXN0IE5l
# dHdvcmsxNTAzBgNVBAMTLFN5bWFudGVjIENsYXNzIDMgU0hBMjU2IENvZGUgU2ln
# bmluZyBDQSAtIEcyMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA15VD
# 1NzfZ645+1KktiYxBHDpt45bKro3aTWVj7vAMOeG2HO73+vRdj+KVo7rLUvwVxhO
# sY2lM9MLdSPVankn3aPT9w6HZbXerRzx9TW0IlGvIqHBXUuQf8BZTqudeakC1x5J
# sTtNh/7CeKu/71KunK8I2TnlmlE+aV8wEE5xY2xY4fAgMxsPdL5byxLh24zEgJRy
# u/ZFmp7BJQv7oxye2KYJcHHswEdMj33D3hnOPu4Eco4X0//wsgUyGUzTsByf/qV4
# IEJwQbAmjG8AyDoAEUF6QbCnipEEoJl49He082Aq5mxQBLcUYP8NUfSoi4T+Idpc
# Xn31KXlPsER0b21y/wIDAQABo4IBeDCCAXQwLgYIKwYBBQUHAQEEIjAgMB4GCCsG
# AQUFBzABhhJodHRwOi8vcy5zeW1jZC5jb20wEgYDVR0TAQH/BAgwBgEB/wIBADBm
# BgNVHSAEXzBdMFsGC2CGSAGG+EUBBxcDMEwwIwYIKwYBBQUHAgEWF2h0dHBzOi8v
# ZC5zeW1jYi5jb20vY3BzMCUGCCsGAQUFBwICMBkaF2h0dHBzOi8vZC5zeW1jYi5j
# b20vcnBhMDYGA1UdHwQvMC0wK6ApoCeGJWh0dHA6Ly9zLnN5bWNiLmNvbS91bml2
# ZXJzYWwtcm9vdC5jcmwwEwYDVR0lBAwwCgYIKwYBBQUHAwMwDgYDVR0PAQH/BAQD
# AgEGMCkGA1UdEQQiMCCkHjAcMRowGAYDVQQDExFTeW1hbnRlY1BLSS0xLTcyNDAd
# BgNVHQ4EFgQU1MAGIknrOUvdk+JcobhHdglyA1gwHwYDVR0jBBgwFoAUtnf6aUhH
# n1MS1cLqBzJ2B9GXBxkwDQYJKoZIhvcNAQELBQADggEBAH/ryqfqi3ZC6z6OIFQw
# 47e53PpIPhbHD0WVEM0nhqNm8wLtcfiqwlWXkXCD+VJ+Umk8yfHglEaAGLuh1KRW
# pvMdAJHVhvNIh+DLxDRoIF60y/kF7ZyvcFMnueg+flGgaXGL3FHtgDolMp9Er25D
# KNMhdbuX2IuLjP6pBEYEhfcVnEsRjcQsF/7Vbn+a4laS8ZazrS359N/aiZnOsjhE
# wPdHe8olufoqaDObUHLeqJ/UzSwLNL2LMHhA4I2OJxuQbxq+CBWBXesv4lHnUR7J
# eCnnHmW/OO8BSgEJJA4WxBR5wUE3NNA9kVKUneFo7wjw4mmcZ26QCxqTcdQmAsPA
# WiMwggZqMIIFUqADAgECAhADAZoCOv9YsWvW1ermF/BmMA0GCSqGSIb3DQEBBQUA
# MGIxCzAJBgNVBAYTAlVTMRUwEwYDVQQKEwxEaWdpQ2VydCBJbmMxGTAXBgNVBAsT
# EHd3dy5kaWdpY2VydC5jb20xITAfBgNVBAMTGERpZ2lDZXJ0IEFzc3VyZWQgSUQg
# Q0EtMTAeFw0xNDEwMjIwMDAwMDBaFw0yNDEwMjIwMDAwMDBaMEcxCzAJBgNVBAYT
# AlVTMREwDwYDVQQKEwhEaWdpQ2VydDElMCMGA1UEAxMcRGlnaUNlcnQgVGltZXN0
# YW1wIFJlc3BvbmRlcjCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAKNk
# Xfx8s+CCNeDg9sYq5kl1O8xu4FOpnx9kWeZ8a39rjJ1V+JLjntVaY1sCSVDZg85v
# Zu7dy4XpX6X51Id0iEQ7Gcnl9ZGfxhQ5rCTqqEsskYnMXij0ZLZQt/USs3OWCmej
# vmGfrvP9Enh1DqZbFP1FI46GRFV9GIYFjFWHeUhG98oOjafeTl/iqLYtWQJhiGFy
# GGi5uHzu5uc0LzF3gTAfuzYBje8n4/ea8EwxZI3j6/oZh6h+z+yMDDZbesF6uHjH
# yQYuRhDIjegEYNu8c3T6Ttj+qkDxss5wRoPp2kChWTrZFQlXmVYwk/PJYczQCMxr
# 7GJCkawCwO+k8IkRj3cCAwEAAaOCAzUwggMxMA4GA1UdDwEB/wQEAwIHgDAMBgNV
# HRMBAf8EAjAAMBYGA1UdJQEB/wQMMAoGCCsGAQUFBwMIMIIBvwYDVR0gBIIBtjCC
# AbIwggGhBglghkgBhv1sBwEwggGSMCgGCCsGAQUFBwIBFhxodHRwczovL3d3dy5k
# aWdpY2VydC5jb20vQ1BTMIIBZAYIKwYBBQUHAgIwggFWHoIBUgBBAG4AeQAgAHUA
# cwBlACAAbwBmACAAdABoAGkAcwAgAEMAZQByAHQAaQBmAGkAYwBhAHQAZQAgAGMA
# bwBuAHMAdABpAHQAdQB0AGUAcwAgAGEAYwBjAGUAcAB0AGEAbgBjAGUAIABvAGYA
# IAB0AGgAZQAgAEQAaQBnAGkAQwBlAHIAdAAgAEMAUAAvAEMAUABTACAAYQBuAGQA
# IAB0AGgAZQAgAFIAZQBsAHkAaQBuAGcAIABQAGEAcgB0AHkAIABBAGcAcgBlAGUA
# bQBlAG4AdAAgAHcAaABpAGMAaAAgAGwAaQBtAGkAdAAgAGwAaQBhAGIAaQBsAGkA
# dAB5ACAAYQBuAGQAIABhAHIAZQAgAGkAbgBjAG8AcgBwAG8AcgBhAHQAZQBkACAA
# aABlAHIAZQBpAG4AIABiAHkAIAByAGUAZgBlAHIAZQBuAGMAZQAuMAsGCWCGSAGG
# /WwDFTAfBgNVHSMEGDAWgBQVABIrE5iymQftHt+ivlcNK2cCzTAdBgNVHQ4EFgQU
# YVpNJLZJMp1KKnkag0v0HonByn0wfQYDVR0fBHYwdDA4oDagNIYyaHR0cDovL2Ny
# bDMuZGlnaWNlcnQuY29tL0RpZ2lDZXJ0QXNzdXJlZElEQ0EtMS5jcmwwOKA2oDSG
# Mmh0dHA6Ly9jcmw0LmRpZ2ljZXJ0LmNvbS9EaWdpQ2VydEFzc3VyZWRJRENBLTEu
# Y3JsMHcGCCsGAQUFBwEBBGswaTAkBggrBgEFBQcwAYYYaHR0cDovL29jc3AuZGln
# aWNlcnQuY29tMEEGCCsGAQUFBzAChjVodHRwOi8vY2FjZXJ0cy5kaWdpY2VydC5j
# b20vRGlnaUNlcnRBc3N1cmVkSURDQS0xLmNydDANBgkqhkiG9w0BAQUFAAOCAQEA
# nSV+GzNNsiaBXJuGziMgD4CH5Yj//7HUaiwx7ToXGXEXzakbvFoWOQCd42yE5FpA
# +94GAYw3+puxnSR+/iCkV61bt5qwYCbqaVchXTQvH3Gwg5QZBWs1kBCge5fH9j/n
# 4hFBpr1i2fAnPTgdKG86Ugnw7HBi02JLsOBzppLA044x2C/jbRcTBu7kA7YUq/OP
# Q6dxnSHdFMoVXZJB2vkPgdGZdA0mxA5/G7X1oPHGdwYoFenYk+VVFvC7Cqsc21xI
# J2bIo4sKHOWV2q7ELlmgYd3a822iYemKC23sEhi991VUQAOSK2vCUcIKSK+w1G7g
# 9BQKOhvjjz3Kr2qNe9zYRDCCBs0wggW1oAMCAQICEAb9+QOWA63qAArrPye7uhsw
# DQYJKoZIhvcNAQEFBQAwZTELMAkGA1UEBhMCVVMxFTATBgNVBAoTDERpZ2lDZXJ0
# IEluYzEZMBcGA1UECxMQd3d3LmRpZ2ljZXJ0LmNvbTEkMCIGA1UEAxMbRGlnaUNl
# cnQgQXNzdXJlZCBJRCBSb290IENBMB4XDTA2MTExMDAwMDAwMFoXDTIxMTExMDAw
# MDAwMFowYjELMAkGA1UEBhMCVVMxFTATBgNVBAoTDERpZ2lDZXJ0IEluYzEZMBcG
# A1UECxMQd3d3LmRpZ2ljZXJ0LmNvbTEhMB8GA1UEAxMYRGlnaUNlcnQgQXNzdXJl
# ZCBJRCBDQS0xMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA6IItmfnK
# wkKVpYBzQHDSnlZUXKnE0kEGj8kz/E1FkVyBn+0snPgWWd+etSQVwpi5tHdJ3InE
# Ctqvy15r7a2wcTHrzzpADEZNk+yLejYIA6sMNP4YSYL+x8cxSIB8HqIPkg5QycaH
# 6zY/2DDD/6b3+6LNb3Mj/qxWBZDwMiEWicZwiPkFl32jx0PdAug7Pe2xQaPtP77b
# lUjE7h6z8rwMK5nQxl0SQoHhg26Ccz8mSxSQrllmCsSNvtLOBq6thG9IhJtPQLnx
# TPKvmPv2zkBdXPao8S+v7Iki8msYZbHBc63X8djPHgp0XEK4aH631XcKJ1Z8D2Kk
# PzIUYJX9BwSiCQIDAQABo4IDejCCA3YwDgYDVR0PAQH/BAQDAgGGMDsGA1UdJQQ0
# MDIGCCsGAQUFBwMBBggrBgEFBQcDAgYIKwYBBQUHAwMGCCsGAQUFBwMEBggrBgEF
# BQcDCDCCAdIGA1UdIASCAckwggHFMIIBtAYKYIZIAYb9bAABBDCCAaQwOgYIKwYB
# BQUHAgEWLmh0dHA6Ly93d3cuZGlnaWNlcnQuY29tL3NzbC1jcHMtcmVwb3NpdG9y
# eS5odG0wggFkBggrBgEFBQcCAjCCAVYeggFSAEEAbgB5ACAAdQBzAGUAIABvAGYA
# IAB0AGgAaQBzACAAQwBlAHIAdABpAGYAaQBjAGEAdABlACAAYwBvAG4AcwB0AGkA
# dAB1AHQAZQBzACAAYQBjAGMAZQBwAHQAYQBuAGMAZQAgAG8AZgAgAHQAaABlACAA
# RABpAGcAaQBDAGUAcgB0ACAAQwBQAC8AQwBQAFMAIABhAG4AZAAgAHQAaABlACAA
# UgBlAGwAeQBpAG4AZwAgAFAAYQByAHQAeQAgAEEAZwByAGUAZQBtAGUAbgB0ACAA
# dwBoAGkAYwBoACAAbABpAG0AaQB0ACAAbABpAGEAYgBpAGwAaQB0AHkAIABhAG4A
# ZAAgAGEAcgBlACAAaQBuAGMAbwByAHAAbwByAGEAdABlAGQAIABoAGUAcgBlAGkA
# bgAgAGIAeQAgAHIAZQBmAGUAcgBlAG4AYwBlAC4wCwYJYIZIAYb9bAMVMBIGA1Ud
# EwEB/wQIMAYBAf8CAQAweQYIKwYBBQUHAQEEbTBrMCQGCCsGAQUFBzABhhhodHRw
# Oi8vb2NzcC5kaWdpY2VydC5jb20wQwYIKwYBBQUHMAKGN2h0dHA6Ly9jYWNlcnRz
# LmRpZ2ljZXJ0LmNvbS9EaWdpQ2VydEFzc3VyZWRJRFJvb3RDQS5jcnQwgYEGA1Ud
# HwR6MHgwOqA4oDaGNGh0dHA6Ly9jcmwzLmRpZ2ljZXJ0LmNvbS9EaWdpQ2VydEFz
# c3VyZWRJRFJvb3RDQS5jcmwwOqA4oDaGNGh0dHA6Ly9jcmw0LmRpZ2ljZXJ0LmNv
# bS9EaWdpQ2VydEFzc3VyZWRJRFJvb3RDQS5jcmwwHQYDVR0OBBYEFBUAEisTmLKZ
# B+0e36K+Vw0rZwLNMB8GA1UdIwQYMBaAFEXroq/0ksuCMS1Ri6enIZ3zbcgPMA0G
# CSqGSIb3DQEBBQUAA4IBAQBGUD7Jtygkpzgdtlspr1LPUukxR6tWXHvVDQtBs+/s
# dR90OPKyXGGinJXDUOSCuSPRujqGcq04eKx1XRcXNHJHhZRW0eu7NoR3zCSl8wQZ
# Vann4+erYs37iy2QwsDStZS9Xk+xBdIOPRqpFFumhjFiqKgz5Js5p8T1zh14dpQl
# c+Qqq8+cdkvtX8JLFuRLcEwAiR78xXm8TBJX/l/hHrwCXaj++wc4Tw3GXZG5D2dF
# zdaD7eeSDY2xaYxP+1ngIw/Sqq4AfO6cQg7PkdcntxbuD8O9fAqg7iwIVYUiuOsY
# Gk38KiGtSTGDR5V3cdyxG0tLHBCcdxTBnU8vWpUIKRAmMYIEXzCCBFsCAQEwgZkw
# gYQxCzAJBgNVBAYTAlVTMR0wGwYDVQQKExRTeW1hbnRlYyBDb3Jwb3JhdGlvbjEf
# MB0GA1UECxMWU3ltYW50ZWMgVHJ1c3QgTmV0d29yazE1MDMGA1UEAxMsU3ltYW50
# ZWMgQ2xhc3MgMyBTSEEyNTYgQ29kZSBTaWduaW5nIENBIC0gRzICECt3g4UAT0ym
# kcB/NZDAe38wDQYJYIZIAWUDBAIBBQCggYQwGAYKKwYBBAGCNwIBDDEKMAigAoAA
# oQKAADAZBgkqhkiG9w0BCQMxDAYKKwYBBAGCNwIBBDAcBgorBgEEAYI3AgELMQ4w
# DAYKKwYBBAGCNwIBFTAvBgkqhkiG9w0BCQQxIgQgyZNxO5Q6rPE9CxBE6rLfORJa
# MxOKyUT5HMjTDTSk3P8wDQYJKoZIhvcNAQEBBQAEggEAGmhtIGv0vRDf0IFaVbPz
# 9L0+m9Pri48HbJ4EIIVRCv6ajXQKvkh949Fg6/lyrYwQJe26RLcsn3sOPinfzOZW
# 7EOjoTyBa2GyWFBJBNSDcyA1j7B3U+bOzO4jGlPd3FMuXiRdOSASeE1809QooxPI
# w+ynQKd/wtpCpFsYdvqQTQiV5zSB2kFZorMkdqJ5CNCAGwuw+nwanyceBSk9RHna
# 5CpN7rGKI20Ca3VHLunACmv1VfMYnk0dajazUgJnZPdNWuaCeCsTnzT9aOAntvPk
# PZOOqeDVpo5vRgbgkz0O9kVG8Bp2OPVTtLkcavzpJynHK9yJ+Jog9jJQtZ/sxXiR
# AaGCAg8wggILBgkqhkiG9w0BCQYxggH8MIIB+AIBATB2MGIxCzAJBgNVBAYTAlVT
# MRUwEwYDVQQKEwxEaWdpQ2VydCBJbmMxGTAXBgNVBAsTEHd3dy5kaWdpY2VydC5j
# b20xITAfBgNVBAMTGERpZ2lDZXJ0IEFzc3VyZWQgSUQgQ0EtMQIQAwGaAjr/WLFr
# 1tXq5hfwZjAJBgUrDgMCGgUAoF0wGAYJKoZIhvcNAQkDMQsGCSqGSIb3DQEHATAc
# BgkqhkiG9w0BCQUxDxcNMTgwNzA5MTU1MjI5WjAjBgkqhkiG9w0BCQQxFgQUYMnH
# AcUTPWGIGifdx4fNCbqc5BAwDQYJKoZIhvcNAQEBBQAEggEAAm2yORj/qPnJu8Kr
# jBnGFCZ5RDbDdqtqozQ/hY011p8l97lfhjw8SLZLg0aBIZ+yLMYyshV4EZc4B6VZ
# pEZ2PgagMcxP4s5K9pYz0PXv2Rac9wub/UDZfx5UTD1UNB79YJJkUfjWqy7Vtj7k
# v8NKaH/w85GxJDzwnLazjj6Wq4hFddE6XPP5XFxPDdJsUenOBAqToPt8BtR22Q5E
# UqCXOHuoB5Rvd3tNST6/goGSBTUCra6q/msJ43MTvsZQfEr1LaEzLjll/eVy2r/F
# uYqGv9CySnTNLMq1+7sMaHVS2hS8sW4Q/oLybEyZaJ20oyOoqXRjuEEt6/zS83ud
# j3heSg==
# SIG # End signature block
