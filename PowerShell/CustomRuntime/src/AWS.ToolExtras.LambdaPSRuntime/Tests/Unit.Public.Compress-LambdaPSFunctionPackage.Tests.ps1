BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Zip.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeModule.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Compress-LambdaPSFunctionPackage' -Tag 'Unit' {
    Context 'a scaffolded function project' {
        It 'places the handler and modules at the archive root and excludes scaffolding files' {
            $project = [System.IO.Path]::Combine($TestDrive, 'fn-zip')
            $null = New-LambdaPSFunctionProject -Path $project -Handler 'handler'
            $outFile = [System.IO.Path]::Combine($TestDrive, 'fn-zip.zip')

            $result = Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile
            $result.PackagePath | Should -Be ([System.IO.Path]::GetFullPath($outFile))
            $result.PSTypeNames | Should -Contain 'AWS.ToolExtras.LambdaPSRuntime.FunctionPackage'
            Test-Path -LiteralPath $outFile | Should -BeTrue

            $entries = Get-ZipEntryNames -ZipPath $outFile

            $entries | Should -Contain 'handler.ps1'
            ($entries | Where-Object { $_ -like 'modules/*' }) | Should -Not -BeNullOrEmpty
            $entries | Should -Not -Contain 'manifest.json'
            $entries | Should -Not -Contain 'README.md'
        }

        It 'writes forward-slash entry paths' {
            $project = [System.IO.Path]::Combine($TestDrive, 'fn-slash')
            $null = New-LambdaPSFunctionProject -Path $project -Handler 'handler'
            $outFile = [System.IO.Path]::Combine($TestDrive, 'fn-slash.zip')

            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile

            $entries = Get-ZipEntryNames -ZipPath $outFile

            ($entries | Where-Object { $_ -like '*\*' }) | Should -BeNullOrEmpty
        }
    }

    Context 'what a folder source ships' {
        It 'leaves out .git and the package being written into the source folder' {
            $project = [System.IO.Path]::Combine($TestDrive, 'fn-git')
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($project, '.git')) -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($project, '.git', 'HEAD')) -Value 'ref: refs/heads/main'
            Set-Content -LiteralPath ([System.IO.Path]::Combine($project, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
            $outFile = [System.IO.Path]::Combine($project, 'fn-git.zip')

            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile
            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile -Force

            (Get-ZipEntry $outFile).Name | Should -Be @('handler.ps1')
        }

        # The output is left out by its real location, so the warning must not say it ships.
        It 'warns about a top-level .zip only when it ships, however the folder is reached' -Skip:$IsWindows {
            $real = [System.IO.Path]::Combine($TestDrive, 'warn-real')
            $project = [System.IO.Path]::Combine($real, 'fn')
            $null = New-Item -ItemType Directory -Path $project -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($project, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
            $link = [System.IO.Path]::Combine($TestDrive, 'warn-link')
            $null = New-Item -ItemType SymbolicLink -Path $link -Target $real
            $outFile = [System.IO.Path]::Combine($project, 'fn.zip')
            $null = Compress-LambdaPSFunctionPackage -Path ([System.IO.Path]::Combine($link, 'fn')) -OutFile $outFile

            $null = Compress-LambdaPSFunctionPackage -Path ([System.IO.Path]::Combine($link, 'fn')) -OutFile $outFile -Force -WarningVariable quiet -WarningAction SilentlyContinue
            @($quiet) | Should -BeNullOrEmpty
            (Get-ZipEntry $outFile).Name | Should -Be @('handler.ps1')

            # A link inside the folder to a .zip ships its content, so it warns.
            Copy-Item -LiteralPath $outFile -Destination ([System.IO.Path]::Combine($project, 'lib.bin'))
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($project, 'old.zip')) -Target 'lib.bin'
            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile -Force -WarningVariable linked -WarningAction SilentlyContinue
            @($linked).Count | Should -Be 1
            "$($linked[0])" | Should -BeLike '*old.zip*ships inside the package*'
        }

        It 'skips a symbolic link that points outside the source, with a warning' -Skip:$IsWindows {
            $outside = [System.IO.Path]::Combine($TestDrive, 'secret')
            $null = New-Item -ItemType Directory -Path $outside -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($outside, 'creds')) -Value 'secret'
            $project = [System.IO.Path]::Combine($TestDrive, 'fn-link')
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($project, 'data')) -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($project, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
            Set-Content -LiteralPath ([System.IO.Path]::Combine($project, 'data', 'config.json')) -Value '{}'
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($project, 'creds')) -Target ([System.IO.Path]::Combine($outside, 'creds'))
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($project, 'secretdir')) -Target $outside
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($project, 'config.json')) -Target ([System.IO.Path]::Combine($project, 'data', 'config.json'))
            $outFile = [System.IO.Path]::Combine($TestDrive, 'fn-link.zip')

            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile -WarningVariable warnings -WarningAction SilentlyContinue

            (Get-ZipEntry $outFile).Name | Sort-Object | Should -Be @('config.json', 'data/config.json', 'handler.ps1')
            @($warnings).Count | Should -Be 2
            ($warnings -join ' ') | Should -BeLike '*outside*'
        }

        It 'skips a folder link to the source root, with a warning, rather than copying it into itself' -Skip:$IsWindows {
            $project = [System.IO.Path]::Combine($TestDrive, 'fn-loop')
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($project, 'sub')) -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($project, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($project, 'sub', 'up')) -Target $project
            $outFile = [System.IO.Path]::Combine($TestDrive, 'fn-loop.zip')

            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile -WarningVariable warnings -WarningAction SilentlyContinue

            (Get-ZipEntry $outFile).Name | Should -Be @('handler.ps1')
            @($warnings).Count | Should -Be 1
            "$($warnings[0])" | Should -BeLike '*a folder it was reached through*'
        }

        It 'warns about a .zip at the top of the folder, which is usually an earlier package' {
            $project = [System.IO.Path]::Combine($TestDrive, 'fn-oldzip')
            $null = New-Item -ItemType Directory -Path $project -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($project, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
            Set-Content -LiteralPath ([System.IO.Path]::Combine($project, 'old.zip')) -Value 'not really a zip'
            $outFile = [System.IO.Path]::Combine($TestDrive, 'fn-oldzip.zip')

            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile -WarningVariable warnings -WarningAction SilentlyContinue

            ($warnings -join ' ') | Should -BeLike '*old.zip*earlier package*'
        }

        It 'warns about a bootstrap file at the top of the folder, which replaces a layer runtime' {
            $project = [System.IO.Path]::Combine($TestDrive, 'fn-bootstrap')
            $null = New-Item -ItemType Directory -Path $project -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($project, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
            Set-Content -LiteralPath ([System.IO.Path]::Combine($project, 'bootstrap')) -Value '#!/bin/sh'

            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile ([System.IO.Path]::Combine($TestDrive, 'fn-bootstrap.zip')) -WarningVariable warnings -WarningAction SilentlyContinue

            ($warnings -join ' ') | Should -BeLike "*'bootstrap' file at its root*instead of the layer's bootstrap*"
        }

        It 'keeps the execute bit of an executable file' -Skip:$IsWindows {
            $project = [System.IO.Path]::Combine($TestDrive, 'fn-exec')
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($project, 'bin')) -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($project, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
            $tool = [System.IO.Path]::Combine($project, 'bin', 'tool')
            Set-Content -LiteralPath $tool -Value '#!/bin/sh'
            [System.IO.File]::SetUnixFileMode($tool, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute, GroupRead, GroupExecute, OtherRead, OtherExecute')
            $outFile = [System.IO.Path]::Combine($TestDrive, 'fn-exec.zip')

            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile

            $entries = Get-ZipEntry $outFile
            ($entries | Where-Object Name -eq 'bin/tool').Mode | Should -Be ([Convert]::ToInt32('755', 8))
            ($entries | Where-Object Name -eq 'handler.ps1').Mode | Should -Be ([Convert]::ToInt32('644', 8))
        }
    }

    # An interrupted run leaves <OutFile>.<random>.partial beside -OutFile; inside the folder it is not code.
    Context 'a partial package left in the folder by an interrupted run' {
        It 'leaves it out of the package' {
            $project = [System.IO.Path]::Combine($TestDrive, 'fn-partial')
            $null = New-LambdaPSFunctionProject -Path $project -Handler 'handler'
            $outFile = [System.IO.Path]::Combine($project, 'fn.zip')
            Set-Content -LiteralPath "$outFile.abc123.partial" -Value 'half a zip'

            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile

            $entries = Get-ZipEntryNames -ZipPath $outFile
            $entries | Should -Contain 'handler.ps1'
            @($entries | Where-Object { $_ -like '*.partial' }) | Should -BeNullOrEmpty
        }
    }

    # A name starting with '.' is hidden on Linux and macOS; it is still the function's folder.
    Context 'a project folder whose name starts with a dot' {
        It 'packages it' {
            $project = [System.IO.Path]::Combine($TestDrive, '.fn-dot')
            $null = New-LambdaPSFunctionProject -Path $project -Handler 'handler'
            $outFile = [System.IO.Path]::Combine($TestDrive, 'fn-dot.zip')

            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile -ErrorVariable compressErrors

            # A hidden folder that is read without -Force reports an error and is then not looked into.
            $compressErrors | Should -BeNullOrEmpty
            $entries = Get-ZipEntryNames -ZipPath $outFile
            $entries | Should -Contain 'handler.ps1'
        }
    }

    Context 'reproducible packages' {
        It 'writes a byte-identical package when unchanged code is packaged twice' {
            $project = [System.IO.Path]::Combine($TestDrive, 'fn-repro')
            $null = New-LambdaPSFunctionProject -Path $project -Handler 'handler'
            $a = [System.IO.Path]::Combine($TestDrive, 'fn-repro-a.zip')
            $b = [System.IO.Path]::Combine($TestDrive, 'fn-repro-b.zip')

            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $a
            Get-ChildItem -LiteralPath $project -Recurse -File | ForEach-Object { $_.LastWriteTime = [datetime]::Now.AddMinutes(5) }
            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $b

            (Get-FileHash -LiteralPath $b -Algorithm SHA256).Hash | Should -Be (Get-FileHash -LiteralPath $a -Algorithm SHA256).Hash
        }
    }

    Context '-Force behavior' {
        It 'throws when the output file exists and -Force is absent' {
            $project = [System.IO.Path]::Combine($TestDrive, 'fn-exists')
            $null = New-LambdaPSFunctionProject -Path $project -Handler 'handler'
            $outFile = [System.IO.Path]::Combine($TestDrive, 'fn-exists.zip')
            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile

            { Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile } |
                Should -Throw -ErrorId 'OutFileExists,Compress-LambdaPSFunctionPackage'
        }

        It 'overwrites an existing output file when -Force is supplied' {
            $project = [System.IO.Path]::Combine($TestDrive, 'fn-force')
            $null = New-LambdaPSFunctionProject -Path $project -Handler 'handler'
            $outFile = [System.IO.Path]::Combine($TestDrive, 'fn-force.zip')
            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile
            Set-Content -LiteralPath ([System.IO.Path]::Combine($project, 'added.ps1')) -Value '# added'

            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile -Force

            $entries = Get-ZipEntryNames -ZipPath $outFile
            $entries | Should -Contain 'added.ps1'
        }

        It 'throws OutFileIsDirectory when -OutFile is an existing directory, even with -Force' {
            $project = [System.IO.Path]::Combine($TestDrive, 'fn-outdir')
            $null = New-LambdaPSFunctionProject -Path $project -Handler 'handler'
            $outFile = [System.IO.Path]::Combine($TestDrive, 'fn-outdir.zip')
            $null = New-Item -ItemType Directory -Path $outFile -Force

            { Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile -Force } |
                Should -Throw -ErrorId 'OutFileIsDirectory,Compress-LambdaPSFunctionPackage'
            @(Get-ChildItem -LiteralPath $outFile -Force) | Should -BeNullOrEmpty
        }

        It 'takes the destination as -PackagePath too, the name of the result field' {
            $src = [System.IO.Path]::Combine($TestDrive, 'alias.ps1')
            Set-Content -LiteralPath $src -Value 'param($LambdaInput, $LambdaContext)'
            $outFile = [System.IO.Path]::Combine($TestDrive, 'alias.zip')

            $result = Compress-LambdaPSFunctionPackage -Path $src -PackagePath $outFile

            $result.PackagePath | Should -Be ([System.IO.Path]::GetFullPath($outFile))
        }

        It 'creates the parent directory of the output file when missing' {
            $project = [System.IO.Path]::Combine($TestDrive, 'fn-parent')
            $null = New-LambdaPSFunctionProject -Path $project -Handler 'handler'
            $outFile = [System.IO.Path]::Combine($TestDrive, 'nested', 'deep', 'fn-parent.zip')

            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $outFile
            Test-Path -LiteralPath $outFile | Should -BeTrue
        }
    }

    Context 'a supplied (non-scaffolded) folder' {
        It 'packages it as-is, keeping a root README.md and manifest.json when there is no scaffold marker manifest' {
            # A folder with a README.md but NO manifest.json is not a scaffolded project.
            $dir = [System.IO.Path]::Combine($TestDrive, 'supplied')
            $null = New-Item -ItemType Directory -Path $dir -Force
            Set-Content -LiteralPath (Join-Path $dir 'script.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            Set-Content -LiteralPath (Join-Path $dir 'README.md') -Value '# keep me'
            $outFile = [System.IO.Path]::Combine($TestDrive, 'supplied.zip')

            $null = Compress-LambdaPSFunctionPackage -Path $dir -OutFile $outFile

            $entries = Get-ZipEntryNames -ZipPath $outFile
            $entries | Should -Contain 'script.ps1'
            $entries | Should -Contain 'README.md'
        }

        It 'keeps a manifest.json and README.md of the customer''s own, which the scaffolder did not write' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'own-manifest')
            $null = New-Item -ItemType Directory -Path $dir -Force
            Set-Content -LiteralPath (Join-Path $dir 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            Set-Content -LiteralPath (Join-Path $dir 'manifest.json') -Value '{"settings":{"retries":3}}'
            Set-Content -LiteralPath (Join-Path $dir 'README.md') -Value '# keep me'
            $outFile = [System.IO.Path]::Combine($TestDrive, 'own-manifest.zip')

            $null = Compress-LambdaPSFunctionPackage -Path $dir -OutFile $outFile

            $names = Get-ZipEntryNames -ZipPath $outFile
            $names | Should -Contain 'manifest.json'
            $names | Should -Contain 'README.md'
            $names | Should -Contain 'handler.ps1'
        }

        It 'stops with InvalidProjectManifest, and writes no package, when manifest.json is not valid JSON' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'broken-manifest')
            $null = New-Item -ItemType Directory -Path $dir -Force
            Set-Content -LiteralPath (Join-Path $dir 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            Set-Content -LiteralPath (Join-Path $dir 'manifest.json') -Value '{ "HandlerString": "handler.ps1",'
            $outFile = [System.IO.Path]::Combine($TestDrive, 'broken-manifest.zip')

            $err = { Compress-LambdaPSFunctionPackage -Path $dir -OutFile $outFile } |
                Should -Throw -ErrorId 'InvalidProjectManifest,Compress-LambdaPSFunctionPackage' -PassThru
            $err.Exception.Message | Should -BeLike "*$(Join-Path $dir 'manifest.json')*"
            $outFile | Should -Not -Exist
        }
    }

    Context 'a single .ps1 source' {
        It 'zips a lone script at the archive root' {
            $src = [System.IO.Path]::Combine($TestDrive, 'lone.ps1')
            Set-Content -LiteralPath $src -Value 'param($LambdaInput, $LambdaContext)'
            $outFile = [System.IO.Path]::Combine($TestDrive, 'lone.zip')

            $result = Compress-LambdaPSFunctionPackage -Path $src -OutFile $outFile

            $entries = Get-ZipEntryNames -ZipPath $outFile
            $entries | Should -Contain 'lone.ps1'
            $entries | Should -Be @('lone.ps1')
            @($result.ModulesStaged).Count | Should -Be 0
            $result.PackagePath | Should -Be ([System.IO.Path]::GetFullPath($outFile))
        }

        # -Force replaces an existing -OutFile, so the source itself (or a link to it) is refused, leaving it as it was.
        It 'throws OutFileIsSource, even with -Force, when -OutFile is <Case>' -ForEach @(
            @{ Case = 'the single-file source'; Link = $false }
            @{ Case = 'a link to the single-file source'; Link = $true }
        ) {
            if ($Link -and $IsWindows) { Set-ItResult -Skipped -Because 'creating a symbolic link needs elevation on Windows'; return }
            $src = [System.IO.Path]::Combine($TestDrive, "only-copy-$Link.ps1")
            Set-Content -LiteralPath $src -Value 'param($LambdaInput, $LambdaContext) ''my only copy'''
            $outFile = if ($Link) {
                $linkPath = [System.IO.Path]::Combine($TestDrive, "only-copy-link-$Link.zip")
                $null = New-Item -ItemType SymbolicLink -Path $linkPath -Target $src
                $linkPath
            }
            else { $src }

            { Compress-LambdaPSFunctionPackage -Path $src -OutFile $outFile -Force } |
                Should -Throw -ErrorId 'OutFileIsSource,Compress-LambdaPSFunctionPackage'
            Get-Content -LiteralPath $src -Raw | Should -Match 'my only copy'
        }

        It 'throws SourceNotPowerShell when a single-file source is not a .ps1' {
            $src = [System.IO.Path]::Combine($TestDrive, 'notes.txt')
            Set-Content -LiteralPath $src -Value 'hi'
            $outFile = [System.IO.Path]::Combine($TestDrive, 'notes.zip')
            { Compress-LambdaPSFunctionPackage -Path $src -OutFile $outFile } |
                Should -Throw -ErrorId 'SourceNotPowerShell,Compress-LambdaPSFunctionPackage'
        }
    }

    Context 'error handling' {
        It 'throws when the project directory is missing' {
            $missing = [System.IO.Path]::Combine($TestDrive, 'does-not-exist')
            $outFile = [System.IO.Path]::Combine($TestDrive, 'missing.zip')
            { Compress-LambdaPSFunctionPackage -Path $missing -OutFile $outFile } |
                Should -Throw -ErrorId 'ProjectNotFound,Compress-LambdaPSFunctionPackage'
        }

        It 'throws UnzippedPackageTooLarge for code over 250 MB unzipped and writes no .zip' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'too-large')
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($dir, 'modules', 'Huge', '1.0.0')) -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
            $file = [System.IO.File]::Create([System.IO.Path]::Combine($dir, 'modules', 'Huge', '1.0.0', 'huge.bin'))
            try { $file.SetLength(251MB) } finally { $file.Dispose() }
            $out = [System.IO.Path]::Combine($TestDrive, 'too-large.zip')

            { Compress-LambdaPSFunctionPackage -Path $dir -OutFile $out } |
                Should -Throw -ErrorId 'UnzippedPackageTooLarge,Compress-LambdaPSFunctionPackage'
            Test-Path -LiteralPath $out | Should -BeFalse
        }

        # Zipped as handler code, such a project would have no pwsh and a bootstrap that is not executable.
        It 'throws NotAFunctionProject for a <Target> project' -ForEach @(
            @{ Target = 'Layer'; Scaffold = { param($p) New-LambdaPSRuntimeProject -Path $p -PowerShellVersion '7.4.*' -Target Layer } }
            @{ Target = 'SelfContainedZip'; Scaffold = { param($p) New-LambdaPSFunctionProject -Path $p -Handler 'handler' -Target SelfContainedZip -PowerShellVersion '7.4.*' } }
            @{ Target = 'Container'; Scaffold = { param($p) New-LambdaPSFunctionProject -Path $p -Handler 'handler' -Target Container -PowerShellVersion '7.4.*' } }
        ) {
            $project = [System.IO.Path]::Combine($TestDrive, "marker-$Target")
            $null = & $Scaffold $project
            $out = [System.IO.Path]::Combine($TestDrive, "marker-$Target.zip")

            $err = { Compress-LambdaPSFunctionPackage -Path $project -OutFile $out } |
                Should -Throw -ErrorId 'NotAFunctionProject,Compress-LambdaPSFunctionPackage' -PassThru
            $err.Exception.Message | Should -BeLike "*is a $Target project*"
            $err.Exception.Message | Should -BeLike '*Build-LambdaPSRuntimePackage*Build-LambdaPSRuntimeImage*'
            Test-Path -LiteralPath $out | Should -BeFalse
        }

        # A handler that is not there would package without the modules its #Requires declares.
        It 'throws HandlerScriptNotFound for <Case>' -ForEach @(
            @{ Case = 'a misspelt -HandlerScript'; Name = 'hs-typo'; HandlerScript = 'hanlder.ps1'; Manifest = $null }
            @{ Case = 'a manifest.json ScriptFile that is not in the folder'; Name = 'hs-manifest'; HandlerScript = $null
                Manifest = '{"HandlerString":"gone.ps1::handler","ScriptFile":"gone.ps1"}' }
        ) {
            $dir = [System.IO.Path]::Combine($TestDrive, $Name)
            $null = New-Item -ItemType Directory -Path $dir -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
            if ($Manifest) { Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'manifest.json')) -Value $Manifest }
            $params = @{ Path = $dir; OutFile = [System.IO.Path]::Combine($TestDrive, "$Name.zip") }
            if ($HandlerScript) { $params.HandlerScript = $HandlerScript }

            { Compress-LambdaPSFunctionPackage @params } |
                Should -Throw -ErrorId 'HandlerScriptNotFound,Compress-LambdaPSFunctionPackage'
            Test-Path -LiteralPath $params.OutFile | Should -BeFalse
        }

        It 'throws HandlerScriptNotFound for a -HandlerScript outside the folder: <HandlerScript>' -ForEach @(
            @{ HandlerScript = '../hs-outside.ps1' }
            @{ HandlerScript = '<absolute>' }
        ) {
            $outside = [System.IO.Path]::Combine($TestDrive, 'hs-outside.ps1')
            Set-Content -LiteralPath $outside -Value '#Requires -Modules @{ ModuleName = ''LambdaPSNoSuchModule''; RequiredVersion = ''1.0.0'' }', 'param($LambdaInput, $LambdaContext)'
            $dir = [System.IO.Path]::Combine($TestDrive, 'hs-inside')
            $null = New-Item -ItemType Directory -Path $dir -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
            $name = if ($HandlerScript -eq '<absolute>') { $outside } else { $HandlerScript }

            { Compress-LambdaPSFunctionPackage -Path $dir -OutFile ([System.IO.Path]::Combine($TestDrive, 'hs-inside.zip')) -HandlerScript $name -MissingModuleAction Stop } |
                Should -Throw -ErrorId 'HandlerScriptNotFound,Compress-LambdaPSFunctionPackage'
        }

        # Run from another folder, so './single.ps1' only finds the script against the source folder. Its
        # #Requires names a module that is not available, so ModuleNotFoundLocally proves it was read.
        It 'reads -HandlerScript <Form> for a single-file source, resolved against its folder' -ForEach @(
            @{ Form = 'as a relative path'; Name = './single.ps1' }
            @{ Form = 'as the full path'; Name = '<full>' }
        ) {
            $folder = [System.IO.Path]::Combine($TestDrive, 'single-src')
            $null = New-Item -ItemType Directory -Path $folder -Force
            $src = [System.IO.Path]::Combine($folder, 'single.ps1')
            Set-Content -LiteralPath $src -Value "#Requires -Modules @{ ModuleName = 'LambdaPSNoSuchModule'; RequiredVersion = '1.0.0' }", 'param($LambdaInput, $LambdaContext)'
            $elsewhere = [System.IO.Path]::Combine($TestDrive, 'single-elsewhere')
            $null = New-Item -ItemType Directory -Path $elsewhere -Force
            $handler = if ($Name -eq '<full>') { $src } else { $Name }

            Push-Location -LiteralPath $elsewhere
            try {
                { Compress-LambdaPSFunctionPackage -Path $src -OutFile ([System.IO.Path]::Combine($TestDrive, 'single.zip')) -HandlerScript $handler -MissingModuleAction Stop } |
                    Should -Throw -ErrorId 'ModuleNotFoundLocally,Compress-LambdaPSFunctionPackage'
            }
            finally { Pop-Location }
        }

        # On a case-sensitive file system the file is simply not found; this guards Windows and macOS,
        # where it is found, but Lambda's file system would not find it.
        It 'throws HandlerScriptNotFound for a -HandlerScript that differs from the folder''s script only in case' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'hs-case')
            $null = New-Item -ItemType Directory -Path $dir -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'

            { Compress-LambdaPSFunctionPackage -Path $dir -OutFile ([System.IO.Path]::Combine($TestDrive, 'hs-case.zip')) -HandlerScript 'Handler.ps1' } |
                Should -Throw -ErrorId 'HandlerScriptNotFound,Compress-LambdaPSFunctionPackage'
        }

        # The copy skips a link that leaves the source, so a handler reached that way would not ship.
        It 'throws HandlerScriptNotFound when the handler is reached through <Case>' -Skip:$IsWindows -ForEach @(
            @{ Case = 'a file link that leaves the source'; Name = 'hl-file'; HandlerScript = 'handler.ps1' }
            @{ Case = 'a folder link that leaves the source'; Name = 'hl-folder'; HandlerScript = 'lib/handler.ps1' }
        ) {
            $shared = [System.IO.Path]::Combine($TestDrive, "$Name-shared")
            $null = New-Item -ItemType Directory -Path $shared -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($shared, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
            $dir = [System.IO.Path]::Combine($TestDrive, $Name)
            $null = New-Item -ItemType Directory -Path $dir -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'other.ps1')) -Value '# other'
            if ($HandlerScript -eq 'handler.ps1') {
                $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($dir, 'handler.ps1')) -Target ([System.IO.Path]::Combine($shared, 'handler.ps1'))
            }
            else {
                $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($dir, 'lib')) -Target $shared
            }
            $outFile = [System.IO.Path]::Combine($TestDrive, "$Name.zip")

            { Compress-LambdaPSFunctionPackage -Path $dir -OutFile $outFile -HandlerScript $HandlerScript -WarningAction SilentlyContinue } |
                Should -Throw -ErrorId 'HandlerScriptNotFound,Compress-LambdaPSFunctionPackage'
            # -WhatIf reports it too, as a real run does.
            { Compress-LambdaPSFunctionPackage -Path $dir -OutFile $outFile -HandlerScript $HandlerScript -WhatIf -WarningAction SilentlyContinue } |
                Should -Throw -ErrorId 'HandlerScriptNotFound,Compress-LambdaPSFunctionPackage'
            Test-Path -LiteralPath $outFile | Should -BeFalse
        }

        # A link into .git stays inside the source, so only the copy leaves it out, without a warning.
        It 'says the copy left out a handler that links into .git' -Skip:$IsWindows {
            $dir = [System.IO.Path]::Combine($TestDrive, 'hl-git')
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($dir, '.git')) -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, '.git', 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($dir, 'handler.ps1')) -Target '.git/handler.ps1'

            $err = { Compress-LambdaPSFunctionPackage -Path $dir -OutFile ([System.IO.Path]::Combine($TestDrive, 'hl-git.zip')) -HandlerScript 'handler.ps1' -WarningAction SilentlyContinue } |
                Should -Throw -ErrorId 'HandlerScriptNotFound,Compress-LambdaPSFunctionPackage' -PassThru
            $err.Exception.Message | Should -BeLike '*left out by the copy*a link into .git*'
        }

        It 'throws HandlerScriptNotFound when -HandlerScript names another script than a single-file source' {
            $src = [System.IO.Path]::Combine($TestDrive, 'one.ps1')
            Set-Content -LiteralPath $src -Value 'param($LambdaInput, $LambdaContext)'

            { Compress-LambdaPSFunctionPackage -Path $src -OutFile ([System.IO.Path]::Combine($TestDrive, 'one.zip')) -HandlerScript 'other.ps1' } |
                Should -Throw -ErrorId 'HandlerScriptNotFound,Compress-LambdaPSFunctionPackage'
        }

        # The package leaves out a folder link that leaves the source, so it would not contain the
        # script, and its #Requires would be read from outside.
        It 'throws HandlerScriptNotFound for a -HandlerScript reached through a folder link that leaves the source' -Skip:$IsWindows {
            $outside = [System.IO.Path]::Combine($TestDrive, 'hs-link-outside')
            $null = New-Item -ItemType Directory -Path $outside -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($outside, 'h.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
            $dir = [System.IO.Path]::Combine($TestDrive, 'hs-link')
            $null = New-Item -ItemType Directory -Path $dir -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($dir, 'lib')) -Target $outside

            { Compress-LambdaPSFunctionPackage -Path $dir -OutFile ([System.IO.Path]::Combine($TestDrive, 'hs-link.zip')) -HandlerScript 'lib/h.ps1' -WarningAction SilentlyContinue } |
                Should -Throw -ErrorId 'HandlerScriptNotFound,Compress-LambdaPSFunctionPackage'
        }

        # Off Windows '\' is a file name character, not a separator: refused, while 'sub/h.ps1' is accepted.
        It 'refuses \ in -HandlerScript off Windows, and accepts /' -Skip:$IsWindows {
            $dir = [System.IO.Path]::Combine($TestDrive, 'hs-backslash')
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($dir, 'sub')) -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'sub', 'h.ps1')) -Value 'param($LambdaInput, $LambdaContext)'

            { Compress-LambdaPSFunctionPackage -Path $dir -OutFile ([System.IO.Path]::Combine($TestDrive, 'hs-backslash.zip')) -HandlerScript 'sub\h.ps1' } |
                Should -Throw -ErrorId 'InvalidHandlerPath,Compress-LambdaPSFunctionPackage'
            $result = Compress-LambdaPSFunctionPackage -Path $dir -OutFile ([System.IO.Path]::Combine($TestDrive, 'hs-slash.zip')) -HandlerScript 'sub/h.ps1'
            $entries = Get-ZipEntryNames -ZipPath $result.PackagePath
            $entries | Should -Contain 'sub/h.ps1'
        }

        # On a case-insensitive file system, -Path ./Handler.ps1 opens handler.ps1, and the folder lists
        # it as handler.ps1. The listing is simulated here; the file is still its own handler.
        It 'packages a single-file source whose folder lists it in another case' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'hs-single-case')
            $null = New-Item -ItemType Directory -Path $dir -Force
            $src = [System.IO.Path]::Combine($dir, 'Handler.ps1')
            Set-Content -LiteralPath $src -Value 'param($LambdaInput, $LambdaContext)'
            $script:caseDir = $dir
            Mock 'Get-ChildItem' -ModuleName $script:moduleName -ParameterFilter { $File -and $LiteralPath -eq $script:caseDir } -MockWith {
                [PSCustomObject]@{ Name = 'handler.ps1' }
            }

            $result = Compress-LambdaPSFunctionPackage -Path $src -OutFile ([System.IO.Path]::Combine($TestDrive, 'hs-single-case.zip'))

            $entries = Get-ZipEntryNames -ZipPath $result.PackagePath
            $entries | Should -Contain 'Handler.ps1'
        }
    }
}

