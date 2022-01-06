# This script allows to update Dockerfiles with next ASP.NET Core patch version.
# It greps the current version from Dockerfile, increment the patch version and fetches checksum file from Microsoft server.
# If checksum file is available, it will update the Dockerfile next version and its checksum.
# NOTE: This scripts only updates patch version by incrementing the next patch version.
# If Microsoft ever releases a minor version, this needs to updated accordingly to include minor version.

param ([Parameter(Mandatory)]$DockerfilePath)

# Updates the Dockerfile with next ASP.NET Core version and checksum512 hash if available
function Update-Dockerfile ([string]$DockerfilePath) {
    Write-Host "Updating $DockerfilePath with next ASP.NET Core version"

    $nextVersion = Get-NextASPNETVersion -DockerfilePath $DockerfilePath

    $checksumFilePath = "${nextVersion}-checksum.txt"

    $checksumUri = "https://dotnetcli.blob.core.windows.net/dotnet/checksums/${nextVersion}-sha.txt"
    Write-Host "Downloading checksums from $checksumUri"

    Invoke-WebRequest -Uri $checksumUri -OutFile $checksumFilePath

    $arch = Get-Architecture -DockerfilePath $DockerfilePath

    $artifact = "aspnetcore-runtime-${nextVersion}-linux-${arch}.tar.gz"
    $checksum = Get-Checksum -Artifact $artifact -DockerfilePath $checksumFilePath

    (Get-Content $DockerfilePath) -replace 'ARG ASPNET_VERSION=.*', "ARG ASPNET_VERSION=${nextVersion}" -replace 'ARG ASPNET_SHA512=.*', "ARG ASPNET_SHA512=${checksum}" | Out-File $DockerfilePath

    Write-Host "Updated ${DockerfilePath} to ${nextVersion}."

    # This allows checksumring the $DockerfilePath variable between steps
    # which is needed to update the description of the PR
    Write-Host "::set-output name=${DockerfilePath}::- Updated ${DockerfilePath} to ${nextVersion}<br> - Artifact: ${artifact}<br> - Checksum Source: ${checksumUri}"
}

# Returns Checksum of given ASP.NET Core version from the give Checksum file
function Get-Checksum ([string]$artifact, [string]$DockerfilePath) {
    $line = Select-String -Path $DockerfilePath -Pattern $artifact | Select-Object -Property Line -ExpandProperty Line
    Write-Host $line

    $checksum = $line.Split(" ")[0]
    return $checksum
}

# Returns the architecture of the Dockerfile by checking the path of Dockerfile
function Get-Architecture ([string]$DockerfilePath) {
    if ($DockerfilePath.Contains("amd64")) {
        return "x64"
    } elseif ($DockerfilePath.Contains("arm64")) {
        return "arm64"
    } else {
        throw "Unsupported architecture"
    }
}

# Returns the next ASP.NET version to be updated in the Dockerfile
function Get-NextASPNETVersion ([string]$DockerfilePath) {
    $line = Select-String -Path $DockerfilePath -Pattern "ARG ASPNET_VERSION=" | Select-Object -Property Line -ExpandProperty Line
    $currentVersion = $line.Split("=")[1]
    Write-Host "Current ASPNET version: ${currentVersion}"

    $nextVersion = Update-PatchVersion -Version $currentVersion
    Write-Host "Next ASPNET version: ${nextVersion}"

    return $nextVersion
}

# Returns the next patch version of the given version
function Update-PatchVersion ([string]$version) {
    $components = $version.Split(".");
    $major = $components[0];
    $minor = $components[1];
    $patch = $components[2];
    $patch = [int]$patch + 1;
    $newVersion = $major + "." + $minor + "." + $patch;
    return $newVersion;
}

Update-Dockerfile $DockerfilePath