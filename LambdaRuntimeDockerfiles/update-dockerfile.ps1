param ([Parameter(Mandatory)]$Dockerfile)

# Updates the Dockerfile with next ASP.NET Core version and checksum256 hash if available
function Update-Dockerfile ([string]$path) {
    Write-Host "Updating $path with next ASP.NET Core version"

    $nextVersion = Get-NextASPNETVersion -Dockerfile $path

    $checksumFilePath = "${nextVersion}-checksum.txt"

    $checksumUri = "https://dotnetcli.blob.core.windows.net/dotnet/checksums/${nextVersion}-sha.txt"
    Write-Host "Downloading checksums from $checksumUri"

    try {
        Invoke-WebRequest -Uri $checksumUri -OutFile $checksumFilePath
    }
    catch {
        # Ignore errors to allow the script to continue checking rest of the dockerfiles when called in sequence
        Write-Host "Failed to download checksums for ${nextVersion}. ${nextVersion} is not available yet."
        return
    }

    $arch = Get-Architecture -Dockerfile $path

    $artifact = "aspnetcore-runtime-${nextVersion}-linux-${arch}.tar.gz"
    $checksum = Get-Checksum -Artifact $artifact -Path $checksumFilePath

    (Get-Content $path) -replace 'ARG ASPNET_VERSION=.*', "ARG ASPNET_VERSION=${nextVersion}" -replace 'ARG ASPNET_SHA512=.*', "ARG ASPNET_SHA512=${checksum}" | Out-File $path

    Write-Host "Updated ${path} to ${nextVersion}."

    # This allows checksumring the $path variable between steps
    # which is needed to update the description of the PR
    Write-Host "::set-output name=${path}::- Updated ${path} to ${nextVersion}<br> - Artifact: ${artifact}<br> - Checksum Source: ${checksumUri}"
}

# Returns Checksum of given ASP.NET Core version from the give Checksum file
function Get-Checksum ([string]$artifact, [string]$path) {
    $line = Select-String -Path $path -Pattern $artifact | Select-Object -Property Line -ExpandProperty Line
    Write-Host $line

    $checksum = $line.Split(" ")[0]
    return $checksum
}

function Get-Architecture ([string]$Dockerfile) {
    if ($Dockerfile.Contains("amd64")) {
        return "x64"
    } elseif ($Dockerfile.Contains("arm64")) {
        return "arm64"
    } else {
        throw "Unsupported architecture"
    }
}

# Returns the next ASP.NET version to be updated in the Dockerfile
function Get-NextASPNETVersion ([string]$Dockerfile) {
    $line = Select-String -Path $Dockerfile -Pattern "ARG ASPNET_VERSION=" | Select-Object -Property Line -ExpandProperty Line
    $currentVersion = $line.Split("=")[1]
    Write-Host "Current ASPNET version: ${currentVersion}"

    $nextVersion = Update-PatchVersion -Version $currentVersion
    Write-Host "Next ASPNET version: ${nextVersion}"

    return $nextVersion
}

# Returns the next path version of the given version
function Update-PatchVersion ([string]$version) {
    $components = $version.Split(".");
    $major = $components[0];
    $minor = $components[1];
    $patch = $components[2];
    $patch = [int]$patch + 1;
    $newVersion = $major + "." + $minor + "." + $patch;
    return $newVersion;
}

Update-Dockerfile $Dockerfile