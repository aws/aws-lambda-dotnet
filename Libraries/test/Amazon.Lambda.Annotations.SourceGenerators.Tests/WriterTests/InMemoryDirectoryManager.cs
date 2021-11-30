using System.IO;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public class InMemoryDirectoryManager : IDirectoryManager
    {
        public string GetDirectoryName(string path) => Path.GetDirectoryName(path);

        public string GetRelativePath(string relativeTo, string path)
        {
            var relativePath = Path.GetRelativePath(relativeTo, path);
            return relativePath.Replace(Path.DirectorySeparatorChar, '/');
        }

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            throw new System.NotImplementedException();
        }

        public bool Exists(string path)
        {
            throw new System.NotImplementedException();
        }
    }
}