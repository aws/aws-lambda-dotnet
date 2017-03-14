using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.PlatformAbstractions;

namespace BLUEPRINT_BASE_NAME.Tests
{
    public static class TestUtils
    {
        /// <summary>
        ///     Gets the full path to the target project path that we wish to test
        /// </summary>
        /// <param name="solutionRelativePath">
        ///     The parent directory of the target project.
        ///     e.g. src, samples, test, or test/Websites
        /// </param>
        /// <returns>The full path to the target project.</returns>
        public static string GetProjectPath(string solutionRelativePath)
        {
            // Get the target project's assembly.
            var startupAssembly = typeof(Startup).GetTypeInfo().Assembly;

            // Get name of the target project which we want to test
            var projectName = startupAssembly.GetName().Name;

            // Get currently executing test project path
            var applicationBasePath = PlatformServices.Default.Application.ApplicationBasePath;

            // Find the folder which contains the solution file. We then use this information to find the target
            // project which we want to test.
            var directoryInfo = new DirectoryInfo(applicationBasePath);
            do
            {
                var solutionFileInfo = new FileInfo(Path.Combine(directoryInfo.FullName, "BLUEPRINT_BASE_NAME.sln"));
                if (solutionFileInfo.Exists)
                {
                    return Path.GetFullPath(Path.Combine(directoryInfo.FullName, solutionRelativePath, projectName));
                }

                directoryInfo = directoryInfo.Parent;
            } while (directoryInfo.Parent != null);

            throw new Exception($"Solution root could not be located using application root {applicationBasePath}.");
        }

        // Returns a path relative to the current project directory
        public static string GetRelativeToProjectPath(string path)
        {
            return Path.Combine(PlatformServices.Default.Application.ApplicationBasePath, path);
        }
    }
}