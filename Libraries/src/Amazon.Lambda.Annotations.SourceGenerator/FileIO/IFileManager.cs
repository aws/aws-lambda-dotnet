using System.IO;

namespace Amazon.Lambda.Annotations.SourceGenerator.FileIO
{
    public interface IFileManager
    {
        string ReadAllText(string path);
        void WriteAllText(string path, string content);
        bool Exists(string path);
        FileStream Create(string path);
    }
}