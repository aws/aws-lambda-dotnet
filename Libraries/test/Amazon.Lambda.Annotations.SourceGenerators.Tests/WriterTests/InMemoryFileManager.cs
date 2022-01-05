using System.Collections.Generic;
using System.IO;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public class InMemoryFileManager : IFileManager
    {
        private readonly IDictionary<string, string> _cacheContent;

        public InMemoryFileManager()
        {
            _cacheContent = new Dictionary<string, string>();
        }

        public string ReadAllText(string path)
        {
            return _cacheContent.TryGetValue(path, out var content) ? content : null;
        }

        public void WriteAllText(string path, string contents) => _cacheContent[path] = contents;

        public bool Exists(string path) => throw new System.NotImplementedException();

        public FileStream Create(string path) => throw new System.NotImplementedException();
    }
}