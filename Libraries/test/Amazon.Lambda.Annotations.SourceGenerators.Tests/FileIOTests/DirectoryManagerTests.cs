using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.FileIOTests
{
    public class DirectoryManagerTests
    {
        [Theory]
        [InlineData("C:/CodeBase/Src/MyProject", "C:/CodeBase/Src/MyProject", ".")]
        [InlineData("C:/CodeBase/Src/MyProject/MyFile.cs", "C:/CodeBase/Src/MyProject/MyFile.cs", ".")]
        [InlineData("C:/CodeBase/Src/MyProject", "C:/CodeBase/Src/MyProject/MyProject.csproj", "MyProject.csproj")]
        [InlineData("C:/CodeBase/Src/MyProject", "C:/CodeBase/Src", "..")]
        [InlineData("C:/CodeBase/Src/MyProject", "C:/CodeBase/Test", "../../Test")]
        [InlineData("C:/CodeBase/Src/MyProject", "C:/CodeBase/Test/TestApp.csproj", "../../Test/TestApp.csproj")]
        [InlineData("C:/CodeBase/Src/MyProject", "C:/CodeBase/Src/MyProject/MyFolder/MyResources", "MyFolder/MyResources")]
        [InlineData("C:/CodeBase/Src/MyProject/MyProject.csproj", "C:/CodeBase/Src/MyProject", "..")]
        [InlineData("C:/CodeBase/Src/MyProject/MyProject.csproj", "C:/CodeBase/Src", "../..")]
        [InlineData("MyFolder", "MyFolder", ".")]
        [InlineData("C:/CodeBase/Src/MyProject/MyProject.csproj", "D:/CodeBase/Src/MyProject/MyProject.csproj", "D:/CodeBase/Src/MyProject/MyProject.csproj")]
        public void GetRelativePath(string relativeTo, string path, string expectedPath)
        {
            var directoryManager = new DirectoryManager();
            var relativePath = directoryManager.GetRelativePath(relativeTo, path);
            Assert.Equal(expectedPath, relativePath);
        }
    }
}
