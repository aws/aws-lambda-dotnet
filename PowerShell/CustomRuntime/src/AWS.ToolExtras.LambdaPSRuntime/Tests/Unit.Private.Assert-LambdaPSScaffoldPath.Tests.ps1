# Pester evaluates -Skip at discovery, even inside a Context it then skips, so the root check runs
# here once: Windows has no 'id', and $IsWindows keeps it from being called there.
BeforeDiscovery {
    $script:runAsRoot = -not $IsWindows -and (id -u) -eq '0'
}

BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))

    # The helper runs 'id' by its resolved path, so another current user is simulated with a fake
    # 'id' executable that Get-Command is mocked to return: fake-id/<uid> prints <uid>.
    if (-not $IsWindows) {
        foreach ($uid in '4294967294', '0') {
            $fake = [System.IO.Path]::Combine($TestDrive, 'fake-id', $uid)
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($fake)) -Force
            [System.IO.File]::WriteAllText($fake, "#!/bin/sh`necho $uid`n")
            [System.IO.File]::SetUnixFileMode($fake, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute')
        }
    }
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Assert-LambdaPSScaffoldPath' -Tag 'Unit' {
    It 'returns nothing for a path that does not exist yet' {
        InModuleScope $script:moduleName -Parameters @{ Path = [System.IO.Path]::Combine($TestDrive, 'new') } {
            param($Path)
            (Assert-LambdaPSScaffoldPath -Path $Path).Count | Should -Be 0
        }
    }

    It 'accepts an existing empty directory the current user owns' {
        $dir = [System.IO.Path]::Combine($TestDrive, 'mine')
        $null = New-Item -ItemType Directory -Path $dir
        InModuleScope $script:moduleName -Parameters @{ Path = $dir } {
            param($Path)
            (Assert-LambdaPSScaffoldPath -Path $Path).Count | Should -Be 0
        }
    }

    # The owner is simulated by mocking the 'id -u' answer against a directory the test user owns.
    Context 'who may own it' -Skip:$IsWindows {
        It 'refuses a directory you own that every user can write to, naming its mode and the fix' -Skip:$script:runAsRoot {
            $dir = [System.IO.Path]::Combine($TestDrive, 'mine-shared-777')
            $null = New-Item -ItemType Directory -Path $dir
            [System.IO.File]::SetUnixFileMode($dir, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute, GroupRead, GroupWrite, GroupExecute, OtherRead, OtherWrite, OtherExecute')
            InModuleScope $script:moduleName -Parameters @{ Path = $dir } {
                param($Path)
                { Assert-LambdaPSScaffoldPath -Path $Path -Force } | Should -Throw -ErrorId 'TargetWritableByOthers' -ExpectedMessage "*has mode 777*chmod o-w '$Path'*"
            }
        }

        # Other users cannot replace your files in a sticky folder.
        It 'accepts a directory you own that every user can write to with the sticky bit (1777)' -Skip:$script:runAsRoot {
            $dir = [System.IO.Path]::Combine($TestDrive, 'mine-shared-1777')
            $null = New-Item -ItemType Directory -Path $dir
            [System.IO.File]::SetUnixFileMode($dir, [System.IO.UnixFileMode]'StickyBit, UserRead, UserWrite, UserExecute, GroupRead, GroupWrite, GroupExecute, OtherRead, OtherWrite, OtherExecute')
            InModuleScope $script:moduleName -Parameters @{ Path = $dir } {
                param($Path)
                (Assert-LambdaPSScaffoldPath -Path $Path).Count | Should -Be 0
            }
        }

        # A user-private group (umask 002) makes group-writable the default for your own folders.
        It 'accepts a directory you own that only your group can also write to' -Skip:$script:runAsRoot {
            $dir = [System.IO.Path]::Combine($TestDrive, 'mine-group')
            $null = New-Item -ItemType Directory -Path $dir
            [System.IO.File]::SetUnixFileMode($dir, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute, GroupRead, GroupWrite, GroupExecute, OtherRead, OtherExecute')
            InModuleScope $script:moduleName -Parameters @{ Path = $dir } {
                param($Path)
                (Assert-LambdaPSScaffoldPath -Path $Path).Count | Should -Be 0
            }
        }

        It 'checks the owner with the real id, not a session function named id' -Skip:$script:runAsRoot {
            InModuleScope $script:moduleName {
                Mock Get-Item { [PSCustomObject]@{ LinkTarget = $null; UnixStat = [PSCustomObject]@{ UserId = 4294967294 }; UnixFileMode = [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute'; User = 'other' } } -ParameterFilter { $LiteralPath -like '*foreign-shadow*' }
                $dir = [System.IO.Path]::Combine($TestDrive, 'foreign-shadow')
                $null = New-Item -ItemType Directory -Path $dir -Force
                function global:id { '0' }
                try { { Assert-LambdaPSScaffoldPath -Path $dir } | Should -Throw -ErrorId 'TargetOwnedByAnotherUser' }
                finally { Remove-Item -LiteralPath 'Function:\global:id' -ErrorAction SilentlyContinue }
            }
        }

        It 'refuses a directory root owns that others can write to (<Mode>), as another user' -ForEach @(
            @{ Mode = 'StickyBit, UserRead, UserWrite, UserExecute, GroupRead, GroupWrite, GroupExecute, OtherRead, OtherWrite, OtherExecute' }
            @{ Mode = 'UserRead, UserWrite, UserExecute, GroupRead, GroupWrite, GroupExecute' }
        ) {
            InModuleScope $script:moduleName -Parameters @{ Mode = $Mode } {
                param($Mode)
                Mock Get-Command { [PSCustomObject]@{ Source = [System.IO.Path]::Combine($TestDrive, 'fake-id', '4294967294') } } -ParameterFilter { $Name -eq 'id' }
                Mock Get-Item { [PSCustomObject]@{ LinkTarget = $null; UnixStat = [PSCustomObject]@{ UserId = 0 }; UnixFileMode = [System.IO.UnixFileMode]$Mode; User = 'root' } } -ParameterFilter { $LiteralPath -like '*root-shared*' }
                $dir = [System.IO.Path]::Combine($TestDrive, 'root-shared')
                $null = New-Item -ItemType Directory -Path $dir -Force
                { Assert-LambdaPSScaffoldPath -Path $dir -Force } | Should -Throw -ErrorId 'TargetOwnedByAnotherUser'
            }
        }

        It 'refuses the real /tmp (root-owned, 1777), as another user' -Skip:(-not (Test-Path -LiteralPath '/tmp') -or (Get-Item -LiteralPath '/tmp' -Force).UnixStat.UserId -ne 0) {
            InModuleScope $script:moduleName {
                Mock Get-Command { [PSCustomObject]@{ Source = [System.IO.Path]::Combine($TestDrive, 'fake-id', '4294967294') } } -ParameterFilter { $Name -eq 'id' }
                { Assert-LambdaPSScaffoldPath -Path '/tmp' -Force } | Should -Throw -ErrorId 'TargetOwnedByAnotherUser'
            }
        }

        It 'accepts a directory root owns that only root can write to, as another user' {
            InModuleScope $script:moduleName {
                Mock Get-Command { [PSCustomObject]@{ Source = [System.IO.Path]::Combine($TestDrive, 'fake-id', '4294967294') } } -ParameterFilter { $Name -eq 'id' }
                Mock Get-Item { [PSCustomObject]@{ LinkTarget = $null; UnixStat = [PSCustomObject]@{ UserId = 0 }; UnixFileMode = [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute, GroupRead, GroupExecute, OtherRead, OtherExecute'; User = 'root' } } -ParameterFilter { $LiteralPath -like '*root-owned*' }
                $dir = [System.IO.Path]::Combine($TestDrive, 'root-owned')
                $null = New-Item -ItemType Directory -Path $dir -Force
                (Assert-LambdaPSScaffoldPath -Path $dir).Count | Should -Be 0
            }
        }

        It 'accepts any owner when the caller is root' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'caller-root')
            $null = New-Item -ItemType Directory -Path $dir
            InModuleScope $script:moduleName -Parameters @{ Path = $dir } {
                param($Path)
                Mock Get-Command { [PSCustomObject]@{ Source = [System.IO.Path]::Combine($TestDrive, 'fake-id', '0') } } -ParameterFilter { $Name -eq 'id' }
                (Assert-LambdaPSScaffoldPath -Path $Path).Count | Should -Be 0
            }
        }

        It 'throws IdCommandNotFound when id is not on PATH' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'no-id')
            $null = New-Item -ItemType Directory -Path $dir
            InModuleScope $script:moduleName -Parameters @{ Path = $dir } {
                param($Path)
                Mock Get-Command { } -ParameterFilter { $Name -eq 'id' }
                { Assert-LambdaPSScaffoldPath -Path $Path } | Should -Throw -ErrorId 'IdCommandNotFound'
            }
        }
    }

    # Without the listing, a folder you can enter but not read would look empty, and the scaffold would
    # write over what is in it.
    It 'refuses a folder it cannot list with TargetNotReadable, <Case>' -Skip:($IsWindows -or $script:runAsRoot) -ForEach @(
        @{ Case = 'without -Force'; Force = $false }, @{ Case = 'even with -Force'; Force = $true }
    ) {
        $dir = [System.IO.Path]::Combine($TestDrive, "unlistable-$Force")
        $null = New-Item -ItemType Directory -Path $dir
        Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'README.md')) -Value 'keep me'
        [System.IO.File]::SetUnixFileMode($dir, [System.IO.UnixFileMode]'UserWrite, UserExecute')
        try {
            InModuleScope $script:moduleName -Parameters @{ Path = $dir; Force = $Force } {
                param($Path, $Force)
                { Assert-LambdaPSScaffoldPath -Path $Path -Force:$Force } |
                    Should -Throw -ErrorId 'TargetNotReadable' -ExpectedMessage '*cannot be listed*'
            }
        }
        finally { [System.IO.File]::SetUnixFileMode($dir, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute') }
    }

    It 'names a link to nothing as such, with TargetIsFile' -Skip:$IsWindows {
        $link = [System.IO.Path]::Combine($TestDrive, 'dangling')
        $null = New-Item -ItemType SymbolicLink -Path $link -Target ([System.IO.Path]::Combine($TestDrive, 'nothing-here'))
        InModuleScope $script:moduleName -Parameters @{ Path = $link } {
            param($Path)
            { Assert-LambdaPSScaffoldPath -Path $Path } | Should -Throw -ErrorId 'TargetIsFile' -ExpectedMessage '*is a link to*which does not exist*'
        }
    }

    # File.Exists follows the link, so a link to a file that exists is not called a link to nothing.
    It 'names a link to a file as such, with TargetIsFile' -Skip:$IsWindows {
        $file = [System.IO.Path]::Combine($TestDrive, 'real-file.txt')
        Set-Content -LiteralPath $file -Value 'x'
        $link = [System.IO.Path]::Combine($TestDrive, 'link-to-file')
        $null = New-Item -ItemType SymbolicLink -Path $link -Target $file
        InModuleScope $script:moduleName -Parameters @{ Path = $link; File = $file } {
            param($Path, $File)
            { Assert-LambdaPSScaffoldPath -Path $Path -Force } | Should -Throw -ErrorId 'TargetIsFile' -ExpectedMessage "*is a link to the file '$File', not a directory*"
        }
    }

    # Another user's directory is simulated by reporting a different current user ID.
    Context 'a directory another user owns' -Skip:$IsWindows {
        It 'refuses it with TargetOwnedByAnotherUser, <Case>' -ForEach @(
            @{ Case = 'even when it is empty'; Force = $false; Link = $false }
            @{ Case = 'even with -Force'; Force = $true; Link = $false }
            @{ Case = 'through a link to it'; Force = $false; Link = $true }
        ) {
            $dir = [System.IO.Path]::Combine($TestDrive, "foreign-$($Case -replace '\W', '-')")
            $null = New-Item -ItemType Directory -Path $dir
            $path = $dir
            if ($Link) {
                $path = "$dir-link"
                $null = New-Item -ItemType SymbolicLink -Path $path -Target $dir
            }
            InModuleScope $script:moduleName -Parameters @{ Path = $path; Force = $Force } {
                param($Path, $Force)
                Mock Get-Command { [PSCustomObject]@{ Source = [System.IO.Path]::Combine($TestDrive, 'fake-id', '4294967294') } } -ParameterFilter { $Name -eq 'id' }
                { Assert-LambdaPSScaffoldPath -Path $Path -Force:$Force } |
                    Should -Throw -ErrorId 'TargetOwnedByAnotherUser' -ExpectedMessage '*owned by another user*'
            }
        }

        # The owner checked is the link's target's: a link you own does not make another user's
        # directory yours.
        It 'refuses a link the current user owns to a directory another user owns' -Skip:$script:runAsRoot {
            $dir = [System.IO.Path]::Combine($TestDrive, 'foreign-target')
            $link = "$dir-own-link"
            $null = New-Item -ItemType Directory -Path $dir
            $null = New-Item -ItemType SymbolicLink -Path $link -Target $dir
            InModuleScope $script:moduleName -Parameters @{ Dir = $dir; Link = $link; Me = [long](id -u) } {
                param($Dir, $Link, $Me)
                $mode = [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute, GroupRead, GroupExecute, OtherRead, OtherExecute'
                # The method body names the target literally, since it runs in no scope of this test.
                $resolve = [scriptblock]::Create("param(`$ReturnFinalTarget) [PSCustomObject]@{ FullName = '$Dir' }")
                Mock Get-Item {
                    $linkItem = [PSCustomObject]@{ LinkTarget = $Dir; UnixStat = [PSCustomObject]@{ UserId = $Me }; UnixFileMode = $mode; User = 'me' }
                    $linkItem | Add-Member -MemberType ScriptMethod -Name ResolveLinkTarget -Value $resolve -PassThru
                }.GetNewClosure() -ParameterFilter { $LiteralPath -eq $Link }
                Mock Get-Item { [PSCustomObject]@{ LinkTarget = $null; UnixStat = [PSCustomObject]@{ UserId = 4294967294 }; UnixFileMode = $mode; User = 'other' } }.GetNewClosure() -ParameterFilter { $LiteralPath -eq $Dir }
                { Assert-LambdaPSScaffoldPath -Path $Link } | Should -Throw -ErrorId 'TargetOwnedByAnotherUser' -ExpectedMessage '*owned by another user*'
            }
        }
    }

    # Only checked when -Force has something to delete. A junction needs no privilege on Windows, where
    # creating a symbolic link may.
    Context 'what -Force would empty' {
        BeforeAll {
            $script:linkType = if ($IsWindows) { 'Junction' } else { 'SymbolicLink' }
        }

        It 'refuses a non-empty link with TargetIsLink, naming the folder it points to' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'link-target')
            $link = [System.IO.Path]::Combine($TestDrive, 'link-to-it')
            $null = New-Item -ItemType Directory -Path $dir
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'keep.txt')) -Value 'x'
            $null = New-Item -ItemType $script:linkType -Path $link -Target $dir
            InModuleScope $script:moduleName -Parameters @{ Path = $link; Dir = $dir } {
                param($Path, $Dir)
                { Assert-LambdaPSScaffoldPath -Path $Path -Force } | Should -Throw -ErrorId 'TargetIsLink' -ExpectedMessage "*is a link to '$Dir'*"
            }
        }

        It 'accepts an empty link, which -Force has nothing to delete in' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'empty-link-target')
            $link = [System.IO.Path]::Combine($TestDrive, 'empty-link')
            $null = New-Item -ItemType Directory -Path $dir
            $null = New-Item -ItemType $script:linkType -Path $link -Target $dir
            InModuleScope $script:moduleName -Parameters @{ Path = $link } {
                param($Path)
                (Assert-LambdaPSScaffoldPath -Path $Path -Force).Count | Should -Be 0
            }
        }

        It 'refuses <Case> with TargetProtected' -ForEach @(
            @{ Case = 'the current location'; Path = '.'; Message = '*is the current location*' }
            @{ Case = 'a folder that contains the current location'; Path = '..'; Message = '*contains the current location*' }
            @{ Case = 'a path through a link that contains the current location'; Path = 'LINK'; Message = '*contains the current location*' }
        ) {
            $base = [System.IO.Path]::Combine($TestDrive, "protected-$($Case -replace '\W', '-')")
            $work = [System.IO.Path]::Combine($base, 'real', 'sub', 'work')
            $null = New-Item -ItemType Directory -Path $work
            Set-Content -LiteralPath ([System.IO.Path]::Combine($work, 'keep.txt')) -Value 'x'
            if ($Path -eq 'LINK') {
                $null = New-Item -ItemType $script:linkType -Path ([System.IO.Path]::Combine($base, 'alias')) -Target ([System.IO.Path]::Combine($base, 'real'))
                $Path = [System.IO.Path]::Combine($base, 'alias', 'sub')
            }
            Push-Location -LiteralPath $work
            try {
                InModuleScope $script:moduleName -Parameters @{ Path = $Path; Message = $Message } {
                    param($Path, $Message)
                    { Assert-LambdaPSScaffoldPath -Path $Path -Force } | Should -Throw -ErrorId 'TargetProtected' -ExpectedMessage $Message
                }
            }
            finally { Pop-Location }
        }

        # Run from the root of the drive, so the home folder does not contain the current location.
        It 'refuses the home folder with TargetProtected' -Skip:(-not $HOME) {
            Push-Location -LiteralPath ([System.IO.Path]::GetPathRoot($TestDrive))
            try {
                InModuleScope $script:moduleName {
                    $homePath = Get-LambdaPSRealPath -Path $HOME
                    { Assert-LambdaPSScaffoldPath -Path $homePath -Force } | Should -Throw -ErrorId 'TargetProtected' -ExpectedMessage '*is your home folder*'
                }
            }
            finally { Pop-Location }
        }

        It 'returns the contents of a folder of its own, for -Force to replace' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'own-folder')
            $null = New-Item -ItemType Directory -Path $dir
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'old.txt')) -Value 'x'
            InModuleScope $script:moduleName -Parameters @{ Path = $dir } {
                param($Path)
                (Assert-LambdaPSScaffoldPath -Path $Path -Force).Name | Should -Be 'old.txt'
            }
        }
    }
}

