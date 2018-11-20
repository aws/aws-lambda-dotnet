using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

namespace Amazon.Lambda.TestTool.ExternalCommands
{
    public static class Utils
    {
        public static AWSCredentials GetCredentials(string profileName)
        {
            AWSCredentials credentials = null;
            if (!string.IsNullOrEmpty(profileName))
            {
                var chain = new CredentialProfileStoreChain();
                chain.TryGetAWSCredentials(profileName, out credentials);
            }
            else
            {
                credentials = FallbackCredentialsFactory.GetCredentials();
            }
            return credentials;
        }
    }
}