### 3.3.313.0 (2018-07-09)
  * Amazon Route 53 Auto Naming
    * Added support for the Amazon Route 53 Auto Naming service (Service Discovery). Amazon Route 53 auto naming lets you configure public or private namespaces that your microservice applications run in. When instances of the service become available, you can call the auto naming API to register the instance, and Route 53 automatically creates up to five DNS records and an optional health check. Clients that submit DNS queries for the service receive an answer that contains up to eight healthy records. Cmdlets for the service have the noun prefix SD and can be listed with the command *Get-AWSCmdletName -Service SD*.
  * Amazon CloudFront
    * [Breaking Change] The DeleteServiceLinkedRole api was released in error and has now been removed by the service. Accordingly the Remove-CFServiceLinkedRole cmdlet has been removed from the module. We apologize for any inconvenience caused.
  * Amazon EC2
    * Added parameter -CpuOption to the New-EC2Instance cmdlet to enable optimizing CPU options for your new instance(s). For more details see [Optimizing CPU Options](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/instance-optimize-cpu.html) in the Amazon EC2 User Guide.
    * Added revised examples for the Get-EC2ConsoleOutput and Grant-EC2SecurityGroupIngress cmdets based on user feedback.
  * Amazon Workpaces
    * Renamed the _-ResourceId_ parameter for the New-WKSTag and Get-WKSTag cmdlets to _-WorkspaceId_ to improve consistency with other Amazon Workspaces cmdlets. A backwards compatible alias of ResourceId has also been applied to this parameter to support existing scripts.
  * Amazon Relational Database Service
    * The name of the cmdlet _Get-RDSReservedDBInstancesOffering_, which maps to the service API _PurchaseReservedDBInstancesOffering_, has been corrected to _New-RDSReservedDBInstancesOfferingPurchase_. The Get verb had been applied incorrectly as this cmdlet/API actually performs a purchase and does not simply return information. An alias for the old name has been included in the module for backwards compatibility but we encourage users of this cmdlet to adopt the new name at their earliest convenience.
    * Updated cmdlets to support new service feature enabling users to specify the retention period for Performance Insights data for RDS instances. You can either choose 7 days (default) or 731 days.
  * Amazon Comprehend
    * Updated cmdlets to support new service feature enabling batch processing of a set of documents stored within an S3 bucket.
  * Amazon ECS
    * Updated cmdlets to support new service feature enabling daemon scheduling capability to deploy one task per instance on selected instances in a cluster.
    * Added parameter _-Enforce_ flag to the Remove-ECSService cmdlet to allow deleting a service without requiring to scale down the number of tasks to zero. This parameter maps to the _Force_ value in the underlying DeleteService API.
  * Amazon Inspector
    * Added cmdlets to support new service feature for viewing and previewing exclusions. Exclusions show which intended security checks are excluded from an assessment, along with reasons and recommendations to fix.
  * Amazon Pinpoint
    * Updated cmdlets to support new service features for creating complex segments and validating phone numbers for SMS messages. It also adds the ability to get or delete endpoints based on user IDs, remove attributes from endpoints, and list the defined channels for an app.
  * Amazon Redshift
    * Updated cmdlets to support new service features. (1) On-demand cluster release version: When Amazon Redshift releases a new cluster version, you can choose to upgrade to that version immediately instead of waiting until your next maintenance window. You can also choose to roll back to a previous version. (2) Upgradeable reserved instance - You can now exchange one Reserved Instance for a new Reserved Instance with no changes to the terms of your existing Reserved Instance (term, payment type, or number of nodes).
  * Amazon SageMaker
    * Added support for Notebook instances and the ability to run hyperparameter tuning jobs. A hyperparameter tuning job will create and evaluate multiple training jobs while tuning algorithm hyperparameters, to optimize a customer specified objective metric.
  * AWS Secrets Manager
    * Updated cmdlets to support new service feature for esource-based policies that attach directly to your secrets. These policies provide an additional way to control who can access your secrets and what they can do with them. For more information, see https://docs.aws.amazon.com/secretsmanager/latest/userguide/auth-and-access_resource-based-policies.html in the Secrets Manager User Guide.
  * AWS Shield
    * Added support for DDoS Response Team access management.
  * AWS Storage Gateway
    * Updated cmdlets to support new service features for using Server Message Block (SMB) protocol to store and access objects in Amazon Simple Storage Service (S3).
  * AWS CloudFormation
    * Updated the New-CFNStackSet and Update-CFNStack set with parameters to support filtered updates for StackSets based on accounts and regions (this feature will allow flexibility for the customers to roll out updates on a StackSet based on specific accounts and regions) and to support customized ExecutionRoleName (this feature will allow customers to attach ExecutionRoleName to the StackSet thus ensuring more security and controlling the behavior of any AWS resources in the target accounts).
  * AWS IoT
    * Added cmdlets to support new APIs released by the service: Remove-IOTJob (DeleteJob API) and Remove-IOTJobExecution (DeleteJobExecution API).
    * Updated the Stop-IOTJob cmdlet to support forced cancellation.
    * Added cmdlet Stop-IOTJobExecution (CancelJobExecution API).
  * AWS Systems Manager
    * Added example for the New-SSMAssociation cmdlet.
    * Updated cmdlets to support new service features around sending RunCommand output to CloudWatch Logs, add the ability to view association execution history and execute an association.
  * AWS AppStream
    * Updated cmdlets to support new service feature enabling customers to find their VPC private IP address and ENI ID associated with AppStream streaming sessions.
  * AWS Certificate Manager Private Certificate Authority
    * Updated cmdlets to support new service 'Restore' feature, enabling users to restore a private certificate authority that has been deleted. When you use the Remove-PCACertificateAuthority cmdlet, you can now specify the number of days (7-30, with 30 being the default) in which the private certificate authority will remain in the DELETED state. During this time, the private certificate authority can be restored with the new Restore-PCACertificateAuthority (RestoreCertificateAuthority API) cmdlet and then be returned to the PENDING_CERTIFICATE or DISABLED state, depending upon the state prior to deletion.
  * AWS Cloud Directory
    * Updated cmdlets to support new service 'Flexible Schema' feature. This feature lets customers using new capabilities like: variant typed attributes, dynamic facets and AWS managed Cloud Directory schemas.
  * AWS Glue
    * Updated cmdlets to support new service feature for sending delay notification to Amazon CloudWatch Events when an ETL job runs longer than the specified delay notification threshold.
  * AWS Elemental MediaLive
    * Added and updated cmdlets to support new service features for reserved inputs and outputs. You can reserve inputs and outputs with a 12 month commitment in exchange for discounted hourly rates. Pricing is available at https://aws.amazon.com/medialive/pricing/.
  * AWS Elemental MediaConvert
    * Added cmdlets to support tagging queues, presets, and templates during creation of those resources on MediaConvert.
  * AWS Config
    * Updated cmdlets to support new service feature for retention, allowing you to specify a retention period for your AWS Config configuration items.
  * AWS Directory Service
    * Added cmdlet Reset-DSUserPassword (ResetUserPassword API). Customers can now reset their users' passwords without providing the old passwords in Simple AD and Microsoft AD.

### 3.3.283.0 (2018-05-18)
  * AWS Service Catalog
    * Users can now pass a new option to Get-SCAcceptedPortfolioSharesList called PortfolioShareType with a value of AWS_SERVICECATALOG in order to access Getting Started Portfolios that contain selected products representing common customer use cases.
  * Alexa for Business
    * Added operations for creating and managing address books of phone contacts for use in A4B managed shared devices.
    * Added Get-ALXBDeviceEventList to paginated list of device events (such as ConnectionStatus).
    * Added ConnectionStatus param to Find-ALXBDevice and Start-ALXBDeviceSync.
    * Added new Device status "DEREGISTERED". This release also adds DEVICE_STATUS as the new DeviceEventType.
  * AWS Certificate Manager
    * Updated documentation notes to
      * Replace incorrect userguide links in Add-ACMCertificateTag, Import-ACMCertificate, New-ACMCertificate, Send-ACMValidationEmail and Update-ACMCertificateOption with the correct links.
      * Added comments in Export-ACMCertificate detailing the issuer of a private certificate as the Private Certificate Authority (CA).
  * AWS CodeBuild
    * Added support for more override fields such as CertificateOverride, EnvironmentTypeOverride for Start-CBBuild
    * Added support for idempotency token field (IdempotencyToken) for Start-CBBuild
  * AWS CodePipeline
    * Added new cmdlets to support webhooks in AWS CodePipeline. A webhook is an HTTP notification that detects events in another tool, such as a GitHub repository, and connects those external events to a pipeline. AWS CodePipeline creates a webhook when you use the console to create or edit your GitHub pipeline and deletes your webhook when you delete your pipeline.
  * Amazon DynamoDB
    * Adds two new cmdlets Get-DDBGlobalTableSetting and Update-DDBGlobalTableSetting to get region specific settings for a global table and update settings for a global table respectively. This update introduces new constraints in the New-DDBGlobalTable and Update-DDBGlobalTable. Tables must have the same write capacity units. If Global Secondary Indexes exist then they must have the same write capacity units and key schema.
  * Amazon EC2
    * Added EC2 Fleet cmdlets. Amazon EC2 Fleet is a new feature that simplifies the provisioning of Amazon EC2 capacity across different EC2 instance types, Availability Zones, and the On-Demand, Reserved Instance, and Spot Instance purchase models. With a single call, you can now provision capacity to achieve desired scale, performance, and cost.
  * Amazon Elasticsearch
    * Added Get-ESReservedElasticsearchInstanceList, Get-ESReservedElasticsearchInstanceOfferingList and New-ESReservedElasticsearchInstanceOffering to support Reserved Instances on AWS Elasticsearch.
  * Amazon GameLift
    * AutoScaling Target Tracking scaling simplification along with Start-GMLFleetAction and Stop-GMLFleetAction to suspend and resume automatic scaling at will.
  * Amazon Guard Duty
    * Added new cmdlets for Amazon GuardDuty for creating and managing filters. For each filter, you can specify a criteria and an action. The action you specify is applied to findings that match the specified criteria.
  * Amazon Kinesis Firehose
    * With this release, Amazon Kinesis Data Firehose can convert the format of your input data from JSON to Apache Parquet or Apache ORC before storing the data in Amazon S3. Parquet and ORC are columnar data formats that save space and enable faster queries compared to row-oriented formats like JSON.
  * AWS Organizations
    * Documentation updates for organizations
  * Amazon Relational Database Service
    * Changes to support the Aurora MySQL Backtrack feature. Cmdlets added are Reset-RDSDBCluster and Get-RDSDBClusterBacktrackList
    * Correction to the documentation about copying unencrypted snapshots.
  * Amazon Route53 Domains
    * This release adds a SubmittedSince attribute to Get-R53DOperationList, so you can list operations that were submitted after a specified date and time.
  * Amazon SageMaker
    * SageMaker has added support for VPC configuration for both Endpoints and Training Jobs. This allows you to connect from the instances running the Endpoint or Training Job to your VPC and any resources reachable in the VPC rather than being restricted to resources that were internet accessible.
  * AWS Secrets Manager
    * Documentation updates for Secret Manager
  * Amazon Simple Systems Management
    * Added support for new parameter, DocumentVersion, for Send-SSMCommand. Users can now specify version of SSM document to be executed on the target(s).
  * Amazon WorkSpaces
    * Added new IP Access Control cmdlets. You can now create/delete IP Access Control Groups, add/delete/update rules for IP Access Control Groups, Associate/Disassociate IP Access Control Groups to/from a WorkSpaces Directory, and Describe IP Based Access Control Groups.
    * Added cmdlet Edit-WKSWorkspaceState to change the state of a Workspace, and the ADMIN_MAINTENANCE WorkSpace state.

### 3.3.270.0 (2018-04-25)
  * The CmdletsToExport property in the module manifest has been temporarily set to '*-*' instead of the individual cmdlet names to work around a limitation publishing modules that contain over 4000 cmdlets to the PowerShell Gallery. The net effect of this change is to disable tab completion for cmdlet names unless the module is explicitly imported. We are investigating approaches to work around or fix this issue and will re-instate the list of cmdlets as soon as possible.
  * AWS Secrets Manager
    * Added cmdlets to support the new AWS Secrets Manager service that enables you to store, manage, and retrieve, secrets. Cmdlets for the service have the noun prefix SEC and can be listed with the command *Get-AWSCmdletName -Service SEC*.
  * AWS Certificate Manager Private Certificate Authority
    * Added cmdlets to support the new AWS Certificate Manager (ACM) Private Certificate Authority (CA), a managed private CA service that helps you easily and securely manage the lifecycle of your private certificates. ACM Private CA provides you a highly-available private CA service without the upfront investment and ongoing maintenance costs of operating your own private CA. ACM Private CA extends ACM's certificate management capabilities to private certificates, enabling you to manage public and private certificates centrally. Cmdlets for the service have the noun prefix PCA and can be listed with the command *Get-AWSCmdletName -Service PCA*.
  * Firewall Management Service
    * Added cmdlets to support the Firewall Management Service, a new AWS service that makes it easy for customers to centrally configure WAF rules across all their resources (ALBs and CloudFront distributions) and across accounts. Cmdlets for the service have the noun prefix FMS and can be listed with the command *Get-AWSCmdletName -Service FMS*.
  * AWS Certificate Manager
    * Updated the New-ACMCertificate and added new cmdlet Update-ACMCertificateOption (UpdateCertificateOption API) to support disabling Certificate Transparency logging on a per-certificate basis.
    * Added support for new service features for requesting and exporting private certificates. This API enables you to collect batch amounts of metric data and optionally perform math expressions on the data. With one GetMetricData call you can retrieve as many as 100 different metrics and a total of 100,800 data points.
  * Amazon CloudWatch
    * Added cmdlet Get-CWMetricData to support the new GetMetricData API.
  * Amazon DynamoDB
    * Added cmdlets restore-DDBTableToPointInTime (RestoreTableToPointInTime API) and Update-DDBContinuousBackup (UpdateContinuousBackups API) t o support Point-in-time recovery (PITR) continuous backups of your DynamoDB table data. With PITR, you do not have to worry about creating, maintaining, or scheduling backups. You enable PITR on your table and your backup is available for restore at any point in time from the moment you enable it, up to a maximum of the 35 preceding days. PITR provides continuous backups until you explicitly disable it.
  * AWS Identity and Access Management
    * Updated the New-IAMRole cmdlet with parameter -MaxSessionDuration, and added cmdlet Update-IAMRole (UpdateRole API) to support the new longer role sessions.
  * AWS Cloudwatch Logs
    * Fixed bad links to the service API references in the help details for the service's cmdlets.
  * Amazon Inspector
    * Fixed bad links to the service API references in the help details for the service's cmdlets.
  * Alexa for Business
    * Added cmdlets to support new operations related to creating and managing address books of contacts for use in A4B managed shared devices.
  * AWS CloudFormation
    * Updated cmdlets to support use of a customized AdministrationRole to create security boundaries between different users.
  * Amazon CloudFront
    * Added cmdlets to support Field-Level Encryption to further enhance the security of sensitive data, such as credit card numbers or personally identifiable information (PII) like social security numbers. CloudFront's field-level encryption further encrypts sensitive data in an HTTPS form using field-specific encryption keys (which you supply) before a POST request is forwarded to your origin. This ensures that sensitive data can only be decrypted and viewed by certain components or services in your application stack. Field-level encryption is easy to setup. Simply configure the fields that have to be further encrypted by CloudFront using the public keys you specify and you can reduce attack surface for your sensitive data.
  * Amazon Elasticsearch
    * Updated cmdlets to support Amazon Cognito authentication support to Kibana.
  * AWS Config Service
    * Added support for multi-account multi-region data aggregation features. Customers can create an aggregator (a new resource type) in AWS Config that collects AWS Config data from multiple source accounts and regions into an aggregator account. Customers can aggregate data from individual account(s) or an organization and multiple regions. In this release, AWS Config adds several API's for multi-account multi-region data aggregation.
  * AWS Device Farm
    * Added support for Private Device Management feature. Customers can now manage their private devices efficiently - view their status, set labels and apply profiles on them. Customers can also schedule automated tests and remote access sessions on individual instances in their private device fleet.
    * Added cmdlets to support new service features for VPC endpoints.
  * Amazon WorkMail
    * Added cmdlets to support granting users and groups with "Full Access", "Send As" and "Send on Behalf" permissions on a given mailbox.
  * AWS Glue
    * Updated cmdlets to support timeout values for ETL jobs. With this release, all new ETL jobs have a default timeout value of 48 hours. AWS Glue also now supports the ability to start a schedule or job events trigger when it is created.
  * Amazon Cloud Directory
    * Added cmdlets to support new APIs to fetch attributes within a facet on an object or multiple facets or objects.
  * AWS Batch
    * Added support for specifying timeout when submitting jobs or registering job definitions.
  * AWS Systems Manager
    * Added cmdlets Get-SSMInventoryDeletionList (DescribeInventoryDeletions API) and Remove-SSMInventory (DeleteInventory API).
  * AWS X-Ray
    * Added cmdlets Get-XREncryptionConfig (GetEncryptionConfig API) and Write-XREncryptionConfig (PutEncryptionConfig API) to support managing data encryption settings. Use Write-XREncryptionConfig to configure X-Ray to use an AWS Key Management Service customer master key to encrypt trace data at rest.

### 3.3.253.0 (2018-03-26)
  * Amazon WorkMail
    * Added cmdlets to support the Amazon WorkMail service. Cmdlets for the service have the noun prefix WM and can be listed with the command *Get-AWSCmdletName -Service WM*.
  * Amazon Pinpoint
    * Added cmdlets to support the new service feature enabling endpoint exports from your Amazon Pinpoint projects. The new cmdlets are Get-PINExportJobList (GetExportJobs API), Get-PINSegmentExportJobList (GetSegmentExportJobs API) and New-PINExportJob (CreateExportJob API).
    * Added cmdlet Remove-PINEndpoint to support the new DeleteEndpoint API.
  * Amazon SageMaker
    * Added cmdlets to support the new customizable notebook instance lifecycle configuration APIs. The new cmdlets are Get-SMNotebookInstanceLifecycleConfig (DescribeNotebookInstanceLifecycleConfig API), Get-SMNotebookInstanceLifecycleConfig (ListNotebookInstanceLifecycleConfigs API), New-SMNotebookInstanceLifecycleConfig (CreateNotebookInstanceLifecycleConfig API), Remove-SMNotebookInstanceLifecycleConfig (DeleteNotebookInstanceLifecycleConfig API) and Update-SMNotebookInstanceLifecycleConfig (UpdateNotebookInstanceLifecycleConfig API).
  * AWS Elastic Beanstalk
    * Added new cmdlet Get-EBAccountAttribute to support the new DescribeAccountAttributes API. In this release the API will support quotas for resources such as applications, application versions, and environments.

### 3.3.245.0 (2018-03-02)
  * AWS AppStream
    * Added cmdlet Copy-APSImage to support the new CopyImage API. This API enables customers to copy their Amazon AppStream 2.0 images within and between AWS Regions.
  * Auto Scaling
    * Updated the New-ASAutoScalingGroup and Update-ASAutoScalingGroup cmdlets with a new parameter, -ServiceLinkedRoleARN, to support service linked roles.
  * AWS CodeCommit
    * Added cmdlet Write-CFFile to support the new PutFile API. This API enables customers to add a file directly to an AWS CodeCommit repository without requiring a Git client.
  * AWS Cost Explorer
    * Added cmdlet Get-CEReservationCoverage to support the new GetReservationCoverage API.
  * Amazon EC2
    * Updated the New-EC2Snapshot cmdlet to add suport for tagging EBS snapshots.
  * Serverless Application Repository
    * Updated the New-SARApplication and Update-SARApplication cmdlets to support setting a home page URL for the application.
    * Added cmdlet Remove-SARApplication to support the new DeleteApplication API.
  * AWS WAF
    * Added cmdlets Get-WAFPermissionPolicy (GetPermissionPolicy API) and Remove-WAFPermissionPolicy (DeletePermissionPolicy API).
  * AWS WAF Regional
    * Added cmdlets Get-WAFRPermissionPolicy (GetPermissionPolicy API) and Remove-WAFRPermissionPolicy (DeletePermissionPolicy API).
  * AWS Service Catalog
    * Added cmdlet Remove-SCTagOption to support the new DeleteTagOption API.
  * AWS Storage Gateway
    * Updated the New-SGNFSFileShare and Update-SGNFSFileShare cmdlets to support new service features for file share attributes. Users can now specify the S3 Canned ACL to use for new objects created in the file share and create file shares for requester-pays buckets.

### 3.3.234.0 (2018-02-14)
  (This version was only released as part of the combined AWS Tools for Windows installer. It was not released to the PowerShell Gallery.)
  * AWS AppSync
    * Added cmdlet Update-ASYNApiKey to support the new UpdateApiKey API.
  * Amazon Lex Model Builder Service
    * Added cmdlets Get-LMBImport (GetImport API) and Start-LMBImport (StartImport API) to support the new to export and import your Amazon Lex chatbot definition as a JSON file.

### 3.3.232.0 (2018-02-13)
  * AWS Lambda
    * [Breaking Change] The response data from the service's GetPolicy API has been extended to emit both the policy and revision ID of the policy. The output from the corresponding Get-LMPolicy cmdlet has therefore been changed to emit the service response to the pipeline. To keep the original behavior your scripts need to be changed to use _(Get-LMPolicy).Policy_ in place of _Get-LMPolicy_.
    * Updated the Add-LMPermission, Publish-LMVersion, Remove-LMPermission, Update-LMAlias and Update-LMFunctionConfiguration cmdlets to support setting Revision ID on your function versions and aliases, to track and apply conditional updates when you are updating your function version or alias resources.
  * AWS Key Management Service.
    * [Breaking Change] The cmdlet for the service's ListKeyPolicies API had been named in error as Get-KMSKysPolicyList due to a typo. This has now been corrected and the cmdlet name updated to Get-KMSKeyPolicyList.
  * AWS CodeBuild
    * Added new parameters to the Start-CBBuild and Update-CBProject cmdlets to support shallow clone and GitHub Enterprise.
  * AWS Device Farm
    * Updated the New-DFRemoteAccessSession cmdlet to support the service's new InteractionMode setting for the DirectDeviceAccess feature.
  * AWS Elemental MediaLive
    * Updated the New-EMLChannel cmdlet to support the new InputSpecification settings (specification of input attributes is used for channel sizing and affects pricing).
  * Amazon CloudFront
    * Updated the Get-CFCloudFrontOriginAccessIdentityList, Get-CFDistributionList, Get-CFDistributionListByWebACLId, Get-CFInvalidationList and Get-CFStreamDistributionList cmdlets to support automatic pagination of result output. The cmdlets will now make repeated calls to obtain all available data and no longer require users to implement their own pagination logic in scripts or at the command line.
  * Amazon Kinesis
    * Added new cmdlet Get-KINShardList to support the new ListShards service API. Using ListShards a Kinesis Data Streams customer or client can get information about shards in a data stream (including meta-data for each shard) without obtaining data stream level information.
  * AWS OpsWorks
    * Added new cmdlet Get-OPSOperatingSystem to support the new DescribeOperatingSystems API.
  * AWS Glue
    * Added parameters to the New-GLUClassifier and Update-GLUEClassifier cmdlets to support specifying the json paths for customized classifiers. The custom path indicates the object, array or field of the json documents the user would like crawlers to inspect when they crawl json files.
  * AWS Service Catalog
    * [Breaking Change] The response data from the service's DescribeProvisionedProduct api has been changed to emit additional data. The output from the corresponding Get-SCProvisionedProduct cmdlet has therefore been changed to now emit the full service response to the pipeline. To keep the original behavior your scriprs need to be changed to use _(Get-SCProvisionedProduct).ProvisionedProductDetail_ in place of _Get-SCProvisionedProduct_.
    * Added cmdlets to support new APIs: Find-SCProvisionedProduct (SearchProvisionedProduct API), Get-SCProvisionedProductPlan (DescribeProvisionedPlan API), Get-SCProvisionedPlanList (ListProvisionedproductPlans API), New-SCProvisionedproductPlan (CreateProvisionedproductPlan API), Remove-SCProvisionedProductPlan (DeleteProvisionedProductPlan API) and Start-SCProvisionedProductPlanExecution (ExecuteProvisionedProductPlan API).
    * Amazon Systems Manager
      * Updated cmdlets with parameters to support Patch Manager enhancements for configuring Linux repos as part of patch baselines, controlling updates of non-OS security packages and also creating patch baselines for SUSE12.
      * Updated service name in cmdlet help documentation.
  * Amazon AppStream
      * Added parameter -RedirectURL to the New-APSStack and Update-APSStack cmdlets enabling a redirect url to be provided for a stack. Users will be redirected to the link provided by the admin at the end of their streaming session. Update-APSStack also now supports a new parameter enabling attributes to be deleted from a stack.
  * AWS Database Migration Service
    * Added cmdlets to support new APIs for replication instance task logs and rebooting instances. Replication instance task logs allows users to see how much storage each log for a task on a given instance is occupying. The reboot API gives users the option to reboot the application software on the instance and force a fail over for MAZ instances to test robustness of their integration with our service. The new cmdlets are Get-DMSReplicationInstanceTaskLog (DescribeReplicationInstanceTaskLogs API) and Restart-DMSReplicationInstance (RebootReplicationInstance API).
  * Amazon EC2
    * Added cmdlets for new APIs to support determining the longer ID opt-in status of their account. The new cmdlets are Get-EC2AggregatedIdFormat (DescribeAggregatedIdFormat API) and Get-EC2PrincipalIdFormat (DescribePrincipalIdFormat API).
  * Amazon GameLift
    * Added cmdlet Start-GMLMatchBackfill to support the new StartMatchBackfill API. This API allows developers to add new players to an existing game session using the same matchmaking rules and player data that were used to initially create the session.
  * AWS Elemental Media Live
    * Added cmdlet Update-EMLChannel to support the new UpdateChannel API. For idle channels you can now update channel name, channel outputs and output destinations, encoder settings, user role ARN, and input specifications. Channel settings can be updated in the console or with API calls. Please note that running channels need to be stopped before they can be updated. We've also deprecated the 'Reserved' field.
  * AWS Elemental Media Store
    * Added cmdlets Get-EMSCorsPolicy (GetCorsPolicy API), Write-EMSCorsPolicy (PutCorsPolicy API) and Remove-EMSCorsPolicy (DeleteCorsPolicy API) to support per-container CORS configuration.
  * Amazon Cognito Identity Provider
    * Added cmdlet Get-CGIPSigningCertificate to support the new  GetSigningCertificate API.
    * Updated the New-CGIPUserPool and Update-CGIPUserPool cmdlets to support user migration using an AWS Lambda trigger.

