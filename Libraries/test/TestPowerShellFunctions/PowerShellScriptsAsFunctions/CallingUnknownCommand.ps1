Write-Host "Executing Script"
$LambdaContext.Logger.LogLine("Logging From Context")

# This will fail because this CmdLet doesn't exist
New-MagicBeanCmdLet