BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'PwshConfig.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Zip.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeModule.ps1'))

    # A fake pwsh tarball (a stand-in 'pwsh' file at the archive root) so the build runs offline
    # via -PwshArchivePath without downloading the real ~70 MB interpreter.
    $fakeSrc = [System.IO.Path]::Combine($TestDrive, 'fake-pwsh-src')
    $null = New-Item -ItemType Directory -Path $fakeSrc -Force
    # The build runs the BUNDLED pwsh to precompile PowerShellLambdaContext.dll (so it targets the right
    # .NET), so the stand-in forwards to the host pwsh and leaves a marker proving it was the one run.
    $script:bundledRanMarker = [System.IO.Path]::Combine($TestDrive, 'bundled-pwsh-ran')
    Set-Content -LiteralPath ([System.IO.Path]::Combine($fakeSrc, 'pwsh')) -Value "#!/bin/sh`ntouch '$script:bundledRanMarker'`nexec '$([System.Environment]::ProcessPath)' `"`$@`""
    # The stand-in pwsh is NOT executable, as in a tarball extracted onto a file system without Unix
    # modes: the build must set the bit itself. Windows has no chmod and no executable bit to clear (a
    # file there is already the 'not executable' case), and the zip writer stamps 0755 on the named
    # executable entries whatever OS it runs on, so the assertions below hold either way.
    if (-not $IsWindows) { & chmod 644 ([System.IO.Path]::Combine($fakeSrc, 'pwsh')) }
    $script:fakeTarball = [System.IO.Path]::Combine($TestDrive, 'fake-pwsh.tar.gz')
    # The tar the build runs. On Windows the tar on PATH can be Git's GNU tar, which reads C: as a host.
    $script:tar = InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' { Get-LambdaPSTarCommand }
    & $script:tar -czf $script:fakeTarball -C $fakeSrc .
    # The stand-in is not the official tarball, so no pinned hash applies to it here.
    Mock 'Get-LambdaPSPwshArchiveHash' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { }
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Build-LambdaPSRuntimePackage' -Tag 'Unit' {
    It 'builds a container-free Layer .zip with pwsh, bootstrap, runtime.ps1 and the context source' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'layer-proj')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $out = [System.IO.Path]::Combine($TestDrive, 'layer.zip')

        $result = Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball

        $result.PSTypeNames | Should -Contain 'AWS.ToolExtras.LambdaPSRuntime.RuntimePackage'
        $result.PackagePath | Should -Be ([System.IO.Path]::GetFullPath($out))
        $result.Target | Should -Be 'Layer'
        $result.PwshVersion | Should -Be $script:pwshConfig.DefaultPatch['7.4']
        $result.Architecture | Should -Be 'x86_64'
        Test-Path -LiteralPath $out | Should -BeTrue

        $names = Get-ZipEntryNames -ZipPath $out
        $names | Should -Contain 'bootstrap'
        $names | Should -Contain 'runtime.ps1'
        $names | Should -Contain 'powershell/pwsh'
        @($names | Where-Object { $_ -like 'modules/*' }) | Should -Not -BeNullOrEmpty
        # The marker is a build input, not deployment payload. The .cs ships too: the runtime compiles it when the precompiled DLL is absent or does not load.
        $names | Should -Contain 'PowerShellLambdaContext.cs'
        $names | Should -Not -Contain 'lambdaps-runtime.json'

        # bootstrap and pwsh must carry the Unix executable bit (0100755).
        $exec = [Convert]::ToInt32('100755', 8)
        Get-ZipEntryMode -ZipPath $out -EntryName 'bootstrap' | Should -Be $exec
        Get-ZipEntryMode -ZipPath $out -EntryName 'powershell/pwsh' | Should -Be $exec
    }

    # A project edited on Windows can carry a CRLF bootstrap; the kernel would then read the shebang
    # as '/bin/sh\r' and the runtime would fail to start ("bad interpreter").
    It 'writes the bootstrap into the package with LF line endings even if the project has CRLF' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'layer-crlf')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $bootstrapPath = [System.IO.Path]::Combine($proj, 'bootstrap')
        $crlf = [System.IO.File]::ReadAllText($bootstrapPath).Replace("`n", "`r`n")
        [System.IO.File]::WriteAllText($bootstrapPath, $crlf)
        $out = [System.IO.Path]::Combine($TestDrive, 'layer-crlf.zip')

        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball

        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($out)
        try {
            $reader = [System.IO.StreamReader]::new(($zip.Entries | Where-Object FullName -eq 'bootstrap').Open())
            try { $packaged = $reader.ReadToEnd() } finally { $reader.Dispose() }
        }
        finally { $zip.Dispose() }

        $packaged.Contains("`r") | Should -BeFalse
        $packaged | Should -Match '^#!/bin/sh\n'
    }

    It 'builds a self-contained function .zip that includes the handler' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'scz-proj')
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $out = [System.IO.Path]::Combine($TestDrive, 'scz.zip')

        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball

        $names = Get-ZipEntryNames -ZipPath $out
        $names | Should -Contain 'handler.ps1'
        $names | Should -Contain 'powershell/pwsh'
        $names | Should -Contain 'bootstrap'
        $names | Should -Contain 'runtime.ps1'
    }

    It 'ships the project''s other files, and leaves out scaffolding, .git and outside links, as Compress does' -Skip:$IsWindows {
        $proj = [System.IO.Path]::Combine($TestDrive, 'scz-files')
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($proj, 'lib')), ([System.IO.Path]::Combine($proj, 'modules', '.git')) -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($proj, 'lib', 'helper.ps1')) -Value '# helper'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($proj, 'config.json')) -Value '{}'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($proj, 'modules', '.git', 'HEAD')) -Value 'ref: refs/heads/main'
        $outside = [System.IO.Path]::Combine($TestDrive, 'scz-outside')
        Set-Content -LiteralPath $outside -Value 'secret'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($proj, 'modules', 'creds')) -Target $outside
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($proj, 'docs')), ([System.IO.Path]::Combine($proj, '.git')) -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($proj, 'docs', 'README.md')) -Value '# nested readme'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($proj, '.git', 'HEAD')) -Value 'ref: refs/heads/main'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($proj, 'inside.json')) -Target 'config.json'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($proj, 'outside')) -Target $outside
        Set-Content -LiteralPath ([System.IO.Path]::Combine($proj, 'old.zip')) -Value 'an earlier package'
        $out = [System.IO.Path]::Combine($TestDrive, 'scz-files.zip')

        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball -WarningVariable warnings -WarningAction SilentlyContinue

        $names = Get-ZipEntryNames -ZipPath $out
        $names | Should -Contain 'lib/helper.ps1'
        $names | Should -Contain 'config.json'
        $names | Should -Not -Contain 'modules/.git/HEAD'
        $names | Should -Not -Contain 'modules/creds'
        $names | Should -Not -Contain 'lambdaps-runtime.json'
        $names | Should -Not -Contain 'README.md'
        $names | Should -Contain 'docs/README.md'
        $names | Should -Not -Contain '.git/HEAD'
        $names | Should -Not -Contain 'outside'
        $names | Should -Contain 'old.zip'
        $zip = [System.IO.Compression.ZipFile]::OpenRead($out)
        try {
            $reader = [System.IO.StreamReader]::new($zip.GetEntry('inside.json').Open())
            try { $reader.ReadToEnd().Trim() | Should -Be '{}' } finally { $reader.Dispose() }
        }
        finally { $zip.Dispose() }
        @($warnings | Where-Object { "$_" -like "*outside*it links to*outside*" }).Count | Should -BeGreaterThan 0
        @($warnings | Where-Object { "$_" -like '*old.zip*ships inside the package*' }).Count | Should -Be 1
    }

    # Lambda's paths are case-sensitive, so on Linux only the exact names are left out.
    It 'leaves out only a powershell folder of that exact name, with a warning, and packages PowerShell/ and readme.md, on Linux' -Skip:(-not $IsLinux) {
        $proj = [System.IO.Path]::Combine($TestDrive, 'scz-case')
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($proj, 'powershell')), ([System.IO.Path]::Combine($proj, 'PowerShell')) -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($proj, 'powershell', 'stale.txt')) -Value 'x'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($proj, 'PowerShell', 'lib.ps1')) -Value '# lib'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($proj, 'readme.md')) -Value 'mine'
        $out = [System.IO.Path]::Combine($TestDrive, 'scz-case.zip')

        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball -WarningVariable warnings -WarningAction SilentlyContinue

        $names = Get-ZipEntryNames -ZipPath $out
        # -ccontains: Should -Contain compares case-insensitively.
        $names -ccontains 'PowerShell/lib.ps1' | Should -BeTrue
        $names -ccontains 'readme.md' | Should -BeTrue
        $names -ccontains 'README.md' | Should -BeFalse
        $names -ccontains 'powershell/stale.txt' | Should -BeFalse
        @($warnings | Where-Object { "$_" -like "The project's own 'powershell' folder in '*' is not packaged*" }).Count | Should -Be 1
    }

    It 'throws <ErrorId> for <Case>' -ForEach @(
        @{ Case = 'a missing project directory'; ErrorId = 'ProjectNotFound'; Setup = { } }
        @{ Case = 'a project with no PowerShellLambdaContext.cs'; ErrorId = 'ContextSourceNotFound'
            Setup = { Remove-Item -LiteralPath ([System.IO.Path]::Combine($proj, 'PowerShellLambdaContext.cs')) -Force } }
        @{ Case = 'a marker with no PowerShell version and no -PowerShellVersion'; ErrorId = 'PowerShellVersionRequired'
            Setup = { Set-Content -LiteralPath ([System.IO.Path]::Combine($proj, 'lambdaps-runtime.json')) -Value '{"Target":"Layer"}' } }
    ) {
        $proj = [System.IO.Path]::Combine($TestDrive, "err-$ErrorId")
        if ($ErrorId -ne 'ProjectNotFound') {
            $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
            & $Setup
        }
        Mock 'Get-LambdaPSPwshArchive' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { throw 'must not download' }

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, "err-$ErrorId.zip")) -PwshArchivePath $script:fakeTarball } |
            Should -Throw -ErrorId "$ErrorId,Build-LambdaPSRuntimePackage"
        # Checked before the tarball, so nothing is fetched or checked for a project that cannot build.
        Should -Invoke 'Get-LambdaPSPwshArchive' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -Times 0 -Exactly
    }

    # The copy skips a link that leaves the project, so a required file reached that way is not in the
    # package: the build stops instead of shipping a package that cannot start.
    It 'throws <ErrorId> when <File> links outside the project' -Skip:$IsWindows -ForEach @(
        @{ File = 'bootstrap'; ErrorId = 'ProjectFileNotFound' }
        @{ File = 'runtime.ps1'; ErrorId = 'ProjectFileNotFound' }
        @{ File = 'PowerShellLambdaContext.cs'; ErrorId = 'ContextSourceNotFound' }
    ) {
        $proj = [System.IO.Path]::Combine($TestDrive, "link-$File")
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $outside = [System.IO.Path]::Combine($TestDrive, "outside-$File")
        Move-Item -LiteralPath ([System.IO.Path]::Combine($proj, $File)) -Destination $outside
        Add-Content -LiteralPath $outside -Value '# OUTSIDE CONTENT'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($proj, $File)) -Target $outside
        $out = [System.IO.Path]::Combine($TestDrive, "link-$File.zip")

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball -WarningAction SilentlyContinue } |
            Should -Throw -ErrorId "$ErrorId,Build-LambdaPSRuntimePackage"
        # -WhatIf reports it too, before the pwsh download, as a real run does.
        { Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball -WhatIf -WarningAction SilentlyContinue } |
            Should -Throw -ErrorId "$ErrorId,Build-LambdaPSRuntimePackage"
        Test-Path -LiteralPath $out | Should -BeFalse
    }

    # A link into .git stays inside the project, so only the copy leaves it out, without a warning.
    It 'says the copy left out a required file that links into .git' -Skip:$IsWindows {
        $proj = [System.IO.Path]::Combine($TestDrive, 'link-git-bootstrap')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $git = [System.IO.Path]::Combine($proj, '.git')
        $null = New-Item -ItemType Directory -Path $git -Force
        Move-Item -LiteralPath ([System.IO.Path]::Combine($proj, 'bootstrap')) -Destination ([System.IO.Path]::Combine($git, 'bootstrap'))
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($proj, 'bootstrap')) -Target '.git/bootstrap'

        $err = { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'link-git-bootstrap.zip')) -PwshArchivePath $script:fakeTarball -WarningAction SilentlyContinue } |
            Should -Throw -ErrorId 'ProjectFileNotFound,Build-LambdaPSRuntimePackage' -PassThru
        $err.Exception.Message | Should -BeLike '*left out by the copy*a link into .git*'
        $err.Exception.Message | Should -Not -BeLike '*links outside*'
    }

    It 'throws PwshBinaryNotFound when the tarball holds no pwsh' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'no-pwsh')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $empty = [System.IO.Path]::Combine($TestDrive, 'no-pwsh-src')
        $null = New-Item -ItemType Directory -Path $empty -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($empty, 'readme.txt')) -Value 'no interpreter here'
        $tarball = [System.IO.Path]::Combine($TestDrive, 'no-pwsh.tar.gz')
        & $script:tar -czf $tarball -C $empty .

        # This failure comes after the stage is created, so it shows the stage is removed. A temp folder of
        # its own, so builds running elsewhere on the host cannot change the count.
        $temp = [System.IO.Path]::Combine($TestDrive, 'no-pwsh-temp')
        $null = New-Item -ItemType Directory -Path $temp -Force
        $saved = @{ TMPDIR = $env:TMPDIR; TEMP = $env:TEMP; TMP = $env:TMP }
        try {
            $env:TMPDIR = $temp; $env:TEMP = $temp; $env:TMP = $temp
            [System.IO.Path]::GetTempPath().TrimEnd([System.IO.Path]::DirectorySeparatorChar) | Should -Be $temp
            { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'no-pwsh.zip')) -PwshArchivePath $tarball } |
                Should -Throw -ErrorId 'PwshBinaryNotFound,Build-LambdaPSRuntimePackage'
        }
        finally { $env:TMPDIR = $saved.TMPDIR; $env:TEMP = $saved.TEMP; $env:TMP = $saved.TMP }
        @(Get-ChildItem -LiteralPath $temp -Directory -Filter 'lambdaps-pkg-*') | Should -BeNullOrEmpty
    }

    It 'throws ContextCompileFailed when the bundled pwsh cannot compile the context class' -Skip:(-not $IsLinux) {
        $proj = [System.IO.Path]::Combine($TestDrive, 'bad-context')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        Set-Content -LiteralPath ([System.IO.Path]::Combine($proj, 'PowerShellLambdaContext.cs')) -Value 'this is not C#'
        # The bundled pwsh only runs (and so only compiles) for the host's own architecture.
        $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x86_64' }

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'bad-context.zip')) -PwshArchivePath $script:fakeTarball -Architecture $arch 2>$null } |
            Should -Throw -ErrorId 'ContextCompileFailed,Build-LambdaPSRuntimePackage'
    }

    It 'throws UnsupportedTarget for a CMR project' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'cmr-proj')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4'  # default CMR
        { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'cmr.zip')) -PwshArchivePath $script:fakeTarball } |
            Should -Throw -ErrorId 'UnsupportedTarget,Build-LambdaPSRuntimePackage'
    }

    It 'takes the destination as -PackagePath too, the name of the result field' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'layer-alias')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $out = [System.IO.Path]::Combine($TestDrive, 'layer-alias.zip')

        $result = Build-LambdaPSRuntimePackage -Path $proj -PackagePath $out -PwshArchivePath $script:fakeTarball

        $result.PackagePath | Should -Be ([System.IO.Path]::GetFullPath($out))
        Test-Path -LiteralPath $out | Should -BeTrue
    }

    It 'throws OutFileIsDirectory when -OutFile is an existing directory, even with -Force' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'layer-outdir')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $out = [System.IO.Path]::Combine($TestDrive, 'outdir')
        $null = New-Item -ItemType Directory -Path $out -Force

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball -Force } |
            Should -Throw -ErrorId 'OutFileIsDirectory,Build-LambdaPSRuntimePackage'
        @(Get-ChildItem -LiteralPath $out -Force) | Should -BeNullOrEmpty
    }

    # -Force replaces an existing -OutFile, so the -PwshArchivePath tarball itself is refused, leaving it as it was.
    It 'throws OutFileIsSource, even with -Force, when -OutFile is the -PwshArchivePath tarball' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'layer-out-tarball')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $tarball = [System.IO.Path]::Combine($TestDrive, 'own-pwsh.tar.gz')
        Copy-Item -LiteralPath $script:fakeTarball -Destination $tarball
        $before = (Get-FileHash -LiteralPath $tarball -Algorithm SHA256).Hash

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile $tarball -PwshArchivePath $tarball -Force } |
            Should -Throw -ErrorId 'OutFileIsSource,Build-LambdaPSRuntimePackage'
        (Get-FileHash -LiteralPath $tarball -Algorithm SHA256).Hash | Should -Be $before
    }

    # The tarball is extracted straight into the package, so a link in it would ship the file it points to.
    It 'throws PackageEntryIsLink for a -PwshArchivePath tarball that holds a link, writing no package' -Skip:$IsWindows {
        $proj = [System.IO.Path]::Combine($TestDrive, 'layer-tar-link')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $linkSrc = [System.IO.Path]::Combine($TestDrive, 'link-pwsh-src')
        $null = New-Item -ItemType Directory -Path $linkSrc
        Copy-Item -LiteralPath ([System.IO.Path]::Combine($TestDrive, 'fake-pwsh-src', 'pwsh')) -Destination $linkSrc
        $outside = [System.IO.Path]::Combine($TestDrive, 'outside-credentials')
        Set-Content -LiteralPath $outside -Value 'secret'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($linkSrc, 'libfoo.so')) -Target $outside
        $tarball = [System.IO.Path]::Combine($TestDrive, 'link-pwsh.tar.gz')
        & $script:tar -czf $tarball -C $linkSrc .
        $out = [System.IO.Path]::Combine($TestDrive, 'layer-tar-link.zip')

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $tarball -WarningAction SilentlyContinue } |
            Should -Throw -ErrorId 'PackageEntryIsLink,Build-LambdaPSRuntimePackage'
        Test-Path -LiteralPath $out | Should -BeFalse
    }

    It 'throws OutFileExists when the destination exists without -Force' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'layer-proj2')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $out = [System.IO.Path]::Combine($TestDrive, 'exists.zip')
        Set-Content -LiteralPath $out -Value 'x'
        { Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball } |
            Should -Throw -ErrorId 'OutFileExists,Build-LambdaPSRuntimePackage'
        [System.IO.File]::ReadAllText($out).Trim() | Should -BeExactly 'x'
    }

    It 'overwrites an existing output file when -Force is supplied' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'layer-force')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $out = [System.IO.Path]::Combine($TestDrive, 'force.zip')
        Set-Content -LiteralPath $out -Value 'x'

        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball -Force -WarningAction SilentlyContinue

        $names = Get-ZipEntryNames -ZipPath $out
        $names | Should -Contain 'bootstrap'
    }

    It 'replaces the package under -Force, with the edited handler' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'scz-replace')
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $handler = [System.IO.Path]::Combine($proj, 'handler.ps1')
        Set-Content -LiteralPath $handler -Value 'param($LambdaInput, $LambdaContext)', '$v = 1'
        $out = [System.IO.Path]::Combine($TestDrive, 'scz-replace.zip')
        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball -WarningAction SilentlyContinue
        Set-Content -LiteralPath $handler -Value 'param($LambdaInput, $LambdaContext)', '$v = 2'

        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball -Force -WarningAction SilentlyContinue

        $z = [System.IO.Compression.ZipFile]::OpenRead($out)
        try {
            $r = [System.IO.StreamReader]::new($z.GetEntry('handler.ps1').Open())
            try { $r.ReadToEnd() | Should -Match 'v = 2' } finally { $r.Dispose() }
        }
        finally { $z.Dispose() }
    }

    It 'creates the parent directory of the output file when missing' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'layer-parent')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer

        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'nested', 'deep', 'layer.zip')) -PwshArchivePath $script:fakeTarball -WarningAction SilentlyContinue

        Test-Path -LiteralPath ([System.IO.Path]::Combine($TestDrive, 'nested', 'deep', 'layer.zip')) | Should -BeTrue
    }

    # The help allows the bytes to differ (entry times); the entries themselves do not.
    It 'writes the same entry list on two builds of the same project' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'layer-twice')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $a = [System.IO.Path]::Combine($TestDrive, 'twice-a.zip')
        $b = [System.IO.Path]::Combine($TestDrive, 'twice-b.zip')

        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile $a -PwshArchivePath $script:fakeTarball -WarningAction SilentlyContinue
        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile $b -PwshArchivePath $script:fakeTarball -WarningAction SilentlyContinue

        $first = Get-ZipEntryNames -ZipPath $a
        $second = Get-ZipEntryNames -ZipPath $b
        $second | Should -Be $first
    }

    # Without a precompiled DLL (a host that cannot run the bundled pwsh), an unchanged project rebuilds to the same bytes.
    It 'writes a byte-identical package when an unchanged project is built twice' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'layer-repro')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $other = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'x86_64' } else { 'arm64' }
        $a = [System.IO.Path]::Combine($TestDrive, 'repro-a.zip')
        $b = [System.IO.Path]::Combine($TestDrive, 'repro-b.zip')

        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile $a -PwshArchivePath $script:fakeTarball -Architecture $other -WarningAction SilentlyContinue
        Get-ChildItem -LiteralPath $proj -Recurse -File | ForEach-Object { $_.LastWriteTime = [datetime]::Now.AddMinutes(5) }
        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile $b -PwshArchivePath $script:fakeTarball -Architecture $other -WarningAction SilentlyContinue

        (Get-FileHash -LiteralPath $b -Algorithm SHA256).Hash | Should -Be (Get-FileHash -LiteralPath $a -Algorithm SHA256).Hash
    }

    It 'ships the modules staged at scaffold time under modules/, beside the runtime''s own files' {
        $installed = [System.IO.Path]::Combine($TestDrive, 'inc-installed')
        $null = New-FakeModule -Root $installed -Name 'LambdaPSFake.Inc' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.IncDep'; RequiredVersion = '1.0.0' })"
        $null = New-FakeModule -Root $installed -Name 'LambdaPSFake.IncDep' -Version '1.0.0'
        $proj = [System.IO.Path]::Combine($TestDrive, 'scz-inc')
        $savedPath = $env:PSModulePath
        try {
            $env:PSModulePath = $installed + [System.IO.Path]::PathSeparator + $env:PSModulePath
            $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4' -IncludeModule @{ ModuleName = 'LambdaPSFake.Inc'; RequiredVersion = '1.0.0' } -MissingModuleAction Stop
        }
        finally { $env:PSModulePath = $savedPath }
        $out = [System.IO.Path]::Combine($TestDrive, 'scz-inc.zip')

        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball -WarningAction SilentlyContinue

        $names = Get-ZipEntryNames -ZipPath $out
        $names | Should -Contain 'modules/LambdaPSFake.Inc/1.0.0/LambdaPSFake.Inc.psd1'
        $names | Should -Contain 'modules/LambdaPSFake.IncDep/1.0.0/LambdaPSFake.IncDep.psd1'
        $names | Should -Contain 'modules/pwsh-runtime.psm1'
        $names | Should -Contain 'modules/pwsh-runtime.psd1'
        @($names | Where-Object { $_ -like 'modules/Private/*.ps1' }) | Should -Not -BeNullOrEmpty
        # The bundled pwsh precompiles the context class on a Linux host of the target architecture.
        $hostArch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x86_64' }
        if ($IsLinux -and $hostArch -eq 'x86_64') { $names | Should -Contain 'PowerShellLambdaContext.dll' }
        @($names | ForEach-Object { ($_ -split '/')[0] } | Where-Object { $_ -ne 'PowerShellLambdaContext.dll' } | Sort-Object -Unique) |
            Should -Be @('bootstrap', 'handler.ps1', 'modules', 'powershell', 'PowerShellLambdaContext.cs', 'runtime.ps1')
    }
}

