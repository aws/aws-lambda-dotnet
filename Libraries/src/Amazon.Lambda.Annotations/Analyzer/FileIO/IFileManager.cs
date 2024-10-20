using System.IO;

namespace Amazon.Lambda.Annotations.SourceGenerator.FileIO
{
    internal interface IFileManager
    {
        string ReadAllText(string path);
        void WriteAllText(string path, string content);
        bool Exists(string path);
        FileStream Create(string path);
    }
}