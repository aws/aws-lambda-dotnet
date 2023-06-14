# This script allows to update Dockerfiles to the next ASP.NET Core version.
# It fetches the checksum file of the next .NET version from Microsoft server.
# If checksum file is available, it will update the Dockerfile next version and its checksum.

param ([Parameter(Mandatory)]$DockerfilePath, [Parameter(Mandatory)]$NextVersion)

# Updates the Dockerfile with next ASP.NET Core version and checksum512 hash if available
function Update-Dockerfile ([string]$DockerfilePath, [string]$NextVersion) {
    Write-Host "Updating $DockerfilePath with next ASP.NET Core version"

    $checksumFilePath = "${NextVersion}-checksum.txt"

    $checksumUri = "https://dotnetcli.blob.core.windows.net/dotnet/checksums/${NextVersion}-sha.txt"
    Write-Host "Downloading checksums from $checksumUri"

    Invoke-WebRequest -Uri $checksumUri -OutFile $checksumFilePath

    $arch = Get-Architecture -DockerfilePath $DockerfilePath

    $artifact = "aspnetcore-runtime-${NextVersion}-linux-${arch}.tar.gz"
    $checksum = Get-Checksum -Artifact $artifact -DockerfilePath $checksumFilePath

    (Get-Content $DockerfilePath) -replace 'ARG ASPNET_VERSION=.*', "ARG ASPNET_VERSION=${NextVersion}" -replace 'ARG ASPNET_SHA512=.*', "ARG ASPNET_SHA512=${checksum}" | Out-File $DockerfilePath

    Write-Host "Updated ${DockerfilePath} to ${NextVersion}."

    # This allows checksumring the $DockerfilePath variable between steps
    # which is needed to update the description of the PR
    Write-Host "::set-output name=${DockerfilePath}::- Updated ${DockerfilePath} to ${NextVersion}<br> - Artifact: ${artifact}<br> - Checksum Source: ${checksumUri}"
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

Update-Dockerfile $DockerfilePath $NextVersion