BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Resolve-LambdaPSPath' -Tag 'Unit' {
    It 'resolves <Case> against the PowerShell location' -ForEach @(
        @{ Case = 'a relative path'; Path = 'sub/out.zip'; Expected = 'sub/out.zip' }
        @{ Case = 'a path that does not exist'; Path = 'missing/x'; Expected = 'missing/x' }
        @{ Case = 'a FileSystem:: path'; Path = 'FileSystem::{0}/fs'; Expected = 'fs' }
        @{ Case = 'a path on a FileSystem drive'; Path = 'LambdaPSWork:/drive'; Expected = 'drive' }
    ) {
        $null = New-PSDrive -Name LambdaPSWork -PSProvider FileSystem -Root $TestDrive -Scope Global
        Push-Location -LiteralPath $TestDrive
        try {
            InModuleScope $script:moduleName -Parameters @{ Path = ($Path -f $TestDrive) } {
                param($Path)
                Resolve-LambdaPSPath -Path $Path
            } | Should -Be ([System.IO.Path]::GetFullPath([System.IO.Path]::Combine($TestDrive, $Expected)))
        }
        finally {
            Pop-Location
            Remove-PSDrive -Name LambdaPSWork -Scope Global
        }
    }

    # On Linux and macOS 'a:b' is a file name, but PowerShell reads its first segment as a drive.
    It 'resolves ./a:b/credentials, a file name holding a colon, against the PowerShell location' -Skip:$IsWindows {
        Push-Location -LiteralPath $TestDrive
        try {
            InModuleScope $script:moduleName { Resolve-LambdaPSPath -Path './a:b/credentials' } |
                Should -Be ([System.IO.Path]::Combine("$TestDrive", 'a:b', 'credentials'))
        }
        finally {
            Pop-Location
        }
    }

    It 'throws DriveNotFound for <Path>, whose drive does not exist' -ForEach @(
        @{ Path = 'a:b/credentials' }
        @{ Path = 'TestDriv:x' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Path = $Path } {
            param($Path)
            $err = $null
            try { $null = Resolve-LambdaPSPath -Path $Path } catch { $err = $_ }
            $err.FullyQualifiedErrorId | Should -Be 'DriveNotFound'
            if (-not $IsWindows) { $err.Exception.Message | Should -BeLike "*write './$Path'*" }
        }
    }

    It 'throws PathNotFileSystem for a path on the <Provider> provider' -ForEach @(
        @{ Provider = 'Environment'; Path = 'Env:\HOME' }
        @{ Provider = 'Variable'; Path = 'Variable:/x/../../etc' }
        @{ Provider = 'Function'; Path = 'Function:\prompt' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Path = $Path } {
            param($Path)
            { Resolve-LambdaPSPath -Path $Path } | Should -Throw -ErrorId 'PathNotFileSystem'
        }
    }

    # The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
    # surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
    It 'surfaces <ErrorId> from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'DriveNotFound'; Path = 'TestDriv:x' }
        @{ ErrorId = 'PathNotFileSystem'; Path = 'Env:\HOME' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ ErrorId = $ErrorId; Path = $Path } {
            param($ErrorId, $Path)
            function Invoke-LambdaPSTestCaller {
                [CmdletBinding()]
                param([scriptblock]$Call)
                try { $null = & $Call }
                catch { $PSCmdlet.ThrowTerminatingError($_) }
            }
            $collected = $null
            $err = $null
            try { Invoke-LambdaPSTestCaller -Call { Resolve-LambdaPSPath -Path $Path } -ErrorVariable collected } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be "$ErrorId,Invoke-LambdaPSTestCaller"
            $ids = @($collected | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.FullyQualifiedErrorId } else { $_.ErrorRecord.FullyQualifiedErrorId } })
            $ids | Should -Contain "$ErrorId,Invoke-LambdaPSTestCaller"
            @($ids | Where-Object { $_ -like '*,Resolve-LambdaPSPath' }) | Should -BeNullOrEmpty
        }
    }
}
