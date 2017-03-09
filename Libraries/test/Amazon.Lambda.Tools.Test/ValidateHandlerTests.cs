using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Xunit;

using Amazon.Lambda.Tools;
using Amazon.Lambda.Tools.Commands;

namespace Amazon.Lambda.Tools.Test
{
    public class ValidateHandlerTests
    {
        string _testFunctionProjectLocation;

        private string GetTestProjectPath()
        {
            var assembly = this.GetType().GetTypeInfo().Assembly;
            var fullPath = Path.GetFullPath(Path.GetDirectoryName(assembly.Location) + "../../../../../TestFunction/");
            return fullPath;
        }

        public ValidateHandlerTests()
        {
            var debugVersion = new FileInfo(Path.Combine(GetTestProjectPath(), "bin/Debug/netcoreapp1.0/TestFunction.dll"));
            var releaseVersion = new FileInfo(Path.Combine(GetTestProjectPath(), "bin/Release/netcoreapp1.0/TestFunction.dll"));

            if (!debugVersion.Exists && !releaseVersion.Exists)
                throw new Exception("TestFunction wasn't compiled succesfully");
 
            if (debugVersion.Exists && !releaseVersion.Exists)
                _testFunctionProjectLocation = debugVersion.DirectoryName;
            else if (!debugVersion.Exists && releaseVersion.Exists)
                _testFunctionProjectLocation = releaseVersion.DirectoryName;
            else if (debugVersion.LastWriteTime < releaseVersion.LastWriteTime)
                _testFunctionProjectLocation = releaseVersion.DirectoryName;
            else 
                _testFunctionProjectLocation = debugVersion.DirectoryName;


        }

        [Fact]
        public void NoParameters()
        {
            Utilities.ValidateHandler(_testFunctionProjectLocation, "TestFunction::TestFunction.ValidateHandlerFunctionSignatures::NoParameters");
        }

        [Fact]
        public void OneStringParameters()
        {
            Utilities.ValidateHandler(_testFunctionProjectLocation, "TestFunction::TestFunction.ValidateHandlerFunctionSignatures::OneStringParameters");
        }

        [Fact]
        public void MethodDoesntExist()
        {
            Assert.Throws(typeof(ValidateHandlerException), 
                () => Utilities.ValidateHandler(_testFunctionProjectLocation, "TestFunction::TestFunction.ValidateHandlerFunctionSignatures::YouShallNotPass"));
        }

        [Fact]
        public void TooManyParameters()
        {
            Assert.Throws(typeof(ValidateHandlerException),
                () => Utilities.ValidateHandler(_testFunctionProjectLocation, "TestFunction::TestFunction.ValidateHandlerFunctionSignatures::TooManyParameters"));
        }

        [Fact]
        public void InheritedMethod()
        {
            Utilities.ValidateHandler(_testFunctionProjectLocation, "TestFunction::TestFunction.ValidateHandlerFunctionSignatures::InheritedMethod");
        }
    }
}