# A SelfContainedZip handler's '#Requires -Modules' ship under modules/, as with Compress.
Describe 'Build-LambdaPSRuntimePackage handler modules' -Tag 'Unit' {
    BeforeAll {
        $script:reqProj = [System.IO.Path]::Combine($TestDrive, 'scz-req')
        $null = New-LambdaPSFunctionProject -Path $script:reqProj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $handlerFile = [System.IO.Path]::Combine($script:reqProj, 'handler.ps1')
        Set-Content -LiteralPath $handlerFile -Value (@("#Requires -Modules @{ ModuleName = 'LambdaPSFake.Req'; RequiredVersion = '1.0.0' }") +
            @(Get-Content -LiteralPath $handlerFile))
    }

    It 'stages the modules the handler requires and reports them in ModulesStaged' {
        # The download writes the module where the real tools would.
        Mock 'Save-LambdaPSMissingModule' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith {
            $null = New-FakeModule -Root $Path -Name 'LambdaPSFake.Req' -Version '1.0.0'
            [PSCustomObject]@{ Pending = @() }
        }
        $out = [System.IO.Path]::Combine($TestDrive, 'scz-req.zip')

        $result = Build-LambdaPSRuntimePackage -Path $script:reqProj -OutFile $out -PwshArchivePath $script:fakeTarball -WarningAction SilentlyContinue

        @($result.ModulesStaged | ForEach-Object ToString) | Should -Be @('LambdaPSFake.Req 1.0.0')
        $names = Get-ZipEntryNames -ZipPath $out
        $names | Should -Contain 'modules/LambdaPSFake.Req/1.0.0/LambdaPSFake.Req.psd1'
    }

    It 'fails with ModuleNotFoundLocally under -MissingModuleAction Stop, before fetching pwsh' {
        Mock 'Get-LambdaPSPwshArchive' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -ParameterFilter { -not $ValidateOnly } -MockWith { throw 'must not download' }

        { Build-LambdaPSRuntimePackage -Path $script:reqProj -OutFile ([System.IO.Path]::Combine($TestDrive, 'scz-req-stop.zip')) -PwshArchivePath $script:fakeTarball -MissingModuleAction Stop } |
            Should -Throw -ErrorId 'ModuleNotFoundLocally,Build-LambdaPSRuntimePackage'
        Should -Invoke 'Get-LambdaPSPwshArchive' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -ParameterFilter { -not $ValidateOnly } -Times 0 -Exactly
    }

    It 'reports ModuleNotFoundLocally under -MissingModuleAction Stop and -WhatIf, writing nothing' {
        $out = [System.IO.Path]::Combine($TestDrive, 'scz-req-stop-whatif.zip')

        { Build-LambdaPSRuntimePackage -Path $script:reqProj -OutFile $out -PwshArchivePath $script:fakeTarball -MissingModuleAction Stop -WhatIf } |
            Should -Throw -ErrorId 'ModuleNotFoundLocally,Build-LambdaPSRuntimePackage'
        Test-Path -LiteralPath $out | Should -BeFalse
    }

    It 'builds under -MissingModuleAction Stop when the required module is installed locally' {
        $installed = [System.IO.Path]::Combine($TestDrive, 'stop-installed')
        $null = New-FakeModule -Root $installed -Name 'LambdaPSFake.Req' -Version '1.0.0'
        $savedPath = $env:PSModulePath
        try {
            $env:PSModulePath = $installed + [System.IO.Path]::PathSeparator + $env:PSModulePath
            $result = Build-LambdaPSRuntimePackage -Path $script:reqProj -OutFile ([System.IO.Path]::Combine($TestDrive, 'scz-req-installed.zip')) -PwshArchivePath $script:fakeTarball -MissingModuleAction Stop -WarningAction SilentlyContinue
        }
        finally { $env:PSModulePath = $savedPath }

        @($result.ModulesStaged | ForEach-Object ToString) | Should -Be @('LambdaPSFake.Req 1.0.0')
    }

    It 'throws HandlerNotResolved, before downloading pwsh, for a project with two root scripts and no -HandlerScript' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'scz-two-scripts')
        $null = Copy-Item -LiteralPath $script:reqProj -Destination $proj -Recurse
        Set-Content -LiteralPath ([System.IO.Path]::Combine($proj, 'helpers.ps1')) -Value 'function Get-Greeting { ''hi'' }'
        Mock 'Get-LambdaPSPwshArchive' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -ParameterFilter { -not $ValidateOnly } -MockWith { throw 'not expected' }

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'scz-two-scripts.zip')) -PwshArchivePath $script:fakeTarball -MissingModuleAction Stop } |
            Should -Throw -ErrorId 'HandlerNotResolved,Build-LambdaPSRuntimePackage'
    }

    It 'reads the named handler''s requirements when -HandlerScript picks one of two root scripts' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'scz-two-scripts-named')
        $null = Copy-Item -LiteralPath $script:reqProj -Destination $proj -Recurse
        Set-Content -LiteralPath ([System.IO.Path]::Combine($proj, 'helpers.ps1')) -Value 'function Get-Greeting { ''hi'' }'

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'scz-two-scripts-named.zip')) -PwshArchivePath $script:fakeTarball -HandlerScript 'handler.ps1' -MissingModuleAction Stop } |
            Should -Throw -ErrorId 'ModuleNotFoundLocally,Build-LambdaPSRuntimePackage'
    }

    It 'throws HandlerScriptNotFound for a -HandlerScript that is not in the project' {
        { Build-LambdaPSRuntimePackage -Path $script:reqProj -OutFile ([System.IO.Path]::Combine($TestDrive, 'scz-req-typo.zip')) -PwshArchivePath $script:fakeTarball -HandlerScript 'hanlder.ps1' } |
            Should -Throw -ErrorId 'HandlerScriptNotFound,Build-LambdaPSRuntimePackage'
    }

    It 'throws HandlerScriptNotFound under -WhatIf for a handler that links outside the project' -Skip:$IsWindows {
        $proj = [System.IO.Path]::Combine($TestDrive, 'scz-handler-link')
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $outside = [System.IO.Path]::Combine($TestDrive, 'scz-handler-outside.ps1')
        Move-Item -LiteralPath ([System.IO.Path]::Combine($proj, 'handler.ps1')) -Destination $outside
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($proj, 'handler.ps1')) -Target $outside

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'scz-handler-link.zip')) -PwshArchivePath $script:fakeTarball -WhatIf } |
            Should -Throw -ErrorId 'HandlerScriptNotFound,Build-LambdaPSRuntimePackage'
    }

    # Off Windows '\' is a file name character, not a separator, so a handler named with it is refused.
    It 'refuses a SelfContainedZip handler named with a backslash off Windows' -Skip:$IsWindows {
        $proj = [System.IO.Path]::Combine($TestDrive, 'scz-backslash')
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($proj, 'lib')) -Force
        Move-Item -LiteralPath ([System.IO.Path]::Combine($proj, 'handler.ps1')) -Destination ([System.IO.Path]::Combine($proj, 'lib', 'handler.ps1'))

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'scz-backslash.zip')) -PwshArchivePath $script:fakeTarball -HandlerScript 'lib\handler.ps1' -WhatIf } |
            Should -Throw -ErrorId 'InvalidHandlerPath,Build-LambdaPSRuntimePackage'
    }

    It 'finds the handler with Resolve-LambdaPSHandlerPath, as Compress does' {
        Mock 'Resolve-LambdaPSHandlerPath' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { $null }

        { Build-LambdaPSRuntimePackage -Path $script:reqProj -OutFile ([System.IO.Path]::Combine($TestDrive, 'scz-resolver.zip')) -PwshArchivePath $script:fakeTarball -WhatIf } |
            Should -Throw -ErrorId 'HandlerScriptNotFound,Build-LambdaPSRuntimePackage'
        Should -Invoke 'Resolve-LambdaPSHandlerPath' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -Times 1 -Exactly -ParameterFilter { $HandlerScript -eq 'handler.ps1' }
    }

    # The forms the resolver accepts still build, and the handler ships where it was named.
    It 'builds with the handler <Case>' -Skip:$IsWindows -ForEach @(
        @{ Case = 'in a subfolder'; Name = 'scz-sub'; HandlerScript = 'lib/handler.ps1'; Shipped = 'lib/handler.ps1'; Link = $false }
        @{ Case = 'as a link to a script inside the project'; Name = 'scz-innerlink'; HandlerScript = 'handler.ps1'; Shipped = 'handler.ps1'; Link = $true }
    ) {
        $proj = [System.IO.Path]::Combine($TestDrive, $Name)
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($proj, 'lib')) -Force
        if ($Link) {
            Move-Item -LiteralPath ([System.IO.Path]::Combine($proj, 'handler.ps1')) -Destination ([System.IO.Path]::Combine($proj, 'lib', 'impl.ps1'))
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($proj, 'handler.ps1')) -Target 'lib/impl.ps1'
        }
        else {
            Move-Item -LiteralPath ([System.IO.Path]::Combine($proj, 'handler.ps1')) -Destination ([System.IO.Path]::Combine($proj, 'lib', 'handler.ps1'))
        }
        $out = [System.IO.Path]::Combine($TestDrive, "$Name.zip")

        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball -HandlerScript $HandlerScript -WarningAction SilentlyContinue

        $names = Get-ZipEntryNames -ZipPath $out
        $names | Should -Contain $Shipped
    }

    It 'warns that a Layer ignores <Given>' -ForEach @(
        @{ Given = '-HandlerScript'; Params = @{ HandlerScript = 'handler.ps1' } }
        @{ Given = '-MissingModuleAction, -ModuleRepository'; Params = @{ MissingModuleAction = 'Stop'; ModuleRepository = 'PSGallery' } }
    ) {
        $proj = [System.IO.Path]::Combine($TestDrive, "layer-unused-$($Params.Count)")
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer

        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, "layer-unused-$($Params.Count).zip")) -PwshArchivePath $script:fakeTarball @Params -WarningVariable warnings -WarningAction SilentlyContinue

        $applies = @($warnings | Where-Object { "$_" -like '*not apply to a Layer*' })
        $applies.Count | Should -Be 1
        "$($applies[0])" | Should -BeLike "$Given *"
    }

    It 'gives no such warning for a Layer built without them, or for a SelfContainedZip built with them' {
        $layer = [System.IO.Path]::Combine($TestDrive, 'layer-plain')
        $null = New-LambdaPSRuntimeProject -Path $layer -PowerShellVersion '7.4' -Target Layer
        $null = Build-LambdaPSRuntimePackage -Path $layer -OutFile ([System.IO.Path]::Combine($TestDrive, 'layer-plain.zip')) -PwshArchivePath $script:fakeTarball -WarningVariable layerWarnings -WarningAction SilentlyContinue
        $scz = [System.IO.Path]::Combine($TestDrive, 'scz-plain')
        $null = New-LambdaPSFunctionProject -Path $scz -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $null = Build-LambdaPSRuntimePackage -Path $scz -OutFile ([System.IO.Path]::Combine($TestDrive, 'scz-plain.zip')) -PwshArchivePath $script:fakeTarball -HandlerScript 'handler.ps1' -MissingModuleAction Stop -WarningVariable sczWarnings -WarningAction SilentlyContinue

        @(@($layerWarnings) + @($sczWarnings) | Where-Object { "$_" -like '*not apply to a Layer*' }) | Should -BeNullOrEmpty
    }

    # Lambda finds a module in modules/ (on the function's PSModulePath) and PowerShell's own modules, so
    # the build ships them as they are instead of requiring an exact pin or a download.
    It 'builds when the handler requires one of PowerShell''s own modules, without pinning it' -ForEach @(
        @{ Name = 'scz-builtin'; Requires = 'Microsoft.PowerShell.Utility'; Vendored = $false }
    ) {
        $proj = [System.IO.Path]::Combine($TestDrive, $Name)
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $handlerFile = [System.IO.Path]::Combine($proj, 'handler.ps1')
        Set-Content -LiteralPath $handlerFile -Value (@("#Requires -Modules $Requires") + @(Get-Content -LiteralPath $handlerFile))
        if ($Vendored) {
            $null = New-FakeModule -Root ([System.IO.Path]::Combine($proj, 'modules')) -Name 'LambdaPSFake.Vendored' -Version '1.2.0'
        }
        Mock 'Save-LambdaPSMissingModule' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { throw 'must not download' }

        $result = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, "$Name.zip")) -PwshArchivePath $script:fakeTarball -MissingModuleAction Stop -WarningAction SilentlyContinue

        @($result.ModulesStaged | ForEach-Object ToString) | Should -Be @($(if ($Vendored) { 'LambdaPSFake.Vendored 1.2.0' }))
        Should -Invoke 'Save-LambdaPSMissingModule' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -Times 0 -Exactly
    }

    # A module the project keeps in modules/ goes through the pin check and the dependency walk like any other.
    It 'gives the pin error for a <Case> on a module the project keeps in modules/' -ForEach @(
        @{ Case = 'bare #Requires'; Name = 'scz-vendored'; Requires = 'LambdaPSFake.Vendored' }
        @{ Case = 'minimum-version #Requires'; Name = 'scz-vendored-min'; Requires = "@{ ModuleName = 'LambdaPSFake.Vendored'; ModuleVersion = '1.0.0' }" }
    ) {
        $proj = [System.IO.Path]::Combine($TestDrive, $Name)
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $handlerFile = [System.IO.Path]::Combine($proj, 'handler.ps1')
        Set-Content -LiteralPath $handlerFile -Value (@("#Requires -Modules $Requires") + @(Get-Content -LiteralPath $handlerFile))
        $null = New-FakeModule -Root ([System.IO.Path]::Combine($proj, 'modules')) -Name 'LambdaPSFake.Vendored' -Version '1.2.0'

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, "$Name.zip")) -PwshArchivePath $script:fakeTarball -MissingModuleAction Stop -WhatIf } |
            Should -Throw -ErrorId 'ModuleVersionRequired,Build-LambdaPSRuntimePackage'
    }

    # Each module is named exactly: a pinned #Requires that names a module by its path is refused, not loaded.
    It 'throws ModuleNameInvalid for a #Requires that names a module by its path' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'scz-req-path')
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $handlerFile = [System.IO.Path]::Combine($proj, 'handler.ps1')
        Set-Content -LiteralPath $handlerFile -Value (@("#Requires -Modules @{ ModuleName = './lib/LambdaPSFake.Path.psd1'; RequiredVersion = '1.0.0' }") + @(Get-Content -LiteralPath $handlerFile))

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'scz-req-path.zip')) -PwshArchivePath $script:fakeTarball -MissingModuleAction Stop -WhatIf } |
            Should -Throw -ErrorId 'ModuleNameInvalid,Build-LambdaPSRuntimePackage'
    }

    It 'still refuses a pinned version the project does not ship' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'scz-vendored-other')
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $handlerFile = [System.IO.Path]::Combine($proj, 'handler.ps1')
        Set-Content -LiteralPath $handlerFile -Value (@("#Requires -Modules @{ ModuleName = 'LambdaPSFake.Vendored'; RequiredVersion = '2.0.0' }") + @(Get-Content -LiteralPath $handlerFile))
        $null = New-FakeModule -Root ([System.IO.Path]::Combine($proj, 'modules')) -Name 'LambdaPSFake.Vendored' -Version '1.2.0'

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'scz-vendored-other.zip')) -PwshArchivePath $script:fakeTarball -MissingModuleAction Stop } |
            Should -Throw -ErrorId 'ModuleNotFoundLocally,Build-LambdaPSRuntimePackage'
    }

    It 'reports an empty ModulesStaged for a Layer' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'layer-nomods')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer

        $result = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'layer-nomods.zip')) -PwshArchivePath $script:fakeTarball -WarningAction SilentlyContinue

        @($result.ModulesStaged).Count | Should -Be 0
        $result.PSObject.Properties.Name | Should -Contain 'ModulesStaged'
    }
}

