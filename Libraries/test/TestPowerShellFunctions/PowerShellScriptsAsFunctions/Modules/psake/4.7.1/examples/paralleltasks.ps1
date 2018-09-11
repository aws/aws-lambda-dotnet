Task ParallelTask1 {
    "ParallelTask1"
}

Task ParallelTask2 {
    "ParallelTask2"
}

Task ParallelNested1andNested2 {
    $jobArray = @()
    @("ParallelTask1", "ParallelTask2") | ForEach-Object {
        $jobArray += Start-Job { 
            param($scriptFile, $taskName)
                Invoke-psake $scriptFile -taskList $taskName
            } -ArgumentList $psake.build_script_file.FullName, $_ 
    }
    Wait-Job $jobArray | Receive-Job
}

Task default -depends ParallelNested1andNested2