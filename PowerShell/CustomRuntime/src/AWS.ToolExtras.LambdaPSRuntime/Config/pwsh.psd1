@{
    # Where the official PowerShell release tarballs are published: <ReleaseUrl>/v<version>/<file>.
    # The Layer and self-contained .zip builds download from it, and the scaffolded CMR and Container
    # Dockerfiles fetch the arm64 tarball from it.
    ReleaseUrl    = 'https://github.com/PowerShell/PowerShell/releases/download'

    # How those downloads retry and time out. A failed HTTP response (a GitHub rate limit or server
    # error) is retried up to RetryCount times, RetryDelaySeconds apart. ConnectTimeoutSeconds limits
    # only connecting to the host. OperationTimeoutSeconds stops a transfer that stalls part-way instead
    # of waiting indefinitely: the Layer and self-contained .zip builds' tarball download
    # (Invoke-WebRequest -OperationTimeoutSeconds) fails when one read waits that long for data, and
    # every curl in the scaffolded Dockerfiles (--max-time) fails when the whole transfer takes that
    # long. Those builds and Dockerfiles use all four.
    Download      = @{
        RetryCount              = 3
        RetryDelaySeconds       = 2
        ConnectTimeoutSeconds   = 30
        OperationTimeoutSeconds = 600
    }

    # Microsoft's RPM repository, which the CMR and Container images install pwsh from on amd64: the
    # scaffolded Dockerfiles write Url as the baseurl of the dnf repo file, so the lookup and the
    # install read the same repository. OperationTimeoutSeconds limits each read of that lookup; the retries and connect timeout are
    # Download's.
    PackageRepository = @{
        Url                     = 'https://packages.microsoft.com/rhel/9/prod'
        OperationTimeoutSeconds = 60
    }

    # The ICU libraries the no-Docker runtime image puts next to pwsh (app-local ICU), so cultures work
    # as in the Docker image, which installs libicu. They come from Microsoft's
    # Microsoft.ICU.ICU4C.Runtime.linux-<x64|arm64> NuGet packages; Sha256 is of each .nupkg.
    Icu           = @{
        Version    = '72.1.0.3'
        PackageUrl = 'https://api.nuget.org/v3-flatcontainer'
        Sha256     = @{
            x64   = '4afb3b4c2e79909ad7b531135f4b742b199c671ea9396ee4240f77f898ea5207'
            arm64 = '1f090fc45429292ae21f9199d5b956ad3570c38a7778ea4085258641727eefaf'
        }
    }

    # The pwsh release each supported PowerShell line installs by default, when -PwshVersion is not
    # given. Move a line to its newest servicing release (and pin its hashes below) as releases ship.
    DefaultPatch  = @{
        '7.4' = '7.4.22'
        '7.5' = '7.5.13'
        '7.6' = '7.6.9'
    }

    # SHA256 of the official PowerShell Linux release tarballs
    # (powershell-<version>-linux-<x64|arm64>.tar.gz on the PowerShell GitHub releases page). The
    # Layer and self-contained .zip packages verify their download against these, and the CMR and
    # Container images verify the arm64 tarball against them (Microsoft publishes no aarch64 RPM).
    # A release's hashes stay when DefaultPatch moves past it, so a project that pins an older
    # -PwshVersion still builds.
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
        '7.4.21' = @{
            x64   = '3fc5e1567c450bd7bd5ae69ebb0a9a4a4e0418bbc36b0232cf6dd547f0bfc0eb'
            arm64 = 'd0c216cec4c6c866fc15a105f039740228b53206f8576d2ed0bcd656a35ed6f6'
        }
        '7.5.12' = @{
            x64   = 'db85ae1f45005ffd20e456890585167257151fa99406ccd7ab344f7cf3a766fd'
            arm64 = '29eb382cd510e4bd246902650ff6cf9f3b56f959d1bd0be75e0a1e425177c831'
        }
        '7.6.7'  = @{
            x64   = '6896f9e3a6b0a354bf470835a96da76d2f801cd50dcf047ad7c4563bf93bf431'
            arm64 = 'ba465efdfb0984c1c5dc5ed9eb0b79a7e91a1b52c45f99377058c0659b84d3f1'
        }
        '7.4.22' = @{
            x64   = '9de443bef974f219c98a33b0f4096aace7f30b42188f37a1b14c5da7e82c8a6a'
            arm64 = 'ef95973facf60cf735f6d8caef3383394b9e8200cbd078d6068f7e14ca6b9078'
        }
        '7.5.13' = @{
            x64   = 'e9256bfc6ac98fcbec97270e8a986577a7346a587837cc80d30595eef3c2546b'
            arm64 = '6a556353ccfb0a414b80322334eac29a540a398537d3cf8dcc3ecec9cd8abd66'
        }
        '7.6.9'  = @{
            x64   = '1da6d6d6035d651f20e70ddee7c4c15ac6ab8b9fb40ee92240d91263f28a884b'
            arm64 = '02eb9991af447c7e7fe881acde195961afd397c40b8eb1c2b3856b84ec2d85f2'
        }
    }
}