# Failures stop the build with a named error before the expensive steps.
Describe 'Build-LambdaPSRuntimePackage error handling' -Tag 'Unit' {
    It 'throws ProjectFileNotFound for a missing runtime.ps1 before fetching pwsh' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'err-nort')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        Remove-Item -LiteralPath ([System.IO.Path]::Combine($proj, 'runtime.ps1'))
        Mock 'Get-LambdaPSPwshArchive' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { throw 'must not download' }
        # A temp folder of its own, so builds running elsewhere on the host cannot change the count.
        $temp = [System.IO.Path]::Combine($TestDrive, 'err-nort-temp')
        $null = New-Item -ItemType Directory -Path $temp -Force
        $saved = @{ TMPDIR = $env:TMPDIR; TEMP = $env:TEMP; TMP = $env:TMP }
        try {
            $env:TMPDIR = $temp; $env:TEMP = $temp; $env:TMP = $temp
            [System.IO.Path]::GetTempPath().TrimEnd([System.IO.Path]::DirectorySeparatorChar) | Should -Be $temp
            { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'err-nort.zip')) } |
                Should -Throw -ErrorId 'ProjectFileNotFound,Build-LambdaPSRuntimePackage'
        }
        finally { $env:TMPDIR = $saved.TMPDIR; $env:TEMP = $saved.TEMP; $env:TMP = $saved.TMP }
        Should -Invoke 'Get-LambdaPSPwshArchive' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -Times 0 -Exactly
        Test-Path -LiteralPath ([System.IO.Path]::Combine($TestDrive, 'err-nort.zip')) | Should -BeFalse
        @(Get-ChildItem -LiteralPath $temp -Directory -Filter 'lambdaps-pkg-*') | Should -BeNullOrEmpty
    }

    It 'throws UnzippedPackageTooLarge for a package over 250 MB unzipped, naming pwsh, and writes no .zip' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'err-too-large')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $file = [System.IO.File]::Create([System.IO.Path]::Combine($proj, 'data.bin'))
        try { $file.SetLength(251MB) } finally { $file.Dispose() }
        $out = [System.IO.Path]::Combine($TestDrive, 'err-too-large.zip')

        $err = { Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball -WarningAction SilentlyContinue } |
            Should -Throw -ErrorId 'UnzippedPackageTooLarge,Build-LambdaPSRuntimePackage' -PassThru
        $err.Exception.Message | Should -BeLike "*'err-too-large.zip' would unzip to 251* MB, over the 250 MB*The bundled pwsh takes*"
        Test-Path -LiteralPath $out | Should -BeFalse
    }

    It 'throws PwshArchiveHashMismatch for a supplied tarball that differs from the pinned hash, before extracting' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'err-hash')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $out = [System.IO.Path]::Combine($TestDrive, 'err-hash.zip')
        Mock 'Get-LambdaPSPwshArchiveHash' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { '0' * 64 }

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball } |
            Should -Throw -ErrorId 'PwshArchiveHashMismatch,Build-LambdaPSRuntimePackage'
        Test-Path -LiteralPath $out | Should -BeFalse
    }

    It 'throws PwshArchiveHashMismatch for a -PwshArchivePath of the other architecture, before staging' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'err-arch-hash')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $out = [System.IO.Path]::Combine($TestDrive, 'err-arch-hash.zip')
        Mock 'Get-LambdaPSPwshArchiveHash' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { 'ABC123' }

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -Architecture arm64 -PwshArchivePath $script:fakeTarball } |
            Should -Throw -ErrorId 'PwshArchiveHashMismatch*' -ExpectedMessage '*linux-arm64*'
        Test-Path -LiteralPath $out | Should -BeFalse
    }

    It 'throws TarNotFound before fetching pwsh when tar is not on PATH' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'err-notar')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        # The tar the module asks for on this host: System32\tar.exe on Windows, 'tar' elsewhere.
        Mock 'Get-Command' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -ParameterFilter { $Name -eq $script:tar } -MockWith { }
        Mock 'Get-LambdaPSPwshArchive' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { throw 'must not download' }

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'err-notar.zip')) } |
            Should -Throw -ErrorId 'TarNotFound,Build-LambdaPSRuntimePackage'
        { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'err-notar.zip')) -WhatIf } |
            Should -Throw -ErrorId 'TarNotFound,Build-LambdaPSRuntimePackage'
        Should -Invoke 'Get-LambdaPSPwshArchive' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -Times 0 -Exactly
    }

    # On Windows the tar on PATH can be Git's GNU tar, so the build checks for and runs the tar
    # Get-LambdaPSTarCommand chooses (Windows' own tar.exe there).
    It 'throws TarNotFound when the tar Get-LambdaPSTarCommand chooses does not exist' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'err-chosen-tar')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $script:missingTar = [System.IO.Path]::Combine($TestDrive, 'no-such-dir', 'tar.exe')
        Mock 'Get-LambdaPSTarCommand' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { $script:missingTar }
        Mock 'Get-LambdaPSPwshArchive' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { throw 'must not download' }

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'err-chosen-tar.zip')) -PwshArchivePath $script:fakeTarball } |
            Should -Throw -ErrorId 'TarNotFound,Build-LambdaPSRuntimePackage'
    }

    # The Windows path, on any host: the real Get-LambdaPSTarCommand is run as on Windows, against a stand-in
    # SystemRoot. The build must extract with System32\tar.exe there, not the tar on PATH.
    Context 'on Windows (Get-LambdaPSTarCommand run as on Windows)' {
        BeforeAll {
            $script:realTarCommand = InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' { (Get-Command Get-LambdaPSTarCommand).ScriptBlock }
        }

        It 'extracts pwsh with System32\tar.exe' -Skip:$IsWindows {
            $proj = [System.IO.Path]::Combine($TestDrive, 'win-tar')
            $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
            $script:winRoot = [System.IO.Path]::Combine($TestDrive, 'win-root')
            $system32 = [System.IO.Path]::Combine($script:winRoot, 'System32')
            $null = New-Item -ItemType Directory -Path $system32 -Force
            # A stand-in for Windows' own tar.exe that leaves a marker and hands over to the tar on PATH.
            $script:winTarRan = [System.IO.Path]::Combine($TestDrive, 'win-tar-ran')
            $winTar = [System.IO.Path]::Combine($system32, 'tar.exe')
            Set-Content -LiteralPath $winTar -Value "#!/bin/sh`ntouch '$script:winTarRan'`nexec tar `"`$@`""
            & chmod +x $winTar
            Mock 'Get-LambdaPSTarCommand' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { & $script:realTarCommand -OnWindows $true -SystemRoot $script:winRoot }

            $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'win-tar.zip')) -PwshArchivePath $script:fakeTarball -WarningAction SilentlyContinue

            $script:winTarRan | Should -Exist
        }

        It 'throws TarNotFound when there is no System32\tar.exe and no tar on PATH' {
            $proj = [System.IO.Path]::Combine($TestDrive, 'win-notar')
            $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
            $script:emptyWinRoot = [System.IO.Path]::Combine($TestDrive, 'win-root-empty')
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($script:emptyWinRoot, 'System32')) -Force
            Mock 'Get-LambdaPSTarCommand' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { & $script:realTarCommand -OnWindows $true -SystemRoot $script:emptyWinRoot }
            Mock 'Get-Command' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -ParameterFilter { $Name -eq 'tar' } -MockWith { }
            Mock 'Get-LambdaPSPwshArchive' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { throw 'must not download' }

            { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'win-notar.zip')) -PwshArchivePath $script:fakeTarball } |
                Should -Throw -ErrorId 'TarNotFound,Build-LambdaPSRuntimePackage'
        }
    }

    It 'extracts pwsh with the tar Get-LambdaPSTarCommand chooses' -Skip:$IsWindows {
        $proj = [System.IO.Path]::Combine($TestDrive, 'chosen-tar')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        # A stand-in tar that leaves a marker and hands over to the tar on PATH.
        $script:ranMarker = [System.IO.Path]::Combine($TestDrive, 'chosen-tar-ran')
        $script:chosenTar = [System.IO.Path]::Combine($TestDrive, 'chosen-tar-bin', 'tar')
        $null = New-Item -ItemType Directory -Path (Split-Path -Parent $script:chosenTar)
        Set-Content -LiteralPath $script:chosenTar -Value "#!/bin/sh`ntouch '$script:ranMarker'`nexec tar `"`$@`""
        & chmod +x $script:chosenTar
        Mock 'Get-LambdaPSTarCommand' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { $script:chosenTar }

        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'chosen-tar.zip')) -PwshArchivePath $script:fakeTarball -WarningAction SilentlyContinue

        $script:ranMarker | Should -Exist
    }

    It 'throws PwshArchiveExtractFailed for a tarball tar cannot read' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'err-tar')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $bad = [System.IO.Path]::Combine($TestDrive, 'not-a-tarball.tar.gz')
        Set-Content -LiteralPath $bad -Value 'not gzip'

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'err-tar.zip')) -PwshArchivePath $bad 2>$null } |
            Should -Throw -ErrorId 'PwshArchiveExtractFailed,Build-LambdaPSRuntimePackage'
    }

    It 'keeps PwshArchiveExtractFailed when the caller turns on $PSNativeCommandUseErrorActionPreference' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'err-tar-native')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $bad = [System.IO.Path]::Combine($TestDrive, 'not-a-tarball-native.tar.gz')
        Set-Content -LiteralPath $bad -Value 'not gzip'
        $savedNative = $global:PSNativeCommandUseErrorActionPreference
        $savedAction = $global:ErrorActionPreference
        try {
            $global:PSNativeCommandUseErrorActionPreference = $true
            $global:ErrorActionPreference = 'Stop'
            { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'err-tar-native.zip')) -PwshArchivePath $bad 2>$null } |
                Should -Throw -ErrorId 'PwshArchiveExtractFailed,Build-LambdaPSRuntimePackage'
        }
        finally {
            $global:PSNativeCommandUseErrorActionPreference = $savedNative
            $global:ErrorActionPreference = $savedAction
        }
    }

    It 'says the marker is missing when there is no marker and no -Target' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'err-nomarker')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        Remove-Item -LiteralPath ([System.IO.Path]::Combine($proj, 'lambdaps-runtime.json'))

        $err = { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'err-nomarker.zip')) } |
            Should -Throw -ErrorId 'TargetNotResolved,Build-LambdaPSRuntimePackage' -PassThru
        $err.Exception.Message | Should -BeLike '*No lambdaps-runtime.json marker*-Target Layer*'
    }

    # A function project is handler code for a separately deployed runtime, so it names the cmdlet that packages it.
    It 'throws NotARuntimeProject naming Compress-LambdaPSFunctionPackage for a function project, <Case>' -ForEach @(
        @{ Case = 'with no -Target'; Extra = @{} }
        @{ Case = 'with -Target SelfContainedZip'; Extra = @{ Target = 'SelfContainedZip'; PowerShellVersion = '7.4' } }
    ) {
        $proj = [System.IO.Path]::Combine($TestDrive, "err-function-$($Extra.Count)")
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler'

        $err = { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, "err-function-$($Extra.Count).zip")) -PwshArchivePath $script:fakeTarball @Extra } |
            Should -Throw -ErrorId 'NotARuntimeProject,Build-LambdaPSRuntimePackage' -PassThru
        $err.Exception.Message | Should -BeLike '*Compress-LambdaPSFunctionPackage*'
    }
}

