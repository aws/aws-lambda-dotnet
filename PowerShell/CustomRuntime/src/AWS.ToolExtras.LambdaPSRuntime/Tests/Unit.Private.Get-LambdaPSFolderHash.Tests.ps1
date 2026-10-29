BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))

    # Writes the files in -Content (relative path => text, '/'-separated) under -Path.
    function New-HashFolder {
        [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Test helper: it writes only under the folder a test passes in.')]
        param([string]$Path, [hashtable]$Content)
        foreach ($relative in $Content.Keys) {
            $file = [System.IO.Path]::Combine([string[]](@($Path) + $relative.Split('/')))
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($file)) -Force
            [System.IO.File]::WriteAllText($file, $Content[$relative])
        }
        $Path
    }
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSFolderHash' -Tag 'Unit' {
    It 'is the SHA256 of the sorted path-and-SHA256 lines, so it can be checked without this module' {
        $folder = New-HashFolder -Path (Join-Path $TestDrive 'known') -Content @{ 'b.txt' = 'b'; 'Sub/a.txt' = 'a'; 'a.txt' = 'a' }
        $sha = { param($t) [System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($t))) }
        # Ordinal order: 'Sub/a.txt' sorts before the lower-case names.
        $expected = & $sha ("Sub/a.txt $(& $sha 'a')`na.txt $(& $sha 'a')`nb.txt $(& $sha 'b')")

        InModuleScope $script:moduleName -Parameters @{ Folder = $folder; Expected = $expected } {
            param($Folder, $Expected)
            Get-LambdaPSFolderHash -Path $Folder | Should -BeExactly $Expected
        }
    }

    It 'changes when a file is changed, added or renamed, and ignores an excluded file' {
        $content = @{ 'x.psd1' = '@{}'; 'Private/y.ps1' = 'y' }
        $base = New-HashFolder -Path (Join-Path $TestDrive 'base') -Content $content
        $changed = New-HashFolder -Path (Join-Path $TestDrive 'changed') -Content @{ 'x.psd1' = '@{ }'; 'Private/y.ps1' = 'y' }
        $added = New-HashFolder -Path (Join-Path $TestDrive 'added') -Content ($content + @{ 'z.ps1' = '' })
        $renamed = New-HashFolder -Path (Join-Path $TestDrive 'renamed') -Content @{ 'x.psd1' = '@{}'; 'Private/Y.ps1' = 'y' }
        $excluded = New-HashFolder -Path (Join-Path $TestDrive 'excluded') -Content ($content + @{ 'PSGetModuleInfo.xml' = '<xml/>' })

        InModuleScope $script:moduleName -Parameters @{ Base = $base; Others = @($changed, $added, $renamed); Excluded = $excluded } {
            param($Base, $Others, $Excluded)
            $hash = Get-LambdaPSFolderHash -Path $Base
            foreach ($other in $Others) {
                Get-LambdaPSFolderHash -Path $other | Should -Not -Be $hash -Because $other
            }
            Get-LambdaPSFolderHash -Path $Excluded -Exclude 'PSGetModuleInfo.xml' | Should -Be $hash
            Get-LambdaPSFolderHash -Path $Excluded | Should -Not -Be $hash
        }
    }

    It 'stops when the folder does not exist, rather than hashing nothing' {
        InModuleScope $script:moduleName -Parameters @{ Missing = (Join-Path $TestDrive 'missing') } {
            param($Missing)
            { Get-LambdaPSFolderHash -Path $Missing } | Should -Throw
        }
    }
}
