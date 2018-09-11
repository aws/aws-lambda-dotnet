function ExecuteInBuildFileScope {
    param([string]$buildFile, $module, [scriptblock]$sb)

    # Execute the build file to set up the tasks and defaults
    Assert (test-path $buildFile -pathType Leaf) ($msgs.error_build_file_not_found -f $buildFile)

    $psake.build_script_file = get-item $buildFile
    $psake.build_script_dir = $psake.build_script_file.DirectoryName
    $psake.build_success = $false

    $psake.context.push(@{
        "taskSetupScriptBlock" = {};
        "taskTearDownScriptBlock" = {};
        "executedTasks" = new-object System.Collections.Stack;
        "callStack" = new-object System.Collections.Stack;
        "originalEnvPath" = $env:PATH;
        "originalDirectory" = get-location;
        "originalErrorActionPreference" = $global:ErrorActionPreference;
        "tasks" = @{};
        "aliases" = @{};
        "properties" = @();
        "includes" = new-object System.Collections.Queue;
        "config" = CreateConfigurationForNewContext $buildFile $framework
    })

    LoadConfiguration $psake.build_script_dir

    set-location $psake.build_script_dir

    LoadModules

    $frameworkOldValue = $framework
    . $psake.build_script_file.FullName

    $currentContext = $psake.context.Peek()

    if ($framework -ne $frameworkOldValue) {
        writecoloredoutput $msgs.warning_deprecated_framework_variable -foregroundcolor Yellow
        $currentContext.config.framework = $framework
    }

    ConfigureBuildEnvironment

    while ($currentContext.includes.Count -gt 0) {
        $includeFilename = $currentContext.includes.Dequeue()
        . $includeFilename
    }

    & $sb $currentContext $module
}
