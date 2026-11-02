function Invoke-LambdaPSContextPrecompile {
    <#
    .SYNOPSIS
        Compiles PowerShellLambdaContext.cs to a DLL with the bundled pwsh.
    .DESCRIPTION
        Build-LambdaPSRuntimePackage's precompile step. The DLL references the System.Runtime of the .NET
        it was compiled on: built with a newer host (pwsh 7.6 = .NET 10) it cannot load on a bundled 7.4
        (.NET 8), and every invoke fails with the LambdaContext type missing. So it is compiled with the
        bundled pwsh, which the caller only runs on a Linux host of the target architecture.

          - The bundled pwsh's output goes to the host, not to the caller's pipeline.
          - A bundled pwsh that cannot start (a temp folder mounted noexec, or a musl host for the glibc
            build) returns $false, so the caller ships the .cs alone and the runtime compiles it at cold
            start.
          - A pwsh that starts and then fails, or writes no assembly, is a terminating ContextCompileFailed.
    .PARAMETER PwshPath
        The bundled pwsh binary. Its execute bit is set first: a caller-supplied tarball, or one
        extracted onto a file system without Unix modes, may lack it.
    .PARAMETER SourcePath
        The PowerShellLambdaContext.cs to compile.
    .PARAMETER OutputPath
        The DLL to write.
    .OUTPUTS
        System.Boolean

        $true when the DLL was written, $false when the bundled pwsh could not start.
    .EXAMPLE
        Invoke-LambdaPSContextPrecompile -PwshPath $stage/powershell/pwsh -SourcePath $stage/PowerShellLambdaContext.cs -OutputPath $stage/PowerShellLambdaContext.dll

        Writes the DLL next to the source with the bundled pwsh.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$PwshPath,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$SourcePath,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$OutputPath
    )

    # Every quote character PowerShell accepts is escaped, so no path can end the string.
    $escape = { param($Text) [System.Management.Automation.Language.CodeGeneration]::EscapeSingleQuotedStringContent($Text) }
    $compile = "Add-Type -TypeDefinition ([System.IO.File]::ReadAllText('$(& $escape $SourcePath)')) -OutputAssembly '$(& $escape $OutputPath)'"
    [System.IO.File]::SetUnixFileMode($PwshPath, [System.IO.File]::GetUnixFileMode($PwshPath) -bor [System.IO.UnixFileMode]::UserExecute)

    # The exit code is checked below; with the caller's preference on, pwsh would raise its own error first.
    $PSNativeCommandUseErrorActionPreference = $false
    $previousInvariant = $env:DOTNET_SYSTEM_GLOBALIZATION_INVARIANT
    $env:DOTNET_SYSTEM_GLOBALIZATION_INVARIANT = '1'
    try {
        & $PwshPath -NoProfile -NonInteractive -Command $compile 2>&1 | Out-Host
        $exitCode = $LASTEXITCODE
    }
    catch [System.Management.Automation.ApplicationFailedException] {
        Write-Verbose "The bundled pwsh '$PwshPath' could not start: $($_.Exception.Message)"
        return $false
    }
    finally {
        $env:DOTNET_SYSTEM_GLOBALIZATION_INVARIANT = $previousInvariant
    }

    if ($exitCode -ne 0 -or -not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
        $reason = if ($exitCode -ne 0) { "it exited with $exitCode" } else { 'it wrote no assembly' }
        # `throw`, so the error stops the caller too (see Invoke-LambdaPSExternalTool).
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new("The bundled pwsh did not precompile PowerShellLambdaContext.dll: $reason."),
            'ContextCompileFailed', [System.Management.Automation.ErrorCategory]::InvalidResult, $PwshPath)
    }
    $true
}
