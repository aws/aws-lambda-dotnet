using System.IO;

namespace Amazon.Lambda.Annotations.SourceGenerator.FileIO
{
    public class FileManager : IFileManager
    {
        public string ReadAllText(string path) => File.ReadAllText(path);
        
        public void WriteAllText(string path, string content) => File.WriteAllText(path, content);
        
        public bool Exists(string path) => File.Exists(path);
        
        public FileStream Create(string path) => File.Create(path);
    }
}