# The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
# surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
Describe 'Assert-LambdaPSScaffoldPath raised through a calling cmdlet' -Tag 'Unit' {
    It 'surfaces <ErrorId> from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'TargetIsFile'; Kind = 'file' }
        @{ ErrorId = 'TargetNotEmpty'; Kind = 'nonempty' }
        @{ ErrorId = 'TargetOwnedByAnotherUser'; Kind = 'owned' }
    ) {
        if ($Kind -eq 'owned' -and $IsWindows) { Set-ItResult -Skipped -Because 'the owner check is Linux and macOS only'; return }
        $target = [System.IO.Path]::Combine($TestDrive, "caller-$Kind")
        if ($Kind -eq 'file') { Set-Content -LiteralPath $target -Value 'x' }
        else { $null = New-Item -ItemType Directory -Path $target -Force; Set-Content -LiteralPath ([System.IO.Path]::Combine($target, 'x')) -Value 'x' }
        $Call = { Assert-LambdaPSScaffoldPath -Path $Target }
        InModuleScope $script:moduleName -Parameters @{ ErrorId = $ErrorId; Call = $Call.ToString(); Target = $target; Kind = $Kind } {
            param($ErrorId, $Call, $Target, $Kind)
            # Rebuilt here, so the call runs in the module and reaches its private helper.
            $Call = [scriptblock]::Create($Call)
            # Another user's directory: the caller's uid is not the owner's.
            if ($Kind -eq 'owned') { Mock Get-Command { [PSCustomObject]@{ Source = [System.IO.Path]::Combine($TestDrive, 'fake-id', '4294967294') } } -ParameterFilter { $Name -eq 'id' } }
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
            @($ids | Where-Object { $_ -like '*,Assert-LambdaPSScaffoldPath' }) | Should -BeNullOrEmpty
        }
    }
}