# A downloaded tarball is ~70 MB in the temp folder, so every run removes it; a caller's is theirs.
Describe 'Build-LambdaPSRuntimePackage pwsh tarball cleanup' -Tag 'Unit' {
    BeforeAll {
        # A tarball of its own, standing in for one the seam downloaded to the temp folder.
        $script:downloadedSrc = [System.IO.Path]::Combine($TestDrive, 'downloaded-pwsh-src')
        $null = New-Item -ItemType Directory -Path $script:downloadedSrc -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:downloadedSrc, 'pwsh')) -Value "#!/bin/sh`nexec '$([System.Environment]::ProcessPath)' `"`$@`""
        if (-not $IsWindows) { & chmod +x ([System.IO.Path]::Combine($script:downloadedSrc, 'pwsh')) }
    }

    It 'deletes a downloaded pwsh tarball after the build, but keeps a caller-supplied one' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'cleanup')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $downloaded = [System.IO.Path]::Combine($TestDrive, 'downloaded-pwsh.tar.gz')
        & $script:tar -czf $downloaded -C $script:downloadedSrc .
        # Only the download is mocked: the -ValidateOnly check returns nothing, as the real one does,
        # and a -PwshArchivePath call goes to the real seam.
        $script:downloadedTarball = $downloaded
        Mock 'Get-LambdaPSPwshArchive' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -ParameterFilter { -not $PwshArchivePath } -MockWith {
            if (-not $ValidateOnly) { $script:downloadedTarball }
        }

        $result = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'cleanup.zip'))
        @($result).Count | Should -Be 1
        Test-Path -LiteralPath $downloaded | Should -BeFalse

        $null = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'cleanup2.zip')) -PwshArchivePath $script:fakeTarball
        Test-Path -LiteralPath $script:fakeTarball | Should -BeTrue
    }

    It 'deletes a downloaded pwsh tarball when the build fails after the download' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'cleanup-fail')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $empty = [System.IO.Path]::Combine($TestDrive, 'downloaded-empty-src')
        $null = New-Item -ItemType Directory -Path $empty -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($empty, 'readme.txt')) -Value 'no interpreter here'
        $downloaded = [System.IO.Path]::Combine($TestDrive, 'downloaded-empty.tar.gz')
        & $script:tar -czf $downloaded -C $empty .
        $script:downloadedTarball = $downloaded
        Mock 'Get-LambdaPSPwshArchive' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -ParameterFilter { -not $PwshArchivePath } -MockWith {
            if (-not $ValidateOnly) { $script:downloadedTarball }
        }

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'cleanup-fail.zip')) } |
            Should -Throw -ErrorId 'PwshBinaryNotFound,Build-LambdaPSRuntimePackage'
        Test-Path -LiteralPath $downloaded | Should -BeFalse
    }
}


