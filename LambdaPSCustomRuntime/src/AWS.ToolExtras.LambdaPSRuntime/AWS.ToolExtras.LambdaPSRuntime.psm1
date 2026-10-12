#Requires -Version 7.4

# Attribution token appended to the SDK user-agent (as 'app/<id>') on every AWS call the module
# makes, so adoption of this module can be counted apart from raw cmdlet usage. The identifier is
# treated as STABLE: the 'AWS.ToolExtras.LambdaPSRuntime' portion must not change, since renaming it
# would break continuity of the adoption metric. The version suffix is read from the manifest.
$script:LambdaPSModuleVersion = (Import-PowerShellDataFile -Path ([System.IO.Path]::Combine($PSScriptRoot, 'AWS.ToolExtras.LambdaPSRuntime.psd1'))).ModuleVersion
$script:LambdaPSClientAppId = "AWS.ToolExtras.LambdaPSRuntime#$script:LambdaPSModuleVersion"

# Module settings, one data file per area under Config/ (for example Config/pwsh.psd1 is
# $script:Config.pwsh), so a value such as a pinned pwsh release changes without a code change.
$script:Config = @{}
$configPath = [System.IO.Path]::Combine($PSScriptRoot, 'Config')
# An install whose Config/ is missing, unreadable or incomplete is refused with this error record;
# TargetObject is the path of each file concerned.
$newConfigError = {
    param([string]$Message, [string]$ErrorId, [System.Management.Automation.ErrorCategory]$Category, [string[]]$File)
    [System.Management.Automation.ErrorRecord]::new(
        [System.InvalidOperationException]::new("$Message Reinstall the module."), $ErrorId, $Category,
        @($File | ForEach-Object { [System.IO.Path]::Combine($configPath, $_) }))
}
foreach ($file in @(Get-ChildItem -LiteralPath $configPath -Filter '*.psd1' -ErrorAction SilentlyContinue)) {
    try {
        $script:Config[$file.BaseName] = Import-PowerShellDataFile -Path $file.FullName -ErrorAction Stop
    }
    catch {
        throw (& $newConfigError "The module's Config/$($file.Name) could not be read: $($_.Exception.Message)" 'ConfigUnreadable' InvalidData $file.Name)
    }
}
# Every setting is read without a fallback (a missing function.psd1 would deploy with a 0-second
# timeout), so an install without one of these files or settings is refused here, by name, rather
# than failing or misbehaving later in a helper. A dotted name is a setting inside a table.
$requiredSetting = [ordered]@{
    pwsh     = 'ReleaseUrl', 'Download.RetryCount', 'Download.RetryDelaySeconds', 'Download.ConnectTimeoutSeconds',
               'DefaultPatch', 'ArchiveSha256'
    wait     = 'Function.Attempts', 'Function.IntervalSeconds', 'RuntimeVersion.Attempts', 'RuntimeVersion.IntervalSeconds'
    function = 'Timeout', 'MemorySize'
}
$missingConfig = @($requiredSetting.Keys) | Where-Object { -not $script:Config.ContainsKey($_) }
if ($missingConfig) {
    throw (& $newConfigError ("The module's $(($missingConfig | ForEach-Object { "Config/$_.psd1" }) -join ', ') was not found under " +
        "'$configPath'.") 'ConfigNotFound' ObjectNotFound ($missingConfig | ForEach-Object { "$_.psd1" }))
}
$missingSetting = foreach ($area in $requiredSetting.Keys) {
    foreach ($setting in $requiredSetting[$area]) {
        $node = $script:Config[$area]
        foreach ($segment in $setting.Split('.')) {
            $node = if ($node -is [System.Collections.IDictionary] -and $node.Contains($segment)) { $node[$segment] } else { $null }
        }
        if ($null -eq $node) { [PSCustomObject]@{ Setting = $setting; File = "$area.psd1" } }
    }
}
if ($missingSetting) {
    throw (& $newConfigError ("The module's Config is incomplete. Missing: " +
        "$(($missingSetting | ForEach-Object { "$($_.Setting) in Config/$($_.File)" }) -join ', ').") 'ConfigSettingMissing' InvalidData @($missingSetting.File | Select-Object -Unique))
}

# Dot-source all private helper functions, then public functions.
$private = @(Get-ChildItem -Path ([System.IO.Path]::Combine($PSScriptRoot, 'Private')) -Filter '*.ps1' -ErrorAction SilentlyContinue)
$public = @(Get-ChildItem -Path ([System.IO.Path]::Combine($PSScriptRoot, 'Public')) -Filter '*.ps1' -ErrorAction SilentlyContinue)

foreach ($file in @($private + $public)) {
    . $file.FullName
}

Export-ModuleMember -Function $public.BaseName

# The loader's variables would otherwise stay visible to every function in the module.
Remove-Variable -Name configPath, newConfigError, file, requiredSetting, missingConfig, area, setting, node, segment, missingSetting, private, public -ErrorAction SilentlyContinue
