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
    }
}
