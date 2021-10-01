function Push-MultiArchImageManifest($ManifestList, $Amd64Manifest, $Arm64Manifest)
{
    docker manifest create $ManifestList $Amd64Manifest $Arm64Manifest
    if (!$?)
    {
        throw "Failed to create $ManifestList manifestlist with $Amd64Manifest $Arm64Manifest manifests"
    }

    docker manifest push $ManifestList
    if (!$?)
    {
        throw "Failed to push $ManifestList manifestlist"
    }

    docker manifest inspect $ManifestList
    if (!$?)
    {
        throw "Failed to inspect $ManifestList manifestlist"
    }
}
