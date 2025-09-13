# This script fetches the latest ASP.NET Core runtime version for a specified .NET major version
# It uses the NuGet API to query for Microsoft.AspNetCore.App.Runtime.linux-x64 package versions

param(
    [Parameter(Mandatory=$true)]
    [string]$MajorVersion
)

function Get-LatestAspNetVersion {
    param (
        [string]$majorVersion
    )
    
    Write-Host "Fetching latest ASP.NET Core runtime version for .NET $majorVersion..."
    
    try {
        # Use NuGet API to find latest version
        $response = Invoke-RestMethod -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.app.runtime.linux-x64/index.json"
        
        # Filter versions matching the major version
        $versions = @()
        foreach ($ver in $response.versions) {
            if ($ver -like "$majorVersion.*") {
                $versions += $ver
            }
        }
        
        if ($versions.Count -eq 0) {
            Write-Error "No versions found for .NET $majorVersion"
            return $null
        }
        
        # Separate release and preview versions
        $releaseVersions = @()
        $previewVersions = @()
        $rcVersions = @()
        
        foreach ($ver in $versions) {
            if ($ver -match '-preview') {
                $previewVersions += $ver
            } elseif ($ver -match '-rc') {
                $rcVersions += $ver
            } else {
                $releaseVersions += $ver
            }
        }
        
        # If we have release versions, get the latest
        if ($releaseVersions.Count -gt 0) {
            $verObjects = @()
            foreach ($ver in $releaseVersions) {
                try {
                    $verObj = New-Object PSObject
                    Add-Member -InputObject $verObj -MemberType NoteProperty -Name "OriginalVersion" -Value $ver
                    $versionObj = [Version]$ver
                    Add-Member -InputObject $verObj -MemberType NoteProperty -Name "Version" -Value $versionObj
                    $verObjects += $verObj
                } catch {
                    Write-Host "Warning: Could not parse version $ver, skipping."
                }
            }
            $sortedVersions = $verObjects | Sort-Object -Property Version -Descending
            if ($sortedVersions.Count -gt 0) {
                $latestVersion = $sortedVersions[0].OriginalVersion
            } else {
                $latestVersion = $null
            }
        }
        # Otherwise get the latest rc version
        elseif ($rcVersions.Count -gt 0) {
            $rcObjs = @()
            $maxRcParts = 0
            foreach ($ver in $rcVersions) {
                # Match versions like 10.0.0-rc.1.25451.107 or 10.0.0-rc.1
                if ($ver -match '^(\d+)\.(\d+)\.(\d+)-rc\.(.+)$') {
                    $major = [int]$matches[1]
                    $minor = [int]$matches[2]
                    $patch = [int]$matches[3]
                    $rcParts = $matches[4] -split '\.'
                    $rcNumbers = $rcParts | ForEach-Object { [int]$_ }
                    if ($rcNumbers.Count -gt $maxRcParts) { $maxRcParts = $rcNumbers.Count }
                    $rcObj = New-Object PSObject
                    Add-Member -InputObject $rcObj -MemberType NoteProperty -Name "OriginalVersion" -Value $ver
                    Add-Member -InputObject $rcObj -MemberType NoteProperty -Name "Major" -Value $major
                    Add-Member -InputObject $rcObj -MemberType NoteProperty -Name "Minor" -Value $minor
                    Add-Member -InputObject $rcObj -MemberType NoteProperty -Name "Patch" -Value $patch
                    for ($i = 0; $i -lt $rcNumbers.Count; $i++) {
                        Add-Member -InputObject $rcObj -MemberType NoteProperty -Name ("RC$i") -Value $rcNumbers[$i]
                    }
                    $rcObjs += $rcObj
                }
            }
            # Pad missing RC fields with 0 for sorting
            foreach ($obj in $rcObjs) {
                for ($i = 0; $i -lt $maxRcParts; $i++) {
                    if (-not ($obj.PSObject.Properties.Name -contains ("RC$i"))) {
                        Add-Member -InputObject $obj -MemberType NoteProperty -Name ("RC$i") -Value 0
                    }
                }
            }
            $sortProps = @("Major", "Minor", "Patch") + @(for ($i = 0; $i -lt $maxRcParts; $i++) { "RC$i" })
            $sortedRCs = $rcObjs | Sort-Object -Property $sortProps -Descending
            if ($sortedRCs.Count -gt 0) {
                $latestVersion = $sortedRCs[0].OriginalVersion
            } else {
                $latestVersion = ($rcVersions | Sort-Object)[-1]
            }
        }
        # Otherwise get the latest preview version
        elseif ($previewVersions.Count -gt 0) {
            $previewObjs = @()
            foreach ($ver in $previewVersions) {
                if ($ver -match '(\d+)\.(\d+)\.(\d+)-preview\.(\d+)') {
                    $major = [int]$matches[1]
                    $minor = [int]$matches[2]
                    $patch = [int]$matches[3]
                    $preview = [int]$matches[4]
                    $previewObj = New-Object PSObject
                    Add-Member -InputObject $previewObj -MemberType NoteProperty -Name "OriginalVersion" -Value $ver
                    Add-Member -InputObject $previewObj -MemberType NoteProperty -Name "Major" -Value $major
                    Add-Member -InputObject $previewObj -MemberType NoteProperty -Name "Minor" -Value $minor
                    Add-Member -InputObject $previewObj -MemberType NoteProperty -Name "Patch" -Value $patch
                    Add-Member -InputObject $previewObj -MemberType NoteProperty -Name "Preview" -Value $preview
                    $previewObjs += $previewObj
                }
            }
            $sortedPreviews = $previewObjs | Sort-Object -Property Major, Minor, Patch, Preview -Descending
            if ($sortedPreviews.Count -gt 0) {
                $latestVersion = $sortedPreviews[0].OriginalVersion
            } else {
                $latestVersion = ($previewVersions | Sort-Object)[-1]
            }
        }
        else {
            $latestVersion = $null
        }
        
        if ($latestVersion) {
            Write-Host "Latest ASP.NET Core runtime version for .NET $majorVersion is $latestVersion"
            return $latestVersion
        } else {
            Write-Error "Could not determine latest version for .NET $majorVersion"
            return $null
        }
    }
    catch {
        $errorMessage = "Error fetching versions for .NET $majorVersion " + $_
        Write-Error $errorMessage
        return $null
    }
}

# Get latest version for the specified .NET major version
$version = Get-LatestAspNetVersion -majorVersion $MajorVersion

# Verify we got a valid version
if (-not $version) { 
    Write-Error "Failed to determine .NET $MajorVersion version"
    exit 1
}

# Output the version directly
Write-Output $version