### 3.3.225.1 (2018-01-24)
  * Amazon Guard Duty
    * Fixed issue with error 'The request is rejected because an invalid or out-of-range value is specified as an input parameter' being emitted when running cmdlets that map to service apis that support pagination (eg Get-GDDetectorList).

### 3.3.225.0 (2018-01-23)
  * Amazon Transcribe
    * Added cmdlets to support the public preview release of the new Amazon Transcribe service. Cmdlets for the service have the noun prefix TRS and can be listed with the command *Get-AWSCmdletName -Service TRS*.
  * AWS Glue
    * Added cmdlets to support the AWS Glue service. Cmdlets for the service have the noun prefix GLUE and can be listed with the command *Get-AWSCmdletName -Service GLUE*.
  * AWS Cloud9
    * Added examples for all Cloud9 cmdlets.
  * Amazon Relational Database Service
    * Updated the Edit-RDSDBInstance, New-RDSDBInstance, New-RDSDBInstanceReadReplica, Restore-RDSDBInstanceFromDBSnapshot, Restore-RDSDBInstanceFromS3 and Restore-RDSDBInstanceToPointInTime cmdlets with new parameters to support integration with CloudWatch Logs.
  * Amazon SageMaker Service
    * Updated the New-SMEndpointConfig cmdlet with support for specifying a KMS key for volume encryption.
  * AWS Budgets
    * Updated the New-BGTBudget and Update-BGTBudget cmdlets to support additional cost types (IncludeDiscount and UseAmortized) to support finer control for different charges included in a cost budget.

### 3.3.221.0 (2018-01-15)
  * Amazon S3
    * Fixed issue with the Copy-S3Object displaying an error when copying objects between buckets and the object keys started with the '/' character.
  * AWS Lambda
    * Updated cmdlets and argument completers to support the newly released dotnetcore2.0 and go1.x runtimes for Lamba functions.

### 3.3.219.0 (2018-01-12)
  * Amazon EC2
    * Updated the Get-EC2Snapshot and Get-EC2Volume cmdlets to use pagination to process data when a snapshot id or volume id is not supplied. This update should help customers with large numbers of snapshots or volumes who were experiencing timeout issues in previous versions which attempted to use a single call to obtain all data.
    * Added support for pipelining the output launch template object types returned by the the New-EC2LaunchTemplate and New-EC2LaunchTemplateVersion into the same cmdlets, and also into the New-EC2Instance cmdlet.
    * Updated the Get-EC2PasswordData cmdlet to display better error message if used to obtain password before the instance is ready.
  * Get-AWSRegion
    * Updated documentation notes and examples to address confusion as to how this cmdlet handles regions launched after the module is released. The cmdlet uses built-in data to display the list of regions, so won't show new regions launched subsequently, but those new regions can still be used with all cmdlets without requiring an update to the tools.
  * AWS CodeDeploy
    * Added cmdlet Remove-CDGitHubAccountToken to support the new DeleteGitHubAccountToken API.
  * AWS Directory Service
    * Updated the New-DSMicrosoftAD cmdlet to add support for an -Edition parameter. The service now supports Standard and Enterprise (the default) editions of Microsoft Active Directory. Microsoft Active Directory (Standard Edition), also known as AWS Microsoft AD (Standard Edition), is a managed Microsoft Active Directory (AD) that is optimized for small and midsize businesses (SMBs). With this SDK release, you can now create an AWS Microsoft AD directory using API. This enables you to run typical SMB workloads using a cost-effective, highly available, and managed Microsoft AD in the AWS Cloud.
  * Amazon Relational Database Service
    * Updated the New-RDSDBInstanceReadReplica cmdlet with support for Multi AZ deployments.

### 3.3.215.0 (2018-01-02)
  * Amazon API Gateway
    * Added support for tagging resources for cost allocation with new cmdlets Add-AGResourceTag (TagResource API), Get-AGResourceTag (GetTags API) and Remove-AGResourceTag (UntagResource API). The New-AGStage cmdlet was also updated with a new -Tag parameter.
    * Updated the New-AGRestApi cmdlet with parameters to support setting minimum compression size and returning API keys from a custom authorizer for use with a usage plan.
  * Amazon ECS
    * Updated the New-ECSService and Update-ECSService cmdlets to support the new service feature enabling a grace period for health checks to be specified.
  * AWS IoT
    * Added cmdlets to support the new service feature for code signed Over-the-air update functionality for Amazon FreeRTOS. Users can now create and schedule Over-the-air updates to their Amazon FreeRTOS devices using these new APIs.
  * Amazon Kinesis Analytics
    * Updated the Add-KINAApplicationOutput cmdlet to support the new service feature enabling AWS Lambda functions as output.
  * Amazon SageMaker Service
    * *BREAKING CHANGE* The -SupplementalContainer parameter for the New-SMModel cmdlet has been removed as it is no longer supported by the service.
  * Amazon WorkSpaces
    * Updated the Edit-WKSWorkspaceProperty cmdlet to support new service features for flexible storage and switching of hardware bundles.

### 3.3.210.0 (2017-12-19)
  * Amazon AppStream
    * Added support add tags to Amazon AppStream 2.0 resources with new cmdlets Add-APSResourceTag (TagResource API) and Remove-APSResourceTag (UntagResource API).
  * Amazon S3
    * Extended the Copy-S3Object cmdlet to support copying of objects larger than 5GB within S3. The cmdlet previously used the CopyObject API which is limited to objects up to 5GB in size. With this release the cmdlet inspects the object size and switches automatically to multi-part copy if necessary. The output of the cmdlet when copying objects within S3 has also changed; previously the output of the CopyObject API was emitted. With this update an Amazon.S3.Model.S3Object, referencing the newly copied object, is emitted to the pipeline.
  * Added tab completion support for the new EU (Paris), eu-west-3, region to the -Region parameter for cmdlets.

### 3.3.208.1 (2017-12-13)
  * Amazon EC2
    * Fixed issue with Get-EC2PasswordData cmdlet reporting a null reference when invoked without the -PemFile parameter. This parameter is not required if the keypair data for the instance is saved in the configuration store for the AWS Toolkit for Visual Studio.
  * All cmdlets
    * Added support for the new China (Ningxia) region (cn-northwest-1).

### 3.3.208.0 (2017-12-13)
  * AWS Cost Explorer
    * Added support for the AWS Cost Explorer service. AWS Cost Explorer helps you visualize, understand, and manage your AWS costs and usage over time. The Cost Explorer API gives you programmatic access to the full Cost Explorer dataset, including advanced metrics (e.g., Reserved Instance utilization) and your cost allocation tags. Cmdlets for the service have the noun prefix 'CE' and can be listed with the command *Get-AWSCmdletName -Service CE*.
  * AWS Resource Groups
    * Added support for the AWS Resource Groups service announced at AWS re:Invent 2017. A resource group is a collection of AWS resources that are all in the same AWS region, and that match criteria provided in a query. Queries include lists of resources that are specified in the following format *AWS::service::resource*, and tags. Tags are keys that help identify and sort your resources within your organization; optionally, tags include values for keys. Cmdlets for the service have the noun prefix 'RG' and can be listed with the command *Get-AWSCmdletName -Service RG*.
  * Amazon Kinesis Video Streams
    * Added support for the Amazon Kinesis Video Streams service announced at AWS re:Invent 2017. Amazon Kinesis Video Streams is a fully managed AWS service that you can use to stream live video from devices to the AWS Cloud, or build applications for real-time video processing or batch-oriented video analytics. Cmdlets for the service have the noun prefix 'KV' and can be listed with the command *Get-AWSCmdletName -Service KV*.
  * Amazon Kinesis Video Streams Media
    * Added support for the Amazon Kinesis Video Streams Media service announced at AWS re:Invent 2017. Cmdlets for the service have the noun prefix 'KVM' and can be listed with the command *Get-AWSCmdletName -Service KVM*.
  * AWS IoT Jobs Data Plane
    * Added support for the AWS IoT Jobs Data Plane service announced at AWS re:Invent 2017. Cmdlets for the service have the noun prefix 'IOTJ' and can be listed with the command *Get-AWSCmdletName -Service IOTJ*.
  * AWS AppSync
    * Added support for the AWS AppSync service announced at AWS re:Invent 2017. AWS AppSync is an enterprise level, fully managed GraphQL service with real-time data synchronization and offline programming features. Cmdlets for the service have the noun prefix 'ASYN' and can be listed with the command *Get-AWSCmdletName -Service ASYN*.
  * Alexa for Business
    * Added support for the Alexa for Business service announced at AWS re:Invent 2017. Alexa for Business gives you the tools you need to manage Alexa devices, enroll users, and assign skills. You can build your own voice skills using the Alexa Skills Kit and the Alexa for Business API. You can also make them available as private skills for your organization. Cmdlets for the service have the noun prefix 'ALXB' and can be listed with the command *Get-AWSCmdletName -Service ALXB*.
  * AWS Cloud9
    * Added support for the AWS Cloud9 service announced at AWS re:Invent 2017. AWS Cloud9 is a cloud-based integrated development environment (IDE) that you use to write, run, and debug code. Cmdlets for the service have the noun prefix 'C9' and can be listed with the command *Get-AWSCmdletName -Service C9*.
  * AWS Serverless Application Repository
    * Added support for the AWS Serverless Application Repository. With AWS Serverless Application Repository, you can quickly find and deploy serverless applications in the AWS Cloud. You can browse applications by category, or search for them by name, publisher, or event source. Cmdlets for the service have the noun prefix 'SAR' and can be listed with the command *Get-AWSCmdletName -Service SAR*.
  * Amazon SageMaker Service
    * Added support for Amazon SageMaker Service, announced at AWS re:Invent 2017. Amazon SageMaker is a fully managed machine learning service. With Amazon SageMaker, data scientists and developers can quickly and easily build and train machine learning models, and then directly deploy them into a production-ready hosted environment. Cmdlets for the service have the noun prefix 'SM' and can be listed with the command *Get-AWSCmdletName -Service SM*. Cmdlets for the Amazon SameMaker Runtime service have the noun prefix 'SMR' and can be listed with the command *Get-AWSCmdletName -Service SMR*.

