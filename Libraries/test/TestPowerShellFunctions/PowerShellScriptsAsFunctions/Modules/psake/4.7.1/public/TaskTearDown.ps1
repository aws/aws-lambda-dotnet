
function TaskTearDown {
    <#
        .SYNOPSIS
        Adds a scriptblock to the build that will be executed after each task

        .DESCRIPTION
        This function will accept a scriptblock that will be executed after each task in the build script.

        .PARAMETER teardown
        A scriptblock to execute

        .EXAMPLE
        A sample build script is shown below:

        Task default -depends Test

        Task Test -depends Compile, Clean {
        }

        Task Compile -depends Clean {
        }

        Task Clean {
        }

        TaskTearDown {
            "Running 'TaskTearDown' for task $context.Peek().currentTaskName"
        }

        The script above produces the following output:

        Executing task, Clean...
        Running 'TaskTearDown' for task Clean
        Executing task, Compile...
        Running 'TaskTearDown' for task Compile
        Executing task, Test...
        Running 'TaskTearDown' for task Test

        Build Succeeded

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
        Task
        .LINK
        TaskSetup
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$teardown
    )

    $psake.context.Peek().taskTearDownScriptBlock = $teardown
}
