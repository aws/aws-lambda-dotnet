$response = @{}
$response.StatusCode = 200
$response.Body = "Hello World from PowerShell in Lambda"
$response.Headers = @{}
$response.Headers.ContentType = "text/plain"

return $response