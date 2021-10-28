using System.IO;

namespace Amazon.Lambda.Annotations.SourceGenerator.FileIO
{
    public class DirectoryManager : IDirectoryManager
    {
        public string GetDirectoryName(string path) => Path.GetDirectoryName(path);
        
        public bool Exists(string path) => Directory.Exists(path);
        
        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return Directory.GetFiles(path, searchPattern, searchOption);
        }
    }
}