Describe 'Build-LambdaPSRuntimePackage pwsh version' -Tag 'Unit' {
    It 'fails closed on a recorded SelfContainedZip pwsh version with no pinned hash, before downloading' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'scz-unpinned')
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4' -PwshVersion '7.4.19' -WarningAction SilentlyContinue
        $out = [System.IO.Path]::Combine($TestDrive, 'scz-unpinned.zip')

        { Build-LambdaPSRuntimePackage -Path $proj -OutFile $out } |
            Should -Throw -ErrorId 'UnknownPwshArchiveHash*' -ExpectedMessage '*7.4.19*'
        Test-Path -LiteralPath $out | Should -BeFalse
    }

    It 'bundles the pwsh version the project was scaffolded with' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'pin-proj')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -PwshVersion '7.4.2' -Target Layer -WarningAction SilentlyContinue
        (Get-Content -LiteralPath ([System.IO.Path]::Combine($proj, 'lambdaps-runtime.json')) -Raw | ConvertFrom-Json).PwshVersion | Should -Be '7.4.2'

        $result = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'pin.zip')) -PwshArchivePath $script:fakeTarball

        $result.PwshVersion | Should -Be '7.4.2'
    }

    It 'lets -PwshVersion override the recorded version' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'pin-override')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -PwshVersion '7.4.2' -Target Layer -WarningAction SilentlyContinue

        $result = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'pin-override.zip')) -PwshVersion '7.4.3' -PwshArchivePath $script:fakeTarball

        $result.PwshVersion | Should -Be '7.4.3'
    }

    # A pattern ending in '$' also matches before a final newline, so the versions are anchored with \z.
    It 'refuses -<Parameter> with a trailing newline at binding' -ForEach @(
        @{ Parameter = 'PowerShellVersion'; Value = "7.4`n" }
        @{ Parameter = 'PwshVersion'; Value = "7.4.20`n" }
    ) {
        $proj = [System.IO.Path]::Combine($TestDrive, "newline-$Parameter")
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $params = @{ Path = $proj; OutFile = [System.IO.Path]::Combine($TestDrive, "newline-$Parameter.zip"); PwshArchivePath = $script:fakeTarball; $Parameter = $Value }
        { Build-LambdaPSRuntimePackage @params } |
            Should -Throw -ErrorId "ParameterArgumentValidationError,Build-LambdaPSRuntimePackage"
        # The version helpers refuse the same value with the same error, so the parameter's own pattern
        # is what shows the binding refuses it.
        $pattern = ((Get-Command Build-LambdaPSRuntimePackage).Parameters[$Parameter].Attributes |
            Where-Object { $_ -is [System.Management.Automation.ValidatePatternAttribute] }).RegexPattern
        $Value -match $pattern | Should -BeFalse
    }

    # \d also matches other scripts' digits, such as Arabic-Indic ones; a version is ASCII digits only.
    It 'refuses -<Parameter> in non-ASCII digits at binding, naming the form it takes' -ForEach @(
        @{ Parameter = 'PowerShellVersion'; Value = "$([char]0x0667).$([char]0x0664)"; Message = '*is not a PowerShell version in major.minor form, for example 7.4.' }
        @{ Parameter = 'PwshVersion'; Value = "7.4.$([char]0x0662)$([char]0x0660)"; Message = '*is not a pwsh release in x.y.z form, for example 7.4.20.' }
    ) {
        $proj = [System.IO.Path]::Combine($TestDrive, "digits-$Parameter")
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $params = @{ Path = $proj; OutFile = [System.IO.Path]::Combine($TestDrive, "digits-$Parameter.zip"); PwshArchivePath = $script:fakeTarball; $Parameter = $Value }

        { Build-LambdaPSRuntimePackage @params } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,Build-LambdaPSRuntimePackage' -ExpectedMessage $Message
    }

    It 'bundles the default patch of the -PowerShellVersion line, not the recorded version of another line' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'pin-line')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -PwshVersion '7.4.2' -Target Layer -WarningAction SilentlyContinue

        # The tarball is checked against the hash pinned for the version it maps to.
        $script:fakeTarballHash = (Get-FileHash -LiteralPath $script:fakeTarball -Algorithm SHA256).Hash
        Mock 'Get-LambdaPSPwshArchiveHash' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { $script:fakeTarballHash }
        $out = [System.IO.Path]::Combine($TestDrive, 'pin-line.zip')

        $result = Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PowerShellVersion '7.6' -PwshArchivePath $script:fakeTarball

        $result.PwshVersion | Should -Be $script:pwshConfig.DefaultPatch['7.6']
        Should -Invoke 'Get-LambdaPSPwshArchiveHash' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -ParameterFilter { $PwshVersion -eq $script:pwshConfig.DefaultPatch['7.6'] }
        Should -Invoke 'Get-LambdaPSPwshArchiveHash' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -ParameterFilter { $PwshVersion -eq '7.4.2' } -Times 0 -Exactly
        $names = Get-ZipEntryNames -ZipPath $out
        $names | Should -Not -Contain 'README.md'
        $names | Should -Not -Contain 'lambdaps-runtime.json'
    }
}

