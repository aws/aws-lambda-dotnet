function ThrowSystemException
{
	throw [System.IO.FileNotFoundException]::new()
}

function CustomException
{
	throw @{'Exception'='AccountNotFound';'Message'='The Account is not found'}
}

function CustomExceptionNoMessage
{
	throw @{'Exception'='CustomExceptionNoMessage'}
}

function ThrowWithStringMessage
{
	throw "Here is your error"
}

function ThrowWithStringErrorCode
{
	throw "ErrorCode42"
}

function WriteErrorWithMessageTest
{
	Write-Error "Testing out Write-Error"
}

function WriteErrorWithExceptionTest
{
	$e = [System.IO.FileNotFoundException]::new()
	Write-Error -Exception $e
}