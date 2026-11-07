# InModuleScope wraps the tests at discovery, so the module is imported then too.
BeforeDiscovery {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' {
    Describe 'Assert-LambdaPSOciBaseImageReservedPath' -Tag 'Unit' {
        BeforeAll {
            # Writes a gzipped layer tar; names ending in / are folders.
            $script:newLayer = {
                param([string]$Name, [string[]]$Entries)
                $path = [System.IO.Path]::Combine($TestDrive, "$Name.tar.gz")
                $file = [System.IO.File]::Create($path)
                $gzip = [System.IO.Compression.GZipStream]::new($file, [System.IO.Compression.CompressionLevel]::Fastest)
                $writer = [System.Formats.Tar.TarWriter]::new($gzip)
                foreach ($entryName in $Entries) {
                    $type = if ($entryName.EndsWith('/')) { [System.Formats.Tar.TarEntryType]::Directory } else { [System.Formats.Tar.TarEntryType]::RegularFile }
                    $writer.WriteEntry([System.Formats.Tar.PaxTarEntry]::new($type, "./$entryName"))
                }
                $writer.Dispose()
                $path
            }
            $script:plan = [PSCustomObject]@{
                PSTypeName = 'AWS.ToolExtras.LambdaPSRuntime.OciImagePlan'
                Target     = 'CMR'
                BaseImage  = 'registry.test/lambda/provided:al2023'
            }
            $script:useLayers = {
                param([string[]]$Paths)
                $script:layers = $Paths
                Mock Invoke-WebRequest {
                    [PSCustomObject]@{ StatusCode = 401; Headers = @{ 'WWW-Authenticate' = 'Bearer realm="https://registry.test/token",service="registry.test"' } }
                } -ParameterFilter { $Method -eq 'Head' }
                $script:digests = @($Paths | ForEach-Object { "sha256:$((Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant())" })
                Mock Invoke-WebRequest {
                    Copy-Item -LiteralPath $script:layers[[array]::IndexOf($script:digests, ($Uri -replace '.*/', ''))] -Destination $OutFile
                } -ParameterFilter { $OutFile }
                Mock Invoke-RestMethod { [PSCustomObject]@{ token = 'anonymous' } } -ParameterFilter { $Uri -like 'https://registry.test/token?*' }
                Mock Invoke-RestMethod {
                    [PSCustomObject]@{ layers = @($script:digests | ForEach-Object { [PSCustomObject]@{ digest = $_ } }) }
                } -ParameterFilter { $Uri -ceq 'https://registry.test/v2/lambda/provided/manifests/sha256:base' -and $Headers.Authorization -ceq 'Bearer anonymous' }
            }
        }

        It 'stops with ReservedPathNotEmpty on the files the layers leave in /opt and /var/task, after whiteouts' {
            $first = & $script:newLayer 'first' @('opt/', 'opt/a/', 'opt/a/x', 'tmp/', 'tmp/t1', 'var/task/', 'var/task/f', 'usr/opt/q')
            $second = & $script:newLayer 'second' @('tmp/.wh.t1', 'var/task/.wh..wh..opq', 'var/task/g', 'opt/c')
            & $script:useLayers @($first, $second)

            { Assert-LambdaPSOciBaseImageReservedPath -Plan $script:plan -ManifestDigest 'sha256:base' } |
                Should -Throw -ErrorId 'ReservedPathNotEmpty' -ExpectedMessage "*has files in /opt (2 entries): a c; /var/task (1 entries): g.*"
            Should -Invoke Invoke-RestMethod -ParameterFilter { $Uri -like 'https://registry.test/token?*scope=repository%3Alambda%2Fprovided%3Apull' } -Times 1 -Exactly
        }

        It 'passes a base image whose /opt, /var/task and /tmp are empty folders' {
            & $script:useLayers @(& $script:newLayer 'empty' @('opt/', 'tmp/', 'var/', 'var/task/', 'var/runtime/bootstrap'))

            Assert-LambdaPSOciBaseImageReservedPath -Plan $script:plan -ManifestDigest 'sha256:base' -WarningVariable warnings -WarningAction SilentlyContinue

            $warnings | Should -BeNullOrEmpty
        }

        It 'warns and returns when <Case>' -ForEach @(
            @{ Case = 'the registry cannot be reached'; Reason = 'no route to host' }
            @{ Case = 'a layer does not match its digest'; Reason = 'does not match its digest.' }
        ) {
            if ($Reason -eq 'no route to host') { Mock Invoke-WebRequest { throw 'no route to host' } }
            else {
                & $script:useLayers @(& $script:newLayer 'tampered' @('opt/x'))
                $script:digests = @('sha256:' + '0' * 64)
            }

            Assert-LambdaPSOciBaseImageReservedPath -Plan $script:plan -ManifestDigest 'sha256:base' -WarningVariable warnings -WarningAction SilentlyContinue

            $warnings | Should -BeLike "Could not list /opt, /var/task and /tmp in base image 'registry.test/lambda/provided:al2023'*$Reason"
        }

        It 'does not check a Container function image' {
            Mock Invoke-WebRequest { }
            $containerPlan = [PSCustomObject]@{ PSTypeName = 'AWS.ToolExtras.LambdaPSRuntime.OciImagePlan'; Target = 'Container'; BaseImage = $script:plan.BaseImage }

            Assert-LambdaPSOciBaseImageReservedPath -Plan $containerPlan -ManifestDigest 'sha256:base'

            Should -Not -Invoke Invoke-WebRequest
        }
    }
}