### 3.3.205.0 (2017-12-08)
  * Amazon EC2
    * Updated the New-EC2Instance cmdlet to support the new [T2 Unlimited](http://docs.aws.amazon.com/AWSEC2/latest/UserGuide/t2-unlimited.html#launch-t2) feature.
  * Amazon Cloud Directory
    * Added support for new APIs to enable schema changes across your directories with in-place schema upgrades. Your directories now remain available while backward-compatible schema changes are being applied, such as the addition of new fields. You also can view the history of your schema changes in Cloud Directory by using both major and minor version identifiers, which can help you track and audit schema versions across directories. The new cmdlets are Get-CDIRAppliedSchemaVersion (GetAppliedSchemaVersion API), Update-CDIRAppliedSchema (UpgradeAppliedSchema API) and Update-CDIRPublishedSchema (UpgradePublishedSchema API).
  * AWS Budgets
    * Added support for additional cost types to support finer control for different charges included in a cost budget.
  * Amazon Elasticsearch
    * Updated the New-ESDomain cmdlet to add support for encryption of data at rest on Amazon Elasticsearch Service using AWS KMS.
  * Amazon Simple Email Service
    * Added cmdlets to support new service features for customization of the emails that Amazon SES sends when verifying new identities. This feature is helpful for developers whose applications send email through Amazon SES on behalf of their customers.
  * Amazon MQ
    * Added support for the Amazon MQ service launched at AWS re:Invent 2017. Amazon MQ is a managed message broker service for [Apache ActiveMQ](http://activemq.apache.org/) that makes it easy to set up and operate message brokers in the cloud. Cmdlets for the service have the noun prefix 'MQ' and can be listed with the command *Get-AWSCmdletName -Service MQ*.
  * Amazon GuardDuty
    * Added support for the Amazon GuardDuty service launched at AWS re:Invent 2017. Amazon GuardDuty is a continuous security monitoring service that analyzes and processes VPC Flow Logs, AWS CloudTrail event logs, and DNS logs. It uses threat intelligence feeds, such as lists of malicious IPs and domains, and machine learning to identify unexpected and potentially unauthorized and malicious activity within your AWS environment. Cmdlets for the service have the noun prefix 'GD' and can be listed with the command *Get-AWSCmdletName -Service GD*.
  * Amazon Comprehend
    * Added support for the Amazon Comprehend service launched at AWS re:Invent 2017. Amazon Comprehend uses natural language processing (NLP) to extract insights about the content of documents. Amazon Comprehend processes any text file in UTF-8 format. It develops insights by recognizing the entities, key phrases, language, sentiments, and other common elements in a document. Use Amazon Comprehend to create new products based on understanding the structure of documents. For example, using Amazon Comprehend you can search social networking feeds for mentions of products or scan an entire document repository for key phrases. Cmdlets for the service have the noun prefix 'COMP' and can be listed with the command *Get-AWSCmdletName -Service COMP*.
  * Amazon Translate
    * Added support for Amazon Translate service launched at AWS re:Invent 2017. Amazon Translate translates documents from the following six languages into English, and from English into these languages:
        * Arabic
        * Chinese
        * French
        * German
        * Portuguese
        * Spanish
      Cmdlets for the service have the noun prefix 'TRN' and can be listed with the command *Get-AWSCmdletName -Service TRN*.

### 3.3.201.0 (2017-12-02)
  * Updated cmdlets for multiple services to include new APIs and API updates released during AWS re:Invent 2017, for services already supported by the tools. New services launched at the conference will be added to the tools in the coming days. The updated services are:
    * Auto Scaling
    * Amazon API GatewayGateway
    * Amazon DynamoDB
    * Amazon Cognito
    * Amazon Cognito Identity Provider
    * Amazon EC2
    * Amazon Elastic Container Service
    * Amazon Lightsail
    * Amazon Rekognition
    * Amazon Systems Manager
    * AWS Batch
    * AWS CloudFormation
    * AWS CodeDeploy
    * AWS Greengrass
    * AWS IoT
    * AWS Lambda
    * AWS WAF
    * AWS WAF Regional

### 3.3.197.0 (2017-11-27)
  * AWS Elemental Media Convert
    * Added support for the new service. Cmdlets for the service have the noun prefix 'EMC'.
  * AWS Elemental Media Live
    * Added support for the new service. Cmdlets for the service have the noun prefix 'EML'.
  * AWS Elemental Media Package
    * Added support for the new service. Cmdlets for the service have the noun prefix 'EMP'.
  * AWS Elemental Media Store
    * Added support for the new service. Cmdlets for the service have the noun prefix 'EMS'.
  * AWS Elemental Media Data Plane
    * Added support for the new service. Cmdlets for the service have the noun prefix 'EMSD'.

### 3.3.196.0 (2017-11-25)
  * AWS Certificate Manager
    * Updated the New-ACMCertificate and Get-ACMCertificateList cmdlets for new service features enabling the ability to import domainless certs and additional Key Types as well as an additional validation method for DNS.
  * Amazon API Gateway
    * Updated the Get-AGDocumentationPartList and Write-AGIntegration cmdlets for new service features enabling support for access logs and customizable integration timeouts.
  * AWS CloudFormation
    * Updated the New-CFNStackInstance cmdlet to support instance-level parameter overrides.
    * Added new cmdlet Update-CFNStackInstance to support the new UpdateStackInstances API.
  * AWS CodeBuild
    * Updated the New-CBProject and Update-CBProject cmdlets to support new service features for accessing Amazon VPC resources from AWS CodeBuild, dependency caching and build badges.
    * Added the cmdlet Reset-CBProjectCache to support the new InvalidateProjectCache API.
  * AWS CodeCommit
    * Added new cmdlets for the new service feature supporting pull requests.
  * AWS Database Migration Service
    * Added cmdlet Start-DMSReplicationTaskAssessment to support the new StartReplicationTaskAssessment API.
  * Amazon ECS
    * Updated the New-ECSTask, New-ECSService, Start-ECSTask and Update-ECSService cmdlets to add support for new mode for Task Networking in ECS, called awsvpc mode.
  * Amazon Elastic Map Reduce
    * Updated the Start-EMRJobFlow cmdlet to support new service feature enabling Kerberos.
  * Amazon Kinesis
    * Added cmdlet Get-KINStreamSummary to support the new DescribeStreamSummary API.
  * Amazon Kinesis Firehose
    * Updated the New-KINFDeliveryStream and Update-KINFDestination cmdlets to support Splunk as Kinesis Firehose delivery destination. You can now use Kinesis Firehose to ingest real-time data to Splunk in a serverless, reliable, and salable manner. This release also includes a new feature that allows you to configure Lambda buffer size in Kinesis Firehose data transformation feature. You can now customize the data buffer size before invoking Lambda function in Kinesis Firehose for data transformation. This feature allows you to flexibly trade-off processing and delivery latency with cost and efficiency based on your specific use cases and requirements.
  * Amazon Lightsail
    * Added cmdlets to support the new service feature enabling attached block storage, which allows you to scale your applications and protect application data with additional SSD-backed storage disks. This feature allows Lightsail customers to attach secure storage disks to their Lightsail instances and manage their attached disks, including creating and deleting disks, attaching and detaching disks from instances, and backing up disks via snapshot.
  * AWS Organizations
    * Added cmdlets to support new service APIs to enable and disable integration with AWS services designed to work with AWS Organizations. This integration allows the AWS service to perform operations on your behalf on all of the accounts in your organization. Although you can use these APIs yourself, we recommend that you instead use the commands provided in the other AWS service to enable integration with AWS Organizations.
  * Amazon Relational Database Service
    * Added new cmdlet Restore-RDSDBInstanceFromS3 to support the new RestoreDBInstanceFromS3 API. This feature supports importing MySQL databases by using backup files from Amazon S3.
  * Amazon Rekognition
    * Added new cmdlet Find-REKText to support the new DetectText service API. This API allows you to recognize and extract textual content from images.
    * Updated cmdlets related to face detection to support the new Face Model Versioning feature.
    * [BREAKING CHANGE] The service output for the APIs called by the Get-REKCollectionIdList and Get-REKFaceList cmdlet has been updated and it is no longer possible for these cmdlets to return all available data using automatic pagination. You script will need to be updated to manually paginate the returned data using the *NextToken* parameter and field in the returned service response.
  * Amazon Route53
    * Added cmdlets Get-R53AccountLimit (GetAccountLimit API), Get-R53HostedZoneLimit (GetHostedZoneLimit API) and Get-R53ReusableDelegationSetLimit (GetReusableDelegationSetLimit API). These cmdlets enable you to view your current limits (including custom set limits) on Route 53 resources such as hosted zones and health checks. These APIs also return the number of each resource you're currently using to enable comparison against your current limits.
  * AWS Shield
    * Added cmdlet Get-SHLDSubscriptionState to support the new GetSubscriptionState API.
  * Amazon Simple Email Service
    * Added and updated cmdlets to support new service features enabling Reputation Metrics and Email Pausing Today, two features that build upon the capabilities of the reputation dashboard. The first is the ability to export reputation metrics for individual configuration sets. The second is the ability to temporarily pause email sending, either at the configuration set level, or across your entire Amazon SES account.
  * Amazon EC2 Systems Manager
    * Updated the Get-SSMInventory and Get-SSMInventorySchema cmdlets to support aggregation.
  * Amazon Set Functions
    * Added cmdlets Get-SFNStateMachineForExecution (DescribeStateMachineForExecution API) and Update-SFNStateMachine (UpdateStateMachine API). The new APIs enable you to update your state machine definition and role ARN. Existing executions will continue to use the previous definition and role ARN. You can use the DescribeStateMachineForExecution API to determine which state machine definition and role ARN is associated with an execution.
  * AWS Storage Gateway
    * Updated the New-SGNFileShare and Update-SGNFileShare cmdlets to enable guessing of MIME types for uploaded files based on the file extension.
    * Added cmdlet Send-SGUploadedNotification (NotifyWhenUploaded API). This API enables you to get notification when all your files written to your NFS file share have been uploaded.
  * Amazon WorkDocs
    * Added cmdlet Get-WDGroup to support the new DescribeGroups API.
    * Updated cmdlets to support new service features.

### 3.3.189.1 (2017-11-13)
  * Amazon EC2
    * Added cmdlet New-EC2DefaultSubnet (CreateDefaultSubnet API) enabling creation of a default subnet in an Availability Zone if no default subnet exists.
  * Amazon S3
    * Added parameter -RequesterPay to the Get-S3PresignedUrl cmdlet to support 'requester pays' mode when generating a presigned url.
    * Fixed issue with Test-S3Bucket reporting an exception 'x-amz-security-token cannot be used as both a header and a parameter' when invoked from an EC2 instance launched with an instance profile (this bug was introduced into the 3.3.189.0 release of the AWS SDK for .NET and affected v3.3.189.0 of the AWS Tools for Windows PowerShell, released only as part of the combined AWS Tools for Windows installer).

### 3.3.187.0 (2017-11-09)
  * AWS CloudFormation
    * Fixed issue with the Wait-CFNStack cmdlet not exiting when testing for 'DELETE_COMPLETE' status on a deleted stack.
  * Amazon S3
    * Added support for new bucket encryption APIs with three new cmdlets: Get-S3BucketEncryption (GetBucketEnryption API), Set-S3BucketEncryption (PutBucketEncryption API) and Remove-S3BucketEncryption (DeleteBucketEncryption API).
  * AWS Price List Service
    * Added cmdlets to support the new service. AWS Price List Service API is a centralized and convenient way to programmatically query Amazon Web Services for services, products, and pricing information. Cmdlets for the service have the noun prefix 'PLS' and can be listed using the command 'Get-AWSCmdletName -Service PLS'.
  * AWS Cloud HSM V2
    * Added cmdlets to support the Cloud HSM V2 service. Cmdlets for the service have the noun prefix 'HSM2' and can be listed using the command 'Get-AWSCmdletName -Service HSM2'.
  * Amazon API Gateway
    * Added support for new service features to create and manage regional and edge-optimized API endpoints.
  * Amazon EC2
    * [BREAKING CHANGE] The DescribeVpcEndpointServices service API, called by the cmdlet Get-EC2VpcEndpointService, has been extended to include an extra collection of service names. This additional collection is incompatible with automatic pagination therefore this cmdlet has had to be updated to disable automatic pagination of all results.
  * Application Auto Scaling
    * Added support for new service APIs to enable scheduling adjustments to MinCapacity and MaxCapacity, which makes it possible to pre-provision adequate capacity for anticipated demand and then reduce the provisioned capacity as demand lulls. The new cmdlets are Get-AASScheduledAction (DescribeScheduledActions API), Remove-AASScheduledAction (DeleteScheduledAction API) and Set-AASScheduledAction (PutScheduledAction API).
    * Renamed the existing Write-AASScalingPolicy cmdlet has been renamed to a more appropriate verb, Set-AASScalingPolicy. An alias for backwards compatibility is automatically enabled by the module.
  * Amazon ElastiCacheElastiCache
    * Added cmdlet Edit-ECReplicationGroupShardConfiguration to support the new ModifyReplicationGroupShardConfiguration service API.

### 3.3.181.0 (2017-11-01)
  * Amazon EC2 Systems Manager
    * Fixed bug in the Get-SSMParametersByPath cmdlet preventing automatic pagination.

### 3.3.180.0 (2017-10-30)
  * Amazon CloudFront
    * Added cmdlet Remove-CFServiceLinkedRole for the new DeleteServiceLinkedRole API.
  * AWS Migration Hub
    * Added cmdlets to support the AWS Migration Hub service. Cmdlets for the service have the noun prefix 'MH' and can be listed using the command 'Get-AWSCmdletName -Service MH'. For more information on the service please refer to the service user guide [here](http://docs.aws.amazon.com/migrationhub/latest/ug/whatishub.html).
  * Amazon Pinpoint
    * Added cmdlets to support the new service APIs for APNs VoIP messages.

### 3.3.178.0 (2017-10-24)
  * Amazon EC2
    * The Get-EC2SecurityGroup cmdlet has been updated to support automatic pagination of all groups to match the corresponding enhancement in the underlying DescribeSecurityGroups API.
  * Amazon Elasticsearch
    * Added cmdlet Remove-ESElasticsearchServiceRole (DeleteElasticsearchServiceRole API).
  * Amazon EC2 Systems Manager
    * Updated the Write-SSMParameter cmdlet to return the version of the parameter. Previously the cmdlet, and the underlying service PutParameter API, generated no output.
  * Amazon SQS
    * Added new cmdlets to support tracking cost allocation by adding, updating, removing, and listing the metadata tags of Amazon SQS queues. The new cmdlets are Add-SQSResourceTag (TagQueue API), Get-SQSResourceTag (ListQueueTags API) and Remove-SQSResourceTag (UntagQueue API).

### 3.3.173.0 (2017-10-16)
  * AWS Elastic Beanstalk
    * Added support for tagging environments, including two additional cmdlets Get-EBResourceTag (ListResourceTags API) and Update-EBResourceTag (UpdateResourceTags API).
  * AWS CodeCommit
    * Added support for the new DeleteBranch API with cmdlet Remove-CCBranch.
  * Amazon EC2 Container Registry
    * Added cmdlets to support new APIs for repository lifecycle policies. Lifecycle policies enable you to specify the lifecycle management of images in a repository. The configuration is a set of one or more rules, where each rule defines an action for Amazon ECR to apply to an image. This allows the automation of cleaning up unused images, for example expiring images based on age or status. A lifecycle policy preview API is provided as well, which allows you to see the impact of a lifecycle policy on an image repository before you execute it.
  * Elastic Load Balancing v2
    * Added cmdlets to support Server Name Indication (SNI). Server Name Indication (SNI) is an extension to the TLS protocol by which a client indicates the hostname to connect to at the start of the TLS handshake. The load balancer can present multiple certificates through the same secure listener, which enables it to support multiple secure websites using a single secure listener. Application Load Balancers also support a smart certificate selection algorithm with SNI. If the hostname indicated by a client matches multiple certificates, the load balancer determines the best certificate to use based on multiple factors including the capabilities of the client. The new cmdlets are Add-ELB2ListenerCertificate (AddListenerCertificates API), Get-ELB2ListenerCertificate (DescribeListenerCertificates API) and Remove-ELB2ListenerCertificate (RemoveListenerCertificates API).
  * AWS OpsWorks CM
    * [BREAKING CHANGE] The service response data from the DescribeNodeAssociationStatus API (Get-OWCMNodeAssociationStatus cmdlet) has been updated to include additional fields. This cmdlet therefore now emits the service response object to the pipeline.
  * Amazon Relational Database Service
    * Added new cmdlet Get-RDSValidDBInstanceModification (DescribeValidDBInstanceModifications API) enabling you to query what modifications can be made to your DB instance.
  * Amazon Simple Email Service
    * Added cmdlets to support new service APIs related to email template management and templated email sending operations.
  * Amazon EC2
    * Added new cmdlet Edit-EC2VpcTenancy (ModifyVpcTenancy API) enabling you to change the tenancy of your VPC from dedicated to default with a single API operation. For more details refer to the documentation for changing VPC tenancy.
  * AWS WAF
    * Added cmdlets to support regular expressions as match conditions in rules, and support for geographical location by country of request IP address as a match condition in rules.
  * AWS WAF Regional
    * Added cmdlets to support regular expressions as match conditions in rules, and support for geographical location by country of request IP address as a match condition in rules.

### 3.3.169.0 (2017-10-09)
  * AWS Lambda
    * Revised parameter sets and mandatory parameters for the Update-LMFunctionCode and Publish-LMFunction cmdlets based on user feedback. Also made the options for how the function code can be supplied the same.
  * Amazon AppStream
    * Added cmdlets to support new service APIs for APIs for managing and accessing image builders, and deleting images. The new cmdlets are: Get-APSImageBuilderList (DescribeImageBuilders API), New-APSImageBuilder (CreateImageBuilder API), New-APSImageBuilderStreamingURL (CreateImageBuilderStreamingURL API), Start-APSImageBuilder (StartImageBuilder API), Stop-APSImageBuilder (StopImageBuilder API), Remove-APSImage (DeleteImage API) and Remove-APSImageBuilder (DeleteImageBuilder API).
  * AWS CodeBuild
    * Added cmdlets New-CBWebHook (CreateWebHook API) and Remove-CBWebHook (DeleteWebHook API) to support the new service update for building GitHub pull requests.
  * Amazon Kinesis Analytics
    * Added support for schema discovery on objects in S3 and the ability to support configure input data preprocessing through Lambda with new cmdletsAdd-KINAApplicationInputProcessingConfiguration (AddApplicationInputProcessingConfiguration API) and Remove-KINAApplicationInputProcessingConfiguration (DeleteApplicationInputProcessingConfiguration API).
  * Amazon Route 53 Domains
    * Added cmdlet Test-R53DDomainTransferability to support the new CheckDomainTransferability API.

### 3.3.164.0 (2017-09-29)
  * Amazon EC2 Container Registry
    * Implemented a new helper cmdlet, Get-ECRLoginCommand, to return the login command to the pipeline for your default or specified registry or registries. This command is similar to the AWS CLI's 'ecr get-login' command.
  * AWS CloudFormation
    * Added cmdlet Update-CFNTerminationProtection to support the new UpdateTerminationProtection API enabling you to prevent a stack from being accidentally deleted. If you attempt to delete a stack with termination protection enabled, the deletion fails and the stack, including its status, remains unchanged. You can also enable termination protection on a stack when you create it using the new -EnableTerminationProtection parameter on the New-CFNStack cmdlet. Termination protection on stacks is disabled by default. After creation, you can set termination protection on a stack whose status is CREATE_COMPLETE, UPDATE_COMPLETE, or UPDATE_ROLLBACK_COMPLETE.
  * Amazon EC2
    * Added argument completion support for the Attribute parameter of the Edit-EC2ImageAttribute cmdlet, enabling tab completion of the possible attribute names. The service models this parameter as a simple string type, rather than a service enumeration, so tab completion was not available by default.
  * Amazon S3
    * Updated the Copy-S3Object cmdlet to support copying (downloading) multiple objects, identified by common key prefix, to a folder on the local file system. Previously the cmdlet could only process single object download to a file or folder. You may therefore use either Read-S3Object or Copy-S3Object to download one or multiple files from S3 to the local system (the functionality is the same). To copy files within S3, use the Copy-S3Object cmdlet.
  * Amazon Pinpoint
    * Added new cmdlets to support new APIs for new push notification channels: Amazon Device Messaging (ADM) and, for push notification support in China, Baidu Cloud Push.
    * Added new cmdlet for direct message deliveries to user IDs, enabling you to message an individual user on multiple endpoints.

### 3.3.161.0 (2017-09-22)
  * AWS CodePipeline
    * [BREAKING CHANGE]
      The output of the Get-CPPipeline cmdlet has changed as a result of a service update to the underlying GetPipeline API. The api response data now includes pipeline metadata info in addition to the pipeline declaration data (which was the previous output). To access the original data, use the '.Pipeline' member of the output object. To access the new metadata use the the '.Metadata' member of the output object.
  * AWS Greengrass
    * Added new cmdlet Reset-GGDeployment to support the new ResetDeployments API.
  * AWS CloudWatchLogs
    * Added support for associating LogGroups with KMS Keys with new cmdlets Register-CWLKmsKey (AssociateKmsKey API) and Unregister-CWLKmsKey (DisassociateKmsKey API).
  * Amazon EC2
    * Added support for new operations on Amazon FPGA Images (AFIs), within the same region and across multiple regions, with new cmdlets Get-EC2FpgaImageAttribute (DescribeFpgaImageAttribute API), Edit-EC2FpgaImageAttribute (ModifyFpgaImageAttribute API), Reset-EC2FpgaImageAttribute (ResetFpgaImageAttribute API),  Remove-EC2FpgaImage (DeleteFpgaImage API) and Copy-EC2FpgaImage (CopyFpgaImage API). AFI attributes include name, description and granting/denying other AWS accounts to load the AFI.
  * AWS Identity and Access Management
    * Added support for new APIs to submit a service-linked role deletion request and querying the status of the deletion with new cmdlets Remove-IAMServiceLinkedRole (DeleteServiceLinkedRole API) and Get-IAMServiceLinkedRoleDeletionStatus (GetServiceLinkedRoleDeletionStatus API).
  * Amazon Simple Email Service
    * Added support for new APIs enabling domain customization for tracking open and click events with new cmdlets New-SESConfigurationSetTrackingOption (CreateConfigurationSetTrackingOptions API), Update-SESConfigurationSetTrackingOption (UpdateConfigurationSetTrackingOptions API) and Remove-SESConfigurationSetTrackingOption (DeleteConfigurationSetTrackingOptions API).

### 3.3.158.0 (2017-09-17)
  * AWS Service Catalog
    - Added cmdlets to support the new CopyProduct and DescribeCopyProductStatus apis.

### 3.3.152.0 (2017-09-09)
  * Amazon CloudWatch Logs
    - Added cmdlets for managing resource polices
  * Amazon EC2
    - Added new cmdlets Update-EC2SecurityGroupRuleIngressDescription and Update-EC2SecurityGroupRuleEgressDescription
  * Amazon Lex Model Building Service
	- Added new cmdlet Get-LMBExport
  * Amazon Route53
    - Added new cmdlets to manage query logging config.

### 3.3.150.0 (2017-09-07)
  * AWS CodeBuild
    - Added new cmdlet RemoveCBBuildBatch
  * AWS CodeStar
    - Added new cmdlets Add-CSTTagsForProject, Remove-CSTTagsForProject and Get-CSTTagsForProject
  * Amazon GameLift
    - Added new cmdlets for setting up EC2 VPC peering.

### 3.3.143.0 (2017-08-24)
  * Amazon Simple Systems Management
    - Added new cmdlet, Get-SSMAssociationVersionList (ListAssociationVersions API), to returns versioned associations.
  * Amazon Kinesis Firehose
    - Added new cmdlet, Get-KINFKinesisStream (GetKinesisStream API).

### 3.3.140.0 (2017-08-16)
  * All services
    - Improved error messaging for 'name resolution failure' errors. The cmdlets now detail the endpoint they attempted to access and list some possible causes (for example, use of an availability zone instead of a region code).
  * Amazon Simple Systems Management
    - Added and updated cmdlets to support new service features. Maintenance Windows include the following changes or enhancements: New task options using Systems Manager Automation, AWS Lambda, and AWS Step Functions; enhanced ability to edit the targets of a Maintenance Window, including specifying a target name and description, and ability to edit the owner field; enhanced ability to edits tasks; enhanced support for Run Command parameters; and you can now use a --safe flag when attempting to deregister a target. If this flag is enabled when you attempt to deregister a target, the system returns an error if the target is referenced by any task. Also, Systems Manager now includes Configuration Compliance to scan your fleet of managed instances for patch compliance and configuration inconsistencies. You can collect and aggregate data from multiple AWS accounts and Regions, and then drill down into specific resources that aren't compliant.
  * AWS Elastic File System
    - Extended the New-EFSFileSystem cmdlet to support encrypted EFS file systems and specifying a KMS master key to encrypt it with.
  * AWS Storage Gateway
    - Extended the Remove-SGFileShare with new parameter -ForceDelete. If set, the file share is immediately deleted and all data uploads aborted immediately otherwise the share is not deleted until all uploads complete.
  * AWS CodeDeploy
    - Updated the New-CDDeploymentGroup and Update-CDDeploymentGroup cmdlets with parameters to support specifying Application Load Balancers in deployment groups, for both in-place and blue/green deployments.
  * Amazon Cognito Identity Provider
    - Updated and added cmdlets to support new features for Amazon Cognito User Pools that enable application developers to easily add and customize a sign-up and sign-in user experience, use OAuth 2.0, and integrate with Facebook, Google, Login with Amazon, and SAML-based identity providers.
  * Amazon EC2
    - Updated the New-EC2Address cmdlet with a new parameter, -Address, to support the new service feature enabling recovery of an elastic IP address (EIP) that was released.
  * AWS Elastic Beanstalk
    - Updated the Get-EBEnvironment cmdlet to support automatic pagination now that the underlying service API, DescribeEnvironments, can paginate output.
  * Amazon Route 53
    - Applied parameter alias "Id" to all cmdlets that accept a -HostedZoneId parameter to make pipeline scenarios more convenient, for example Get-R53HostedZoneList | Get-R53ResourceRecordSet. The change avoids the need for an intermediate 'Select-Object -ExpandProperty Id' clause in the pipeline to extract the zone ID from the output of Get-R53ResourceRecordSet to pass to Get-R53ResourceRecordSet.
  * Amazon S3
    - Extended the Write-S3Object cmdlet to accept tags for all operating modes. Previously tags could only be specified for single file or object uploads, now they can also be specified when uploading multiple objects from a folder hierarchy. The specified tags will be applied to all uploaded objects.
  * Amazon GameLift
    - New and updated cmdlets to support the Matchmaking Grouping Service, a new feature that groups player match requests for a given game together into game sessions based on developer configured rules.

### 3.3.133.0 (2017-08-04)
  * AWS CodeDeploy
    - Updated the New-CDDeployment, New-CDDeploymentGroup and Update-CDDeploymentGroup with parameters to support the use of multiple tag groups in a single deployment group (an intersection of tags) to identify the instances for a deployment. When you create or update a deployment group, use the new -Ec2TagSetList and -OnPremisesTagSetList parameters to specify up to three groups of tags. Only instances that are identified by at least one tag in each of the tag groups are included in the deployment group.
    - Revised some parameter names in the New-CDDeployment, New-CDDeploymentGroup and Update-CDDeploymentGroup to improve usability. Backwards compatible aliases were also applied to the affected parameters.
  * Amazon Pinpoint
    - Added support for the new app management APIs with new cmdlets Get-PINApp (GetApp API), Get-PINAppList (GetApps API), New-PINApp (CreateApp API) and Remove-PINApp (DeleteApp API).
  * AWS Config Service
    - Added new cmdlet, Get-CFGDiscoveredResourceCount (GetDiscoveredResourceCounts API), to returns the resource types and the number of each resource type in your AWS account.
  * Amazon Simple Systems Management
    - Added new cmdlet, Send-SSMAutomationSignal (SendAutomationSignal API). This API is used to send a signal to an automation execution to change the current behavior or status of the execution.

### 3.3.130.0 (2017-07-28)
  * Fixed issue using Kerberos authentication when obtaining SAML federated credentials. The authentication process failed for some users with a '401 unauthorized' exception.
  * Added new output format templates for the LogGroup and LogStream types for Amazon CloudWatch Logs.
  * AWS Directory Service
    - You can now improve the resilience and performance of your Microsoft AD directory by deploying additional domain controllers with the new Set-DSDomainControllerCount cmdlet (UpdateNumberofDomainControllers API). This cmdlet enables you to update the number of domain controllers you want for your directory. The new Get-DSDomainControllerList cmdlet (DescribeDomainControllers API) enables you to describe the detailed information of each domain controller of your directory. The output of the Get-Directory cmdlet was also extended to contain a new field,  'DesiredNumberOfDomainControllers'.
  * Amazon Kinesis
    - You can now encrypt your data at rest within an Amazon Kinesis Stream using server-side encryption using new cmdlets Start-KINStreamEncryption (StartStreamEncryption API) and Stop-KINStreamEncryption (StopStreamEncryption API). Server-side encryption via AWS KMS makes it easy for customers to meet strict data management requirements by encrypting their data at rest within the Amazon Kinesis Streams, a fully managed real-time data processing service.
  * Amazon EC2 Simple Systems Management
    - Added parameters to support patching for Amazon Linux, Red Hat and Ubuntu systems.
  * Amazon API Gateway
    - Added new cmdlets Get-AGGatewayResponse (GetGatewayResponse API), Get-AG-GatewayResponseList (GetGatewayResponses API), Update-AGGatewayResponse(UpdateGatewayResponse API), Remove-AGGatewayResponse (DeleteGatewayResponse API) and Write-AGGatewayResponse (PutGatewayResponse API) to support management of gateway responses.
  * AWS AppStream
    - Amazon AppStream 2.0 image builders and fleets can now access applications and network resources that rely on Microsoft Active Directory (AD) for authentication and permissions. This new feature allows you to join your streaming instances to your AD, so you can use your existing AD user management tools. New cmdlets to support this feature are Get-APSDirectoryConfigList (DescribeDirectoryConfigs API), New-APSDirectoryConfig (CreateDirectoryConfig API), Remove-APSDirectoryConfig (DeleteDirectoryConfig API) and Update-APSDirectoryConfig (UpdateDirectoryConfig API). New-APSFleet was updated with new parameters to support specifying domain information.
  * Application Discovery Service
    - Updated the Get-ADSExportTask and Start-ADSExportTask cmdlets to support filters, allow export based on per agent id.
  * Auto Scaling
    - Updated the Write-ASScalingPolicy to support a new type of scaling policy called target tracking scaling policies that you can use to set up dynamic scaling for your application.
  * Amazon Cognito Identity Provider
    - Updated the New-CGIPUserPool cmdlet to support configuration of user pools for email/phone based signup and sign-in.
  * Amazon EC2
    - X-ENI (or Cross-Account ENI) is a new feature that allows the attachment or association of Elastic Network Interfaces (ENI) between VPCs in different AWS accounts located in the same availability zone. With this new capability, service providers and partners can deliver managed solutions in a variety of new architectural patterns where the provider and consumer of the service are in different AWS accounts. To support this new feature the cmdlets Get-EC2NetworkInterfacePermission (DescribeNetworkInterfacePermissions API), New-EC2NetworkInterfacePermission (CreateNetworkInterfacePermission API) and Remove-EC2NetworkInterfacePermission (DeleteNetworkInterfacePermission) were added.
    - Added cmdlet Get-EC2ElasticGpu (DescribeElasticGpus API). Amazon EC2 Elastic GPUs allow you to easily attach low-cost graphics acceleration to current generation EC2 instances. With Amazon EC2 Elastic GPUs, you can configure the right amount of graphics acceleration to your particular workload without being constrained by fixed hardware configurations and limited GPU selection.
    - Added cmdlet New-EC2DefaultVpc (CreateDefaultVpc API). You no longer need to contact AWS support, if your default VPC has been deleted.
  * Amazon Elastic MapReduce
    - Added support for the ability to use a custom Amazon Linux AMI and adjustable root volume size when launching a cluster.
  * Amazon Lambda
    - Updated the Get-LMFunctionList cmdlet to support the latest Lambda@Edge enhancements.
  * AWS CloudFormation
    - Added and updated cmdlets to support StackSets, enabling users to manage stacks across multiple accounts and regions.

### 3.3.119.0 (2017-07-07)
  * AWS CloudFormation
    - Added new helper cmdlets Test-CFNStack, which tests a CloudFormation stack to determine if it's in a certain status and Wait-CFNStack which Pauses execution of the script until the desired CloudFormation Stack status has been reached or timeout occurs.
  * Added new format definitions for several types to improve output usability. The new formats take effect on objects of type:
    - Amazon.AutoScaling.Model.AutoScalingGroup
    - Amazon.AutoScaling.Model.LaunchConfiguration
    - Amazon.CloudFormation.Model.Stack
    - Amazon.CloudFormation.Model.StackEvent
    - Amazon.CloudWatch.Model.MetricAlarm
    - Amazon.CloudWatchEvents.Model.Rule
    - Amazon.EC2.Model.Instance
    - Amazon.IdentityManagement.Model.Role
    - Amazon.Lambda.Model.FunctionConfiguration
    - Amazon.SimpleSystemsManagement.Model.AssociationDescription
    - Amazon.WorkSpaces.Model.Workspace
  * AWS Service Catalog
    - Added support for the new TagOption library with new cmdlets Add-SCTagOptionToResource (AssociateTagOptionWithResource API), Get-SCTagOptionList (ListTagOptions API), Get-SCResourcesForTagOption (ListResourcesForTagOption API), Get-SCTagOption (DescribeTagOption API), New-SCTagOption (CreateTagOption API), Remove-SCTagOptionFromResource (DisassociateTagOptionFromResource API) and Update-SCTagOption (UpdateTagOption API).
  * Amazon Simple Systems Management
    - Added cmdlets for Resource Data Sync support with SSM Inventory. The new cmdlets are New-SSMResourceDataSync (CreateResourceDataSync API)- creates a new resource data sync configuration, Get-SSMResourceDataSync (ListResourceDataSync API) - lists existing resource data sync configurations, and Remove-SSMResourceDataSync (DeleteResourceDataSync API) - deletes an existing resource data sync configuration.
  * Amazon DynamoDB Accelerator (DAX)
    - Added cmdlet support for the new Amazon DynamoDB Accelerator (DAX) service. DAX is a managed caching service engineered for Amazon DynamoDB. DAX dramatically speeds up database reads by caching frequently-accessed data from DynamoDB, so applications can access that data with sub-millisecond latency. You can create a DAX cluster easily, using the AWS Management Console. With a few simple modifications to your code, your application can begin taking advantage of the DAX cluster and realize significant improvements in read performance. Cmdlets for the new service have the noun prefix 'DAX' and can be listed with the command 'Get-AWSCmdletName -Service DAX'.
  * Amazon CloudWatch Events
    - CloudWatch Events now allows different AWS accounts to share events with each other through a new resource called event bus. Event buses accept events from AWS services, other AWS accounts and Write-CWEEvent (PutEvents API) calls. Currently all AWS accounts have one default event bus. To send events to another account, customers simply write rules to match the events of interest and attach an event bus in the receiving account as the target to the rule. The new cmdlets are Get-CWEEventBus (DescribeEventBus API), Write-CWEPermission (PutPermission API) and Remove-CWEPermission (RemovePermission).
  * Amazon S3
    - Added new parameter -TagSet to the Write-S3Object and Copy-S3Object cmdlets to support specifying tags when uploading individual files to a bucket or copying objects within S3.
    - Updated cmdlets to switch to using path style addressing if the -EndpointUrl parameter is used. The default behavior without the parameter is to prefer virtual host style addressing unless the bucket name is not DNS compatible.
  * AWS Greengrass
    - Added cmdlet support for the new AWS Greengrass service. AWS Greengrass seamlessly extends AWS onto physical devices so they can act locally on the data they generate, while still using the cloud for management, analytics, and durable storage. AWS Greengrass ensures your devices can respond quickly to local events and operate with intermittent connectivity. AWS Greengrass minimizes the cost of transmitting data to the cloud by allowing you to author AWS Lambda functions that execute locally. Cmdlets for the service have the noun prefix 'GG' and can be listed with the command 'Get-AWSCmdletName -Service GG'.
  * AWS CloudWatch
    - Added cmdlets to support the new functionality for dynamically building and maintaining dashboards to monitor your infrastructure and applications. The new cmdlets are Get-CWDashboard (GetDashboard API), Get-CWDashboardList (ListDashboards API), Remove-CWDashboard (DeleteDashboard API) and Write-CWDashboard (PutDashboard API).

### 3.3.113.0 (2017-06-23)
  * Amazon Simple Systems Management
    - Added cmdlets Get-SSMParameter (GetParameter API), Get-SSMParametersByPath (GetParametersByPath API), Remove-SSMParameterCollection (DeleteParameters API).
    - Updated Get-SSMParameterList with a new parameter, -ParameterFilter, to support filtering of the results.
    - Updated Write-SSMParameter with a new parameter, -AllowedPattern, to support enforcement of the parameter value by applying a regex.
  * AWS WAF
    - Added new cmdlets to support creation, editing, updating, and deleting a new type of WAF rule with a rate tracking component. The new cmdlets are Get-WAFRateBasedRule (GetRateBasedRule API), Get-WAFRateBasedRuleList (ListRateBasedRules API), Get-WAFRateBasedRuleManagedKey (GetRateBasedRuleManagedKeys API), New-WAFRateBasedRule (CreateRateBasedRule API), Remove-WAFRateBasedRule (DeleteRateBasedRule API) and Update-WAFRateBasedRule (UpdateRateBasedRule API).
  * AWS WAF Regional
    - Added new cmdlets to support creation, editing, updating, and deleting a new type of WAF rule with a rate tracking component. The new cmdlets are Get-WAFRRateBasedRule (GetRateBasedRule API), Get-WAFRRateBasedRuleList (ListRateBasedRules API), Get-WAFRRateBasedRuleManagedKey (GetRateBasedRuleManagedKeys API), New-WAFRRateBasedRule (CreateRateBasedRule API), Remove-WAFRRateBasedRule (DeleteRateBasedRule API) and Update-WAFRRateBasedRule (UpdateRateBasedRule API).
  * Amazon WorkDocs
    - Added cmdlets Get-WDActivity (DescribeActivities API), Get-WDCurrentUser (GetCurrentUser API) and Get-WDRootFolder (DescribeRootFolders API) to retrieve the activities performed by WorkDocs users.
  * AWS CodePipeline
    - Added cmdlet Get-CDPipelineExecutionSummary to support the new ListPipelineExecutions API. This cmdlet enables you to retrieve summary information about the most recent executions in a pipeline, including pipeline execution ID, status, start time, and last updated time. You can request information for a maximum of 100 executions. Pipeline execution data is available for the most recent 12 months of activity.
    - Enabled automatic pagination of results for the Get-CPActionType and Get-CDPipelineList cmdlets.
  * Amazon Lightsail
    - The Get-LSOperationListForResource cmdlet now supports automatic pagination.
  * AWS Data Migration Service
    - Extended the Import-DSMCertificate cmdlet to support tagging.

### 3.3.109.0 (2017-06-19)
  * Amazon EC2
    - Added cmdlet Get-EC2FpgaImage to support the new DescribeFpgaImages API. This API enables customers to describe Amazon FPGA Images (AFIs) available to them, which includes public AFIs, private AFIs that you own, and AFIs owned by other AWS accounts for which you have load permissions.
  * Application AutoScaling
    - Updated the Write-AASScalingPolicy cmdlet to support automatic scaling of read and write throughput capacity for DynamoDB tables and global secondary indexes.
  * AWS IoT
    - [Breaking change] Updated the Get-IOTCertificate cmdlet to remove the parameter -CertificatePem, previously added in v3.3.104.0.
  * Amazon Relational Database Service
    - Updated the Copy-RDSDBSnapshot and Restore-RDSDBClusterToPointInTime cmdlets to support copy-on-write, a new Aurora MySQL Compatible Edition feature that allows users to restore their database, and support copy of TDE enabled snapshot cross region.
  * AWS Service Catalog
    - Added cmdlet Get-SCProvisionedProductDetail to support the new DescribeProvisionedProduct API.
    - Added parameter -ReturnCloudFormationTemplate to the Get-SCProvisioningArtifact cmdlet. This parameter maps to the 'Verbose' request property in the underlying DescribeProvisioningArtifact API call.

### 3.3.104.0 (2017-06-09)
  * Amazon S3
    - Fixed issue with the Remove-S3Object cmdlet not correctly binding version data when a collection of Amazon.S3.Model.S3ObjectVersion instances are piped to the -InputObject parameter. The examples for this cmdlet have been updated to illustrate usage scenarios deleting single and multiple objects.
  * AWS OpsWorks
    - Added support for resource tagging with new cmdlets Add-OPSResourceTag (TagResource API), Get-OPSResourceTag (ListTags API) and Remove-OPSResourceTag (UntagResource API).
  * AWS IoT
    - Updated the Get-IOTCertificate cmdlet with a new parameter -CertificatePem to support retrieving the description of a certificate with the certificate's PEM.
  * Amazon Pinpoint
    - Added cmdlets to support SMS Text and Email Messaging in addition to Mobile Push Notifications.
  * AWS Rekognition
    - Added cmdlets Get-REKCelebrityInfo (GetCelebrityInfo API) and Find-REKCelebrity (RecognizeCelebrities API).

### 3.3.99.0 (2017-06-05)
  * All services
    - Resolved issue causing the backwards-compatible aliases introduced recently to not be available on PowerShell v3 or v4 systems.
  * Amazon Kinesis Analytics
    - Added support for publishing error messages concerning application misconfigurations to to AWS CloudWatch Logs with new cmdlets Add-KINAApplicationCloudWatchLoggingOption (AddApplicationCloudWatchLoggingOption API) and Remove-KINAApplicationCloudWatchLoggingOption (DeleteApplicationCloudWatchLoggingOption API).
  * Amazon WorkDocs
    - Added support for managing tags and custom metadata on resources with new cmdlets New-WDCustomMetadata (CreateCustomMetadata API), New-WDLabel (CreateLabels API), Remove-WDCustomMetadata (DeleteCustomMetadata API) and Remove-WDLabel (DeleteLabels API).
    - Added new cmdlets to support adding and retrieving comments at the document level: Get-WDComment (DescribeComments API), New-WDComment (CreateComment API) and Remove-WDComment (DeleteComment API)

### 3.3.98.0 (2017-06-02)
  * Amazon Relational Database Service
    - Customers can now easily and quickly stop and start their DB instances using two new cmdlets, Start-RDSDBInstance (StartDBInstance API) and Stop-RDSDBInstance (StopDBInstance API).
  * AWS CodeDeploy
    - Added new cmdlet Get-CDGitHubAccountTokenNameList (ListGitHubAccountTokenNames API) to support improved GitHub account and repository connection management. You can now create and store up to 25 connections to GitHub accounts in order to associate AWS CodeDeploy applications with GitHub repositories. Each connection can support multiple repositories. You can create connections to up to 25 different GitHub accounts, or create more than one connection to a single account.
  * Amazon Cognito Identity Provider
    - Added cmdlets to support integration of external identity providers. The new cmdlets are Get-CGIPIdentityProvider (DescribeIdentityProvider API), Get-CGIPIdentityProviderByIdentifier (GetIdentityProviderByIdentifier API), Get-CGIPIdentityProviderList (ListIdentityProviders API), Get-CGIPUserPoolDomain (DescribeUserPoolDomain API), New-CGIPUserPoolDomain (CreateUserPoolDomain API), Remove-CGIPIdentityProvider (DeleteIdentityProvider API), Remove-CGIPUserPoolDomain (DeleteUserPoolDomain API) and Update-CGIPIdentityProvider (UpdateIdentityProvider API).

### 3.3.96.0 (2017-05-31)
  * All services
    - Cmdlet names for all services have been reviewed and those with plural nouns corrected to be singular to follow best practices. For more details see the blog post 'Updates to AWSPowerShell Cmdlet Names' on the AWS .NET Developer Blog https://aws.amazon.com/blogs/developer/category/net/. A new submodule, loaded automatically, contains backwards-compatible aliases to ensure current scripts will continue to work with the older cmdlet names.
  * Amazon Cloud Directory
    - Added support for Typed Links, enabling customers to create object-to-object relationships that are not hierarchical in nature. Typed Links enable customers to quickly query for data along these relationships. Customers can also enforce referential integrity using Typed Links, ensuring data in use is not inadvertently deleted.

### 3.3.95.0 (2017-05-26)
  * AWS AppStream
    - Updated the New-APSStack cmdlet with new parameter -StorageConnector, and the Update-APSStack with new parameters -StorageConnector and -DeleteStorageConnector, to add support for persistent user storage, backed by S3.
  * AWS Data Migration Service
    - Updated cmdlets to add support for using Amazon S3 and Amazon DynamoDB as targets for database migration, and using MongoDB as a source for database migration.
    - Added cmdlets to support event subscriptions: Edit-DMSEventSubscription (ModifyEventSubscription API), Get-DMSEvent (DescribeEvents API), Get-DMSEventCategory (DescribeEventCategories API), Get-DMSEventSubscription (DescribeEventSubscriptions API), New-DMSEventSubscription (CreateEventSubscription API), Remove-DMSEventSubscription (DeleteEventSubscription API).
    - Added cmdlet Restore-DMSTable to support the new ReloadTables API.

### 3.3.90.0 (2017-05-19)
  * AWSPowerShell.NetCore module
    - Added CompatiblePSEditions entry to the module manifest to mark the module as only being compatible with PowerShell Core environments.
  * Amazon Inspector
    - Added cmdlet Get-INSAssessmentReport to support the new GetAssessmentReport API. This new API adds the ability to produce an assessment report that includes detailed and comprehensive results of a specified assessment run.
  * Amazon Lightsail
    - Added cmdlet Set-LSInstancePublicPort to support the new PutInstancePublicPorts API, enabling a single request to both open and close public ports on an instance.
  * Amazon Athena
    - Added support for the new Amazon Athena service. Amazon Athena is an interactive query service that makes it easy to analyze data in Amazon S3 using standard SQL. Athena is serverless, so there is no infrastructure to manage, and you pay only for the queries that you run. Cmdlets for this service have the noun prefix 'ATH' and can be listed using the command 'Get-AWSCmdletName -Service ATH'.

### 3.3.86.0 (2017-05-12)
  * AWS Lambda
    - Added support for automatic pagination of all available results on service APIs that supported paging. Updated cmdlets are Get-LMAliasList, Get-LMEventSourceMappings, Get-LMFunctions and Get-LMVersionsByFunction.
  * Elastic Load Balancing
    - Added a new cmdlet, Get-ELBAccountLimit (DescribeAccountLimits API), enabling customers to describe their account limits, such as load balancer limit, target group limit etc.
  * Elastic Load Balancing v2
    - Added a new cmdlet, Get-ELB2AccountLimit (DescribeAccountLimits API), enabling customers to describe their account limits, such as load balancer limit, target group limit etc.
  * Amazon Lex Model Builder Service
    - Added support for new APIs with three new cmdlets: Remove-LMBBotVersion (DeleteBotVersion API), Remove-LMBIntentVersion (DeleteIntentVersion API) and Remove-LMBSlitTypeVersion (DeleteSlotTypeVersion API).
    - *Breaking Change*:  The -Version parameter has been removed from the Remove-LMBBot, Remove-LMBIntent and Remove-LMBSlotType cmdlets in favor of the specific cmdlets to delete versions.
  * Amazon Cognito Identity Provider
    - Added support for the new group support APIs with new cmdlets Add-CGIPUserToGroupAdmin (AdminAddUserToGroup API), Get-CGIPGroup (GetGroup API), Get-CGIPGroupList (ListGroups API), Get-CGIPGroupsForuserAdmin (AdminListGroupsForUser API), Get-CGIPUsersInGroup (ListUsersInGroup API), New-CGIPGroup (CreateGroup API), Remove-CGIPGroup (DeleteGroup API), Remove-CGIPuserFromGroup (AdminRemoveUserFromGroup API) and Update-CGIPGroup (UpdateGroup API).

### 3.3.84.0 (2017-05-05)
  * AWS Marketplace Entitlement Service
    - Added support for the new Marketplace Entitlement Service. The cmdlet noun prefix for this service is 'MES' and the cmdlets can be listed using the command 'Get-AWSCmdletName -Service MES'. Currently the service exposes one cmdlet, Get-MESEntitlement (GetEntitlements API).

### 3.3.83.0 (2017-05-01)
  * Amazon Kinesis Firehose
    - Fixed bug in handling of the -Text and -FilePath parameters that could cause a null reference exception.
  * AWS AppStream
    - Updated cmdlets to support the new 'Default Internet Access' service feature. This feature enables Internet access from AppStream 2.0 instances - image builders and fleet instances. Admins can enable this feature when creating an image builder or while creating/updating a fleet with the new -EnableDefaultInternetAccess parameter on the New-APSFleet and Update-APSFleet cmdlets.
  * Amazon Relational Database Service
    - Added the parameter -EnableIAMDatabaseAuthentication to the New-RDSDBCluster, Edit-RDSDBCluster, New-RDSDBInstance, Edit-RDSDBInstance, New-RDSDBInstanceReadReplica, Restore-RDSDBClusterFromS3, Restore-RDSDBClusterFromSnapshot, Restore-RDSDBClusterToPointInTime, Restore-RDSDBInstanceFromDBSnapshot and Restore-RDSDBInstanceToPointInTime cmdlets to enable authentication to your MySQL or Amazon Aurora DB instance using IAM authentication.
  * AWS CloudFormation
    - Added a new parameter, -ClientRequestToken, to the New-CFNStack, Remove-CFNStack, Resume-CFNUpdateRollback, Start-CFNChangeSet, Stop-CFNUpdateStack and Update-CFNStack cmdlets to enable supplying a customer-provided idempotency token to allow safely retrying operations.
  * AWS Import/Export Snowball
    - Added new parameter -ForwardingAddressId to the New-SNOWCluster, Update-SNOWCluster, New-SNOWJob and Update-SNOWJob cmdlets. The New-SNOWAddress cmdlet was updated with a new parameter -Address_IsRestricted to enable you to mark the primary address.

### 3.3.81.0 (2017-04-24)
  * All service API cmdlets
    - Cmdlets that invoke APIs on AWS services can now display a message stating the API and service endpoint or region the call is being dispatched to when the -Verbose switch is used. This can be used to help diagnose endpoint resolution failures.
  * AWS CodeStar
    - Added support for the new AWS CodeStar service. Cmdlets for the service have the noun prefix CST and can be listed with the command Get-AWSCmdletName -Service CST.
  * Amazon Lex Model Builder Service
    - Added support for the new Amazon Lex Model Builder Service. Cmdlets for the service have the noun prefix LMB and can be listed with the command Get-AWSCmdletName -Service LMB.
  * Amazon EC2
    - Added a new cmdlet, New-EC2FpgaImage (CreateFpgaImage API), to support creating an Amazon FPGA Image (AFI) from a specified design checkpoint (DCP).
  * AWS Rekognition
    - Added a new cmdlet, Find-REKModerationLabel (DetectModerationLabel API), to support detection of explicit or suggestive adult content in an image. The cmdlet a list of corresponding labels with confidence scores, as well as a taxonomy (parent-child relation) for each label.
  * AWS Identity and Access Management
    - Updated the New-IAMRole cmdlet with a new parameter, -Description, and added a new cmdlet Update-IAMRoleDescription (UpdateRoleDescription API) to support specifying a user-provided description for the role.
    - Added cmdlet New-IAMServiceLinkedRole (CreateServiceLinkedRole API). This cmdlet enables creation of a new role type, Service Linked Role, which works like a normal role but must be managed via services' control.
  * Amazon Lambda
    - Added new cmdlets to support the new service feature using tags to group and filter Lambda functions. The new cmdlets are Add-LMResourceTag (TagResource API), Get-LMResourceTag (ListTags API) and Remove-LMResourceTag (UntagResource API).
    - Added a new parameter, -TracingConfig_Mode, to the Update-LMFunctionConfiguration cmdlet to support integration with CloudDebugger service to enable customers to enable tracing for the Lambda functions and send trace information to the CloudDebugger service.
  * Amazon API Gateway
    - Extended the Get-AGDeployment, Get-AGResource and Get-AGResourceList cmdlets with a new parameter, -Embed, to support specifying embedded resources to retrieve.
  * Amazon Polly
    - Added a new parameter, -SpeechMarkType, to the Get-POLSpeech cmdlet to support defining the speech marks to be used in the returned text.
  * AWS DeviceFarm
    - Added support for the new service feature for deals and promotions with a new cmdlet, Get-DFOfferingPromotion (ListOfferingPromotions API).

### 3.3.76.0 (2017-04-11)
  * Amazon S3
    - Fixed issue with the Copy-S3Object cmdlet throwing 'access denied' error when attempting to copy objects between buckets in different regions.
  * Amazon EC2
    - Fixed issue with 'file not found' errors when using a relative path with the -PemFile parameter.
  * Amazon API Gateway
    - Added cmdlets to support the new request validators feature. The new cmdlets are: New-AGRequestValidator (CreateRequestValidator API), Get-AGRequestValidator (GetRequestValidator API), Get-AGRequestValidatorList (GetRequestValidators API), Update-AGRequestValidator (UpdateRequestValidator API) and Remove-AGRequestValidator (DeleteRequestValidator API). The Write-AGMethod cmdlet was also updated with a new parameter, -RequestValidatorId, enabling you to specify a request validator for the method.
  * AWS Batch
    - Updated the New-BATComputeEnvironment cmdlet with a new parameter, -ComputeResources_ImageId, to support specifying an AMI for MANAGED Compute Environment.
  * AWS GameLift
    - Updated the New-GMLGameSessionQueue and Update-GMLGameSessionQueue cmdlets with a new parameter, -PlayerLatencyPolicy, to enable developers to specify a maximum allowable latency per queue.
  * AWS OpsWorks
    - Updated the New-OPSLayer and Update-OPSLayer cmdlets with new parameters -CloudWatchLogsConfiguration_Enabled and -CloudWatchLogsConfiguration_LogStream to attaching a Cloudwatch Logs agent configuration to OpsWorks Layers. OpsWorks will then automatically install and manage the CloudWatch Logs agent on the instances that are part of the OpsWorks Layer.

### 3.3.75.0 (2017-04-10)
  * Fixed issue with cmdlets taking a long time to return when no region was specified.
  * Fixed issue with the argument completer for the -ProfileName parameter issuing a warning message about use of a deprecated switch on the Get-AWSCredentials cmdlet.
  * Amazon Redshift
    - Added a new cmdlet, Get-RSClusterCredential, to support the new GetClusterCredentials API.

### 3.3.74.0 (2017-04-07)
  * AWS CloudWatch
    - Updated the Write-CWMetricAlarm cmdlet with support for new alarm configurations for missing data treatment and percentile metrics. Missing data can now be treated as alarm threshold breached, alarm threshold not breached, maintain alarm state and the current default treatment. Percentile metric alarms are for alarms that can trigger unnecessarily if the percentile is calculated from a small number of samples. The new rule can treat percentiles with low sample counts as same as missing data. If the first rule is enabled, the same treatment will be applied when an alarm encounters a percentile with low sample counts.
  * Amazon ElastiCacheElastiCache
    - Added support for testing the Elasticache Multi-AZ feature with Automatic Failover. This support includes a new parameter, -NodeGroupId, for the Edit-ECReplicationGroup cmdlet and -ShowCacheClustersNotInReplicationGroup added to Get-ECCacheCluster and a new cmdlet, Test-ECFailover (TestFailover API).
  * Amazon Lex
    - Added a new cmdlet, Send-LEXContent, to support the new PostContent API.

### 3.3.71.0 (2017-03-31)
  * AWS Resource Groups Tagging API
    - Added support for the new Resource Groups Tagging API service. Cmdlets for the service have the noun prefix 'RGT' and can be listed using the command 'Get-AWSCmdletName -Service RGT': Add-RGTResourceTag (TagResources API), Get-RGTResource (GetResources API), Get-RGTTagKey (GetTagKeys API), Get-RGTTagValue (GetTagValues API) and Remove-RGTResourceTag (UntagResources API).
  * AWS Storage Gateway
    - Added a new cmdlet, Invoke-CacheRefresh (RefreshCache API), and extended the New-SGNFSFileShare and Update-SGNFSFileShare cmdlets with new parameters -ReadOnly and -Squash.
  * Amazon Cloud Directory
    - Updated the Get-CDIRObjectAttribute cmdlet with two new parameters, -FacetFilter_FacetName and -FacetFilter_SchemaArn, to support the new service feature enabling filtering by facet.

### 3.3.69.0 (2017-03-29)
  * AWS Batch
    - Added parameters to support specifying retry strategy for the Register-BATJobDefinition and Submit-BATJob cmdlets. The parameter, -RetryStrategy_Attempt, accepts a numeric value for attempts. This is the number of non successful executions before a job is considered FAILED. In addition, the JobDetail object returned by other cmdlets now has an attempts field and shows all execution attempts.
  * Amazon EC2
    - Added support for tagging Amazon EC2 Instances and Amazon EBS Volumes at the time of their creation with the addition of a parameter, -TagSpecification, to the New-EC2Volume and New-EC2Instance cmdlets. By tagging resources at the time of creation, you can eliminate the need to run custom tagging scripts after resource creation. In addition, you can now set resource-level permissions on the CreateVolume, CreateTags, DeleteTags, and the RunInstances APIs. This allows you to implement stronger security policies by giving you more granular control over which users and groups have access to these APIs. You can also enforce the use of tagging and control what tag keys and values are set on your resources. When you combine tag usage and resource-level IAM policies together, you can ensure your instances and volumes are properly secured upon creation and achieve more accurate cost allocation reporting. These new features are provided at no additional cost.
  * Added the legacy alias Get-SSMParameterNameList (an alias for Get-SSMParameterValue) to the AliasesToExport collection in the module manifest so that the alias is available without needing to explicitly import the module.

### 3.3.67.0 (2017-03-24)
  * AWS Application Discovery Service
    - Added two new cmdlets Get-ADSExportTask (DescribeExportTasks API) and Start-ADSExportTask (StartExportTask API) to support the new service feature for exporting configuration options.
  * AWS Lambda
    - Added argument completion support for the new Node.js v6.10 runtime option.

### 3.3.65.0 (2017-03-21)
  * Amazon S3
    - Fixed issue with the Write-S3Object cmdlet reporting error "The difference between the request time and the current time is too large" when uploading very large (> 100GB) files.
  * Amazon Pinpoint
    - Added cmdlets Get-PINEventStream (GetEventStream API), Remove-PINEventStream (DeleteEventStream API) and Write-PINEventStream (PutEventStream API) to support publishing of raw app analytics and campaign events data as events streams to Kinesis and Kinesis Firehose. The Update-PINSegment and New-PINSegment cmdlets were updated with a new parameter, -Dimensions_UserAttribute, to support the ability to segment endpoints by user attributes in addition to endpoint attributes.
  * All cmdlet names are now enumerated into the module manifest enabling tab-completion of command names without needing to explicitly import the module into the environment.

### 3.3.64.2 (2017-03-17)
  * Fixed issue with Initialize-AWSDefaults reporting 'source profile not found' error when run to load credentials from the 'default' profile.

### 3.3.64.1 (2017-03-15)
  * Added support for reading and writing credential profiles to both the AWS .NET SDK encrypted credential store and the shared credential store used by other AWS SDKs and tools. Previously credentials could only be read from both stores, and written to only the SDK's encrypted store.
  * Added support for new credential profile types supporting cross-account IAM roles (aka 'assume role' profiles) and credentials requiring use of an MFA. For more details on the updated credential support see the blog post announcing the update at https://aws.amazon.com/blogs/developer/aws-sdk-dot-net-credential-profiles/ and the credential setup topics in the user guide at http://docs.aws.amazon.com/powershell/latest/userguide/pstools-getting-started.html.
  * AWS Device Farm
    - Added support for network shaping. Network shaping allows users to simulate network connections and conditions while testing their Android, iOS, and web apps with AWS Device Farm. The update includes new cmdlets Get-DFNetworkProfile (GetNetworkProfile API), Get-DFNetworkProfileList (ListNetworkProfiles API), New-DFNetworkProfile (CreateNetworkProfile API), Remove-DFNetworkProfile (DeleteNetworkProfile API) and Update-DFNetworkProfile (UpdateNetworkProfile API).

### 3.3.62.0 (2017-03-13)
  * Fixed issue with Set-AWSSamlRoleProfile incorrectly formatting user identities specified in email format. The identity was being stored in the role profile with as \email@domain.com (domain\userid format) but with empty domain. The leading \ then had to be removed by the user each time a password demand was issued.
  * Amazon Relation Database Service
    - Updated the Copy-RDSDBClusterSnapshot and New-RDSDBCluster cmdlets to add support for using encrypted clusters as cross-region replication masters and encrypted cross region copy of Aurora cluster snapshots.
  * Amazon Simple Systems Management
    - Added help examples for all SSM cmdlets.
  * Amazon WorkDocs
    - Added support for the Amazon WorkDocs service. The Amazon WorkDocs SDK provides full administrator level access to WorkDocs site resources, allowing developers to integrate their applications to manage WorkDocs users, content and permissions programmatically. Cmd;lets for this service have the noun prefix 'WD' and can be listed using the command 'Get-AWSCmdletName -Service WD'.
  * AWS Organizations
    - *Breaking Change*
    Updated cmdlets to fix an issue with the wrong service model version being used. This may be a breaking change for some users. The Enable-ORGFullControl cmdlet has been removed, and the Enable-ORGAllFeatures cmdlet added in the corrected.
  * Amazon Cloud Directory
    - Added a new cmdlet, Get-CDIRObjectParentPath, to enable retrieval of all available parent paths for any type of object (a node, leaf node, policy node, and index node) in a hierarchy.
  * Amazon API Gateway
    - Updated the New-AGDomainName cmdlet to add support for ACM certificates on custom domain names. Both Amazon-issued certificates and uploaded third-part certificates are supported.
  * Amazon Elastic MapReduce
    - Added support for instance fleets with new cmdlets Add-EMRInstanceFleet (AddInstanceFleet API), Edit-EMRInstanceFleet (ModifyInstanceFleet API) and Get-EMRInstanceFleets (ListInstanceFleets API).

### 3.3.58.0 (2017-03-06)
  * Amazon EC2
    - Added two new keys, WINDOWS_2016_CORE and WINDOWS_2012R2_CORE, to the Get-EC2ImageByName cmdlet, to support retrieving the latest Windows Server 2016 Core and Windows Server 2012R2 Core AMIs.
  * AWS OpsWorks
    - OpsWorks for Chef Automate has added a new field "AssociatePublicIpAddress" to the New-OWCMServer cmdlet, "CloudFormationStackArn" to the Server model and "TERMINATED" server state.

### 3.3.57.0 (2017-02-27)
  * AWS Organizations
    - Added support for the new AWS Organizations service. AWS Organizations is a web service that enables you to consolidate your multiple AWS accounts into an organization and centrally manage your accounts and their resources. Cmdlets for this service have the noun prefix ORG and can be listed using the command Get-AWSCmdletName -Service ORG.
  * Amazon MTurk Service
    - Added support for Amazon Mechanical Turk (MTurk), a web service that provides an on-demand, scalable, human workforce to complete jobs that humans can do better than computers, for example, recognizing objects in photos. Cmdlets for this service have the noun prefix MTR and can be listed using the command Get-AWSCmdletName -Service MTR.
  * Amazon Elasticsearch
    - Added three cmdlets to support new APIs for exposing limits imposed by Elasticsearch. The new cmdlets are Get-ESInstanceTypeLimit (DescribeElasticsearchInstanceTypeLimits API), Get-ESInstanceTypeList (ListElasticsearchInstanceTypes API) and Get-ESVersionList (ListElasticsearchVersions API).
  * Amazon DynamoDB
    - Added two new cmdlets to support the new 'Time to Live' APIs. The new cmdlets are Get-DDBTimeToLive (DescribeTimeToLive API) and Update-DDBTimeToLive (UpdateTimeToLive API).

### 3.3.56.0 (2017-02-23)
  * Fixed issue with the Set-AWSSamlRoleProfile cmdlet reporting a null reference exception.
  * (NetCore module only) Fixed issue with cmdlet help not being available due to a misnamed help file.
  * Amazon EC2
    - Added -InstanceType completion support for the new I3 instance type.

### 3.3.55.0 (2017-02-22)
  * Amazon EC2
    - Updated the Register-EC2Image cmdlet with a new parameter, -BillingProduct.
  * Amazon GameLift
    - Added cmdlet support for the new service feature enabling developers to configure global queues for creating GameSessions and to allow PlayerData on PlayerSessions to store player-specific data.
  * AWS Elastic Beanstalk
    - Added support for creating and managing custom platforms with environments.
    - *Breaking Change*
      The update for custom platform support changed the service response data for the DescribeConfigurationOptions API (Get-EBConfigurationOption cmdlet) to include an additional field, PlatformArn. Previously the cmdlet was able to pipe the configuration options collection to the pipeline. In this release the output from the cmdlet has changed so that the entire service response data (Amazon.ElasticBeanstalk.Model.DescribeConfigurationOptionsResponse) is emitted to the pipeline, not the collection of configuration options. To obtain the previous behavior, add a select clause to your code, for example: 'Get-EBConfigurationOption ...parameters... | select -expandproperty Options | ...'.

### 3.3.53.1 (2017-02-20)
  * Fixed bug in Get-AWSCredentials. When the -ListProfiles switch was used the collection of profile names was not correctly enumerated to the pipeline.

### 3.3.53.0 (2017-02-17)
  * AWS Direct Connect
    - Added cmdlet support for the new ability for Direct Connect customers to take advantage of Link Aggregation (LAG). This allows you to bundle many individual physical interfaces into a single logical interface, referred to as a LAG. This makes administration much simpler as the majority of configuration is done on the LAG while you are free to add or remove physical interfaces from the bundle as bandwidth demand increases or decreases. A concrete example of the simplification added by LAG is that customers need only a single BGP session as opposed to one session per physical connection.

### 3.3.52.0 (2017-02-16)
  * This version was only distributed in the downloadable AWS Tools for Windows msi installer.
  * AWS Config
    - Updated the Write-CFGEvaluations cmdlet with a new parameter, -TestMode. Set the TestMode parameter to true in your custom rule to verify whether your AWS Lambda function will deliver evaluation results to AWS Config. No updates occur to your existing evaluations, and evaluation results are not sent to AWS Config.

### 3.3.51.0 (2017-02-15)
  * Amazon EC2
    - Added cmdlet support for the new Modify Volumes APIs. This includes two new cmdlets, Edit-EC2Volume (ModifyVolume API) and Get-EC2VolumeModification (DescribeVolumesModification API).
  * AWS Key Management Service
    - Added support for the new tagging apis with three new cmdlets: Add-KMSResourceTag (TagResource API), Get-KMSResourceTag (ListResourceTags API) and Remove-KMSResourceTag (UntagResource API). The New-KMSKey cmdlet was also extended to support a -Tag parameter.
  * AWS Storage Gateway
    - Updated the New-SGNFSFileShare and Update-SGNFSFileShare cmdlets to support acess to objects in S3 as files on a Network File System (NFS) mount point. Customers can restrict the clients that have read/write access to the gateway by specifying the list of clients as a list of IP addresses or CIDR blocks to the new -ClientList parameter on these cmdlets.

### 3.3.48.0 (2017-02-09)
  * Amazon EC2
    - Fixed issue with the Get-EC2Instance cmdlet not accepting instance IDs that were supplied as PSObject types. The cast to string on the supplied PSObject(s) failed, resulting in the cmdlet listing all instances on output, not the requested instances.
    - Added support for a new feature enabling users to associate an IAM profile with running instances that were not launched with a profile. The new cmdlets are Get-EC2IamInstanceProfileAssociation (DescribeIamInstanceProfileAssociations API), Register-EC2IamInstanceProfile (AssociateIamInstanceProfile API), Set-EC2IamInstanceProfileAssociation (ReplaceIamInstanceProfileAssociation API) and Unregister-EC2IamInstanceProfileAssociation (DisassociateIamInstanceProfileAssociation API).

### 3.3.47.0
  * This build was not released.

### 3.3.46.0 (2017-02-07)
  * Fixed issue with the Get-AWSCredentials cmdlet reporting a 'could not find part of path' error attempting to access the %userprofile%\.aws folder (default folder of the shared credentials file) when run on systems where the shared credential file was not in use and the folder did not exist. The cmdlet should instead have tested for folder existence and silently skipped over it when not present.
  * Fixed issue with using SAML credential profiles in regions where a region-specific Security Token Service endpoint is required, eg China (Beijing). After successfully authenticating with the ADFS endpoint the tools request temporary credentials from STS and this call was always directed against the global sts.amazonaws.com endpoint, not the regional endpoint, thus failing to obtain credentials. To enable SAML role profiles to specify that a regional endpoint must be used, the Set-AWSSAMLRoleProfile cmdlet has been extended with a new parameter, -STSEndpointRegion, that can be used to specify the region system name (eg -STSEndpointRegion cn-north-1), resulting in the correct STS endpoint being used to obtain credentials.
  * Amazon Lex
    - Added support for the new Amazon Lex service, for building conversational interactions into any application using voice or text. Cmdlets for this service have the noun prefix 'LEX' and can be viewed using the command 'Get-AWSCmdletName -Service LEX'. Currently the service contains a single cmdlet, Send-LEXText, corresponding to the service's PostText API.

### 3.3.45.0 (2017-01-26)
  * Amazon Cloud Directory
    - Added support for the new Amazon Cloud Directory service, a highly scalable, high performance, multi-tenant directory service in the cloud. Its web-based directories make it easy for you to organize and manage application resources such as users, groups, locations, devices, policies, and the rich relationships between them. Cmdlets for this service have the noun prefix 'CDIR' and can be viewed using the command 'Get-AWSCmdletName -Service CDIR'.
  * AWS CodeDeploy
    - Updated cmdlets to support the new service feature enabling blue/green deployments. In a blue/green deployment, the current set of instances in a deployment group is replaced by new instances that have the latest application revision installed on them. After traffic is rerouted behind a load balancer to the replacement instances, the original instances can be terminated automatically or kept running for other uses. In addition to additional parameters on existing cmdlets, two new cmdlets were added: Resume-CDDeployment (ContinueDeployment API) and Skip-CDWaitTimeForInstanceTermination (SkipWaitTimeForInstanceTermination API).
  * Amazon EC2
    - Updated the Request-EC2SpotFleet cmdlet with a new parameter, -SpotFleetRequestConfig_ReplaceUnhealthyInstance, to support instance health check functionality to replace EC2 spot fleet instances with new ones.
  * Amazon Relational Database Service
    - Updated cmdlets to support the new Snapshot Engine version upgrade. This update includes a new cmdlet, Edit-RDSDBSnapshot, mapped to the ModifyDBSnapshot API.

### 3.3.44.0 (2017-01-25)
  * Amazon WorkSpaces
    - *Breaking Change* Fixed issue with incorrect mapping of Stop-WKSWorkspace to the TerminateWorkspaces API, which could cause data loss if termination was not expected. A new cmdlet, Remove-WKSWorkspace, has been added which maps to the TerminateWorkspaces API. Stop-WKSWorkspace has been mapped to the StopWorkspaces API and the existing -Request parameter changed to accept types of Amazon.Workspaces.Model.StopRequest (previously it accepted Amazon.Workspaces.Model.TerminateRequest). For users employing New-Object to construct the parameters, this is a breaking change (customers known to have used this cmdlet in the past few months have been contacted about this change).
    - In addition to introducing a new cmdlet and correcting the mapping we have also taken steps to improve the usability of the cmdlets related to manipulating workspaces (Start-WKSWorkspace, Stop-WKSWorkspace, Remove-WKSWorkspace, Reset-WKSWorkspace and Rebuild-WKSWorkspace). These cmdlets all now support an additional –WorkspaceId parameter. This parameter accepts an array of strings that are the IDs of the workspaces to operate on, improving pipeline usability. Examples have been added to the cmdlet help to show the new simplified usages.
  * Elastic Load Balancing V2
    - Application Load Balancers now support native Internet Protocol version 6 (IPv6) in an Amazon Virtual Private Cloud (VPC). With this ability, clients can now connect to the Application Load Balancer in a dual-stack mode via either IPv4 or IPv6. The New-ELB2LoadBalancer was extended with a new parameter, -IpAddressType, to support this new feature and one new cmdlet was added, Set-ELB2IpAddressType (SetIpAddressType API).
  * Amazon Relational Database Service
    - Extended the New-RDSDBInstanceReadReplica cmdlet with parameters -KmsKeyId. -PresignedUrl and -SourceRegion to support cross-region read replica copying.

### 3.3.43.0 (2017-01-24)
  * AWS CodeCommit
    - Added new cmdlets Get-CCBlob (GetBlob API) and Get-CCDifferenceList (GetDifferences API) to support the new service feature for viewing differences between commits. The existing Get-CCBranchList and get-CCRepositoryList cmdlets have also been extended to support automatic pagination of all results now that it is supported on these API calls by the service.
  * Amazon EC2 Container Service (ECS)
    - Added a new cmdlet, Update-ECSContainerInstancesState (UpdateContainerInstancesState API) to support the new service feature enabling state for container instances that can be used to drain a container instance in preparation for maintenance or cluster scale down.

### 3.3.42.0 (2017-01-20)
  * This version was only distributed in the downloadable AWS Tools for Windows msi installer.
  * AWS Health
    - Updated the parameter documentation for several cmdlets to match latest service documentation updates.
  * AWS Certificate Manager
    - Updated the parameter documentation for several cmdlets to match latest service documentation updates.

### 3.3.41.0 (2017-01-19)
  * This version was only distributed in the downloadable AWS Tools for Windows msi installer.
  * Amazon EC2
    - Extended the Request-EC2SpotInstance cmdlet with a new parameter, Placement_Tenacy, to support the new service feature for dedicated tenancy, providing the ability to run Spot instances single-tenant manner on physically isolated hardware within a VPC to satisfy security, privacy, or other compliance requirements. Dedicated Spot instances can be requested using RequestSpotInstances and RequestSpotFleet.
  * AWS Health
    - Updated the parameter documentation for several cmdlets to match latest service documentation updates.

### 3.3.40.0 (2017-01-18)
  * This release contained only changes in the underlying AWS SDK for .NET and was not published to the gallery.

### 3.3.39.0 (2017-01-17)
  * Amazon DynamoDB
    - Added support for the new APIs to tag tables and indexes with three new cmdlets: Add-DDBResourceTag (TagResource API), Get-DDBResourceTag (ListTagsOfResource API) and Remove-DDBResourceTag (UntagResource API).

### 3.3.38.0 (2017-01-16)
  * AWS Cost and Usage Report
    - Added support for the new AWS Cost and Usage Report service. This service API allows you to enable and disable the Cost & Usage report, as well as modify the report name, the data granularity, and the delivery preferences. Cmdlets for this service have the noun prefix 'CUR' and can be viewed by running the command 'Get-AWSCmdletName -Service CUR'.

### 3.3.37.2 (2017-01-12)
  * Amazon EC2
    - Added new keys to the Get-EC2ImageByName cmdlet to support querying for Windows Server 2012R2 images with SQL Server 2016.

### 3.3.37.1 (2017-01-09)
  * AWS Health
    - Added support for the AWS Health service. The AWS Health API serves as the primary source for you to receive personalized information related to your AWS infrastructure, guiding your through scheduled changes, and accelerating the troubleshooting of issues impacting your AWS resources and accounts. Cmdlets for the service have the noun prefix 'HLTH' and can be viewed using the command 'Get-AWSCmdletName -Service HLTH'.
  * Get-AWSCmdletName
    - Fixed bug when invoking the cmdlet with no parameters. Instead of outputting the set of all service cmdlets an error 'Value cannot be null' was displayed.

### 3.3.37.0 (2017-01-05)
  * AWS Lambda
    - Updated the Publish-LMFunction and Update-LMFunctionCode with new parameters to support the latest features in these APIs (e.g. environment variable support, VPC configuration etc) and made both cmdlets consistent with support for specifying the function code to upload using an object in an Amazon S3 bucket. The parameter names used in both cmdlets have also been made consistent, with aliases applied for backwards compatibility. Help examples for the two cmdlets have also been added.
  * AWS Marketplace Commerce Analytics
    - Added argument completion support for the new data set disbursed_amount_by_instance_hours, with historical data available starting 2012-09-04. New data is published to this data set every 30 days.

### 3.3.36.0 (2016-12-29)
  * AWS CodeDeploy
    - Updated the Register-CDOnPremiseInstance cmdlet with a new parameter, -IamSessionArn, to support association of on-premise instances with IAM sessions.
  * Amazon EC2 Container Service (ECS)
    - Added and updated cmdlets to support the new service capability enabling placement of tasks on container instances and setting attributes on ECS resources. The new cmdlets are Get-ECSAttributeList (ListAttributes API), Remove-ECSAttribute (DeleteAttributes API) and Write-ECSAttribute (PutAttributes API).
  * Amazon Simple Systems Management
    - To better reflect its operation, the cmdlet Get-SSMParameterNameList has been renamed to Get-SSMParameterValue. An aliases submodule (AWSPowerShellLegacyAliases.psm1) has been added to the module that sets up a backwards compatible alias on module load for scripts that rely on the original cmdlet name.

### 3.3.35.0 (2016-12-22)
  * Amazon API Gateway
    - Added cmdlets to support the new service feature for generating SDKs in more languages. This update introduces two new operations used to dynamically discover these SDK types and what configuration each type accepts. The new cmdlets are: Get-AGSdkType (GetSdkType API), Get-AGSdkTypeList (GetSdkTypes API). In addition the existing cmdlet Write-AGMethod has been updated with a new parameter, -OperationName, enabling users to set a human-friendly operation identifier for the method.
  * AWS Identity and Access Management
    - Added new cmdlets to support service-specific credentials for IAM users. This makes it easier to onboard AWS CodeCommit customers. Service-specific credentials are username/password credentials that work with a single service (currently only AWS CodeCommit). The new cmdlets are: Get-IAMServiceSpecificCredentialList (ListServiceSpecificCredentials API), New-IAMServiceSpecificCredential (CreateServiceSpecificCredential API), Remove-IAMServiceSpecificCredential (DeleteServiceSpecificCredential API), Reset-IAMServiceSpecificCredential (ResetServiceSpecificCredential API) and Update-IAMServiceSpecificCredential (UpdateServiceSpecificCredential API).
  * AWS Elastic Beanstalk
    - Added a new cmdlet, Update-EBApplicationResourceLifecycle, to support the new UpdateApplicationVersionResourceLifecycle API.

### 3.3.34.0 (2016-12-22)
  * Amazon EC2 Container Registry
    - Updated cmdlets to support the service update to implement Docker Image Manifest V2, Schema 2 providing the ability to use multiple tags per image, support for storing Windows container images, and compatibility with the Open Container Initiative (OCI) image format. With this update, customers can also add tags to an image via PutImage and delete tags using BatchDeleteImage.
  * Amazon Relation Database Service
    - Updated the Copy-RDSDBSnapshot cmdlet to add support for cross region encrypted snapshot copying.

### 3.3.33.1 (2016-12-20)
  (This version was only released as part of the combined AWS SDK and Tools Windows Installer, and not published to the PowerShell Gallery.)
  * Bug fix: Fixed issue with finding shared credentials file in user's home folder via %USERPROFILE% on some systems.
  * Amazon Kinesis Firehose
    - Extended the New-KINFDeliveryStream and Update-KINFDestination cmdlets to support the new service capability enabling users to process and modify records before Amazon Firehose delivers them to destinations.
  * Amazon Route53
    - Added enumeration completion for the new eu-west-2 and ca-central-1 regions.
  * AWS Storage Gateway
    - Added cmdlets to support the new File Gateway mode. File gateway is a new mode that supports a file interface into S3, alongside the current block-based volume and VTL storage. File gateway combines a service and virtual software appliance, enabling you to store and retrieve objects in Amazon S3 using industry standard file protocols such as NFS. The software appliance, or gateway, is deployed into your on-premises environment as a virtual machine (VM) running on VMware ESXi. The gateway provides access to objects in S3 as files on a Network File System (NFS) mount point. The new cmdlets are Get-SGFileShareList (ListFileShares API), Get-SGNFSFileShareList (DescribeNFSFileShares API), New-SGNFSFileShare (CreateNFSFileShare API), Remove-SGFileShare (DeleteFileShare API) and Update-SGNFSFileShare (UpdateNFSFileShare API). The existing New-SGCachediSCSIVolume cmdlet was also updated with a new parameter, -SourceVolumeARN, as part of this feature.

### 3.3.32.0 (2016-12-19)
  * AWS Application Discovery Service
    - Added cmdlets to support new APIs for grouping discovered servers into Applications with summary and neighbor data. Added support for additional filters enabled on the service's ListConfigurations and DescribeAgents APIs.

### 3.3.31.1 (2016-12-16)
  * Added support for the new EU West (London) region, eu-west-2.
  * AWS Batch
    - Added support for the new AWS Batch service, a batch computing service that lets customers define queues and compute environments and then submit work as batch jobs. Cmdlets for the service have the noun prefix 'BAT' and can be viewed using the command 'Get-AWSCmdletName -Service BAT'.
  * Amazon Simple System Management
    - Added new cmdlets to support the new patch baseline and patch compliance apis.
  * Amazon CloudWatch Logs
    - Added support for associating log groups with tags with new cmdlets: Add-CWLLogGroupTag (TagLogGroup API), Get-CWLLogGroupTag (ListTagLogGroups API) and Remove-CWLLogGroupTag (UntagLogGroup API). New-CWLLogGroup was also extended to support adding tags during group creation.
    - Extended the Write-CWLSubscriptionFilter cmdlet with a new parameter, -Distribution, enabling grouping of log data to an Amazon Kinesis stream in a more random distribution than the default grouping by log stream.
  * Amazon Relation Database Service
    - Added support for SSL enabled Oracle endpoints and task modification.
  * Bug fix: Fixed null reference error reported by Get-AWSCmdletName when run on latest versions of PowerShell 6 alpha.

### 3.3.30.0 (2016-12-09)
  * Added support for the new Canada (Central) region, ca-central-1.
  * AWS WAF Regional
    - Added support for the new AWS WAF Regional service, enabling customers to use AWS WAF directly on Application Load Balancers in a VPC within available regions to protect their websites and web services from malicious attacks such as SQL injection, Cross Site Scripting, bad bots, etc. Cmdlets for this service have the noun prefix 'WAFR' and can be viewed using the command 'Get-AWSCmdletName -Service WAFR'.
  * Amazon CloudFront
    - Updated the New-CFDistribution, New-CFDistributionWithTag and Update-CFDistribution cmdlets with support for adding Lambda function associations to cache behaviors.
  * Amazon Relation Database Service
    - Updates in the underlying AWS SDK for .NET support for Amazon RDS enable you to now specify cluster creation time in the DBCluster API cmdlets.

### 3.3.29.0 (2016-12-08)
  * Amazon S3
    - Added support for specifying object version id in the new object tagging cmdlets. If not supplied the tag values are set on or retrieved from the latest object version.
  * AWS Config Service
    - Updated the argument completers for the service to recognize the new support for Redshift Cluster, ClusterParameterGroup, ClusterSecurityGroup, ClusterSnapshot, ClusterSubnetGroup, and EventSubscription resource types for all customers.
  * Amazon SQS
    - Updates to cmdlet documentation.

### 3.3.28.0 (2016-12-07)
  * Amazon S3
    - Added support for the new object tagging apis with three new cmdlets: Get-S3ObjectTagSet (GetObjectTagging API), Write-S3ObjectTagSet (PutObjectTagging API) and Remove-S3ObjectTagSet (DeleteObjectTagging API).
  * Amazon EC2
    - Added argument completion support for the -InstanceType parameter for the new t2.xlarge, t2.2xlarge and R4 instance types.
  * AWS Config Service
    - The default number of config rules for users has been increased from 25 to 50. As part of this change the Get-CFGConfigRuleEvaluationStatus cmdlet has been extended to support pagination with the addition of -Limit and -NextToken parameters. By default the cmdlet will auto-paginate all available rules to the pipeline but if you want to manually iterate you can use these parameters to take full control of data retrieval.

### 3.3.27.0 (2016-12-02)
  Roll-up release of all new features and services added during AWS re:Invent 2016:
  * Amazon API Gateway
    - Updated and added cmdlets to support publishing your APIs on Amazon API Gateway as products on the AWS Marketplace. You can use cmdlets to associate your APIs on API Gateway with Marketplace Product Codes. API Gateway will then send metering data to the Marketplace Metering Service on your behalf. New cmdlets also enable documenting your API.
  * Amazon EC2
    - Updated cmdlets with support for IPv6 new F1 Instance types.
  * Amazon Pinpoint
    - Added support for the new Amazon Pinpoint service. Amazon Pinpoint makes it easy to run targeted campaigns to improve user engagement. Pinpoint helps you understand your users behavior, define who to target, what messages to send, when to deliver them, and tracks the results of the campaign. Cmdlets for the new service have the noun prefix 'PIN' and can be viewed using the command 'Get-AWSCmdletName -Service PIN'.
  * Amazon Simple Systems Management
    - Updated cmdlets to support all new features announced during the conference.
  * Amazon Lightsail
    - Added support for the new Amazon Lightsail service, a simplified VM creation and management service. Cmdlets for the new service have the noun prefix 'LS' and can be viewed with the command 'Get-AWSCmdletName -Service LS'.
  * Amazon Polly
    - Added suport for the new Amazon Polly service. Amazon Polly service turns text into lifelike speech, making it easy to develop applications that use high-quality speech to increase engagement and accessibility. Cmdlets for this service have a noun prefix of 'POL' and can be viewed with the command 'Get-AWSCmdletName -Service POL'.
  * Amazon Rekognition
    - Added support for the new Amazon Rekognition service, a deep-learning based service to search, verify and organize images. With Rekognition, you can detect objects, scenes, and faces in images. You can also search and compare faces. Cmdlets for the service have the noun prefix 'REK' and can be viewed with the command 'Get-AWSCmdletName -Service REK'.
  * AWS AppStream
    - Added support for AWS AppStream, a fully managed desktop application streaming service that provides users instant access to their apps from a web browser. The cmdlets for the new service have the noun prefix 'APS' and can be viewed using the command 'Get-AWSCmdletName -Service APS'.
  * AWS CodeBuild
    - Added support for the new AWS CodeBuild service, a fully-managed build service in the cloud. CodeBuild compiles source code, runs tests, and produces packages that are ready to deploy. Cmdlets for this service have a noun prefix of 'CB' and can be viewed with the command 'Get-AWSCmdletName -Service CB'.
  * AWS DirectConnect
    - Updated cmdlets to support IPv6. The update includes two new cmdlets, New-DCBGPPeer (CreateBGPPeer API) and Remove-DCBGPPeer (DeleteBGPPeer API).
  * AWS Elastic Beanstalk
    - Updated cmdlets to support the new integration with AWS CodeBuild.
  * AWS Lambda
    - Added cmdlet Get-LMAccountSetting to support the new GetAccountSettings API, added dotnetcore 1.0 as a supported runtime, and added support for DeadLetterConfig and event source mappings with Kinesis streams.
  * AWS OpsWorksCM
    - Added support for the new AWS OpsWorks Managed Chef service, enabling single tenant Chef Automate server. The Chef Automate server is fully managed by AWS and supports automatic backup, restore and upgrade operations. Cmdlets for the new service have the noun prefix 'OWCM' and can be viewed using the command 'Get-AWSCmdletName -Service OWCM'.
  * AWS Shield
    - Added support for the new AWS Shield service. AWS Shield is a managed Distributed Denial of Service (DDoS) protection for web applications running on AWS. Cmdlets for this service have a noun prefix of 'SHLD' and can be viewed with the command 'Get-AWSCmdletName -Service SHLD'.
  * AWS StepFunctions
    - Added support for the new AWS StepFunctions service. AWS StepFunctions allows developers to build cloud applications with breakthrough reliability using state machines. Cmdlets for the new service have the noun prefix 'SFN' and can be viewed using the command 'Get-AWSCmdletName -Service SFN'.
  * AWS X-Ray
    - Added support for the new AWS X-Ray service. AWS X-Ray helps developers analyze and debug distributed applications. With X-Ray, you can understand how your application and its underlying services are performing to identify and troubleshoot the root cause of performance issues and errors. Cmdlets for the new service have the noun prefix 'XR' and can be viewed with the command 'Get-AWSCmdletName -Service XR'.
  * AWS Import/Export Snowball
    - Updated cmdlets to support a new job type, added cmdlets to support the new cluster apis, and the new AWS Snowball Edge device to support local compute and storage use cases.

### 3.3.24.0 (2016-11-22)
  * Amazon S3
    - Added a new -Tier parameter to the Restore-S3Object cmdlet to support specifying the Amazon Glacier job retrieval tier.
  * AWS CloudFormation
    - Added a new cmdlet, Get-CFNImportList, to support the new ListImports API

### 3.3.23.0 (2016-11-21)
  * AWS CloudTrail
    - Added two new cmdlets to suppor configuring your trail with event selectors: Get-CTEventSelectors (GetEventSelectors API) and Write-CTEventSelectors (PutEventSelectors API).
  * Amazon S3
    - Extended the Get-S3ObjectMetadata and Restore-S3Object cmdlets with a new parameter to support requestor-pays.

### 3.3.22.0 (2016-11-18)
  * Amazon Gamelift
    - Added a new cmdlet, Get-GMLInstanceAccess (GetInstanceAccess API), providing the ability to remote access into GameLift managed servers.
  * AWS Lambda
    - Updated the Update-LMFunctionConfiguration cmdlet with a new parameter, -Environment_Variable, to support the new service functionality.
  * Amazon Elastic MapReduce
    - Added support for automatic Scaling of EMR clusters based on metrics. This update adds a new parameter -InstanceGroup to the Edit-EMRInstanceGroup cmdlet, adds parameters -AutoScalingRole and -ScaleDownBehavior to the Start-EMRJobFlow cmdlet, and adds new cmdlets Write-EMRAutoScalingPolicy (PutAutoScalingPolicy API), Stop-EMRStops (CancelSteps API) and Remove-EMRAutoScalingPolicy (RemoveAutoScalingPolicy API).
  * Amazon Elastic Transcoder
    - The Test-ETSRole cmdlet now emits a deprecation warning, due to the deprecation of the underlying TestRole service API.
    - Updated the New-ETSJob cmdlet with new parameters to add support for the new service feature emabling multiple media input files to be stitched together.
  * Application AutoScaling
    - Added support for setting a new target resource (EMR Instance Group) as a scalable target.

### 3.3.21.0 (2016-11-17)
  (This version was only released as part of the combined AWS SDK and Tools Windows Installer, and not published to the PowerShell Gallery.)
  * Amazon API Gateway
    - Added new parameters to several cmdlets to support custom encoding.
  * Amazon CloudWatch
    - Updated cmdlet documentation.
  * AWS Marketplace Metering
    - Added support for third party submission of metering records with two new cmdlets: Send-MMMeteringDataBatch (BatchMeterUsage API) and Get-MMCustomerMetadata (ResolveCustomer API).
  * Amazon SQS
    - Updated cmdlet documentation and added new parameters to Send-SQSMessage and Receive-SQSMessage cmdlets to support the new FIFO message queue feature.

### 3.3.20.0 (2016-11-16)
  * Amazon Route53
    - Added support for cross-account VPC assocation with new cmdlets: get-R53VPCAssociationAuthorizationList (ListVPCAssociationAuthorizations API), New-R53VPCAssociationAuthorization (CreateVPCAssociationAuthorization API) and Remove-R53VPCAssociationAuthorization (DeleteVPVAssociationAuthorization API).
    - Three cmdlets that were marked obsolete have now been removed due to the removal of the underlying service APIs (the service apis had also been set to return error states since being marked obsolete so no script would have been able to use them successfully). The cmdlets that have been removed were: Get-R53ChangeDetails, Get-R53ChangeBatchesByHostedZone and Get-R53ChangeBatchesByRRSet.
  * AWS Service Catalog
    - Added support for the new administrative operations with 32 new cmdlets. The full list of cmdlets for this service can be viewed with the command 'Get-AWSCmdletName -Service SC'.
    - [BREAKING CHANGE] With the addition of a new CreateProduct API for this service we have had to rename the original New-SCProduct cmdlet (which mapped to the ProvisionProduct API) to 'New-SCProvisionedProduct'. The New-SCProduct cmdlet now maps to the 'CreateProduct' API. We aplogize for any inconvenience this change may cause.

### 3.3.19.0 (2016-11-16)
  (This version was only released as part of the combined AWS SDK and Tools Windows Installer, and not published to the PowerShell Gallery.)
  * Amazon ElastiCache
    - Added a new parameter, -AuthToken, to the New-ECCacheCluster and New-ECReplicationGroup cmdlets to support provision of an authtoken for Redis.
  * AWS Directory Service
    - Added support for schema extensions with three new cmdlets: Get-DSSchemaExtension (ListSchemaExtensions API),  Start-DSSchemaExtension (StartSchemaExtension API) and Stop-DSSchemaExtension (CancelSchemaExtension API).
  * Amazon Kinesis
    - Updates to support describing shard limits, open shard count and stream creation timestamp. This includes two new cmdlets, Get-KINLimit (DescribeLimits API) and Update-KINShardCount (UpdateShardCount API).

### 3.3.18.0 (2016-11-15)
  (This version was only released as part of the combined AWS SDK and Tools Windows Installer, and not published to the PowerShell Gallery.)
  * Amazon Cognito Identity Provider
    - Added a new parameter, -Schema, to the New-CGIPIdentityPool cmdlet to support specifying schema attributes.

### 3.3.17.0 (2016-11-13)
  * AWS CloudFormation
    - Added a new cmdlet, Get-CFNExport, to support the new ListExports API. Also updated existing cmdlets to support resources to skip during rollback, and new ChangeSet types and transforms.
  * Amazon Simple Email Service
    - Added new cmdlets to support API updates enabling tracking of bounce, complaint, delivery, sent, and rejected email metrics with fine-grained granularity.
  * AWS Direct Connect
    - Added three new cmdlets to support tagging operations: Add-DCResourceTag (TagResource API), Get-DCResourceTag (DescribeTags API) and Remove-DCResourceTag (UntagResource API).
  * Amazon CloudWatch Logs
    - Updated cmdlets to add support for CloudWatch Metrics to Logs, a capability that helps pivot from your logs-extracted metrics directly to the corresponding logs.

### 3.3.14.0 (2016-10-25)
  * AWS Server Migration Service
    - Added support for the new Server Migration Service, an agentless service that makes it easier and faster for you to migrate thousands of on-premises workloads to AWS. Cmdlets for the service have the noun prefix 'SMS'. The cmdlets and the service APIs they map to can be listed with the command 'Get-AWSCmdletName -Service "SMS"'.

### 3.3.13.0 (2016-10-20)
  (This version was only released as part of the combined AWS SDK and Tools Windows Installer, and not published to the PowerShell Gallery.)
  * AWS Budgets
    - Added support for the new AWS Budgets service. Cmdlets for this service have the noun prefix 'BGT'. The cmdlets and the service APIs they map to can be listed with the command 'Get-AWSCmdletName -Service "BGT"'.

### 3.3.12.0 (2016-10-19)
  * Amazon EC2
    - Added support for retrieving latest Windows Server 2016 images using the Get-EC2ImageByName cmdlet.

### 3.3.11.0 (2016-10-18)
  * Amazon EC2
    - Added HostId and Affinity parameters to the New-EC2Instance cmdlet to support launching instances on a dedicated host.
  * Bug fix: fixed issue in underlying AWS SDK for .NET that could lead to file corruption when updating the credential profile store file.
  * Amazon Relational Database Service
    - Added support for Amazon Aurora integration with other AWS services, allowing you to extend your Aurora DB cluster to utilize other capabilities in the AWS cloud. Permission to access other AWS services is granted by creating an IAM role with the necessary permissions, and then associating the role with your DB cluster. This support includes two new cmdlets, Add-RDSRoleToDBCluster (AddRoleToDBCluster API) and Remove-RDSRoleFromDBCluster (RemoveRoleFromDBCluster API).

### 3.3.10.0 (2016-10-17)
  * Added support for the new US East (Ohio) region. To select this region, specify the value 'us-east-2' for the -Region parameter when invoking a cmdlet.
  * Amazon Route53
    - Added support for specifying the new US East (Ohio) region when defining resource record sets.

### 3.3.9.0 (2016-10-13)
  * AWS Certificate Manager
    - Added a new cmdlet, Import-ACMCertificate, to support the new ImportCertificate API. This cmdlet enables users to import third-party SSL/TLS certificates into ACM.
  * Amazon Gamelift
    - Added a new cmdlet, Get-GMLInstance, to support the new DescribeInstances API. The existing cmdlets were also updated to support new service features to protect game developer resources (builds, alias, fleets, instances, game sessions and player sessions) against abuse.

### 3.3.8.0 (2016-10-12)
  * Amazon EC2 Container Registry
    - Added a new cmdlet, Get-ECRImageMetadata, to support the new DescribeImages API. This cmdlet can be used to expose image metadata which today includes image size and image creation timestamp.
  * Amazon ElastiCache
    - Updated cmdlets for service update adding support for a new major engine release of Redis, 3.2 (providing stability updates and new command sets over 2.8), as well as ElasticSupport for enabling Redis Cluster in 3.2, which provides support for multiple node groups to horizontally scale data, and superior engine failover capabilities.

### 3.3.6.2 (2016-10-11)
  * Added support for proxy bypass lists and 'bypass on local' mode for proxies configured using the Set-AWSproxy cmdlet. This enables use of proxies configured using this cmdlet in environments with SAML Federated Identity in an Active Directory Federated Services environment. Previously the proxy had to be configured at the system level.
  * Fixed issues with the Set-AWSCredentials cmdlet when attempting to store credential profiles on Nano Server instances. In previous releases the cmdlet would either display an error message about a missing CryptProtectData api or if the -ProfilesLocation parameter was used, the folder path used with the parameter had to exist before the credentials file could be created.

### 3.3.5.0 (2016-10-06)
  * Amazon Cognito Identity Provider
    - Added a cmdlet, New-CGIPUserAdmin, to support the new AdminCreateUser API. The New-CGIPUserPool and Update-CGIPUerPool cmdlets were also updated with new parameters related to email and administrator control of creating new users.

### 3.3.4.0 (2016-09-29)
  * Amazon EC2
    - Added support for convertible reserved instances with two new cmdlets: Get-EC2ReservedInstancesExchangeQuote (GetReservedInstancesExchangeQuote API) and Confirm-EC2ReservedInstancesExchangeQuote (AcceptReservedInstancesExchangeQuote API).
    - Added completion support for new m4.16xlarge, P2 and X1 instance types.

### 3.3.3.0 (2016-09-27)
  * AWS CloudFormation
    - Updated the New-CFNChangeSet, New-CFNStack, Remove-CFNStack, Resume-CFNUpdateRollback and UpdateStack with a new parameter, -RoleARN. When specified AWS CloudFormation uses the role's credentials to make calls on your behalf for future operations on the stack.

### 3.3.2.0 (2016-09-22)
  * Amazon API Gateway
    - Extended the argument completer for the -Type parameter on Write-AGIntegration to support the new values related to the Simple Proxy feature.

### 3.3.1.0 (2016-09-20)
  * Amazon Elastic MapReduce
    - Added support for Security Configurations which can be used to enable encryption at-rest and in-transit for certain applications on Amazon EMR with new cmdlets Get-EMRSecurityConfiguration (DescribeSecurityConfiguration API), Get-EMRSecurityConfigurationList (ListSecurityConfigurations API), New-EMRSecurityConfiguration (CreateSecurityConfiguration API) and Remove-EMRSecurityConfiguration (DeleteSecurityConfiguration API). The Start-EMRJobFlow cmdlet was also extended with a new '-SecurityConfiguration' parameter.
  * Amazon Relational Database Service
    - Added support for local time zones for AWS RDS SqlServer database instances: the Get-RDSDBEngineVersion was extended with a new parameter '-ListSupportedTimeZone' and the New-RDSDBInstance cmdlet was extended with a new parameter '-Timezone'.
  * Amazon Redshift
    - This release of Amazon Redshift introduces Enhanced VPC Routing. When you use Amazon Redshift Enhanced VPC Routing, Amazon Redshift forces all COPY and UNLOAD traffic between your cluster and your data repositories through your Amazon VPC. The Edit-RSCluster, New-RSCluster and Restore-RSFromClusterSnapshot cmdlets were extended with a new 'EnhancedVPCRouting' parameter.
  * AWS CodeDeploy
    - AWS CodeDeploy now integrates with Amazon CloudWatch alarms, making it possible to stop a deployment if there is a change in the state of a specified alarm for a number of consecutive periods, as specified in the alarm threshold. AWS CodeDeploy also now supports automatically rolling back a deployment if certain conditions are met, such as a deployment failure or an activated alarm. The New-CDDeployment cmdlet was extended with new parameters '-AutoRollbackConfiguration_Enabled' and '-AutoRollbackConfiguration_Event' and the Stop-CDDeployment cmdlet extended with a '-AutoRollbackEnabled' parameter. The New-CDDeploymentGroup and Update-CDDeploymentGroup cmdlets were extended with several parameters related to alarm configuration.

### 3.3.0.0 (2016-09-19)
  * This release marks the General Availability (GA) of the AWS Tools for PowerShell Core (AWSPowerShell.NetCore) and the underlying AWS SDK for .NET Core. Starting with this release both AWS modules for PowerShell will update in sync with new service releases and service updates. The product version has incremented to 3.3.0.0 to match the underlying product version of the SDK. For more information see the blog post at http://blogs.aws.amazon.com/net/post/Tx3O6TT4NKFM0FU/.
  * Set-AWSCredentials
    - Fixed an issue in the AWSPowerShell.NetCore version of the cmdlet that caused it to not update the credentials file on non-Windows systems.

### 3.1.101.0 (2016-09-15)
  * AWS Lambda
    - Extended the Update-LMFunctionCode cmdlet with a new parameter, -ZipFilename, that enables more a convenient way to specify the code to be updated. The original -ZipFile parameter, of type System.IO.MemoryStream, has been retained for backwards compatibility.
  * Amazon Relational Database Service
    - The Amazon.RDS.Model.DBCluster type in the underlying AWS SDK for .NET SDK has been extended with a new ReaderEndpoint property to support the new Aurora cluster reader end-point feature.

### 3.1.100.0 (2016-09-13)
  * Amazon S3
    - Extended the Amazon.S3.Model.S3Object type in the underlying AWS SDK for .NET with a new member containing the name of the bucket containing the object. This enables improved pipelining of the S3 cmdlets, for example you can now run a pipeline such as 'Get-S3Bucket | ? { $_.BucketName -like '*config*' } | Get-S3Object | ? { $_.Key -like '*.json' } | Read-S3Object -Folder C:\Temp'. Read-S3Object has been updated to accept an S3Object (or S3ObjectVersion) instance to allow for use downloading objects with the -File or -Folder parameters, the other S3 cmdlets will automatically bind BucketName, Key and VersionId parameters (where relevant) from the piped-in object.
  * AWS Service Catalog
    - Updated cmdlets to support the new Access Level Filtering feature.

### 3.1.99.0 (2016-09-08)
  * Amazon CloudFront
    - Updated the New-CFDistribution, New-CFDistributionWithTag and Update-CFDistribution to enable specifying HTTP2 support for distributions (-DistributionConfig_HttpVersion).

### [AWSPowerShell.NetCore] 3.2.8.0-RC (2016-09-08)
  * Updates the service API support in the module cmdlets to match the 3.1.98.0 release of the AWSPowerShell desktop edition.
  * Upgrades the underlying AWS SDK for .NET to the 2.3.8 RC version.

### 3.1.98.0 (2016-09-06)
  * Amazon Relation Database Service
    - Added support for the new DescribeSourceRegions API with new cmdlet Get-RDSSourceRegion. The DescribeSourceRegions API provides a list of all the source region names and endpoints for any region. Source regions are the regions where current region can get a replica or copy a snapshot from.
  * AWS CodePipeline
    -  Incorporated API revisions to correct naming of members in types used in the recently published view changes APIs. Please note these are breaking changes in the model shapes, but do not affect the published parameters of the cmdlets.

### 3.1.97.0 (2016-09-01)
  * Amazon Gamelift
    - Updated the New-GMLBuild cmdlet with a new parameter, -OperatingSystem, enabling customers to now use Linux in addition to Windows EC2 instances.
  * Amazon Cognito Identity Provider
    - Added support for bulk import of users with new cmdlets: Get-CGIPCSVHeader (GetCSVHeader API), Get-CGIPUserImportJob (DescribeUserImportJob API), Get-CGIPUserImportJobList (ListUserImportJobs API), New-CGIPUserImportJob (CreateUserImportJob API), Start-CGIPUserImportJob (StartUserImportJob API) and Stop-CGIPUserImportJob (StopUserImportJob API).
  * AWS Config
    - Updated the argument completer for parameters of type ResourceType to include support for specifying the application load balancer resource type.
  * Amazon Relation Database Service
    - Updates to the model types in the underlying AWS SDK for .NET to enable customers to add options to a RDS option group that are mutually exclusive. To avoid conflict issues while validating the request to add an option to the option group the API response now includes information about options that conflict with each other.

### 3.1.96.0 (2016-08-30)
  * Amazon CloudFront
    - Updated the New-CFDistribution, New-CFDistributionWithTag and Update-CFDistribution cmdlets with parameters to support querystring whitelisting. Customers can now choose to forward certain querystring keys instead of a.) all of them or b.) none of them.
  * Amazon Route53
    - Updated cmdlets to add support for new service features: support for the NAPTR DNS record type and support metric-based health check in ap-south-1 region. In addition a new cmdlet, Test-R53DNSAnswer, was added to support the new TestDNSAnswer API which enables customers to send a test query against a specific name server using spoofed delegation nameserver, resolver, and ECS IPs.
  * AWS CodePipeline
    - CodePipeline has introduced a new feature to return pipeline execution details. Execution details consists of source revisions that are running in the pipeline. Customers will be able to tell what source revisions that are running through the stages in pipeline by fetching execution details of each stage. This support includes a new cmdlet, Get-CPPipelineExecution (GetPipelineExecution API) and updates to the existing Write-CPJobSuccessResult and Write-CPThirdPartyJobSuccessResult cmdlets.

### 3.1.95.0 (2016-08-23)
  (This version was only released as part of the combined AWS SDK and Tools Windows Installer, and not published to the PowerShell Gallery.)
  * The Get-AWSPublicIpRange cmdlet was updated to return both IPv4 and IPv6 address range details.
  * Amazon Relation Database Service
    - The output objects in the from various Get-* (Describe* APIs) were updated to include resource ARNs.
  * AWS OpsWorks
    - Minor documentation update to support region expansion.

### [AWSPowerShell.NetCore] 3.2.7.0-Beta (2016-08-23)
  * First release of the AWS Tools for PowerShell Core ("AWSPowerShell.NetCore") module
    - The AWSPowerShell.NetCore module is built on top of the .NET Core version of the AWS SDK for .NET, which is in beta while we finish up testing and port a few more features from the .NET Framework version of the AWS SDK for .NET. Note that updates to this module for new service features may lag a little behind the sister AWS Tools for Windows PowerShell ("AWSPowerShell") module while the .NET Core version of the AWS SDK for .NET is in beta.
    - The services and service APIs supported in this release correspond to the 3.1.94.0 release of the AWSPowerShell module.
    - Installation Note:
      Some users are reporting issues with the Install-Module cmdlet built into PowerShell Core with errors related to semantic versioning (see https://github.com/OneGet/oneget/issues/202). Using the NuGet provider appears to resolve the issue currently. To install using this provider run this command, setting an appropriate destination folder (on Linux for example try -Destination ~/.local/share/powershell/Modules):

        Install-Package -Name AWSPowerShell.NetCore -Source https://www.powershellgallery.com/api/v2/ -ProviderName NuGet -ExcludeVersion -Destination <destination>

  * Unsupported cmdlets in this release
    - As noted in the blog post at http://blogs.aws.amazon.com/net/post/TxTUNCCDVSG05F/Introducing-AWS-Tools-for-PowerShell-Core-Edition there is a high degree of compatibility between the two AWS modules. A small number of cmdlets are not supported in this edition of the tools:
    -- Proxy cmdlets: Set-AWSProxy, Clear-AWSProxy
    -- Logging cmdlets: Add-AWSLoggingListener, Remove-AWSLoggingListener, Set-AWSResponseLogging, Enable-AWSMetricsLogging, Disable-AWSMetricsLogging
    -- SAML federated credentials cmdlets: Set-AWSSamlEndpoint, Set-AWSSamlRoleProfile
    -- Legacy EC2 import cmdlets: Import-EC2Instance, Import-EC2Volume
  * Known issues in this release
    - There are no known issues in this release of the tools.

### 3.1.94.0 (2016-08-18)
  * Amazon WorkSpaces
    - New cmdlets to support the launch and management of WorkSpaces that are paid for and used by the hour. The new cmdlets are Edit-WKSWorkspaceProperty (ModifyWorkspaceProperties API), Get-WKSWorkspacesConnectionStatus (DescribeWorkspacesConnectionStatus API) and Start-WKSWorkspace (StartWorkspaces API).
    NOTE: the new api to stop a workspace (StopWorkspace) is currently NOT supported as it clashes with an existing cmdlet, Stop-WKSWorkspace, which actually maps to the TerminateWorkspaces API. We are investigating correcting this with a new cmdlet, Remove-WKSWorkspace (which will map to TerminateWorkspaces) and updating the existing Stop-WKSWorkspace cmdlet to map to the new StopWorkspaces API. This is of course a breaking change.
  * Amazon EC2
    - Added support for dedicated host reservations with new cmdlets Get-EC2HostReservation (DescribeHostReservations API), Get-EC2HostReservationOffering (DescribeHostReservationOfferings API), Get-EC2HostReservationPurchasePreview (GetHostReservationPurchasePreview API) and New-EC2HostReservation (PurchaseHostReservation API).
  * Amazon S3
    - Added a -StorageClass parameter (type Amazon.S3.StorageClass) to the Copy-S3Object cmdlet. This parameter enables you to set the STANDARD_IA storage class in addition to the existing reduced redundancy and standard classes. The switch parameters used previously to set these classes have been retained for backwards compatibility but we encourage you to update your scripts to use the new parameter so that new classes Amazon S3 may add in future can be easily specified.
  * Fixed an issue in the Set-AWSSamlRoleProfile cmdlet that caused the user name to be stored with an initial \ if a domain name was not present in the supplied network credential. This could occur on systems where the user credential was created using user name in email format.
  * Added support for tab completion of region, credential profile and image keys (Get-EC2ImageByName).

### 3.1.93.0 (2016-08-16)
  * Authenticode signing has now been enabled for the module enabling it to be used in environments that mandate an 'AllSigned' execution policy. The module manifest (.psd1), new argument completion script module (.psm1) and type extension and formats files (.ps1xml) now all contain an Authenticode signature. For more information on execution policies see the Microsoft TechNet article at https://technet.microsoft.com/en-us/library/dd347641.aspx.
  * Custom argument completers have been added to the module to support service enumeration types in the underlying AWS SDK for .NET. When constructing a command at the console or in the Windows PowerShell ISE (or other PowerShell hosts) you can now access the valid values for an enumeration type using the Tab or Ctrl+Space key sequences. The ISE will automatically display the possible completions. For more information on this enhancement see the blog post "Argument Completion in Windows PowerShell" on the AWS .NET Development Blog, http://blogs.aws.amazon.com/net/.
  * Amazon API Gateway
    - Added cmdlets to support the new usage plan APIs. Usage plans allows you to easily manage and monetize your APIs for your API-based business.
    - [Breaking Change] The response data returned to the Get-AGApiKeyList cmdlet (GetApiKeys API) now contains a collection of warning messages for warnings logged during the import of API keys. This is incompatible with automatic pagination and therefore automatic pagination has been disabled for this cmdlet.

### 3.1.92.0 (2016-08-11)
  * Amazon Import/Export Snowball
    - Added cmdlets to support the new Import/Export Snowball service. The API for this service enables a customer to create and manage Snowball jobs without needing to use the AWS Console. The cmdlets for this service have the prefix 'SNOW' applied to the noun portion of the cmdlet name. The cmdlets and the service APIs they map to can be listed with the command 'Get-AWSCmdletName -Service "SNOW"'.
  * Amazon Kinesis Analytics
    - Added cmdlets to support the new Amazon Kinesis Analytics service, a fully managed service for continuously querying streaming data using standard SQL. With Kinesis Analytics, you can write standard SQL queries on streaming data and gain actionable insights in real-time, without having to learn any new programming skills. The service allows you to build applications that continuously read data from streaming data sources, process that data using standard SQL, and send the processed data to up to four destinations of your choice. Kinesis Analytics enables you to generate time-series analytics, feed a real-time dashboard, create real-time alarms and notifications, and much more. The cmdlets for this service have the prefix 'KINA' applied to the noun portion of the cmdlet name. The cmdlets and the service APIs they map to can be listed with the command 'Get-AWSCmdletName -Service "KINA"'.
  * Elastic Load Balancing V2
    - Added cmdlets to support the new V2 API for Elastic Load Balancing. The cmdlets for this service have the prefix 'ELB2' applied to the noun portion of the cmdlet name. The cmdlets and the service APIs they map to can be listed with the command 'Get-AWSCmdletName -Service "ELB2"'.
  * Auto Scaling
    - Added cmdlets to support the launch of the new V2 API for Elastic Load Balancing. The new cmdlets are Add-ASLoadBalancerTargetGroup (AttachLoadBalancerTargetGroups API), Dismount-ASLoadBalancerTargetGroup (DetachLoadBalancerTargetGroups API) and Get-ASLoadBalancerTargetGroup (DescribeLoadBalancerTargetGroups API).
  * AWS Key Management.
    - Added new cmdlets to support new import key features. This new support enables you to import keys from your own key management infrastructure to KMS for greater control over generation and storage of keys and meeting compliance requirements of sensitive workloads. The new cmdlets are Import-KMSKeyMaterial (ImportKeyMaterial API), Get-KMSParametersForImport (GetParametersForImport API) and Remove-KMSImportedKeyMaterial (DeleteImportedKeyMaterial API).
  * Amazon S3
    - Added support for dualstack (Ipv6) endpoints. All S3 cmdlets have been extended with a new -UseDualstackEndpoint switch parameter. If set, this parameter will cause the cmdlet to use the dualstack endpoint for the specified region. Note that not all regions support dualstack endpoints. You should consult S3 documentation to verify that a dualstack endpoint is available in the region you wish to use before specifying this switch.

### 3.1.91.0 (2016-08-09)
  * Amazon CloudFront
    - Added cmdlets to support tagging for Web and Streaming distributions. Tags make it easier for you to allocate costs and optimize spending by categorizing and grouping AWS resources. The new cmdlets are Add-CFResourceTag (TagResource API), Get-CFResourceTag (ListTagsForResource API), New-CFDistributionWithTag (CreateDistributionWithTags API), New-CFStreamingDistributionWithTags (CreateStreamingDistributionWithTags API) and Remove-CFResourceTag (UntagResource API).
  * Amazon EC2 Container Registry
    - Updated the Get-ECRImage cmdlet with a new parameter, -Filter_TagStatus, to support filtering of requests based on whether an image is tagged or untagged.
  * AWS Marketplace Commerce Analytics
    - Added a new cmdlet, Start-MCASupportDataExport, to support the new StartSupportDataExport API.

### 3.1.90.0 (2016-08-04)
  * Amazon Cognito Identity Provider
    - Updates to enable authentication support for Cognito User Pools. This update also added a number of new cmdlets: Approve-CGIPDevice (ConfirmDevice API), Disconnect-CGIPDeviceGlobal (GlobalSignOut API), Disconnect-CGIPUserGlobalAdmin (AdminUserGlobalSignOut API), Edit-CGIPDeviceStatus (UpdateDeviceStatus API), Edit-CGIP-DeviceStatusAdmin (AdminUpdateDeviceStatus API), Get-CGIPDeviceStatus (GetDevice API), Get-CGIPDeviceStatusAdmin (AdminGetDevice API), Get-CGIPDeviceList (ListDevices API), Get-CGIPDeviceListAdmin (AdminListDevices API), Send-CGIPAuthChallengeResponse (RespondToAuthChallenge API), Send-CGIPAuthChallengeResponseAdmin (AdminRespondToAuthChallenge API), Start-CGIPAuth (InitiateAuth API), Start-CGIPAuthAdmin (AdminInitiateAuth API), Stop-CGIPDeviceTracking (ForgetDevice API) and  Stop-CGIPDeviceTrackingAdmin (AdminForgetDevice API).
  * Amazon Gamelift
    - Added a new cmdlet, Find-GMLGameSession, to support the new SearchGameSessions API.
  * Amazon Relation Database Service
    - Added support for S3 snapshot ingestion with a new cmdlet, Restore-RDSDBClusterFromS3 (RestoreDBClusterFromS3 API). The Edit-RDSDBInstance cmdlet was also updated with a new parameter, -DBSubnetGroupName, to support moving DB instances between VPCs or different subnets within a VPC.

### 3.1.89.0 (2016-08-3)
  * Amazon Route53 Domains
    - Added new cmdlets to support new APIs: Update-R53DDomainRenewal (RenewDomain API, renew domains for a specified duration), Get-R53DDomainSuggestion (GetDomainSuggestions API) and Get-R53DBillingRecord (ViewBilling API).
  * Amazon Relation Database Service
    - Updated the Edit-RDSDBInstance cmdlet to support specifying license model. You can also now specify versioning of option groups.
  * AWS IoT
    - Added a new cmdlet Get-IOTOutgoingCertificate to support the new ListOutgoingCertificates API. Register-IOTCACertificate and Update-IOTCACertificate cmdlets were updated to support a new AllowAutoRegistration parameter.

### 3.1.88.0 (2016-07-30)
  * Amazon API Gateway
    - Updating the New-AGAuthorizer cmdlet to add support for Cognito User Pools Auth.
  * AWS Directory Service
    - Added support for new apis to manage routing with Microsoft AD. The new cmdlets are Add-DSIpRoutes (AddIpRoutes API), Get-DSIpRoutes (ListIpRoutes API) and Remove-DSIpRoutes (RemoveIpRoutes API).
  * Amazon Elasticsearch
    - Added a new parameter, -ElasticsearchVersion, to the New-ESDomain cmdlet to support selection of a new api version. Amazon Elasticsearch Version 2.3 offers improved performance, memory management, and security. It also offers several new features including: pipeline aggregations to perform advanced analytics like moving averages and derivatives, and enhancements to geospatial queries.

### 3.1.87.0 (2016-07-26)
  * AWS IoT
    - This update adds support for thing types. Thing types are entities that store a description of common features of Things that are of the same logical type. The new cmdlets to support Thing types are: Get-IOTThingType (DescribeThingType API), Get-IOTThingTypesList (ListThingTypes API), New-IOTThingType (CreateThingType API), Remove-IOTThingType (DeleteThingType API) and Set-IOTThingTypeDeprecation (DeprecateThingType API).

### 3.1.86.0 (2016-07-21)
  * AWS Config
    - Added new cmdlets Start-CFGConfigRulesEvaluation (StartConfigRulesEvaluation API) and Remove-CFGEvaluationResult (DeleteEvaluationResults API). For more information on rule evaluation see the release note at https://aws.amazon.com/about-aws/whats-new/2016/07/now-use-aws-config-to-record-changes-to-rds-and-acm-resources-and-write-config-rules-to-evaluate-their-state/.
  * AWS Certificate Manager
    - The output object (type Amazon.CertificateManager.Model.CertificateDetail) of the Get-CMCertificateDetail cmdlet (DescribeCertificate API) has been extended with a new field, FailureReason, indicating the reason why a certificate request failed.

### 3.1.85.1 (2016-07-19)
  * AWS Device Farm
    - Corrected a cmdlet name in the previous update. The cmdlet for the GetRemoteAccessSessions API should have been named Get-DFRemoteAccessSessionList.

### 3.1.85.0 (2016-07-19)
  * AWS Device Farm
    - Added support for new APIs for managing remote access sessions. This update adds the cmdlets Get-DFRemoteAccessSession (GetRemoteAccessSession API), Get-DFRemoteAccessSessions (ListRemoteAccessSessions API), Install-DFToRemoteAccessSession (InstallToRemoteAccessSession API), New-DFRemoteAccessSession (CreateRemoteAccessSession API), Remove-DFRemoteAccessSession (DeleteRemoteAccessSession API) and Stop-DFRemoteAccessSession (StopRemoteAccessSession API).
  * Amazon Simple Systems Management
    - Updated the Send-SSMCommand cmdlet with new parameters enabling notifications to be sent when a command terminates.

### 3.1.84.0 (2016-07-13)
  * Amazon RDS
    - Updated the Start-RDSDBClusterFailover (FailoverDBCluster API) cmdlet with a new parameter TargetDBInstanceIdentifier. Added a new cmdlet, Copy-RDSDBClusterParameterGroup, to support the new CopyDBClusterParameterGroup API.
  * Amazon ECS
    - Added a new parameter, Overrides_TaskRoleArn, to the New-ECSTask and Start-EC2Task cmdlets. Added a new parameter, TaskRoleArn, to the Register-ECSTaskDefinition cmdlet. These parameters enable you to specify an IAM role for tasks.
  * Amazon Database Migration Service
    - Updated various cmdlets with new CertificateArn and SslMode parameters enabling use of SSL-enabled endpoints. Added new cmdlets Get-DMSCertificate (DescribeCertificates API), Import-DSMCertificate (ImportCertificate API) and Remove-DSMCertificate (DeleteCertificate API).

### 3.1.83.0 (2016-07-07)
  * AWS Service Catalog
    - Added cmdlets to support the new AWS Service Catalog service. AWS Service Catalog allows organizations to create and manage catalogs of IT services that are approved for use on AWS. The cmdlets for this service have the prefix 'SC' applied to the noun portion of the cmdlet name. The cmdlets and the service APIs they map to can be listed with the command 'Get-AWSCmdletName -Service "Service Catalog"'.
  * AWS Config
    - Added a new cmdlet, Remove-CFGConfigurationRecorder, to support the new DeleteConfigurationRecorder API.
  * AWS Directory Service
    - Added new cmdlets to support resource tagging: Add-DSResourceTag (AddTagsToResource API), Get-DSResourceTag (ListTagsForResource API) and Remove-DSResourceTag (RemoveTagsFromResource API). Additionally, automatic pagination has been enabled for cmdlets in this service where the response data contains a single logical field that can be enumerated to the pipeline (Get-DSDirectory, Get-DSSnapshot and Get-DSTrust).

### 3.1.82.0 (2016-07-05)
  * AWS CodePipeline
    - Added support for manual approvals with a new cmdlet, Write-CPApprovalResult (PutApprovalResult API).

### 3.1.81.0 (2016-06-30)
  (This version was only released as part of the combined AWS SDK and Tools Windows Installer, and not published to the PowerShell Gallery.)
  * AWS Database Migration Service
    - Added support to enable VpcSecurityGroupId to be specified for the replication instance.
  * Amazon Simple Systems Management
    - Added support for using Amazon SSM with any instance or virtual machine outside of AWS, including your own data centers or other clouds with cmdlets: Add-SSMResourceTag (AddTagsToResource API), Get-SSMActivation (DescribeActivations API), Get-SSMResourceTag (ListTagsForResource API), New-SSMActivation (CreateActivation API), Remove-SSMActivation (DeleteActivation API), Remove-SSMResourceTag (RemoveTagsFromResource API), Unregister-SSMManagedInstance (DeregisterManagedInstance API), Update-SSMManagedInstanceRole (UpdateManagedInstanceRole API).

### 3.1.80.0 (2016-06-28)
  (This version was only released as part of the combined AWS SDK and Tools Windows Installer, and not published to the PowerShell Gallery.)
  * Amazon EC2
    - Updated the Edit-EC2InstanceAttribute and Register-EC2Image cmdlets with new parameter -EnaSupport for specifying enhanced networking.
  * Amazon Elastic File System
    - Added new parameter -PerformanceMode to the New-EFSFileSystem cmdlet (CreateFileSystem API).
  * Amazon Gamelift
    - Added new cmdlets for multi-process support. The new cmdlets are Get-GMLGameSessionDetail (DescribeGameSessionDetails API), Get-GMLRuntimeConfiguration (DescribeRuntimeConfiguration API), Get-GMLScalingPolicy (DescribeScalingPolicies API), Remove-GMLScalingPolicy (DeleteScalingPolicy API), Update-GMLRuntimeConfiguration (UpdateRuntimeConfiguration API) and Write-GMLScalingPolicy (PutScalingPolicy API).

### 3.1.79.0 (2016-06-28)
  * This version contained updates to the underlying AWS SDK for .NET components and was only distributed in the downloadable AWS Tools for Windows msi installer.

### 3.1.78.0 (2016-06-23)
  (This version was only released as part of the combined AWS SDK and Tools Windows Installer, and not published to the PowerShell Gallery.)
  * Amazon Cognito Identity
    - Added -CognitoIdentityProvider and -SamlProviderARN parameters to the New-CGIIdentityPool and Update-CGIIdentityPool cmdlets to support role customization.
  * AWS Direct Connect
    - Added two new cmdlets, Get-DCConnectionLoa (DescribeConnectionLoa API) and Get-DCInterconnectLoa (DescribeInterconnectLoa API).
  * Amazon EC2
    - Added support for IdentityId Format with new cmdlets Edit-EC2IdentityIdFormat (ModifyIdentityIdFormat API) and Get-EC2IdentityIdFormat (DescribeIdentityIdFormat API).

### 3.1.77.0 (2016-06-21)
  (This version was only released as part of the combined AWS SDK and Tools Windows Installer, and not published to the PowerShell Gallery.)
  * AWS CodePipeline
    - Added a new cmdlet, Redo-CPStageExecution, to support the new RetryStageExecution API.

### 3.1.76.0 (2016-06-14)
  * Amazon Relational Database Service
    - Added a new cmdlet, Convert-RDSReadReplicaDBCluster, to support the new PromoteReadReplicaDBCluster API.
  * Amazon Simple Email Service
    - Added a new cmdlet, Set-SESIdentityHeadersInNotificationsEnable, to support the new SetIdentityHeadersInNotificationsEnabled API.

 ### 3.1.75.0 (2016-06-07)
  * AWS IoT
    - Added a new cmdlet, Get-IOTPolicyPrincipalsList, to support the new ListPolicyPrincipals API. ListPolicyPrincipals allows you to list all your principals (certificate or other credential, such as Amazon Cognito ID) attached to a given policy.
  * Amazon Machine Learning
    - Added support for new tagging APIs with new cmdlets Add-MLTag (AddTags API), Get-MLTag (DescribeTags API) and Remove-MLTag (DeleteTags API). Tags are commonly used for cost allocation and can be applied to Amazon Machine Learning datasources, models, evaluations, and batch predictions.

### 3.1.74.0 (2016-06-03)
  * AWS CodeDeploy
    - Added automatic pagination support to the Get-CDApplicationList, Get-CDApplicationRevisionList, Get-CDDeploymentConfigList, Get-CDDeploymentInstanceList, Get-CDDeploymentList and Get-CDOnPremiseInstanceList cmdlets.
  * Amazon Security Token Service (STS) and Identity and Access Management (IAM) Service
    - Corrected potential performance issue caused in the previous release by the automatic detection of region from EC2 instance metadata. If no region data was available locally (from parameter, shell variable or environment variable) the cmdlets could attempt to access metadata even when not running on an EC2 instance.
  * Amazon EC2
    - API update to the Request-EC2SpotFleet (RequestSpotFleet API) and Get-EC2SpotFleetRequest (DescribeSpotFleetRequests API). The RequestSpotFleet API can now indicate whether a Spot fleet will only 'request' the target capacity or also attempt to 'maintain' it. When you want to 'maintain' a certain target capacity, Spot fleet will place the required bids to meet this target capacity, and enable you to automatically replenish any interrupted instances. When you simply 'request' a certain target capacity, Spot fleet will only place the required bids but will not attempt to replenish Spot instances if capacity is diminished, nor will it submit bids in alternative Spot pools if capacity is not available. By default, the API is set to 'maintain'. The DescribeSpotFleetRequests API now returns two new parameters: the 'fulfilledCapacity' of a Spot fleet to indicate the capacity that has been successfully launched, and the 'type' parameter to indicate whether the fleet is meant to 'request' or 'maintain' capacity.

### 3.1.73.0 (2016-05-26)
  * Amazon EC2
    - Added a new cmdlet, Remove-EC2Instance (TerminateInstances API) following user suggestion. The existing -Terminate switch is still present on Stop-EC2Instance but is considered deprecated.
    - Added a new cmdlet, Get-EC2InstanceMetadata, to return instance metadata when running on EC2 instances. For more information see the blog post at http://blogs.aws.amazon.com/net/.
  * Added support for specifying AWS access key, secret key and session token values from environment variables. For more information see the blog post at http://blogs.aws.amazon.com/net/.
  * Added support for auto-detecting AWS region using instance metadata when cmdlets are run on EC2 instances. The Initialize-AWSDefaults cmdlets no longer prompts for region selection when run in this scenario. To set a region different to that in which the instance is running, specify the -Region parameter.

### 3.1.72.0 (2016-05-24)
  * Amazon EC2
    - Added the new cmdlet Get-EC2ConsoleSnapshot to support the new GetConsoleSnapshot API.
  * Amazon RDS
    - Added support for cross-account snapshot sharing with two new cmdlets Edit-RDSDBClusterSnapshot (ModifyDBClusterSnapshot API) and Get-RDSDBClusterSnapshot (DescribeDBClusterSnapshotAttributes API).

### 3.1.71.0 (2016-05-19)
  * AWS Application Discovery Service [Breaking Change]
    - The initial release for this service used an incorrect service model. The corrections involve two new new cmdlets, Get-ADSConfiguration (DescribeConfigurations API) and Get-ADSExportConfigurationsId (ExportConfigurations API). The existing Get-ADSExportConfiguration has been remapped to the correct DescribeExportConfigurations API.
  * Amazon ECS
    - Updated the ECSTaskDefinitionFamilies cmdlet to add a new Status parameter. This enables filtering on active, inactive, or all task definition families.

### 3.1.70.0 (2016-05-18)
  * Application Auto Scaling
    - Added support for Application Auto Scaling, a general purpose Auto Scaling service for supported elastic AWS resources. With Application Auto Scaling, you can automatically scale your AWS resources, with an experience similar to that of Auto Scaling. The cmdlets for this service have the prefix 'AAS' applied to the noun portion of the cmdlet name. The cmdlets and the service APIs they map to can be listed with the command 'Get-AWSCmdletName -Service aas'.

### 3.1.69.0 (2016-05-17)
  * Amazon WorkSpaces
    - Added support for the new tagging APIs with three new cmdlets: Get-WKSTag (DescribeTags API), New-WKSTag (CreateTags API) and Remove-WKSTag (DeleteTags API).

### 3.1.68.0 (2016-05-12)
  * AWS Application Discovery Service [New Service]
    - Added cmdlets to support the new service. The AWS Application Discovery Service helps Systems Integrators quickly and reliably plan application migration projects by automatically identifying applications running in your data center, their associated dependencies, and their performance profile. Cmdlets for this service have the prefix 'ADS' applied to the noun portion of the cmdlet name. The cmdlets and the service APIs they map to can be listed with the command 'Get-AWSCmdletName -Service ads'.
  * Amazon EC2 Simple Systems Management
    - Added support for the new document sharing features with two new cmdlets, Get-SSMDocumentPermission (DescribeDocumentPermission API) and Edit-SSMDocumentPermission (ModifyDocumentPermission API).
  * Amazon EC2
    - Added support for identitfying stale security groups with two new cmdlets, Get-EC2SecurityGroupReference (DescribeSecurityGroupReferences API) and Get-EC2StaleSecurityGroup (DescribeStaleSecurityGroups API).

### 3.1.67.0 (2016-05-10)
  * AWS Storage Gateway
    - Added a new cmdlet, Get-SGTape, to support the new ListTapes API.
  * Amazon Elastic MapReduce
    - Added parameter InstanceState to the Get-EMRInstances cmdlet to support filtering by state.

### 3.1.66.0 (2016-05-05)
  * Amazon EC2
    - The parameter '-Instance' on the Get-EC2Instance and Stop-EC2Instance cmdlets has been renamed to '-InstanceId' to match usage in other EC2 cmdlets (this is a backwards compatible change). The two affected cmdlets continue to accept collections of string instance ids, Amazon.EC2.Model.Instance objects or Amazon.EC2.Model.Reservation objects to specify the instances to operate on.
  * AWS Key Management, Amazon Security Token Service
    - Updated cmdlet documentation to match the latest service API reference.
  * Amazon API Gateway
    - Updated the Write-AGIntegration cmdlet to support a new parameter, -PassthroughBehavior. This parameter enables you to configure pass-through behavior for incoming requests based on the Content-Type header and the available request templates defined on the integration.

### 3.1.65.0 (2016-05-03)
  * Amazon Cognito Identity Provider
    - Added support for the new Cognito Identity Provider service. The cmdlets for this service have the noun prefix 'CGIP'; you can view the set of cmdlets available and the API functions they map to with the command 'Get-AWSCmdletName -Service cgip'.

### 3.1.64.0 (2016-04-28)
  * Amazon Route53 Domains
    - Add support for new service operations ResendContactReachabilityEmail (Send-R53DContactReachability cmdlet) and GetContactReachabilityStatus (Get-R53DContactReachabilityStatus).
  * AWS OpsWorks
    - Updated the New-OPSDeployment and New-OPSInstance cmdlets to add support for default tenancy selection.

### 3.1.63.0 (2016-04-26)
  * Amazon EC2
    - Added a new cmdlet, Edit-EC2VpcPeeringConnectionOption, to support the new ModifyVpcPeeringConnectionOptions API for VPC peering with ClassicLink.
  * Amazon EC2 Container Registry
    - The repository object output by Get-ECRRepository and other cmdlets has been extended to include the repository URL.

### 3.1.62.0 (2016-04-21)
  * AWS IoT
    - Added support for specifying the SQL rules engine to be used with new parameters on the New-IOTTopicRule and Set-IOTTopicRule cmdlets.
  * AWS Certificate Manager
    - Added support for tagging with new cmdlets Add-ACMCertificateTag (AddTagsToCertificate API), Get-ACMCertificateTagList (ListTagsForCertificate API) and Remove-ACMCertificateTag (RemoveTagsFromCertificate API).

### 3.1.61.0 (2016-04-19)
  * AWS Elastic Beanstalk
    - Added support for automatic platform version upgrades with managed updates with three new cmdlets: Get-EBEnvironmentManagedAction (DescribeEnvironmentManagedAction API), Get-EBEnvironmentManagedActionHistory (DescribeEnvironmentManagedActionHistory API) and Submit-EBEnvironmentManagedAction (ApplyEnvironmentManagedAction API).
  * Amazon Kinesis
    - Added support for enhanced monitoring with two new cmdlets: Enable-KINEnhancedMonitoring (EnableEnhancedMonitoring API) and Disable-KINEnhancedMonitoring (DisableEnhancedMonitoring API).
  * Amazon Kinesis Firehose
    - Updated the Update-KINFDestination and New-KINFDeliveryStream cmdlets with additional parameters to support Elastic Search and Cloudwatch Logs.
  * Amazon S3
    - Added support for S3 Accelerate. This adds two new cmdlets, Write-S3BucketAccelerateConfiguration (PutBucketAccelerateConfiguration API) and Get-S3BucketAccelerateConfiguration (GetBucketAccelerateConfiguration API). Cmdlets for S3 operations that support accelerated endpoints have a new parameter, -UseAccelerateEndpoint.

### 3.1.60.0 (2016-04-12)
  (This version was only released as part of the combined AWS SDK and Tools Windows Installer, and not published to the PowerShell Gallery.)
  * AWS IoT
    - Added support for “Bring your own Certificate” with new cmdlets: Get-IOTCACertificate (DescribeCACertificate API), Get-IOTCACertificateList (ListCACertificates API), Get-IOTCertificateListByCA (ListCertificatesByCA API), Get-IOTRegistrationCode (GetRegistrationCode API), Register-IOTCACertificate (RegisterCACertificate API), Register-IOTCertificate (RegisterCertificate API), Remove-IOTCACertificate (DeleteCACertificate API), Remove-IOTRegistrationCode (DeleteRegistrationCode API) and Update-IOTCACertificate (UpdateCACertificate API).

### 3.1.59.0 (2016-04-08)
  * AWS Directory Service
    - Added support for conditional forwarding with new cmdlets New-DSConditionalForwarder (CreateConditionalForwarder API), Remove-DSConditionalForwarder (DeleteConditionalForwarder API), Get-DSConditionalForwarder (DescribeConditionalForwarders API), Remove-DSTrust (DeleteTrust API) and Update-DSConditionalForwarder (UpdateConditionalForwarder API).

### 3.1.58.0 (2016-04-06)
  * Amazon Security Token Service
    - Added a new cmdlet, Get-STSCallerIdentity, to support the new GetCallerIdentity API. This API returns details about the credentials used to make an API call.
  * Amazon Route 53
    - Updated the Update-R53HealthCheck cmdlet with new parameters, -InsufficientDataHealthStatus and -AlarmIdentifier_Name, to support CloudWatch metric-based health checks.
  * Amazon API Gateway
    - Added new cmdlets, Import-AGRestApi and Write-AGRestAopi, to support the ImportRestApi and PutRestApi operations.
  * Amazon Inspector
    - The cmdlets for this service have been recreated to match a major revision to the service API. Please note that existing scripts that use this service will need to be updated to work with the new API.

### 3.1.57.0 (2016-03-29)
  * AWS CloudFormation
    - Added support for ChangeSets. ChangeSets give users detailed information into what CloudFormation intends to perform when changes are executed to a stack, giving users the ability to preview and change the results before actually applying them. The new cmdlets are: Get-CFNChangeSet (DescribeChangeSet API), Get-CFNChangeSetList (ListChangeSets API), New-CFNChangeSet (CreateChangeSet API), Remove-CFNChangeSet (DeleteChangeSet API) and Start-CFNChangeSet (ExecuteChangeSet API).
  * Amazon ElastiCache
    - The Copy-ECSnapshot cmdlet has been updated to remove the -TargetBucket parameter. This parameter was included in a previous release due to an error in the service model.
  * Amazon Redshift
    - Added support for Cluster IAM Roles. Clusters can now assume an IAM Role to perform operations like COPY/UNLOAD as well as cryptographic operations involving KMS keys. This support includes a new cmdlet, Edit-RSClusterIamRoles, for the new ModifyClusterIamRoles API and the addition of an -IamRole parameter to the New-RSCluster and Restore-RSClusterFromSnapshot cmdlets.
  * AWS WAF
    - Added support for XSS (Cross-site scripting) protection in WAF with new cmdlets Get-WAFXssMatchSet (GetXssMatchSet API), Get-WAFXssMatchSetList (ListXssMatchSets API), New-WAFXssMatchSet (CreateXssMatchSet API), Remove-WAFXssMatchSet (DeleteXssMatchSet API) and Update-WAFXssMatchSet (UpdateXssMatchSet API).

### 3.1.56.0 (2016-03-25)
  * Amazon RDS
    - Extended the Edit-RDSDBInstance, New-RDSDBInstance and Restore-RDSDBInstanceFromDBSnapshot cmdlets with new parameters, -Domain and -DomainIAMRoleName, to support Windows Authentication for RDS SQL Server instances.
  * Amazon ElastiCache
    - Added support to allow vertically scaling from one instance type to another. This support consists of one new cmdlet, Get-ECAllowedNodeTypeModification (ListAllowedNodeTypeModifications API) and a new parameter, -CacheNodeType, added to the Edit-ECCacheCluster and Edit-ECReplicationGroup cmdlets.
  * AWS Storage Gateway
    - Added a new cmdlet, Set-SGLocalConsolePassword, to support the new SetLocalConsolePassword API.

### 3.1.55.0 (2016-03-22)
  * AWS Device Farm
    - Added new cmdlets to support purchasing and managing unmetered devices in a self service manner, and to stop runs which are currently executing. The new cmdlets are: Get-DFOffering (ListOfferings API), Get-DFOfferingStatus (GetOfferingStatus API), Get-DFOfferingTransaction (ListOfferingTransactions API), New-DFOfferingPurchase (PurchaseOffering API), New-DFOfferingRenewal (RenewOffering API) and Stop-DFRun (StopRun API).
  * Amazon RDS
    - Extended the New-RDSDBInstance and Edit-RDSDBInstance cmdlets with a new parameter, PromotionTier, to support customer specifiable fail-over order for read replicas in Amazon Aurora.
  * AWS Elastic Beanstalk
    - Updated the documentation for the cmdlets from the latest service documentation.

### 3.1.54.0 (2016-03-17)
  * AWS Cloud HSM
    - Added support for resource tagging with new cmdlets Set-HSMResourceTag (AddTagsToResource API), Get-HSMResourceTag (ListTagsForResource API) and  Remove-HSMResourceTag (RemoveTagsFromResource API).
  * AWS Marketplace Metering
    - Added support for the new service. Cmdlets for the service share the noun prefix 'MM' and can be listed with the command 'Get-AWSCmdletName -service mm'. At this time the service has a single cmdlet, Send-MMMeteringData (MeterUsage API).
  * Elastic Load Balancing
    - Corrected a help example for New-ELBLoadBalancerPolicy.

### 3.1.53.0 (2016-03-15)
  * AWS Database Migration Service
    - Added support for the new AWS Database Migration Service. Cmdlets for the service share the noun prefix 'DMS' and can be listed with the command 'Get-AWSCmdletName -Service dms'.
  * AWS CodeDeploy
    - Added new cmdlet Get-CDDeploymentGroupBatch to support the new BatchGetDeploymentGroups API.
  * Amazon Simple Email Service
    - Added new cmdlets Get-SESIdentityMailFromDomainAttributes (GetIdentityMailFromDomainAttributes API) and Set-SESIdentityMailFromDomain (SetIdentityMailFromDomain API) to support custom MAIL FROM domains. For more information see the service release notes at http://aws.amazon.com/releasenotes/Amazon-SES/8381987420423821.

### 3.1.52.1 (2016-03-11)
  * Amazon S3
    - Fixed issue with NullReferenceException error being reported when MFA serial and authentication values were supplied to the Remove-S3Object cmdlet.

### 3.1.52.0 (2016-03-10)
  * Amazon Redshift
    - Added support for restore tables from a cluster snapshot to a new table in an active cluster. The new cmdlets are Get-RSTableRestoreStatus (DescribeTableRestoreStatus API) and Restore-RSTableFromClusterSnapshot (RestoreTableFromClusterSnapshot API). For more information on this feature see <a href="http://docs.aws.amazon.com/redshift/latest/mgmt/working-with-snapshots.html#working-with-snapshot-restore-table-from-snapshot">Restoring a Table from a Snapshot</a>.

### 3.1.51.0 (2016-03-08)
  * Amazon Kineses
    - Added support for dataplane operations with new cmdlets Get-KINRecord (GetRecords API), Get-KINShardIterator (GetShardIterator API), Write-KINRecord (PutRecord API) and Write-KINMultipleRecord (PutRecords API). Write-KINRecord allows the data to be written to be specified using either a memory stream, simple text string or path to a file containing the data. Write-KINMultipleRecord currently supports only a memory stream within each individual record instance.
  * AWS CodeCommit
    - Added support for repository triggers and retrieving information about a commit. The new cmdlets are Get-CCCommit (GetCommit API), Get-CCRepositoryTrigger (GetRepositoryTriggers API), Set-CCRepositoryTrigger (PutRepositoryTriggers API) and Test-CCRepositoryTrigger (TestRepositoryTriggers API).

  * Amazon Kinesis Firehose
    - The Write-KINFRecord cmdlet has been updated to use the same parameter names for data sources as the new Write-KINRecord cmdlet for Amazon Kinesis. In addition support for supplying data in a file and supplying the name of the file to the cmdlet has been added. The new parameter names, -Blob (MemoryStream), -Text (simple text string) and -FilePath (fully qualified name of source data file) have had the original parameter names (Record_Data, Record_Text and Record_FilePath respectively) applied as aliases for backwards compatibility. These aliases have also been applied to the equivalent parameters in Write-KINRecord for consistency.

### 3.1.50.0 (2016-03-03)
  * AWS Directory Service
    - Added support for SNS topic notifications with new cmdlets Register-DSEventTopic (RegisterEventTopic API), Get-DSEventTopic (DescribeEventTopics API) and Unregister-DSEventTopic (DeregisterEventTopic API).

### 3.1.49.0 (2016-03-01)
  * Amazon API Gateway
    - Added new cmdlets, Clear-AGStageAuthorizerCache (FlushStageAuthorizersCache API) and Test-AGInvokeAuthorizer (TestInvokeAuthorizer API).
  * Amazon DynamoDB
    - Added new cmdlet, Get-DDBProvisionLimit (DescribeLimits API). This cmdlet enables you to query the current provisioning limits for your account.

### 3.1.48.0 (2016-02-26)
  * Auto Scaling
    - Added new InstanceId parameter to the Write-ASLifecycleActionHeartbeat and Complete-ASLifecycleAction cmdlets. Updated parameter documentation in several cmdlets to match latest service documentation.
  * AWS CloudFormation
    -  Added new RetainResource parameter to Remove-CFNStack for to allow the specification the logical IDs of resources that you want to retain after stack deletion.
    - Added Tag parameter to Update-CFNStack to enable updating of tags on stacks.
  * Amazon CloudFront
    - Minor help example and documentation updates.
  * Amazon CloudWatch Logs
    - Minor documentation update to several cmdlets.

### 3.1.47.0 (2016-02-23)
  * This version contained documentation updates for Amazon Route 53 and only released as part of the combined AWS SDK and Tools Windows Installer.

### 3.1.46.0 (2016-02-19)
  * This version contained minor documentation updates and only released as part of the combined AWS SDK and Tools Windows Installer.

### 3.1.45.0 (2016-02-18)
  * AWS Storage Gateway
    - Added a new cmdlet, New-SGTapeWithBarcode, to support the CreateTapeWithBarcode API.
  * AWS CodeDeploy
    - Added two new cmdlets, Get-CDApplicationRevisionBatch and Get-CDDeploymentInstanceBatch to support the BatchGetApplicationRevisions and BatchGetDeploymentInstances APIs.

### 3.1.44.0 (2016-02-17)
  * Amazon RDS
    - Updated the Copy-RDSDBSnapshot cmdlet with a new parameter, KmsKeyId, to support cross-account encrypted snapshot sharing and updated the documentation for several other cmdlets.

### 3.1.43.0 (2016-02-11)
  * Amazon API Gateway
    - Added new cmdlets to support custom request authorizers. With custom request authorizers, developers can authorize their APIs using bearer token authorization strategies, such as OAuth using an AWS Lambda function. The new cmdlets are Get-AGAuthorizer (GetAuthorizer API), Get-AGAuthorizerList (GetAuthorizers API), Get-AGExport (GetExport API), New-AGAuthorizer (CreateAuthorizer API), Remove-AGAuthorizer (DeleteAuthorizer API) and Update-AGAuthorizer (UpdateAuthorizer API).
  * AWS Lambda
    - Updated the Update-LMFunctionConfiguration cmdlet to add support for configuring a Lambda function to access resources in your VPC. These resources could be AWS service resources (for example, Amazon Redshift data warehouses, Amazon ElastiCache clusters, or Amazon RDS instances), or they could be your own services running on your own EC2 instances. For more information see http://docs.aws.amazon.com/lambda/latest/dg/vpc.html.

### 3.1.42.0 (2016-02-09)
  * Amazon Gamelift
    - Added support for Amazon Gamelift, a managed service that allows game developers the ability to deploy and configure their multiplayer games. The new cmdlets share the noun prefix 'GML'. The cmdlets and their mapping to the service APIs can be listed using the command 'Get-AWSCmdletName -Service gamelift' (or 'Get-AWSCmdletName -Service gml').
  * Amazon EC2
    - Updated the Get-EC2ImageByName cmdlet to fix an issue where the control file mapping the logical version-independent keys to filters could not be downloaded if a proxy was in use, causing the cmdlet to error out.
  * Amazon CloudFront
    - Added a new parameter ViewerCertificate_ACMCertificateARN to the New-CFDistribution and Update-CFDistribution cmdlets. This field replaces the ViewerCertificate_CertificateSource and ViewerCertificate_Certificate parameters that were recently added.
  * AWS Marketplace Commerce Analytics
    - Updated the New-MCADataSet cmdlet with a new parameter, CustomerDefinedValue. This parameter allows customers to submit arbitrary key/value pair strings which will be returned, as provided, in the response, enabling the user of customer-provided identifiers to correlate responses with their internal systems.
  * AWS Config
    - Documentation update for parameters in several cmdlets.

### 3.1.41.0 (2016-01-28)
  * AWS WAF
    - Added new cmdlets to support the constraint set APIs. These APIs can be used to block, allow, or monitor (count) requests based on the content in HTTP request bodies. This is the part of a request that contains any additional data that you want to send to your web server as the HTTP request body, such as data from an HTML form. The new cmdlets are: Get-WAFSizeConstraintSet (GetSizeConstraintSet API), Get-WAFSizeConstraintList (ListSizeConstraintSets API), New-WAFSizeConstraintSet (CreateSizeConstraintSet API), Remove-WAFSizeConstraintSet (DeleteSizeConstraintSet API) and Update-WAFSizeConstraintSet (UpdateSizeConstraintSet API).

### 3.1.40.0 (2016-01-21)
  * AWS Certificate Manager
    - New cmdlets have been added to support the new service. The cmdlets have the noun prefix 'ACM' and can be listed with the command 'Get-AWSCmdletName -Service ACM'. AWS Certificate Manager (ACM) is an AWS service that makes it easier for you to deploy secure SSL based websites and applications on the AWS platform.
  * AWS IoT
    - Added two new cmdlets, Enable-IOTTopicRule (EnableTopicRule API) and Disable-IOTTopicRule (DisableTopicRule API) to support the latest service updates.
  * AWS CloudFormation
    - Added a new cmdlet, Resume-CFNUpdateRollback to support the new ContinueUpdateRollback API.
  * all services
    - Updated the 'Related Links' data in the native PowerShell file to enable use of -Online with Get-Help. Previously this command and option would launch a browser onto the home page for the AWSPowerShell cmdlet reference. It now navigates to the online page for the cmdlet specified to Get-Help. Additional links pointing to the service API reference home page and user/developer guides, as appropriate, have also been added to the help file for the module (previously these were only present on the web-based help pages).

### 3.1.39.0 (2016-01-19)
  * This update contains some minor service and documentation updates (for AWS Device Farm, AWS OpsWorks and Amazon Security Token Service) to maintain versioning compatibility with the underlying AWS SDK for .NET release.

### 3.1.38.0 (2016-01-14)
  * Amazon CloudWatch Events
    - Added cmdlets to support the new Amazon CloudWatch Events service. CloudWatch Events allows you to monitor and rapidly react to changes in your AWS resources. The new cmdlets share the noun prefix 'CWE' and can be listed using the command 'Get-AWSCmdletName -Service CWE'.
  * Amazon EC2
    - Added cmdlets to support the new scheduled instances feature. The cmdlets are: Get-EC2ScheduledInstance (DescribeScheduledInstances API), Get-EC2ScheduledInstanceAvailability (DescribeScheduledInstanceAvailability), New-EC2ScheduledInstance (RunScheduledInstances API) and New-EC2ScheduledInstancePurchase (PurchaseScheduledInstances API).

### 3.1.37.0 (2016-01-12)
  * Amazon EC2
    - Added new cmdlets to support DNS resolution of public hostnames to private IP addresses when queried over ClassicLink. Additionally, you can now access private hosted zones associated with your VPC from a linked EC2-Classic instance. ClassicLink DNS support makes it easier for EC2-Classic instances to communicate with VPC resources using public DNS hostnames. The new cmdlets are: GetEC2VpcClassicLinkDnsSupport (DescribeVpcClassicLinkDnsSupport API), Enable-EC2VpcClassicLinkDnsSupport (EnableVpcClassicLinkDnsSupport API) and Disable-EC2VpcClassicLinkDnsSupport (DisableVpcClassicLinkDnsSupport API).
    - Extended help examples for EC2 cmdlets.

### 3.1.36.2 (2016-01-06)
  * Amazon EC2
    - Corrected parameters for the Get-EC2NetworkInterfaceAttribute cmdlet due to errors in the underlying SDK request class for the operation. The class and the cmdlet should have exposed a single Attribute parameter, not members for each value allowed for 'Attribute'.

### 3.1.36.1 (2015-12-22)
  * Initialize-AWSDefaults
    - Fixed further reported issue with the cmdlet reporting 'profile not found' error with no profile name detailed.
    - Fixed null reference error when running on EC2 instance launched with instance profile (for credentials), when trying to save profile containing only region data.

### 3.1.36.0 (2015-12-21)
  * Set-AWSSamlRoleProfile
    - Fixed issue in parsing SAML assertions containing role ARNs from different accounts with the same role names.
  * Amazon EC2 Container Registry (ECR)
    - Added support for the new EC2 Container Registry service, a secure, fully-managed Docker image registry that makes it easy for developers to store and retrieve Docker container images. The cmdlets for the service have the noun prefix 'ECR' and can be enumerated using the command 'Get-AWSCmdletName -Service ecr'.
  * Amazon ECS
    - Add support for deployment configurations to New/Update-ECSService cmdlets.
  * Amazon Elastic MapReduce
    - Update Start-EMRJobFLow cmdlet to accept the Instances_ServiceAccessSecurityGroup parameter.

### 3.1.35.0 (2015-12-17)
  * Fixed issue in AWS SDK for .NET causing 'Path cannot be the empty string or all whitespace' errors being output when running cmdlets on systems with account that had no home or profile folder (https://github.com/aws/aws-sdk-net/issues/287).
  * Amazon EC2
    - Added cmdlets Get-EC2NatGateway (DescribeNatGateways API), New-EC2NatGateway (CreateNatGateway API) and Remove-EC2NatGateway (DeleteNatGateway API) to support the new Managed NAT for VPCs feature.
  * AWS Config
    - Added RecordingGroup_IncludeGlobalResourceType parameter to Write-CFGConfigurationRecorder parameter to support Identity and Access Management (IAM) resource types.
  * Amazon CloudFront
    - Added new parameters to the New-CFDistribution and Update-CFDistribution cmdlets. For web distributions, you can now configure CloudFront to automatically compress files of certain types for both Amazon S3 and custom origins, so downloads are faster and your web pages render faster. Compression also reduces your CloudFront data transfer cost because you pay for the total amount of data served.
  * Amazon RDS
    - Add support for enhanced monitoring in RDS instances via new MonitoringInterval and MonitoringRoleArn parameters on the Edit-RDSDBInstance, New-RDSDBInstance and New-RDSDBInstanceReadReplica cmdlets.
  * AWS CloudTrail
    - Added support for trails that apply across all regions, and support for multiple trails per region.

### 3.1.34.0 (2015-12-15)
  * Fixed issue with Initialize-AWSDefaults reporting 'profile not found' error (with no profile name listed) when entering access and secret key credentials if the host system did not already have a profile named 'default'.
  * Amazon EC2
    - Added new parameters to Copy-EC2Image cmdlet (CopyImage API) enabling AMI copies where all the associated EBS snapshots are encrypted.

### 3.1.33.0 (2015-12-08)
  * Fixed issue with 'profile not found' errors when referencing credential profiles contained in the text-format credential files shared with the AWS CLI.
  * Auto Scaling
    - Added new cmdlet, Set-ASInstanceProtection to support the new SetInstanceProtection API.
  * Amazon RDS
    - Updated the New-RDSDBCluster, Restore-RDSDBClusterFromSnapshot and Restore-RDSDBClusterToPointInTime cmdlets to support encryption using keys managed through AWS Key Management System.

### 3.1.32.0 (2015-12-03)
  * AWS Directory Service
    - Added support for managed directories with new cmdlets Approve-DSTrust (VerifyTrust API), Get-DSTrust (DescribeTrusts API), New-DSMicrosoftAD (CreateMicrosoftAD API), New-DSTrust (CreateTrust API) and Remove-DSTrust (DeleteTrust API).
  * Amazon RDS
    - Added support for modifying DB port number with a new parameter, DBPortNumber, on the Edit-RDSDBInstance cmdlet.
  * Amazon Route 53
    - Added support for the new traffic policy APIs. Running the command 'Get-AWSCmdletName -Service R53 -ApiOperation Traffic -MatchWithRegex' will list the new cmdlets and the corresponding service APIs.
  * AWS Support ApiOperation
    - Fixed bug where the cmdlets did not fallback to assume us-east-1 region if no -Region parameter was supplied or a default region was set in the current shell. Note that if a default shell region is set up and it is not us-east-1 then the -Region parameter, with value us-east-1, must be supplied to the cmdlets when they are run.

### 3.1.31.0 (2015-12-01)
  * Added support for federated user identity with Active Directory Federation Services (AD FS). Two new cmdlets, Set-AWSSamlEndpoint and Set-AWSSamlRoleProfile, enable configuration of a provider endpoint (supporting SAML based identity federation) and one or more role profiles for roles a user is authorized to assume. Once configured the role profiles can then be used with the -ProfileName parameter to the Set-AWSCredentials cmdlet, or any cmdlet that makes AWS service operation calls, to obtain temporary time limited AWS credentials with automatic credential refresh on expiry.

### 3.1.30.0 (2015-11-23)
  * Amazon EC2 Container Service
    - Added support for task stopped reasons and task start/stop times. You can now see if a task was stopped by a user or stopped due to other reasons such as a failing Elastic Load Balancing health check, as well as the time the task was started and stopped. Service scheduler error messages have additional information that describe why tasks cannot be placed in the cluster.
  * AWS Elastic Beanstalk
    - Added support for composable web applications. Applications that consist of several linked modules (micro services architecture) can now deploy, manage, and scale their applications using EB with the new Group-EBEnvironments cmdlet (ComposeEnvironments API).
  * Amazon EC2
    - Added support for dedicated hosts. This feature enables the use of your own per-socket and per-core OS licenses in EC2. This release also supports two new cmdlets Edit-EC2IdFormat ()ModifyIdFormat API) and Get-EC2IdFormat (DescribeIdFormat API) that will be used to manage the transition to longer EC2 and EBS resource IDs. These APIs are reserved for future use.

### 3.1.29.0 (2015-11-19)
  * This version contained updates to the underlying AWS SDK for .NET components and was only distributed in the downloadable AWS Tools for Windows msi installer.

### 3.1.28.0 (2015-11-13)
  * Amazon RDS
    - Updated the Edit-RDSDBInstance with a new parameter, -PubliclyAccessible, to support the new instance visibility service update. Also updated documentation on several cmdlets to note support for the new M4 instance types.

### 3.1.27.0 (2015-11-10)
  * Amazon API Gateway
    - Added suppport for stage variables to the New-AGDeployment, New-AGStage and Test-AGInvokeMethod cmdlets with a new parameter, -Variable, (of Hashtable type). Stage variables act like environment variables for use in your API setup and mapping templates and enable configuration of different deployment stages (e.g., alpha, beta, production) of your API.

### 3.1.26.0 (2015-11-07)
  * AWS IoT
    - Fixed an issue affecting New-IOTKeysAndCertificate, Update-IOTCertificate and others where 'signature mismatch' errors were being returned.

### 3.1.25.0 (2015-11-03)
  * AWS DeviceFarm
    - Added new cmdlets to support the AWS Device Farm APIs to manage projects, device pools, runs, and uploads.
  * Amazon S3
    - Added a new parameter, -StorageClass, to the Write-S3Object cmdlet. This parameter enables support for selecting the STANDARD_IA storage class as well as STANDARD and REDUCED_REDUNDANCY classes. The existing switch parameters for specifying storage class, -StandardStorage and -ReducedRedundancyStorage, have been marked as deprecated.
  * Get-AWSCmdletName
    - Fixed an issue with the -AwsCliCommand option throwing errors if the service identifier parsed from the command matched more than one service by prefix or name (for example 'ec2' matches both Amazon EC2 by prefix and EC2 Container Service by name). The option now looks at the prefix codes in preference and will only attempt to match on name terms if the prefix lookup yields no results.

### 3.1.24.0 (2015-11-02)
  * AWS Identity and Access Management
    - Updated cmdlets for the IAM policy simulator to help test, verify, and understand resource-level permissions.
  * Amazon API Gateway
    - Updated the Write-AGIntegration cmdlet to fix an issue with incorrect marshalling of requests to the service.

### 3.1.23.0 (2015-10-27)
  * Amazon API Gateway
    - Added support for Amazon API Gateway, a fully managed service that makes it easy for developers to create, publish, maintain, monitor, and secure APIs at any scale. The noun prefix for cmdlets belonging to the service is 'AG'.

### 3.1.22.0 (2015-10-26)
  * Amazon EC2 Simple Systems Management
    - Added support for EC2 Run Command APIs, a new EC2 feature that enables you to securely and remotely manage the configuration of your Amazon EC2 Windows instances. Run Command provides a simple way of automating common administrative tasks like executing scripts, running PowerShell commands, installing software or patches and more.
  * Amazon Kinesis Firehose
    - Added parameter 'Record_Text' to the Write-KINFRecord cmdlet to enable write of simple text records to the service.

### 3.1.21.0 (2015-10-22)
  * AutoScaling
    - Adding support for EBS encryption in block device mappings.
  * IdentityManagement
    - Enable Policy Simulator API to do simulation with resource-based policies.
  * Get-AWSCmdletName
    - Added support for listing all cmdlets for services matching the name/prefix supplied to the -Service parameter.

### 3.1.20.0 (2015-10-15)
  * KeyManagementService
    - Add support for deleting Customer Master Keys, including two new APIs for scheduling and canceling key deletion.

### 3.1.19.1 (2015-10-11)
  * AWS IoT
    - AWS IoT offering enables our users to leverage the AWS Cloud for their Internet of things use-cases. Cmdlets for the service are identified with a noun prefix code of 'IOT'.
  * AWS Lambda
    - Lambda now supports function versioning.
  * Amazon ECS
    - Task definitions now support more Docker options.

### 3.1.18.0 (2015-10-07)
  * Amazon Kinesis Firehose
    - Amazon Kinesis Firehose is a fully managed service for ingesting data streams directly into AWS data services such as Amazon S3 and Amazon Redshift. Cmdlets for the service are identified with a noun prefix code of 'KINF'
  * Amazon Inspector
    - Amazon Inspector is a new service from AWS that identifies security issues in your application deployments. Cmdlets for the service are identified with a noun prefix code of 'INS'
  * AWS Marketplace Commerce Analytics
    - The AWS Marketplace Commerce Analytics service allows marketplace partners to programmatically request business intelligence data from AWS Marketplace. Cmdlets for the service are identified with a noun prefix code of 'MCA'.
  * AWS Config
    - Added new cmdlets to support Config Rule and Evaluations.
  * Amazon Kinesis
    - Added two new Amazon Kinesis cmdlets Request-KINStreamRetentionPeriodDecrease, and Request-KINStreamRetentionPeriodIncrease that allow customers to choose how long their data records are stored in their Amazon Kinesis streams.

### 3.1.17.0 (2015-10-06)
  * Amazon
    - Added support for integrating CloudFront with AWS WAF
  * EC2
    - Added new property BlockDurationMinutes to Request-EC2SpotInstance cmdlet. This specifies the duration for which the instance is required.
  * WAF
    - Added support for the new service. Cmdlets for the service are identified with a noun prefix code of 'WAF'.

### 3.1.16.1 (2015-10-02)
  * Amazon Elasticsearch
    - Added support for the new service. Cmdlets for the service are identified with a noun prefix code of 'ES'.

### 3.1.16.0 (2015-10-01)
  * AWS CloudTrail
    - Added new cmdlets for AWS CloudTrail: Add-CTTag, Get-CTTag, Remove-CTTag, and Get-PublicKey. This release of CloudTrail includes support for log file integrity validation, log encryption with AWS KMS–Managed Keys (SSE-KMS), and trail tagging.
  * Amazon RDS
    - Added support for t2.large DB instance, support for copying tags to snapshot, and other minor updates.

### 3.1.15.0 (2015-09-28)
  * Amazon Simple Email Service
    - Amazon Simple Email Service can now accept incoming emails. You can configure Amazon SES to deliver messages to an Amazon S3 bucket, call an AWS Lambda function, publish notifications to Amazon SNS, drop messages, or bounce messages. Added new cmdlets to support this feature.
  * AWS CloudFormation
    - Added new Get-CFNAccountLimits cmdlet, added the ResourceType parameter to New-CFNStack and Update-CFNStack cmdlets.
  * Amazon EC2
    - Added a new cmdlet - Request-EC2SpotFleet.

### 3.1.14.0 (2015-09-17)
  * Amazon CloudWatch Logs
    - Added new cmdlets (Get-CWLExportTasks, New-CWLExportTask, Stop-CWLExportTask) to support exporting log data from Log Groups to Amazon S3 Buckets.

### 3.1.13.0 (2015-09-16)
  * SDK Update for Amazon S3
    - Updated the awssdk.s3.dll assembly to include the new STANDARD_IA storage class and support for multiple lifecycle transitions. There are no changes to the Amazon S3 cmdlets.

### 3.1.12.0 (2015-09-15)
  * Amazon EC2
    - Updated the Request-EC2SpotFleet cmdlet with support for specifying allocation strategy.
    - Get-EC2Snapshot now supports additional DataEncryptionKeyId and StateMessage parameters in the returned Snapshot object.
  * Amazon Elastic File System
    - Updated the Get-EFSMountTarget cmdlet to support a new MountTargetId parameter.
  * Amazon EC2 Simple Systems Manager
    - Fixed bug in the automatic pagination support in Get-SSMDocumentList cmdlet. The cmdlet caused the service to respond with an error message due to the MaxResults parameter being set higher than 25.
  * Amazon Route 53
    - Updated the New-R53HealthCheck and Update-R53HealthCheck cmdlets to add support for calculated and latency health checks.

### 3.1.11.0 (2015-09-10)
  * Amazon S3
    - Reworked progress indication for folder uploads so that the % progress bar tracks bytes uploaded versus total to send for all files. In previous versions it tracked the number of files uploaded which could lead to periods of no progress activity if the files were large.
  * AWS Identity and Access Management
    - Added new cmdlets (Get-IAMContextKeysForCustomPolicy, Get-IAMContextKeysForPrincipalPolicy, Test-IAMCustomPolicy and Test-IAMPrincipalPolicy) to support programmatic access to the IAM policy simulator. The SimulatePrincipalPolicy API (Test-IAMPrincipalPolicy) allows you to programmatically audit permissions in your account and validate a specific user’s permissions. The SimulateCustomPolicy API (Test-IAMCustomPolicy) provides a way to verify a new policy before applying it. These new APIs allow you to automate the validation and auditing of permissions for your IAM users, groups, and roles.

### 3.1.10.0 (2015-09-03)
  * Amazon S3
    - Fixed issue in AWS SDK for .NET that caused Read-S3Object to fail to download a hierarchy of files if zero length files were present in subfolders.
  * AWS Storage Gateway
    - Added new cmdlets (Add-SGResourceTag, Get-SGResourceTags and Remove-SGResourceTag) to support tagging Storage Gateway resources.

### 3.1.9.0 (2015-08-31)
  * Amazon S3
    - Fixed issue with -Verbose option reporting an incorrect count of uploaded files when Write-S3Object cmdlet completed processing.

### 3.1.8.0 (2015-08-27)
  * Amazon CloudFront
    - Added two cmdlets, New-CFSignedUrl and New-CFSignedCookie. These cmdlets enable you to generate signed urls and cookies, using canned or custom policies, to content in CloudFront distributions.
  * AWS Config
    - Added a new cmdlet, Get-CFGDiscoveredResource, to support the new ListDiscoveredResources service api.

### 3.1.7.0 (2015-08-27)
  * This version contained updates to the underlying AWS SDK for .NET components and was only distributed in the downloadable AWS Tools for Windows msi installer.

### 3.1.6.0 (2015-08-25)
  * This version contained updates to the underlying AWS SDK for .NET components and was only distributed in the downloadable AWS Tools for Windows msi installer.

### 3.1.5.1 (2015-08-17)
  * Amazon Cognito Identity
    - Added cmdlets for the control plane APIs. These new cmdlets have the noun prefix 'CGI'.

### 3.1.5.0 (2015-08-12)
  * Elastic Beanstalk
    - Added new cmdlets to support the new instance health APIs: Get-EBEnvironmentHealth (DescribeEnvironmentHealth API) and Get-EBInstanceHealth (DescribeInstancesHealth API).
  * Fixes
    - The Set-AWSCredentials and Initialize-AWSDefaults cmdlets have been updated to use the WriteVerbose api instead of directly writing to the console, allowing their output to be suppressed.

### 3.1.4.0 (2015-08-06)
  * This version contained updates to the underlying AWS SDK for .NET components and was only distributed in the downloadable AWS Tools for Windows msi installer.

### 3.1.3.0 (2015-08-04)
  * DeviceFarm
    - Updated DeviceFarm cmdlets with latest service features, adding support for iOS and retrieving account settings.

### 3.1.2.0 (2015-07-30)
* RDS
    - Add support for Amazon Aurora.
* OpsWorks
    - Add support for ECS clusters.

### 3.1.1.0 (2015-07-28)
* CloudWatch Logs
    - Added cmdlets for 4 new APIs to support cross-account subscriptions. These allow you to collaborate with an owner of a different AWS account and receive their log events on your Amazon Kinesis stream (cross-account data sharing). The new cmdlets are Write-CWLDestination (PutDestination), Write-CWLDestinationPolicy (PutDestinationPolicy), Get-CWLDestination (DescribeDestinations) and Remove-CWLDestination (DeleteDestination).

### 3.1.0.0 (2015-07-28)
* The AWS Tools for Windows PowerShell have been updated to use the new version 3 of the AWS SDK for .NET. The version numbers for AWS SDK for .NET and AWS Tools for Windows PowerShell are kept in sync which is the reason for this major version bump to 3.1. There are otherwise no major changes to AWS Tools for Windows PowerShell.
