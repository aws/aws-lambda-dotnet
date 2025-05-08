// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.UnitTests.Utilities;

/// <summary>
/// A set of helper functions for tests.
/// </summary>
public static class DirectoryHelpers
{
    /// <summary>
    /// Creates a temp directory and copies the working directory to that temp directory.
    /// </summary>
    /// <param name="workingDirectory">The working directory of the test</param>
    /// <returns>A new temp directory with the files from the working directory</returns>
    public static string GetTempTestAppDirectory(string workingDirectory)
    {
        var customTestAppPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".temp", Path.GetRandomFileName());
        Directory.CreateDirectory(customTestAppPath);

        // Ensure the directory is not read-only
        File.SetAttributes(customTestAppPath, FileAttributes.Normal);

        var currentDir = new DirectoryInfo(workingDirectory);
        CopyDirectory(currentDir, customTestAppPath);

        return customTestAppPath;
    }

    /// <summary>
    /// Deletes the provided directory.
    /// </summary>
    /// <param name="directory">The directory to delete.</param>
    public static void CleanUp(string directory)
    {
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// <see cref="https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories"/>
    /// </summary>
    private static void CopyDirectory(DirectoryInfo dir, string destDirName)
    {
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {dir.FullName}");
        }

        var dirs = dir.GetDirectories();

        Directory.CreateDirectory(destDirName);

        var files = dir.GetFiles();
        foreach (var file in files)
        {
            var tempPath = Path.Combine(destDirName, file.Name);
            file.CopyTo(tempPath, false);

            // Ensure copied file is not read-only
            File.SetAttributes(tempPath, FileAttributes.Normal);
        }

        foreach (var subdir in dirs.Where(x => !x.Name.Equals(".git")))
        {
            var tempPath = Path.Combine(destDirName, subdir.Name);
            var subDir = new DirectoryInfo(subdir.FullName);
            CopyDirectory(subDir, tempPath);
        }

        // Ensure the directory itself is not read-only
        File.SetAttributes(destDirName, FileAttributes.Normal);
    }
}
