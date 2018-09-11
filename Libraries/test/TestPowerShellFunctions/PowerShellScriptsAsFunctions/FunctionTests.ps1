

function ToLowerWithBothParams {
	param([PSObject]$lambdaInput,[object]$context)

	Write-Host "Calling ToLower with both parameters"
	Write-Host $context.ToString()

	return $lambdaInput.ToString().ToLower()
}

function ToLowerNoContext {
	param([PSObject]$lambdaInput)

	Write-Host "Calling ToLower with no context"

	return $lambdaInput.ToString().ToLower()
}


function NoParameters {

	Write-Host "Calling NoParameters"
}