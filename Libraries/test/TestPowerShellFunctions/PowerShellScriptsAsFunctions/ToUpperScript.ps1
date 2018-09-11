Write-Host "Executing Script"
$LambdaContext.Logger.LogLine("Logging From Context")


$LambdaInput.ToUpper()