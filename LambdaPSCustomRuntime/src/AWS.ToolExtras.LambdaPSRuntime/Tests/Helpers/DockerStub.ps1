# Pester can only mock a command that RESOLVES: Mock looks the name up first and throws
# 'Could not find Command docker' when it cannot. The docker tests never run docker -- every call is
# mocked -- but on a host without the CLI installed the lookup itself fails, so the whole file errors
# out instead of running offline as intended. Add-LambdaPSDockerStub puts a stand-in docker
# executable first on PATH, so the name resolves as an application, the way the module looks docker
# up (Get-Command -CommandType Application), from both the test session and the module's session
# state (which is what 'Mock -ModuleName' searches). A real docker is not shadowed when one is
# present. Call Remove-LambdaPSDockerStub from the same file's AfterAll.

function Add-LambdaPSDockerStub {
    <#
    .SYNOPSIS
        Puts a stand-in docker executable first on PATH, unless a real one is installed.
    .DESCRIPTION
        The stand-in is a script (docker.cmd on Windows) that appends its arguments, one line per call,
        to docker-calls.log beside it, and exits 0. It is created under $TestDrive (or a new temporary
        folder outside a test), and returned as an object with Path and CallLog; nothing is returned
        when a real docker is installed.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Test helper: it writes a stand-in executable and changes PATH for the test run.')]
    param()

    if ($script:LambdaPSDockerStub) { return $script:LambdaPSDockerStub }
    if (Get-Command -Name 'docker' -CommandType Application -ErrorAction SilentlyContinue) { return }

    $root = if (Get-Variable -Name 'TestDrive' -ValueOnly -ErrorAction Ignore) { $TestDrive } else { [System.IO.Path]::GetTempPath() }
    $directory = [System.IO.Path]::Combine($root, "docker-stub-$([guid]::NewGuid().ToString('n'))")
    $null = New-Item -ItemType Directory -Path $directory
    $callLog = [System.IO.Path]::Combine($directory, 'docker-calls.log')
    if ($IsWindows) {
        $path = [System.IO.Path]::Combine($directory, 'docker.cmd')
        Set-Content -LiteralPath $path -Value "@echo %*>>`"$callLog`""
    }
    else {
        $path = [System.IO.Path]::Combine($directory, 'docker')
        Set-Content -LiteralPath $path -Value "#!/bin/sh`nprintf '%s\n' `"`$*`" >> '$callLog'"
        [System.IO.File]::SetUnixFileMode($path, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute')
    }
    $script:LambdaPSDockerStub = [PSCustomObject]@{
        Path         = $path
        CallLog      = $callLog
        Directory    = $directory
        PreviousPath = $env:PATH
    }
    $env:PATH = $directory + [System.IO.Path]::PathSeparator + $env:PATH
    $script:LambdaPSDockerStub
}

function Remove-LambdaPSDockerStub {
    <#
    .SYNOPSIS
        Undoes Add-LambdaPSDockerStub: restores PATH and deletes the stand-in. Does nothing when no
        stand-in was added.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Test helper: it removes only the stand-in Add-LambdaPSDockerStub created.')]
    param()

    if (-not $script:LambdaPSDockerStub) { return }
    $env:PATH = $script:LambdaPSDockerStub.PreviousPath
    Remove-Item -LiteralPath $script:LambdaPSDockerStub.Directory -Recurse -Force -ErrorAction SilentlyContinue
    $script:LambdaPSDockerStub = $null
}