Describe 'Compress-LambdaPSFunctionPackage module staging' -Tag 'Unit' {
    It 'combines -IncludeModule with the handler #Requires and stages them once' {
        $dir = Join-Path $TestDrive 'stg'
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $dir 'handler.ps1') -Value @'
#Requires -Modules @{ ModuleName = 'AWS.Tools.DynamoDBv2'; RequiredVersion = '5.0.103' }
param($LambdaInput, $LambdaContext)
'@
        $out = Join-Path $TestDrive 'stg.zip'

        InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' -Parameters @{ dir = $dir; out = $out } {
            param($dir, $out)
            Mock 'Copy-LambdaPSModuleSet' -MockWith { }
            Mock 'Save-LambdaPSMissingModule' -MockWith { [PSCustomObject]@{ Downloaded = @(); Pending = @() } }
            $spec = @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }
            $null = Compress-LambdaPSFunctionPackage -Path $dir -OutFile $out -HandlerScript 'handler.ps1' -IncludeModule $spec

            Should -Invoke 'Copy-LambdaPSModuleSet' -Times 1 -Exactly -ParameterFilter {
                ($Module.Name -contains 'AWS.Tools.S3') -and ($Module.Name -contains 'AWS.Tools.DynamoDBv2')
            }
        }
    }

    # The handler is found by its real path; the error still names it as the caller did.
    It 'names the handler as given, when its #Requires does not pin a version: <Case>' -ForEach @(
        @{ Case = 'a handler that is a link inside the source'; Name = 'req-link'; Handler = 'handler.ps1'; Link = $true; Skip = $IsWindows }
        @{ Case = 'a handler in a subfolder'; Name = 'req-sub'; Handler = 'lib/handler.ps1'; Link = $false; Skip = $false }
    ) {
        # Making a symbolic link needs privileges on Windows, so only the link case is skipped there.
        if ($Skip) { Set-ItResult -Skipped -Because 'a symbolic link needs privileges on Windows'; return }
        $dir = Join-Path $TestDrive $Name
        $null = New-Item -ItemType Directory -Path (Join-Path $dir 'impl'), (Join-Path $dir 'lib') -Force
        $script = if ($Link) { Join-Path $dir 'impl/real-impl.ps1' } else { Join-Path $dir $Handler }
        Set-Content -LiteralPath $script -Value '#Requires -Modules AWS.Tools.S3', 'param($LambdaInput, $LambdaContext)'
        if ($Link) { $null = New-Item -ItemType SymbolicLink -Path (Join-Path $dir $Handler) -Target 'impl/real-impl.ps1' }

        $err = { Compress-LambdaPSFunctionPackage -Path $dir -OutFile (Join-Path $TestDrive "$Name.zip") -HandlerScript $Handler } |
            Should -Throw -ErrorId 'ModuleVersionRequired,Compress-LambdaPSFunctionPackage' -PassThru
        # As the caller named it, so a subfolder handler keeps its folder, as Build-LambdaPSRuntimePackage names it.
        $err.Exception.Message | Should -BeLike "*declared with #Requires -Modules in '$Handler'*"
    }

    # PowerShell's own modules are part of every pwsh, so they need no pin and are not staged.
    It 'packages a handler whose #Requires names one of PowerShell''s own modules without pinning it' -ForEach @(
        @{ Name = 'req-builtin'; Requires = 'Microsoft.PowerShell.Utility'; Vendored = $false }
    ) {
        $dir = Join-Path $TestDrive $Name
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $dir 'handler.ps1') -Value "#Requires -Modules $Requires", 'param($LambdaInput, $LambdaContext)'
        if ($Vendored) {
            $null = New-FakeModule -Root (Join-Path $dir 'modules') -Name 'LambdaPSFake.Vendored' -Version '1.2.0'
        }

        $result = Compress-LambdaPSFunctionPackage -Path $dir -OutFile (Join-Path $TestDrive "$Name.zip") -MissingModuleAction Stop

        @($result.ModulesStaged | ForEach-Object ToString) | Should -Be @($(if ($Vendored) { 'LambdaPSFake.Vendored 1.2.0' }))
    }

    # A module the source keeps in modules/ goes through the pin check and the dependency walk like any other:
    # a pinned one is staged with its installed dependencies, and a bare one gets the pin error.
    It 'gives the pin error for a bare #Requires on a module the source keeps in modules/, with no dependencies' {
        $dir = Join-Path $TestDrive 'req-vendored'
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $dir 'handler.ps1') -Value '#Requires -Modules LambdaPSFake.Vendored', 'param($LambdaInput, $LambdaContext)'
        $null = New-FakeModule -Root (Join-Path $dir 'modules') -Name 'LambdaPSFake.Vendored' -Version '1.2.0'

        { Compress-LambdaPSFunctionPackage -Path $dir -OutFile (Join-Path $TestDrive 'req-vendored.zip') -MissingModuleAction Stop } |
            Should -Throw -ErrorId 'ModuleVersionRequired,Compress-LambdaPSFunctionPackage'
    }

    Context 'a shipped module that has dependencies' {
        BeforeAll {
            $script:installed = Join-Path $TestDrive 'deps-installed'
            $null = New-FakeModule -Root $script:installed -Name 'LambdaPSFake.Top' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.Dep'; RequiredVersion = '1.0.0' })"
            $null = New-FakeModule -Root $script:installed -Name 'LambdaPSFake.Dep' -Version '1.0.0'
            $script:newTopSource = {
                param([string]$Name, [string]$Requires)
                $dir = Join-Path $TestDrive $Name
                New-Item -ItemType Directory -Path $dir -Force | Out-Null
                Set-Content -LiteralPath (Join-Path $dir 'handler.ps1') -Value "#Requires -Modules $Requires", 'param($LambdaInput, $LambdaContext)'
                $null = New-FakeModule -Root (Join-Path $dir 'modules') -Name 'LambdaPSFake.Top' -Version '1.0.0' -Requires "@(@{ ModuleName = 'LambdaPSFake.Dep'; RequiredVersion = '1.0.0' })"
                $dir
            }
        }

        It 'stages an installed module pinned by #Requires, with its dependency' {
            $dir = Join-Path $TestDrive 'req-installed'
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $dir 'handler.ps1') -Value "#Requires -Modules @{ ModuleName = 'LambdaPSFake.Top'; RequiredVersion = '1.0.0' }", 'param($LambdaInput, $LambdaContext)'
            $out = Join-Path $TestDrive 'req-installed.zip'
            $savedPath = $env:PSModulePath
            try {
                $env:PSModulePath = $script:installed + [System.IO.Path]::PathSeparator + $env:PSModulePath
                $result = Compress-LambdaPSFunctionPackage -Path $dir -OutFile $out -MissingModuleAction Stop
            }
            finally { $env:PSModulePath = $savedPath }

            @($result.ModulesStaged | ForEach-Object ToString) | Should -Be @('LambdaPSFake.Dep 1.0.0', 'LambdaPSFake.Top 1.0.0')
            $entries = Get-ZipEntryNames -ZipPath $out
            $entries | Should -Contain 'modules/LambdaPSFake.Top/1.0.0/LambdaPSFake.Top.psd1'
        }

        It 'stages a module named by both -IncludeModule and #Requires once' {
            $dir = Join-Path $TestDrive 'req-both'
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $dir 'handler.ps1') -Value "#Requires -Modules @{ ModuleName = 'LambdaPSFake.Top'; RequiredVersion = '1.0.0' }", 'param($LambdaInput, $LambdaContext)'
            $out = Join-Path $TestDrive 'req-both.zip'
            $savedPath = $env:PSModulePath
            try {
                $env:PSModulePath = $script:installed + [System.IO.Path]::PathSeparator + $env:PSModulePath
                $result = Compress-LambdaPSFunctionPackage -Path $dir -OutFile $out -IncludeModule @{ ModuleName = 'LambdaPSFake.Top'; RequiredVersion = '1.0.0' } -MissingModuleAction Stop
            }
            finally { $env:PSModulePath = $savedPath }

            @($result.ModulesStaged | ForEach-Object ToString) | Should -Be @('LambdaPSFake.Dep 1.0.0', 'LambdaPSFake.Top 1.0.0')
        }

        # The message lists the versions in the order the requests arrive, so it is checked for both, not an order.
        It 'stops with ModuleVersionConflict when -IncludeModule and #Requires pin different versions' {
            $dir = Join-Path $TestDrive 'req-conflict'
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $dir 'handler.ps1') -Value "#Requires -Modules @{ ModuleName = 'LambdaPSFake.Top'; RequiredVersion = '1.0.0' }", 'param($LambdaInput, $LambdaContext)'
            $second = Join-Path $TestDrive 'deps-installed-2'
            $null = New-FakeModule -Root $second -Name 'LambdaPSFake.Top' -Version '2.0.0'
            $out = Join-Path $TestDrive 'req-conflict.zip'
            $savedPath = $env:PSModulePath
            try {
                $env:PSModulePath = $script:installed + [System.IO.Path]::PathSeparator + $second + [System.IO.Path]::PathSeparator + $env:PSModulePath
                $err = { Compress-LambdaPSFunctionPackage -Path $dir -OutFile $out -IncludeModule @{ ModuleName = 'LambdaPSFake.Top'; RequiredVersion = '2.0.0' } } |
                    Should -Throw -ErrorId 'ModuleVersionConflict,Compress-LambdaPSFunctionPackage' -PassThru
            }
            finally { $env:PSModulePath = $savedPath }

            $err.Exception.Message | Should -BeLike "*'LambdaPSFake.Top'*"
            $err.Exception.Message | Should -Match ([regex]::Escape('1.0.0'))
            $err.Exception.Message | Should -Match ([regex]::Escape('2.0.0'))
            # Each version is named with what asked for it (here both are requests, in either order).
            $err.Exception.Message | Should -Match '1\.0\.0 \(requested\)'
            $err.Exception.Message | Should -Match '2\.0\.0 \(requested\)'
            Test-Path -LiteralPath $out | Should -BeFalse
        }

        It 'harvests the #Requires of the -HandlerScript among two root scripts' {
            $dir = Join-Path $TestDrive 'req-chosen'
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $dir 'a.ps1') -Value "#Requires -Modules @{ ModuleName = 'LambdaPSNoSuchModule'; RequiredVersion = '1.0.0' }", 'param($LambdaInput, $LambdaContext)'
            Set-Content -LiteralPath (Join-Path $dir 'b.ps1') -Value "#Requires -Modules @{ ModuleName = 'LambdaPSFake.Top'; RequiredVersion = '1.0.0' }", 'param($LambdaInput, $LambdaContext)'
            $out = Join-Path $TestDrive 'req-chosen.zip'
            $savedPath = $env:PSModulePath
            try {
                $env:PSModulePath = $script:installed + [System.IO.Path]::PathSeparator + $env:PSModulePath
                $result = Compress-LambdaPSFunctionPackage -Path $dir -OutFile $out -HandlerScript 'b.ps1' -MissingModuleAction Stop
            }
            finally { $env:PSModulePath = $savedPath }

            @($result.ModulesStaged | ForEach-Object ToString) | Should -Contain 'LambdaPSFake.Top 1.0.0'
        }

        It 'stops with DependencyVersionUnpinned for a dependency declared without a version' {
            $loose = Join-Path $TestDrive 'deps-installed-loose'
            $null = New-FakeModule -Root $loose -Name 'LambdaPSFake.Loose' -Version '1.0.0' -Requires "@('LambdaPSFake.Dep')"
            $src = Join-Path $TestDrive 'req-loose'
            New-Item -ItemType Directory -Path $src -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $src 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            $out = Join-Path $TestDrive 'req-loose.zip'
            $savedPath = $env:PSModulePath
            try {
                $env:PSModulePath = $loose + [System.IO.Path]::PathSeparator + $script:installed + [System.IO.Path]::PathSeparator + $env:PSModulePath
                { Compress-LambdaPSFunctionPackage -Path $src -OutFile $out -IncludeModule @{ ModuleName = 'LambdaPSFake.Loose'; RequiredVersion = '1.0.0' } -MissingModuleAction Stop } |
                    Should -Throw -ErrorId 'DependencyVersionUnpinned,Compress-LambdaPSFunctionPackage'
            }
            finally { $env:PSModulePath = $savedPath }
            Test-Path -LiteralPath $out | Should -BeFalse
        }

        It 'stages its installed dependency with it, so the handler''s #Requires is met from the package' {
            $dir = & $script:newTopSource 'deps-ok' "@{ ModuleName = 'LambdaPSFake.Top'; RequiredVersion = '1.0.0' }"
            $out = Join-Path $TestDrive 'deps-ok.zip'
            $savedPath = $env:PSModulePath
            try {
                $env:PSModulePath = $script:installed + [System.IO.Path]::PathSeparator + $env:PSModulePath
                $result = Compress-LambdaPSFunctionPackage -Path $dir -OutFile $out -MissingModuleAction Stop
            }
            finally { $env:PSModulePath = $savedPath }

            @($result.ModulesStaged | ForEach-Object ToString) | Should -Be @('LambdaPSFake.Dep 1.0.0', 'LambdaPSFake.Top 1.0.0')
            # The handler loads with only the package's modules/ on the module path, as on Lambda.
            $extracted = Join-Path $TestDrive 'deps-ok-extracted'
            Expand-Archive -LiteralPath $out -DestinationPath $extracted
            $probe = "`$env:PSModulePath = '$(Join-Path $extracted 'modules')'; & '$(Join-Path $extracted 'handler.ps1')' `$null `$null; 'loaded'"
            & ([System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName) -NoProfile -NonInteractive -Command $probe 2>&1 | Select-Object -Last 1 | Should -Be 'loaded'
        }

        It 'refuses it under -MissingModuleAction Stop when its dependency is not installed' {
            $dir = & $script:newTopSource 'deps-missing' "@{ ModuleName = 'LambdaPSFake.Top'; RequiredVersion = '1.0.0' }"

            { Compress-LambdaPSFunctionPackage -Path $dir -OutFile (Join-Path $TestDrive 'deps-missing.zip') -MissingModuleAction Stop } |
                Should -Throw -ErrorId 'ModuleNotFoundLocally,Compress-LambdaPSFunctionPackage'
        }

        It 'gives the pin error for a bare #Requires on it' {
            $dir = & $script:newTopSource 'deps-bare' 'LambdaPSFake.Top'

            { Compress-LambdaPSFunctionPackage -Path $dir -OutFile (Join-Path $TestDrive 'deps-bare.zip') -MissingModuleAction Stop } |
                Should -Throw -ErrorId 'ModuleVersionRequired,Compress-LambdaPSFunctionPackage'
        }
    }

    # The source's modules/ folder is copied into the package and the #Requires copy is staged over it.
    Context 'a #Requires module the source also keeps in modules/' {
        BeforeAll {
            $script:vendoredSource = Join-Path $TestDrive 'vendored'
            $null = New-FakeModule -Root (Join-Path $script:vendoredSource 'modules') -Name 'LambdaPSFake.Vendored' -Version '1.2.0'
            Set-Content -LiteralPath (Join-Path $script:vendoredSource 'handler.ps1') -Value "#Requires -Modules @{ ModuleName = 'LambdaPSFake.Vendored'; RequiredVersion = '1.2.0' }", 'param($LambdaInput, $LambdaContext)'
        }

        It 'names the folder in ModuleNotFoundLocally under -MissingModuleAction Stop' {
            $err = $null
            try { $null = Compress-LambdaPSFunctionPackage -Path $script:vendoredSource -OutFile (Join-Path $TestDrive 'vendored-stop.zip') -MissingModuleAction Stop -WarningAction SilentlyContinue }
            catch { $err = $_ }
            $err.FullyQualifiedErrorId | Should -Be 'ModuleNotFoundLocally,Compress-LambdaPSFunctionPackage'
            $err.Exception.Message | Should -BeLike "*'LambdaPSFake.Vendored' 1.2.0 is in '*vendored*modules*LambdaPSFake.Vendored', which is packaged as it is but not searched*"
        }
    }

    It 'names the handler script when its #Requires does not pin a version' {
        $dir = Join-Path $TestDrive 'req-unpinned'
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $dir 'handler.ps1') -Value '#Requires -Modules AWS.Tools.S3', 'param($LambdaInput, $LambdaContext)'

        $err = { Compress-LambdaPSFunctionPackage -Path $dir -OutFile (Join-Path $TestDrive 'req-unpinned.zip') } |
            Should -Throw -ErrorId 'ModuleVersionRequired,Compress-LambdaPSFunctionPackage' -PassThru
        $err.Exception.Message | Should -BeLike "*declared with #Requires -Modules in 'handler.ps1'*#Requires -Modules @{ ModuleName = 'AWS.Tools.S3'*"
        Join-Path $TestDrive 'req-unpinned.zip' | Should -Not -Exist
    }

    # No handler can be told apart, so no script's #Requires is read: none of them names the entry point.
    # A Module:: handler names no script, so this packages, with a warning naming -HandlerScript.
    It 'harvests no #Requires, and warns, when two root scripts leave the handler ambiguous' {
        $dir = Join-Path $TestDrive 'req-ambiguous'
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        foreach ($name in 'a.ps1', 'b.ps1') {
            Set-Content -LiteralPath (Join-Path $dir $name) -Value "#Requires -Modules @{ ModuleName = 'LambdaPSNoSuchModule'; RequiredVersion = '1.0.0' }", 'param($LambdaInput, $LambdaContext)'
        }
        $out = Join-Path $TestDrive 'req-ambiguous.zip'

        $result = Compress-LambdaPSFunctionPackage -Path $dir -OutFile $out -MissingModuleAction Stop -WarningVariable warnings -WarningAction SilentlyContinue

        $entries = Get-ZipEntryNames -ZipPath $out
        $entries | Sort-Object | Should -Be @('a.ps1', 'b.ps1')
        @($result.ModulesStaged).Count | Should -Be 0
        @($warnings).Count | Should -Be 1
        "$($warnings[0])" | Should -BeLike '*a.ps1, b.ps1*-HandlerScript*'
    }

    It 'gives no such warning when no root script declares #Requires -Modules' {
        $dir = Join-Path $TestDrive 'plain-ambiguous'
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        foreach ($name in 'a.ps1', 'b.ps1') { Set-Content -LiteralPath (Join-Path $dir $name) -Value 'param($LambdaInput, $LambdaContext)' }

        $null = Compress-LambdaPSFunctionPackage -Path $dir -OutFile (Join-Path $TestDrive 'plain-ambiguous.zip') -WarningVariable warnings -WarningAction SilentlyContinue

        $warnings | Should -BeNullOrEmpty
    }
}