Describe 'Build-LambdaPSRuntimePackage target' -Tag 'Unit' {
    It 'throws PowerShellVersionRequired for a project with no marker and no -PowerShellVersion' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'nomarker-noversion')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        Remove-Item -LiteralPath ([System.IO.Path]::Combine($proj, 'lambdaps-runtime.json'))
        $out = [System.IO.Path]::Combine($TestDrive, 'nomarker-noversion.zip')

        $err = { Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -Target Layer -PwshArchivePath $script:fakeTarball } |
            Should -Throw -ErrorId 'PowerShellVersionRequired,Build-LambdaPSRuntimePackage' -PassThru
        $err.Exception.Message | Should -BeLike '*has no lambdaps-runtime.json marker*-PowerShellVersion*'
    }

    It 'says the marker records no PowerShell version when it has none' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'marker-noversion')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        Set-Content -LiteralPath ([System.IO.Path]::Combine($proj, 'lambdaps-runtime.json')) -Value '{"Target":"Layer"}'

        $err = { Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'marker-noversion.zip')) -PwshArchivePath $script:fakeTarball } |
            Should -Throw -ErrorId 'PowerShellVersionRequired,Build-LambdaPSRuntimePackage' -PassThru
        $err.Exception.Message | Should -BeLike '*marker*records no PowerShell version*'
    }

    It 'builds a project with no marker for the -Target given' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'target-nomarker')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        Remove-Item -LiteralPath ([System.IO.Path]::Combine($proj, 'lambdaps-runtime.json'))

        $result = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'target-nomarker.zip')) -Target Layer -PowerShellVersion '7.4' -PwshArchivePath $script:fakeTarball

        $result.Target | Should -Be 'Layer'
        $names = Get-ZipEntryNames -ZipPath $result.PackagePath
        $names | Should -Contain 'bootstrap'
    }

    It 'throws TargetMismatch for a -Target that disagrees with the marker' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'target-mismatch')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $out = [System.IO.Path]::Combine($TestDrive, 'target-mismatch.zip')

        $err = { Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -Target SelfContainedZip -PwshArchivePath $script:fakeTarball } |
            Should -Throw -ErrorId 'TargetMismatch,Build-LambdaPSRuntimePackage' -PassThru
        $err.Exception.Message | Should -BeLike '*-Target SelfContainedZip*Layer project*'
        Test-Path -LiteralPath $out | Should -BeFalse
    }

    It 'throws UnsupportedTarget, naming Build-LambdaPSRuntimeImage, for a CMR project whatever -Target says' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'target-cmr')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4'
        $out = [System.IO.Path]::Combine($TestDrive, 'target-cmr.zip')

        $err = { Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -Target Layer -PwshArchivePath $script:fakeTarball } |
            Should -Throw -ErrorId 'UnsupportedTarget,Build-LambdaPSRuntimePackage' -PassThru
        $err.Exception.Message | Should -BeLike '*Build-LambdaPSRuntimeImage*'
        Test-Path -LiteralPath $out | Should -BeFalse
    }

    It 'accepts a -Target that matches the marker' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'target-match')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer

        $result = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'target-match.zip')) -Target Layer -PwshArchivePath $script:fakeTarball

        $result.Target | Should -Be 'Layer'
    }

    # -Target binds in any case, and a marker can be edited by hand; the result names the target as
    # the cmdlets spell it. The marker row is coverage only: the marker reader already gives the
    # canonical spelling, so the build's own canonicalisation is shown by the -Target row.
    It 'reports the canonical target for a <Case> written in lowercase' -ForEach @(
        @{ Case = '-Target'; Marker = $false }, @{ Case = 'marker Target'; Marker = $true }
    ) {
        $proj = [System.IO.Path]::Combine($TestDrive, "target-case-$Marker")
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $markerPath = [System.IO.Path]::Combine($proj, 'lambdaps-runtime.json')
        $params = @{ Path = $proj; OutFile = [System.IO.Path]::Combine($TestDrive, "target-case-$Marker.zip"); PwshArchivePath = $script:fakeTarball }
        if ($Marker) {
            Set-Content -LiteralPath $markerPath -Value ([System.IO.File]::ReadAllText($markerPath).Replace('"Layer"', '"layer"'))
        }
        else {
            Remove-Item -LiteralPath $markerPath
            $params.Target = 'layer'
            $params.PowerShellVersion = '7.4'
        }

        $result = Build-LambdaPSRuntimePackage @params -WarningAction SilentlyContinue

        $result.Target | Should -BeExactly 'Layer'
    }

    # The precompile command is PowerShell text holding the project path in single quotes. PowerShell
    # also ends such a string at the typographic quotes, so a folder named with one must still build.
    # Spaces in the project path and in the -OutFile folder, through scaffold, lint, precompile and zip.
    It 'scaffolds, lints and builds a SelfContainedZip whose paths have spaces' -Skip:(-not $IsLinux) {
        $proj = [System.IO.Path]::Combine($TestDrive, 'spaced dir', 'my fn')
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x86_64' }
        $out = [System.IO.Path]::Combine($TestDrive, 'out dir', 'my fn.zip')

        Test-LambdaPSRuntimeProject -Path $proj -Quiet | Should -BeTrue
        $result = Build-LambdaPSRuntimePackage -Path $proj -OutFile $out -PwshArchivePath $script:fakeTarball -Architecture $arch

        $result.PackagePath | Should -Be ([System.IO.Path]::GetFullPath($out))
        $names = Get-ZipEntryNames -ZipPath $out
        $names | Should -Contain 'PowerShellLambdaContext.dll'
        $names | Should -Contain 'handler.ps1'
    }

    It 'precompiles a project whose path has a typographic apostrophe' -Skip:(-not $IsLinux) {
        $proj = [System.IO.Path]::Combine($TestDrive, "Jo$([char]0x2019)s layer")
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x86_64' }

        $result = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'quote.zip')) -PwshArchivePath $script:fakeTarball -Architecture $arch

        $names = Get-ZipEntryNames -ZipPath $result.PackagePath
        $names | Should -Contain 'PowerShellLambdaContext.dll'
    }
}

