using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Moq;
using System.IO;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    /// <summary>
    /// Tests for <see cref="ProjectFileHandler"/>
    /// </summary>
    public class ProjectFileHandlerTests
    {
        /// <summary>
        /// Asserts that the project is not opted out of description modification when the element is not present
        /// </summary>
        [Fact]
        public void IsNotOptedOut()
        {
            var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
                                    <PropertyGroup>
                                        <TargetFramework>net6.0</TargetFramework>
                                        <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
                                        <AWSProjectType>Lambda</AWSProjectType>
                                        <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
                                    </PropertyGroup>
                                </Project>";

            var mockFileManager = new Mock<IFileManager>();
            mockFileManager.Setup(m => m.ReadAllText(It.IsAny<string>())).Returns(csprojContent);

            Assert.False(ProjectFileHandler.IsTelemetrySuppressed("test.csproj", mockFileManager.Object));
        }

        /// <summary>
        /// Asserts that the project is opted out of description modification when the element is present and true
        /// </summary>
        [Fact]
        public void IsOptedOut()
        {
            var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
                                    <PropertyGroup>
                                        <TargetFramework>net6.0</TargetFramework>
                                        <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
                                        <AWSProjectType>Lambda</AWSProjectType>
                                        <AWSSuppressLambdaAnnotationsTelemetry>true</AWSSuppressLambdaAnnotationsTelemetry>
                                        <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
                                    </PropertyGroup>
                                </Project>";

            var mockFileManager = new Mock<IFileManager>();
            mockFileManager.Setup(m => m.ReadAllText(It.IsAny<string>())).Returns(csprojContent);

            Assert.True(ProjectFileHandler.IsTelemetrySuppressed("test.csproj", mockFileManager.Object));
        }

        /// <summary>
        /// Asserts that the project is not opted out of description modification when the element is present but has no inner text
        /// </summary>
        [Fact]
        public void IsNotOptedOut_PresentButEmpty()
        {
            var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
                                    <PropertyGroup>
                                        <TargetFramework>net6.0</TargetFramework>
                                        <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
                                        <AWSProjectType>Lambda</AWSProjectType>
                                        <AWSSuppressLambdaAnnotationsTelemetry/>
                                        <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
                                    </PropertyGroup>
                                </Project>";

            var mockFileManager = new Mock<IFileManager>();
            mockFileManager.Setup(m => m.ReadAllText(It.IsAny<string>())).Returns(csprojContent);

            Assert.False(ProjectFileHandler.IsTelemetrySuppressed(csprojContent, mockFileManager.Object));
        }

        /// <summary>
        /// Asserts that the project is not opted out of description modification when the element is present but is set to false
        /// </summary>
        [Fact]
        public void IsNotOptedOut_PresentButFalse()
        {
            var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
                                    <PropertyGroup>
                                        <TargetFramework>net6.0</TargetFramework>
                                        <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
                                        <AWSProjectType>Lambda</AWSProjectType>
                                        <AWSSuppressLambdaAnnotationsTelemetry>false</AWSSuppressLambdaAnnotationsTelemetry>
                                        <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
                                    </PropertyGroup>
                                </Project>";

            var mockFileManager = new Mock<IFileManager>();
            mockFileManager.Setup(m => m.ReadAllText(It.IsAny<string>())).Returns(csprojContent);

            Assert.False(ProjectFileHandler.IsTelemetrySuppressed(csprojContent, mockFileManager.Object));
        }

        /// <summary>
        /// Asserts that parsing a csproj with a single TargetFramework property works
        /// </summary>
        [Fact]
        public void TryDetermineTargetFramework_Single_Success()
        {
            var tempFile = Path.GetTempFileName();
            var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
                                    <PropertyGroup>
                                        <TargetFramework>net8.0</TargetFramework>
                                    </PropertyGroup>
                                </Project>";

            try
            {
                File.WriteAllText(tempFile, csprojContent);

                var result = ProjectFileHandler.TryDetermineTargetFramework(tempFile, out var targetFramework);

                Assert.True(result);
                Assert.Equal("net8.0", targetFramework);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        /// <summary>
        /// Asserts that parsing a csproj without TargetFramework(s) fails
        /// </summary>
        [Fact]
        public void TryDetermineTargetFramework_Missing_Failure()
        {
            var tempFile = Path.GetTempFileName();
            var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
                                    <PropertyGroup>
                                    </PropertyGroup>
                                </Project>";

            try
            { 
                File.WriteAllText(tempFile, csprojContent);

                var result = ProjectFileHandler.TryDetermineTargetFramework(tempFile, out var targetFramework);

                Assert.False(result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        /// <summary>
        /// Asserts that parsing a csproj with a single TargetFramework trumps the plural TargetFrameworks
        /// </summary>
        [Fact]
        public void TryDetermineTargetFramework_SingleAndMultiple_Success()
        {
            var tempFile = Path.GetTempFileName();
            var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
                                    <PropertyGroup>
                                        <TargetFramework>net8.0</TargetFramework>
                                        <TargetFrameworks>net7.0</TargetFrameworks>
                                    </PropertyGroup>
                                </Project>";

            try 
            { 
                File.WriteAllText(tempFile, csprojContent);

                var result = ProjectFileHandler.TryDetermineTargetFramework(tempFile, out var targetFramework);

                Assert.True(result);
                Assert.Equal("net8.0", targetFramework); // "TargetFramework" should trump "TargetFrameworks
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        /// <summary>
        /// Asserts that parsing a csproj with a single value in the plural TargetFrameworks property works
        /// </summary>
        [Fact]
        public void TryDetermineTargetFramework_Multiple_Success()
        {
            var tempFile = Path.GetTempFileName();
            var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
                                    <PropertyGroup>
                                        <TargetFrameworks>net7.0</TargetFrameworks>
                                    </PropertyGroup>
                                </Project>";

            try
            {
                File.WriteAllText(tempFile, csprojContent);

                var result = ProjectFileHandler.TryDetermineTargetFramework(tempFile, out var targetFramework);

                Assert.True(result);
                Assert.Equal("net7.0", targetFramework);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        /// <summary>
        /// Asserts that parsing a csproj with a multiple values in the plural TargetFrameworks property fails
        /// </summary>
        [Fact]
        public void TryDetermineTargetFramework_Multiple_Failure()
        {
            var tempFile = Path.GetTempFileName();
            var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
                                    <PropertyGroup>
                                        <TargetFrameworks>net6.0;net8.0</TargetFrameworks>
                                    </PropertyGroup>
                                </Project>";

            try
            {
                File.WriteAllText(tempFile, csprojContent);

                var result = ProjectFileHandler.TryDetermineTargetFramework(tempFile, out var targetFramework);

                Assert.False(result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
