using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Xunit;

using Amazon.Lambda;

namespace Amazon.Lambda.Tools.Test
{
    public class ValidateAspNetCoreAllReferenceTest
    {
        [Fact]
        public void NewerAspNetCoreReference()
        {
            var logger = new TestToolLogger();
            var manifest = File.ReadAllText(@"ManifestTestFiles/SampleManifest.xml");
            var projectFile = File.ReadAllText(@"ManifestTestFiles/NewerAspNetCoreReference.xml");

            Assert.Throws<AmazonLambdaException>(() => Utilities.ValidateMicrosoftAspNetCoreAllReferenceWithManifest(logger, manifest, projectFile));
        }

        [Fact]
        public void CurrentAspNetCoreReference()
        {
            var logger = new TestToolLogger();
            var manifest = File.ReadAllText(@"ManifestTestFiles/SampleManifest.xml");
            var projectFile = File.ReadAllText(@"ManifestTestFiles/CurrentAspNetCoreReference.xml");

            Utilities.ValidateMicrosoftAspNetCoreAllReferenceWithManifest(logger, manifest, projectFile);

            Assert.DoesNotContain("error", logger.Buffer.ToLower());
        }

        [Fact]
        public void NotUsingAspNetCore()
        {
            var logger = new TestToolLogger();
            var manifest = File.ReadAllText(@"ManifestTestFiles/SampleManifest.xml");
            var projectFile = File.ReadAllText(@"ManifestTestFiles/CurrentAspNetCoreReference.xml");

            Utilities.ValidateMicrosoftAspNetCoreAllReferenceWithManifest(logger, manifest, projectFile);

            Assert.DoesNotContain("error", logger.Buffer.ToLower());
        }
    }
}