# ModulesStaged is read from the staged tree, so it reflects everything that ships: modules staged
# from the input lists (including dependencies) AND modules already in the source's own modules/.
Describe 'Compress-LambdaPSFunctionPackage ModulesStaged' -Tag 'Unit' {
    It 'reports staged modules, their dependencies, and modules already in the source' {
        $dir = Join-Path $TestDrive 'ms'
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $dir 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
        # A module the customer dropped into modules/ by hand: it ships, so it must be reported.
        $own = Join-Path $dir 'modules/MyHelpers/1.2.0'
        New-Item -ItemType Directory -Path $own -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $own 'MyHelpers.psd1') -Value '@{ ModuleVersion = ''1.2.0'' }'
        $out = Join-Path $TestDrive 'ms.zip'

        InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' -Parameters @{ dir = $dir; out = $out } {
            param($dir, $out)
            Mock 'Save-LambdaPSMissingModule' -MockWith { [PSCustomObject]@{ Downloaded = @(); Pending = @() } }
            # Stand in for a real copy: S3 plus the dependency it pulls in, laid out as on disk.
            Mock 'Copy-LambdaPSModuleSet' -MockWith {
                foreach ($m in @(@('AWS.Tools.S3', '5.0.103'), @('AWS.Tools.Common', '5.0.103'))) {
                    $d = [System.IO.Path]::Combine($Path, $m[0], $m[1])
                    $null = New-Item -ItemType Directory -Path $d -Force
                    Set-Content -LiteralPath ([System.IO.Path]::Combine($d, "$($m[0]).psd1")) -Value '@{}'
                }
                , [string[]]@('AWS.Tools.S3 5.0.103', 'AWS.Tools.Common 5.0.103')
            }

            $result = Compress-LambdaPSFunctionPackage -Path $dir -OutFile $out -IncludeModule @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }

            @($result.ModulesStaged | ForEach-Object ToString) | Should -Be @('AWS.Tools.Common 5.0.103', 'AWS.Tools.S3 5.0.103', 'MyHelpers 1.2.0')
        }
    }

    It 'reports an empty ModulesStaged for a package with no modules' {
        $dir = Join-Path $TestDrive 'ms-none'
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $dir 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'

        $result = Compress-LambdaPSFunctionPackage -Path $dir -OutFile (Join-Path $TestDrive 'ms-none.zip')
        @($result.ModulesStaged).Count | Should -Be 0
    }
}

