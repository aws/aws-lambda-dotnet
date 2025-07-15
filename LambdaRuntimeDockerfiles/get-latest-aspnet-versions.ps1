# This script fetches the latest ASP.NET Core runtime versions for .NET 8, 9, and 10
# It uses the NuGet API to query for Microsoft.AspNetCore.App.Runtime.linux-x64 package versions

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
        
        foreach ($ver in $versions) {
            if ($ver -match '-preview') {
                $previewVersions += $ver
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
                    
                    # Convert to Version object for proper comparison
                    $versionObj = [Version]$ver
                    Add-Member -InputObject $verObj -MemberType NoteProperty -Name "Version" -Value $versionObj
                    
                    $verObjects += $verObj
                } catch {
                    Write-Host "Warning: Could not parse version $ver, skipping."
                }
            }
            
            # Sort by version (descending) and get the first one
            $sortedVersions = $verObjects | Sort-Object -Property Version -Descending
            
            if ($sortedVersions.Count -gt 0) {
                $latestVersion = $sortedVersions[0].OriginalVersion
            } else {
                $latestVersion = $null
            }
        }
        # Otherwise get the latest preview version
        elseif ($previewVersions.Count -gt 0) {
            # For preview versions like "10.0.0-preview.5.25277.114"
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
            
            # Sort by version components
            $sortedPreviews = $previewObjs | Sort-Object -Property Major, Minor, Patch, Preview -Descending
            
            if ($sortedPreviews.Count -gt 0) {
                $latestVersion = $sortedPreviews[0].OriginalVersion
            } else {
                # Fallback - just take the last one alphabetically
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

# Get latest versions for each .NET version
$net8Version = Get-LatestAspNetVersion -majorVersion "8"
$net9Version = Get-LatestAspNetVersion -majorVersion "9"
$net10Version = Get-LatestAspNetVersion -majorVersion "10"

# Verify we got valid versions
$allVersionsValid = $true
if (-not $net8Version) { 
    Write-Error "Failed to determine .NET 8 version"
    $allVersionsValid = $false
}
if (-not $net9Version) { 
    Write-Error "Failed to determine .NET 9 version" 
    $allVersionsValid = $false
}
if (-not $net10Version) { 
    Write-Error "Failed to determine .NET 10 version" 
    $allVersionsValid = $false
}

if (-not $allVersionsValid) {
    exit 1
}

# Output as GitHub Actions environment variables
Write-Output "NET_8_NEXT_VERSION=$net8Version"
Write-Output "NET_9_NEXT_VERSION=$net9Version"
Write-Output "NET_10_NEXT_VERSION=$net10Version"
