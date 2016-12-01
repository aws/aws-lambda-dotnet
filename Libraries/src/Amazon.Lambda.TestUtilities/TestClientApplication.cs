using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

namespace Amazon.Lambda.TestUtilities
{
    /// <summary>
    /// A test implementation of the IClientApplication interface used for writing local tests of Lambda Functions.
    /// </summary>
    public class TestClientApplication : IClientApplication
    {
        /// <summary>
        /// The application's package name.
        /// </summary>
        public string AppPackageName { get; set; }

        /// <summary>
        /// The application's title.
        /// </summary>
        public string AppTitle { get; set; }

        /// <summary>
        /// The application's version code.
        /// </summary>
        public string AppVersionCode { get; set; }

        /// <summary>
        /// The application's version.
        /// </summary>
        public string AppVersionName { get; set; }

        /// <summary>
        /// The application's installation id.
        /// </summary>
        public string InstallationId { get; set; }
    }
}