Describe 'Compress-LambdaPSFunctionPackage -MissingModuleAction' -Tag 'Unit' {
    BeforeEach {
        $script:maDir = Join-Path $TestDrive "ma-$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $script:maDir -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $script:maDir 'handler.ps1') -Value @'
#Requires -Modules @{ ModuleName = 'AWS.Tools.DynamoDBv2'; RequiredVersion = '5.0.103' }
param($LambdaInput, $LambdaContext)
'@
    }

    It 'downloads the combined set (Save by default) and stages from the download folder' {
        InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' -Parameters @{ dir = $script:maDir; out = (Join-Path $TestDrive 'ma-save.zip') } {
            param($dir, $out)
            Mock 'Save-LambdaPSMissingModule' -MockWith { [PSCustomObject]@{ Downloaded = @(); Pending = @() } }
            Mock 'Copy-LambdaPSModuleSet' -MockWith { }

            $null = Compress-LambdaPSFunctionPackage -Path $dir -OutFile $out -IncludeModule @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }

            Should -Invoke 'Save-LambdaPSMissingModule' -Times 1 -Exactly -ParameterFilter {
                $MissingModuleAction -eq 'Save' -and ($Module.Name -contains 'AWS.Tools.S3') -and ($Module.Name -contains 'AWS.Tools.DynamoDBv2')
            }
            Should -Invoke 'Copy-LambdaPSModuleSet' -Times 1 -Exactly -ParameterFilter { $SearchPath -like '*lambdaps-modules-*' }
        }
    }

    It 'names the repositories to download from when -ModuleRepository is given' {
        InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' -Parameters @{ dir = $script:maDir; out = (Join-Path $TestDrive 'ma-repo.zip') } {
            param($dir, $out)
            Mock 'Save-LambdaPSMissingModule' -MockWith { [PSCustomObject]@{ Downloaded = @(); Pending = @() } }
            Mock 'Copy-LambdaPSModuleSet' -MockWith { }

            $null = Compress-LambdaPSFunctionPackage -Path $dir -OutFile $out -ModuleRepository 'Internal', 'PSGallery' `
                -IncludeModule @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' }

            Should -Invoke 'Save-LambdaPSMissingModule' -Times 1 -Exactly -ParameterFilter { "$ModuleRepository" -eq 'Internal PSGallery' }
        }
    }

    It 'never downloads under Stop' {
        InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' -Parameters @{ dir = $script:maDir; out = (Join-Path $TestDrive 'ma-stop.zip') } {
            param($dir, $out)
            Mock 'Save-LambdaPSMissingModule' -MockWith { throw 'must not be called' }
            Mock 'Copy-LambdaPSModuleSet' -MockWith { }

            $null = Compress-LambdaPSFunctionPackage -Path $dir -OutFile $out -MissingModuleAction Stop

            Should -Invoke 'Save-LambdaPSMissingModule' -Times 0 -Exactly
            Should -Invoke 'Copy-LambdaPSModuleSet' -Times 1 -Exactly
        }
    }

    It 'deletes the download folder even when staging fails' {
        InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' -Parameters @{ dir = $script:maDir; out = (Join-Path $TestDrive 'ma-fail.zip') } {
            param($dir, $out)
            Mock 'Save-LambdaPSMissingModule' -MockWith {
                $null = New-Item -ItemType Directory -Path $Path -Force
                $script:maRoot = $Path
                [PSCustomObject]@{ Downloaded = @(); Pending = @() }
            }
            Mock 'Copy-LambdaPSModuleSet' -MockWith { throw 'copy failed' }

            { Compress-LambdaPSFunctionPackage -Path $dir -OutFile $out } | Should -Throw '*copy failed*'
            Test-Path -LiteralPath $script:maRoot | Should -BeFalse
        }
    }
}

Describe 'Compress-LambdaPSFunctionPackage -HandlerScript help' -Tag 'Unit' {
    It 'says a backslash fails off Windows with InvalidHandlerPath, and to use /' {
        $text = (Get-Help Compress-LambdaPSFunctionPackage -Parameter HandlerScript).Description.Text -join ' '
        $text | Should -Match 'InvalidHandlerPath'
        $text | Should -Not -Match "read as '/'"
    }
}
