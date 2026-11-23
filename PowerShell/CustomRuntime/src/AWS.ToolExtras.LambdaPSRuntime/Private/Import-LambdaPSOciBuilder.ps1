function Import-LambdaPSOciBuilder {
    <#
    .SYNOPSIS
        Loads the AWS.OCI.Builder library the no-Docker image build uses.
    .DESCRIPTION
        The library ships in the module's lib/ folder (build.ps1 builds it from Libraries/src/AWS.OCI.Builder).
        It is loaded here, on first use of the no-Docker build, not through RequiredAssemblies: a Docker
        build never pays for it, and a different AWS.OCI.Builder another module already loaded fails only
        this build, with OciBuilderConflict, instead of the whole module import.
    .EXAMPLE
        Import-LambdaPSOciBuilder

        Loads the library, or checks that the copy already loaded is the same version.
    #>
    [CmdletBinding()]
    [OutputType([void])]
    param()

    $dllPath = [System.IO.Path]::Combine((Split-Path -Parent $PSScriptRoot), 'lib', 'AWS.OCI.Builder.dll')
    $loaded = 'AWS.OCI.Builder.OciImageBuilder' -as [type]
    if ($null -eq $loaded) {
        if (-not [System.IO.File]::Exists($dllPath)) {
            # `throw`, so the error stops the caller too (see Invoke-LambdaPSExternalTool).
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.IO.FileNotFoundException]::new(
                    ("The no-Docker image build needs AWS.OCI.Builder.dll, which was not found at '$dllPath'. " +
                     'Reinstall the module, or build with -BuildMethod Docker.'), $dllPath),
                'OciBuilderNotFound', [System.Management.Automation.ErrorCategory]::NotInstalled, $dllPath)
        }
        try { Add-Type -LiteralPath $dllPath -ErrorAction Stop }
        catch {
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.InvalidOperationException]::new(
                    ("AWS.OCI.Builder could not be loaded from '$dllPath': $($_.Exception.Message) Another module may " +
                     'have loaded a different version of it; start a new session, or build with -BuildMethod Docker.'), $_.Exception),
                'OciBuilderConflict', [System.Management.Automation.ErrorCategory]::ResourceUnavailable, $dllPath)
        }
        $loaded = [AWS.OCI.Builder.OciImageBuilder]
    }
    # A copy another module loaded first is used only when it is the same build as ours.
    $expectedVersion = if ([System.IO.File]::Exists($dllPath)) { [System.Reflection.AssemblyName]::GetAssemblyName($dllPath).Version }
    $loadedName = $loaded.Assembly.GetName()
    if ($null -ne $expectedVersion -and $loadedName.Version -ne $expectedVersion) {
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new(
                ("AWS.OCI.Builder $($loadedName.Version) is already loaded from '$($loaded.Assembly.Location)', but this module " +
                 "needs $expectedVersion. Start a new session, or build with -BuildMethod Docker.")),
            'OciBuilderConflict', [System.Management.Automation.ErrorCategory]::ResourceUnavailable, $loaded.Assembly.Location)
    }
}
