using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Amazon.Lambda.Tools
{
    /// <summary>
    /// Utility class that pulls down configuration files from an S3 bucket that the AWS Toolkit for Visual Studio uses.
    /// </summary>
    public class ToolkitConfigFileFetcher
    {
        const string CLOUDFRONT_CONFIG_FILES_LOCATION = @"https://d3rrggjwfhwld2.cloudfront.net/";
        const string S3_FALLBACK_LOCATION = @"https://aws-vs-toolkit.s3.amazonaws.com/";

        static ToolkitConfigFileFetcher INSTANCE = new ToolkitConfigFileFetcher();
        private ToolkitConfigFileFetcher()
        {
        }

        public static ToolkitConfigFileFetcher Instance
        {
            get { return INSTANCE; }
        }

        /// <summary>
        /// Attempt to get the configuration file from the AWS Toolkit for Visual Studio config bucket.
        /// If there is an error retieving the file like a proxy issue then null is returned.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public async Task<string> GetFileContentAsync(IToolLogger logger, string filename)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var content = await client.GetStringAsync(CLOUDFRONT_CONFIG_FILES_LOCATION + filename);
                    return content;
                }
                catch(Exception)
                {
                    try
                    {
                        var content = await client.GetStringAsync(S3_FALLBACK_LOCATION + filename);
                        return content;
                    }
                    catch(Exception)
                    {
                        return null;
                    }
                }
            }
        }
    }
}
