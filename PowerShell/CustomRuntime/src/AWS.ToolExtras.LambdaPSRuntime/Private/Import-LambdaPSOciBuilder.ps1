function Import-LambdaPSOciBuilder {
    <#
    .SYNOPSIS
        Loads the AWS.OCI.Builder library the no-Docker image build uses, and its progress sink.
    .DESCRIPTION
        The library ships in the module's lib/ folder (build.ps1 builds it from Libraries/src/AWS.OCI.Builder).
        It is loaded here, on first use of the no-Docker build, not through RequiredAssemblies: a Docker
        build never pays for it, and a different AWS.OCI.Builder another module already loaded fails only
        this build, with OciBuilderConflict, instead of the whole module import.

        The library reports push progress through IProgress<LayerPushProgress> on thread-pool threads.
        Neither a scriptblock (System.Progress<T> crashes pwsh: no runspace on that thread) nor a
        PowerShell class (it shares its runspace across threads, and loses events or faults under
        concurrent reports) can receive them, so a small C# sink that only queues them is compiled
        here, once per session. The caller drains the queue on the pipeline thread.
    .OUTPUTS
        System.Type

        The progress sink type, LambdaPSOciProgressQueue[AWS.OCI.Builder.LayerPushProgress].
    .EXAMPLE
        $sinkType = Import-LambdaPSOciBuilder

        Loads the library and returns the sink type to create a queue from.
    #>
    [CmdletBinding()]
    [OutputType([type])]
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

    # Generic, so it compiles without a reference to the library: a net8.0 reference compiled on a newer
    # .NET (pwsh 7.6) fails Add-Type with CS1701 (System.Runtime 8.0 vs 10.0).
    if ($null -eq ('LambdaPSOciProgressQueue`1' -as [type])) {
        Add-Type -ReferencedAssemblies 'System.Collections.Concurrent', 'System.Runtime' -TypeDefinition @'
public sealed class LambdaPSOciProgressQueue<T> : System.IProgress<T>
{
    public System.Collections.Concurrent.ConcurrentQueue<T> Items { get; } = new();
    public void Report(T value) => Items.Enqueue(value);
}
'@
    }
    # Resolved by name at run time: neither type exists when this file is parsed.
    'LambdaPSOciProgressQueue[AWS.OCI.Builder.LayerPushProgress]' -as [type]
}
