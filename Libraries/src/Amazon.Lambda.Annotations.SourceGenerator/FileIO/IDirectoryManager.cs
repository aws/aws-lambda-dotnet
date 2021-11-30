using System.IO;

namespace Amazon.Lambda.Annotations.SourceGenerator.FileIO
{
    public interface IDirectoryManager
    {
        string GetDirectoryName(string path);
        string[] GetFiles(string path, string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly);
        bool Exists(string path);
        string GetRelativePath(string relativeTo, string path);
    }
}