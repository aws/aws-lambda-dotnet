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
        public string keytabFileS3Url { get; set; }
    }

    public class Function
    {
        /// <summary>
        /// This function can be used with AWS Directory Services
        /// or Windows Active Directory for LDAP related operations.
        /// This lambda must be placed in the same VPC as AWS Directory Services.
        /// Also, DNS name must be enabled in the DHCP option set of the VPC
        /// so that the directory can be accessed using directory name.
        /// Also, add S3 endpoint as per https://aws.amazon.com/blogs/aws/new-vpc-endpoint-for-amazon-s3/.
        ///
        ///  Example JSON input :
        ///  {
        ///    "directoryName": "XXX.YYY.com",
        ///    "userName": "USER",
        ///    "baseDN": "DC=XXX,DC=YYY,DC=com",
        ///    "keytabFileS3Url": "s3://KEYTABLOCATION/my.keytab",
        ///    "filter": "(objectClass=*)"
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
                compiler.StartInfo.Arguments = "--directory-name " + inputJson.directoryName + " --user-name " + inputJson.userName + " --base-dn " + inputJson.baseDN + " --keytab-file-s3-url " + inputJson.keytabFileS3Url + " --filter " + inputJson.filter;
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

