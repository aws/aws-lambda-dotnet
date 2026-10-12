BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSCredentialParameter' -Tag 'Unit' {
    BeforeAll {
        # A credential file of the test's own that defines the profile 'dev'.
        $script:credentialFile = [System.IO.Path]::Combine($TestDrive, 'credentials')
        Set-Content -LiteralPath $script:credentialFile -Value "[dev]`naws_access_key_id = AKIAEXAMPLE`naws_secret_access_key = example"
    }

    It 'returns the credential parameters the caller bound, and nothing else' {
        InModuleScope $script:moduleName -Parameters @{ ProfileFile = $script:credentialFile } {
            param($ProfileFile)
            $credential = [PSCustomObject]@{ Kind = 'AWSCredentials stand-in' }
            $bound = [System.Collections.Generic.Dictionary[string, object]]::new()
            $bound['ProfileName'] = 'dev'
            $bound['ProfileLocation'] = $ProfileFile
            $bound['Credential'] = $credential
            $bound['Region'] = 'us-east-1'

            $result = Get-LambdaPSCredentialParameter -BoundParameters $bound

            @($result.Keys | Sort-Object) | Should -Be @('Credential', 'ProfileLocation', 'ProfileName')
            $result.Credential | Should -Be $credential
        }
    }

    It 'returns an empty table when none were bound, so AWS.Tools uses its default credentials' {
        InModuleScope $script:moduleName {
            $bound = [System.Collections.Generic.Dictionary[string, object]]::new()
            $bound['Region'] = 'us-east-1'
            (Get-LambdaPSCredentialParameter -BoundParameters $bound).Count | Should -Be 0
        }
    }

    It 'resolves a relative -ProfileLocation against the PowerShell location, and finds the profile there' {
        $folder = [System.IO.Path]::Combine($TestDrive, 'relative-location')
        $null = New-Item -ItemType Directory -Path $folder -Force
        Copy-Item -LiteralPath $script:credentialFile -Destination ([System.IO.Path]::Combine($folder, 'credentials'))
        Push-Location -LiteralPath $folder
        try {
            InModuleScope $script:moduleName -Parameters @{ Folder = $folder } {
                param($Folder)
                $bound = [System.Collections.Generic.Dictionary[string, object]]::new()
                $bound['ProfileName'] = 'dev'
                $bound['ProfileLocation'] = './credentials'

                $result = Get-LambdaPSCredentialParameter -BoundParameters $bound

                $result.ProfileLocation | Should -Be ([System.IO.Path]::Combine($Folder, 'credentials'))
                $result.ProfileName | Should -Be 'dev'
            }
        }
        finally {
            Pop-Location
        }
    }

    It 'returns an absolute -ProfileLocation unchanged' {
        InModuleScope $script:moduleName -Parameters @{ AbsolutePath = $script:credentialFile } {
            param($AbsolutePath)
            $bound = [System.Collections.Generic.Dictionary[string, object]]::new()
            $bound['ProfileName'] = 'dev'
            $bound['ProfileLocation'] = $AbsolutePath

            (Get-LambdaPSCredentialParameter -BoundParameters $bound).ProfileLocation | Should -BeExactly $AbsolutePath
        }
    }

    It 'resolves -ProfileLocation given without -ProfileName, and checks no profile' {
        Push-Location -LiteralPath $TestDrive
        try {
            InModuleScope $script:moduleName -Parameters @{ Root = "$TestDrive" } {
                param($Root)
                $bound = [System.Collections.Generic.Dictionary[string, object]]::new()
                $bound['ProfileLocation'] = 'no-such-file'

                (Get-LambdaPSCredentialParameter -BoundParameters $bound).ProfileLocation | Should -Be ([System.IO.Path]::Combine($Root, 'no-such-file'))
            }
        }
        finally {
            Pop-Location
        }
    }

    # AWS.Tools runs with its default credentials when the named profile does not exist.
    It 'throws ProfileNotFound, naming the profile and the file, for a profile -ProfileLocation does not define' {
        InModuleScope $script:moduleName -Parameters @{ ProfileFile = $script:credentialFile } {
            param($ProfileFile)
            $bound = [System.Collections.Generic.Dictionary[string, object]]::new()
            $bound['ProfileName'] = 'devv'
            $bound['ProfileLocation'] = $ProfileFile

            $err = $null
            try { $null = Get-LambdaPSCredentialParameter -BoundParameters $bound } catch { $err = $_ }
            $err.FullyQualifiedErrorId | Should -Be 'ProfileNotFound'
            $err.Exception.Message | Should -BeLike "*profile 'devv' was not found in the credential file '$ProfileFile'*"
        }
    }

    # The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
    # surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
    It 'surfaces ProfileNotFound from the calling cmdlet, and names no internal helper in -ErrorVariable' {
        InModuleScope $script:moduleName -Parameters @{ File = $script:credentialFile } {
            param($File)
            function Invoke-LambdaPSTestCaller {
                [CmdletBinding()]
                param($BoundParameters)
                try { $null = Get-LambdaPSCredentialParameter -BoundParameters $BoundParameters }
                catch { $PSCmdlet.ThrowTerminatingError($_) }
            }
            $bound = [System.Collections.Generic.Dictionary[string, object]]::new()
            $bound['ProfileName'] = 'devv'
            $bound['ProfileLocation'] = $File
            $collected = $null

            $err = $null
            try { Invoke-LambdaPSTestCaller -BoundParameters $bound -ErrorVariable collected } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'ProfileNotFound,Invoke-LambdaPSTestCaller'
            $ids = @($collected | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.FullyQualifiedErrorId } else { $_.ErrorRecord.FullyQualifiedErrorId } })
            $ids | Should -Contain 'ProfileNotFound,Invoke-LambdaPSTestCaller'
            @($ids | Where-Object { $_ -like '*,Get-LambdaPSCredentialParameter' }) | Should -BeNullOrEmpty
        }
    }

    # The default store is the test's own empty files, not the machine's ~/.aws. The SDK reads the
    # store once per process, so the check runs in a child pwsh that has read nothing yet.
    It 'throws ProfileNotFound for a profile the default credential store does not hold' {
        $emptyStore = [System.IO.Path]::Combine($TestDrive, 'empty-store')
        Set-Content -LiteralPath $emptyStore -Value ''
        $command = @"
`$env:AWS_SHARED_CREDENTIALS_FILE = '$emptyStore'
`$env:AWS_CONFIG_FILE = '$emptyStore'
`$bound = [System.Collections.Generic.Dictionary[string, object]]::new()
`$bound['ProfileName'] = 'lambdaps-test-no-such-profile'
try { `$null = & (Get-Module AWS.ToolExtras.LambdaPSRuntime) { param(`$b) Get-LambdaPSCredentialParameter -BoundParameters `$b } `$bound; 'RETURNED' }
catch { 'FQID: ' + `$_.FullyQualifiedErrorId; 'MESSAGE: ' + `$_.Exception.Message }
"@

        $output = (Invoke-InChildPwsh -Command $command).Output

        $output | Should -Not -BeLike '*RETURNED*'
        $output | Should -BeLike '*FQID: ProfileNotFound*'
        $output | Should -BeLike "*'lambdaps-test-no-such-profile' was not found in the local credential store*"
    }
}
