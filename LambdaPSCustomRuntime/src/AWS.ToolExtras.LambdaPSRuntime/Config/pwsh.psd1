@{
    # Where the official PowerShell release tarballs are published: <ReleaseUrl>/v<version>/<file>.
    # The Layer and self-contained .zip builds download from it, and the scaffolded CMR and Container
    # Dockerfiles fetch the arm64 tarball from it.
    ReleaseUrl    = 'https://github.com/PowerShell/PowerShell/releases/download'

    # How those downloads retry and time out. A failed HTTP response (a GitHub rate limit or server
    # error) is retried up to RetryCount times, RetryDelaySeconds apart. ConnectTimeoutSeconds limits
    # only connecting to the host; OperationTimeoutSeconds limits a whole download, so a transfer that
    # stalls part-way fails instead of waiting indefinitely. The Layer and self-contained .zip builds
    # use all four for the tarball download, and the scaffolded Dockerfiles for every curl they run.
    Download      = @{
        RetryCount              = 3
        RetryDelaySeconds       = 2
        ConnectTimeoutSeconds   = 30
        OperationTimeoutSeconds = 600
    }

    # The pwsh release each supported PowerShell line installs by default, when -PwshVersion is not
    # given. Move a line to its newest servicing release (and pin its hashes below) as releases ship.
    DefaultPatch  = @{
        '7.4' = '7.4.20'
        '7.5' = '7.5.11'
        '7.6' = '7.6.6'
    }

    # SHA256 of the official PowerShell Linux release tarballs
    # (powershell-<version>-linux-<x64|arm64>.tar.gz on the PowerShell GitHub releases page). The
    # Layer and self-contained .zip packages verify their download against these, and the CMR and
    # Container images verify the arm64 tarball against them (Microsoft publishes no aarch64 RPM).
    ArchiveSha256 = @{
        '7.4.20' = @{
            x64   = 'ed8008345c7f5337f5e293145f5e243ce132e5ee4dfc24ac551c45a4b3092181'
            arm64 = '4d1a7b468eae627809eac5fae79c4e645cd9362fdf9f626298686b4489eb4724'
        }
        '7.5.11' = @{
            x64   = '82a8b13d92b0f3ae48e56cf2f3f7961679371736ca90145ca71617c2913ba9d8'
            arm64 = '830ebda118c731ece3fa7e6b7e8573a21346387cbbca5b2f5e3b9bfe24f96672'
        }
        '7.6.6'  = @{
            x64   = 'ddbc4a2d113bbd46d283cfedcbcd117a70caefd7673f41f2b4e0000badf103bc'
            arm64 = '924829e54c983648f6f1419a2dc7f9433c861b2fb5bd57736ff096c24f133729'
        }
    }
}
