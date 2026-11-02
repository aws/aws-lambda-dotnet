BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSFunctionManifest' -Tag 'Unit' {
    It 'reads ScriptFile, RuntimeAlias and Architecture from a scaffolded project' {
        $dir = [System.IO.Path]::Combine($TestDrive, 'scaffolded')
        $null = New-LambdaPSFunctionProject -Path $dir -Handler 'fn' -RuntimeAlias 'beta' -Architecture 'arm64'
        InModuleScope $script:moduleName -Parameters @{ Dir = $dir } {
            param($Dir)
            $manifest = Get-LambdaPSFunctionManifest -Path $Dir
            $manifest.ScriptFile | Should -Be 'fn.ps1'
            $manifest.RuntimeAlias | Should -Be 'beta'
            $manifest.Architecture | Should -Be 'arm64'
        }
    }

    # Push-Location moves the PowerShell location but not the process working directory.
    It 'reads a project given relative to the current PowerShell location' {
        $dir = [System.IO.Path]::Combine($TestDrive, 'relative-fn')
        $null = New-LambdaPSFunctionProject -Path $dir -Handler 'fn'
        Push-Location -LiteralPath $TestDrive
        try {
            InModuleScope $script:moduleName {
                (Get-LambdaPSFunctionManifest -Path 'relative-fn').ScriptFile | Should -Be 'fn.ps1'
            }
        }
        finally { Pop-Location }
    }

    It 'returns $null for <Case>' -ForEach @(
        @{ Case = 'a manifest.json of the customer''s own'; Content = '{"settings":{"retries":3},"ScriptFile":"x.ps1"}' }
        @{ Case = 'a JSON array'; Content = '[1,2]' }
    ) {
        $dir = [System.IO.Path]::Combine($TestDrive, [guid]::NewGuid().ToString('n'))
        $null = New-Item -ItemType Directory -Path $dir -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'manifest.json')) -Value $Content
        InModuleScope $script:moduleName -Parameters @{ Dir = $dir } {
            param($Dir)
            Get-LambdaPSFunctionManifest -Path $Dir | Should -BeNullOrEmpty
        }
    }

    It 'throws InvalidProjectManifest, naming the file, for a manifest.json that is not valid JSON' {
        $dir = [System.IO.Path]::Combine($TestDrive, 'invalid-json')
        $null = New-Item -ItemType Directory -Path $dir -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'manifest.json')) -Value '{ not json'
        InModuleScope $script:moduleName -Parameters @{ Dir = $dir } {
            param($Dir)
            { Get-LambdaPSFunctionManifest -Path $Dir } |
                Should -Throw -ErrorId 'InvalidProjectManifest' -ExpectedMessage '*manifest.json*not valid JSON*'
        }
    }

    It 'returns $null for a folder with no manifest.json, and for a file' {
        $file = [System.IO.Path]::Combine($TestDrive, 'lone.ps1')
        Set-Content -LiteralPath $file -Value '# handler'
        InModuleScope $script:moduleName -Parameters @{ Dir = $TestDrive; File = $file } {
            param($Dir, $File)
            Get-LambdaPSFunctionManifest -Path $Dir | Should -BeNullOrEmpty
            Get-LambdaPSFunctionManifest -Path $File | Should -BeNullOrEmpty
        }
    }
}

# The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
# surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
Describe 'Get-LambdaPSFunctionManifest raised through a calling cmdlet' -Tag 'Unit' {
    It 'surfaces <ErrorId> from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'InvalidProjectManifest' }
    ) {
        $dir = [System.IO.Path]::Combine($TestDrive, 'caller-invalid-json')
        $null = New-Item -ItemType Directory -Path $dir -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'manifest.json')) -Value '{ not json'
        $Call = { Get-LambdaPSFunctionManifest -Path $Dir }
        InModuleScope $script:moduleName -Parameters @{ ErrorId = $ErrorId; Call = $Call.ToString(); Dir = $dir } {
            param($ErrorId, $Call, $Dir)
            # Rebuilt here, so the call runs in the module and reaches its private helper.
            $Call = [scriptblock]::Create($Call)
            function Invoke-LambdaPSTestCaller {
                [CmdletBinding()]
                param([scriptblock]$Call)
                try { $null = & $Call }
                catch { $PSCmdlet.ThrowTerminatingError($_) }
            }
            $collected = $null
            $err = $null
            try { Invoke-LambdaPSTestCaller -Call $Call -ErrorVariable collected } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be "$ErrorId,Invoke-LambdaPSTestCaller"
            $ids = @($collected | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.FullyQualifiedErrorId } else { $_.ErrorRecord.FullyQualifiedErrorId } })
            $ids | Should -Contain "$ErrorId,Invoke-LambdaPSTestCaller"
            @($ids | Where-Object { $_ -like '*,Get-LambdaPSFunctionManifest' }) | Should -BeNullOrEmpty
        }
    }
}
