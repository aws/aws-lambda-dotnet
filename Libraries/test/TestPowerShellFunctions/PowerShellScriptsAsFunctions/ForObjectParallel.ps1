$SharedVariable = "Hello Shared Variable"
@(1..100) | ForEach-Object -Parallel { 
    $i = $_
    Write-Host "Running against: $i for SharedVariable: $($using:SharedVariable)"
}