@{
    # The AWS.Tools.Installer release saved with PSResourceGet when no Installer 2.0.5 or later 2.x is
    # installed. Its code runs in this session, so the saved files are checked against FilesSha256
    # before it is imported, on every platform (Save-PSResource checks Authenticode on Windows only,
    # and not who signed). FilesSha256 is Get-LambdaPSFolderHash of the saved
    # AWS.Tools.Installer/<Version> folder, leaving out the PSGetModuleInfo.xml PSResourceGet writes.
    # Move Version and FilesSha256 together when a new Installer ships.
    Version     = '2.0.6'
    FilesSha256 = '7B83ECBFF3349060967AD93CB7FAD1CDDC7C5F6724188CE64D5B5C6AAC652555'
}
