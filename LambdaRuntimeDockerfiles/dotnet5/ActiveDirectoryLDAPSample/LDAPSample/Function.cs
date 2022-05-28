using System;
using System.Diagnostics;
using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LDAPSample
{
    public class Input
    {
        public string directoryName { get; set; }
        public string userName { get; set; }
        public string baseDN { get; set; }
        public string filter { get; set; }
        public string vpcEndpointSecretsManagerUrl { get; set; }
        public string secretsManagerKeytabSecretId { get; set; }
    }

    public class Function
    {
        /// <summary>
        /// This function can be used with AWS Directory Services
        /// or Windows Active Directory for LDAP related operations.
        /// This lambda must be placed in the same VPC as AWS Directory Services.
        /// Also, DNS name must be enabled in the DHCP option set of the VPC
        /// so that the directory can be accessed using directory name.
        /// Add VPC endpoint for secrets manager and store keytab file as a binary blob.
        ///
        ///  Example JSON input :
        ///  {
        ///     "directoryName": "EXAMPLE.COM",
        ///     "userName": "user",
        ///     "baseDN": "DC=example,DC=com",
        ///     "vpcEndpointSecretsManagerUrl": "https://vpce-xxxxxxxxxxxxxxxxx-yyyyyyyy.secretsmanager.us-west-1.vpce.amazonaws.com",
        ///     "filter": "(objectClass=*)",
        ///     "secretsManagerKeytabSecretId": "aws/directory-services/d-xxxxxxxxx/keytab"
        ///  }
        /// </summary>
        /// <param name="inputJson"></param>
        /// <param name="context"></param>
        /// <returns>LDAP response</returns>
        public string FunctionHandler(Input inputJson, ILambdaContext context)
        {
            string log = "***Start of function***\n";

            using (Process compiler = new Process())
            {
                compiler.StartInfo.FileName = "ldap_using_kerberos.sh";
                string region = Environment.GetEnvironmentVariable("AWS_REGION");
                compiler.StartInfo.Arguments = "--directory-name " + inputJson.directoryName + " --user-name " + inputJson.userName + " --base-dn " + inputJson.baseDN + " --vpc-endpoint-secretsmanager-url " + inputJson.vpcEndpointSecretsManagerUrl + " --filter " + inputJson.filter + " --region " + region + " --secretsmanager-keytab-secret-id " + inputJson.secretsManagerKeytabSecretId;
                compiler.StartInfo.UseShellExecute = false;
                compiler.StartInfo.RedirectStandardOutput = true;
                compiler.Start();

                var output = compiler.StandardOutput.ReadToEnd();
                Console.WriteLine(output);
                log += output;

                compiler.WaitForExit();
            }

            return log;
        }
    }
}

