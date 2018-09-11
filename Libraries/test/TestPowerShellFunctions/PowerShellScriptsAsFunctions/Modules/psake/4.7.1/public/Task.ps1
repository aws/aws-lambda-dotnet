function Task {
    <#
        .SYNOPSIS
        Defines a build task to be executed by psake

        .DESCRIPTION
        This function creates a 'task' object that will be used by the psake engine to execute a build task.
        Note: There must be at least one task called 'default' in the build script

        .PARAMETER name
        The name of the task

        .PARAMETER action
        A scriptblock containing the statements to execute for the task.

        .PARAMETER preaction
        A scriptblock to be executed before the 'Action' scriptblock.
        Note: This parameter is ignored if the 'Action' scriptblock is not defined.

        .PARAMETER postaction
        A scriptblock to be executed after the 'Action' scriptblock.
        Note: This parameter is ignored if the 'Action' scriptblock is not defined.

        .PARAMETER precondition
        A scriptblock that is executed to determine if the task is executed or skipped.
        This scriptblock should return $true or $false

        .PARAMETER postcondition
        A scriptblock that is executed to determine if the task completed its job correctly.
        An exception is thrown if the scriptblock returns $false.

        .PARAMETER continueOnError
        If this switch parameter is set then the task will not cause the build to fail when an exception is thrown by the task

        .PARAMETER depends
        An array of task names that this task depends on.
        These tasks will be executed before the current task is executed.

        .PARAMETER requiredVariables
        An array of names of variables that must be set to run this task.

        .PARAMETER description
        A description of the task.

        .PARAMETER alias
        An alternate name for the task.

        .EXAMPLE
        A sample build script is shown below:

        Task default -Depends Test

        Task Test -Depends Compile, Clean {
            "This is a test"
        }

        Task Compile -Depends Clean {
            "Compile"
        }

        Task Clean {
            "Clean"
        }

        The 'default' task is required and should not contain an 'Action' parameter.
        It uses the 'Depends' parameter to specify that 'Test' is a dependency

        The 'Test' task uses the 'Depends' parameter to specify that 'Compile' and 'Clean' are dependencies
        The 'Compile' task depends on the 'Clean' task.

        Note:
        The 'Action' parameter is defaulted to the script block following the 'Clean' task.

        An equivalent 'Test' task is shown below:

        Task Test -Depends Compile, Clean -Action {
            $testMessage
        }

        The output for the above sample build script is shown below:

        Executing task, Clean...
        Clean
        Executing task, Compile...
        Compile
        Executing task, Test...
        This is a test

        Build Succeeded!

        ----------------------------------------------------------------------
        Build Time Report
        ----------------------------------------------------------------------
        Name    Duration
        ----    --------
        Clean   00:00:00.0065614
        Compile 00:00:00.0133268
        Test    00:00:00.0225964
        Total:  00:00:00.0782496

        .LINK
        Assert
        .LINK
        Exec
        .LINK
        FormatTaskName
        .LINK
        Framework
        .LINK
        Get-PSakeScriptTasks
        .LINK
        Include
        .LINK
        Invoke-psake
        .LINK
        Properties
        .LINK
        TaskSetup
        .LINK
        TaskTearDown
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$name,

        [scriptblock]$action = $null,

        [scriptblock]$preaction = $null,

        [scriptblock]$postaction = $null,

        [scriptblock]$precondition = {$true},

        [scriptblock]$postcondition = {$true},

        [switch]$continueOnError,

        [string[]]$depends = @(),

        [string[]]$requiredVariables = @(),

        [string]$description = $null,

        [string]$alias = $null
    )

    if ($name -eq 'default') {
        Assert (!$action) ($msgs.error_default_task_cannot_have_action)
    }

    $newTask = @{
        Name = $name
        DependsOn = $depends
        PreAction = $preaction
        Action = $action
        PostAction = $postaction
        Precondition = $precondition
        Postcondition = $postcondition
        ContinueOnError = $continueOnError
        Description = $description
        Duration = [System.TimeSpan]::Zero
        RequiredVariables = $requiredVariables
        Alias = $alias
    }

    $taskKey = $name.ToLower()

    $currentContext = $psake.context.Peek()

    Assert (!$currentContext.tasks.ContainsKey($taskKey)) ($msgs.error_duplicate_task_name -f $name)

    $currentContext.tasks.$taskKey = $newTask

    if($alias)
    {
        $aliasKey = $alias.ToLower()

        Assert (!$currentContext.aliases.ContainsKey($aliasKey)) ($msgs.error_duplicate_alias_name -f $alias)

        $currentContext.aliases.$aliasKey = $newTask
    }
}
