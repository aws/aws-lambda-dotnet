using System;
using System.IO;
using System.Text;

namespace Amazon.Lambda.Annotations.SourceGenerator.FileIO
{
    public class DirectoryManager : IDirectoryManager
    {
        public string GetDirectoryName(string path) => Path.GetDirectoryName(path);

        public bool Exists(string path) => Directory.Exists(path);

        /// <summary>
        /// This method mimics the behaviour of <see href="https://docs.microsoft.com/en-us/dotnet/api/system.io.path.getrelativepath?view=net-6.0"/>
        /// This logic already exists in the aws-extensions-for-dotnet-cli package. <see href="https://github.com/aws/aws-extensions-for-dotnet-cli/blob/9639f8f4902349d289491b290979a3a1671cc0a5/src/Amazon.Common.DotNetCli.Tools/Utilities.cs#L91">see here</see>
        /// Note - Path.GetRelativePath() was introduced in .NET Standard 2.1 but source generators can only be used in .NET Standard 2.0 therefore this
        /// custom implementation is needed.
        /// </summary>
        public string GetRelativePath(string relativeTo, string path)
        {
            if (string.IsNullOrEmpty(relativeTo) || string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException($"{nameof(relativeTo)} or {nameof(path)} cannot be null or empty");
            }

            // If there is no common ancestor, just return the path.
            if (!string.Equals(SanitizePath(relativeTo).Split('/')[0], SanitizePath(path).Split('/')[0]))
            {
                return path;
            }

            // Calculate the full paths by preserving the OS specific directory separator character and then perform the sanitization.
            relativeTo = SanitizePath(new DirectoryInfo(relativeTo).FullName);
            path = SanitizePath(new DirectoryInfo(path).FullName);

            var relativeToDirs = relativeTo.Split('/');
            var pathDirs = path.Split('/');
            var len = Math.Min(relativeToDirs.Length, pathDirs.Length);

            var lastCommonRoot = -1;
            int index;

            for (index = 0; index < len && string.Equals(relativeToDirs[index], pathDirs[index]); index++)
            {
                lastCommonRoot = index;
            }

            var relativePath = new StringBuilder();
            for (index = lastCommonRoot + 1; index < relativeToDirs.Length; index++)
            {
                if (relativeToDirs[index].Length > 0) relativePath.Append("../");
            }

            for (index = lastCommonRoot + 1; index < pathDirs.Length; index++)
            {
                relativePath.Append(pathDirs[index]);
                if(index + 1 < pathDirs.Length)
                {
                    relativePath.Append("/");
                }
            }

            var result = relativePath.ToString();
            if (result.EndsWith("/"))
                result = result.Substring(0, result.Length - 1);
            return string.IsNullOrEmpty(result) ? "." : result;
        }

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return Directory.GetFiles(path, searchPattern, searchOption);
        }

        private string SanitizePath(string path)
        {
            return path
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}