# The DLL must be compiled with the BUNDLED pwsh, not the host's. A 7.6 host targets .NET 10 and the
# bundled 7.4 runs .NET 8, so a host-compiled DLL does not load and every invoke fails.
Describe 'Build-LambdaPSRuntimePackage LambdaContext precompile' -Tag 'Unit' {
    It 'precompiles with the bundled pwsh on a matching Linux host, and ships the .cs too' -Skip:(-not $IsLinux) {
        $proj = [System.IO.Path]::Combine($TestDrive, 'ctx-bundled')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        Remove-Item -LiteralPath $script:bundledRanMarker -ErrorAction SilentlyContinue
        $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x86_64' }

        $zip = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'ctx-bundled.zip')) -PwshArchivePath $script:fakeTarball -Architecture $arch

        Test-Path -LiteralPath $script:bundledRanMarker | Should -BeTrue -Because 'the bundled pwsh, not the host, must compile the DLL'
        $names = Get-ZipEntryNames $zip.PackagePath
        $names | Should -Contain 'PowerShellLambdaContext.dll'
        $names | Should -Contain 'PowerShellLambdaContext.cs'
    }

    It 'returns only the package, not the bundled pwsh''s output' -Skip:(-not $IsLinux) {
        $src = [System.IO.Path]::Combine($TestDrive, 'chatty-pwsh-src')
        $null = New-Item -ItemType Directory -Path $src -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($src, 'pwsh')) -Value ("#!/bin/sh`necho 'fake bundled pwsh: a test line, not a warning'`n" +
            "out=`$(printf '%s' `"`$4`" | sed -n `"s/.*-OutputAssembly '\([^']*\)'.*/\1/p`")`necho dll > `"`$out`"")
        $tarball = [System.IO.Path]::Combine($TestDrive, 'chatty-pwsh.tar.gz')
        & tar -czf $tarball -C $src .
        $proj = [System.IO.Path]::Combine($TestDrive, 'ctx-chatty')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x86_64' }

        $result = @(Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'ctx-chatty.zip')) -PwshArchivePath $tarball -Architecture $arch 6>$null)

        $result.Count | Should -Be 1
        $result[0].PSTypeNames | Should -Contain 'AWS.ToolExtras.LambdaPSRuntime.RuntimePackage'
    }

    # As on a temp folder mounted noexec: the documented fallback, not a failed build.
    It 'warns and ships only the .cs when the bundled pwsh cannot start' -Skip:(-not $IsLinux) {
        $src = [System.IO.Path]::Combine($TestDrive, 'noexec-pwsh-src')
        $null = New-Item -ItemType Directory -Path $src -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($src, 'pwsh')) -Value "$([char]0x7f)ELF not a program" -NoNewline
        $tarball = [System.IO.Path]::Combine($TestDrive, 'noexec-pwsh.tar.gz')
        & tar -czf $tarball -C $src .
        $proj = [System.IO.Path]::Combine($TestDrive, 'ctx-noexec')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x86_64' }

        $zip = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'ctx-noexec.zip')) -PwshArchivePath $tarball -Architecture $arch -WarningVariable warnings -WarningAction SilentlyContinue

        "$($warnings[0])" | Should -BeLike '*cold start*'
        $names = Get-ZipEntryNames $zip.PackagePath
        $names | Should -Not -Contain 'PowerShellLambdaContext.dll'
        $names | Should -Contain 'PowerShellLambdaContext.cs'
    }

    # The pwsh shipped is the tarball's own binary, for the target architecture, unchanged.
    It 'ships a SelfContainedZip for the other architecture with its pwsh, the .cs and no .dll, with a warning' {
        $other = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'x86_64' } else { 'arm64' }
        # An ELF header for the target: 64-bit, little-endian, e_machine aarch64 (0xB7) or x86-64 (0x3E).
        $machine = if ($other -eq 'arm64') { 0xB7 } else { 0x3E }
        $elf = [byte[]]::new(64)
        $elf[0] = 0x7F; $elf[1] = 0x45; $elf[2] = 0x4C; $elf[3] = 0x46; $elf[4] = 2; $elf[5] = 1; $elf[18] = $machine
        $src = [System.IO.Path]::Combine($TestDrive, 'elf-pwsh-src')
        $null = New-Item -ItemType Directory -Path $src -Force
        [System.IO.File]::WriteAllBytes([System.IO.Path]::Combine($src, 'pwsh'), $elf)
        $tarball = [System.IO.Path]::Combine($TestDrive, 'elf-pwsh.tar.gz')
        & $script:tar -czf $tarball -C $src .
        $proj = [System.IO.Path]::Combine($TestDrive, 'scz-cross')
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'

        $zip = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'scz-cross.zip')) -PwshArchivePath $tarball -Architecture $other -WarningVariable warnings -WarningAction SilentlyContinue

        "$($warnings[0])" | Should -BeLike '*cold start*'
        $names = Get-ZipEntryNames $zip.PackagePath
        $names | Should -Contain 'PowerShellLambdaContext.cs'
        $names | Should -Not -Contain 'PowerShellLambdaContext.dll'
        $archive = [System.IO.Compression.ZipFile]::OpenRead($zip.PackagePath)
        try {
            $stream = $archive.GetEntry('powershell/pwsh').Open()
            try { $head = [byte[]]::new(20); $null = $stream.Read($head, 0, 20) } finally { $stream.Dispose() }
        }
        finally { $archive.Dispose() }
        [System.Text.Encoding]::ASCII.GetString($head, 1, 3) | Should -Be 'ELF'
        $head[18] | Should -Be $machine
    }

    It 'warns and ships only the .cs when the host cannot run the bundled pwsh' {
        $proj = [System.IO.Path]::Combine($TestDrive, 'ctx-cross')
        $null = New-LambdaPSRuntimeProject -Path $proj -PowerShellVersion '7.4' -Target Layer
        $other = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'x86_64' } else { 'arm64' }

        $zip = Build-LambdaPSRuntimePackage -Path $proj -OutFile ([System.IO.Path]::Combine($TestDrive, 'ctx-cross.zip')) -PwshArchivePath $script:fakeTarball -Architecture $other -WarningVariable warnings -WarningAction SilentlyContinue

        @($warnings).Count | Should -Be 1
        "$($warnings[0])" | Should -BeLike '*cold start*'
        $names = Get-ZipEntryNames $zip.PackagePath
        $names | Should -Not -Contain 'PowerShellLambdaContext.dll'
        $names | Should -Contain 'PowerShellLambdaContext.cs'
    }
}

Describe 'Build-LambdaPSRuntimePackage -HandlerScript help' -Tag 'Unit' {
    It 'says a backslash fails off Windows with InvalidHandlerPath, and to use /' {
        $text = (Get-Help Build-LambdaPSRuntimePackage -Parameter HandlerScript).Description.Text -join ' '
        $text | Should -Match 'InvalidHandlerPath'
        $text | Should -Not -Match "read as '/'"
    }
}
