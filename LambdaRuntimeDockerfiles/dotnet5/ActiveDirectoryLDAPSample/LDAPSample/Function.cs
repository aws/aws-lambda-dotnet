using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Novell.Directory.Ldap;

using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
namespace LDAPSample
{
    public class Input
    {
        public string directoryName { get; set; }
        public string userName { get; set; }
        public string password { get; set; }
        public string searchString { get; set; }
    }

    public class Function
    {
        /// <summary>
        /// This function can be used with AWS Directory Services
        /// or Windows Active Directory for LDAP related operations.
        /// This lambda must be placed in the same VPC as AWS Directory Services.
        /// Also, DNS name must be enabled in the DHCP option set of the VPC
        /// so that the directory can be accessed using directory name.
        ///
        /// Example JSON input :
        /// {
        ///   "directoryName": "example.com",
        ///   "userName": "admin",
        ///   "password": "XXXXX",
        ///   "searchString": "dc=example,dc=com"
        ///  }
        /// </summary>
        /// <param name="inputJson"></param>
        /// <param name="context"></param>
        /// <returns>LDAP response</returns>

        private const int LdapPort = 389;

        public string FunctionHandler(Input inputJson, ILambdaContext context)
        {
            string log = "";
            string[] arguments = { inputJson.directoryName,
                                   inputJson.searchString,
                                   inputJson.userName,
                                   inputJson.password
                                 };

            var ldapConn = new LdapConnection();
            ldapConn.Connect(inputJson.directoryName, LdapPort);
            ldapConn.Bind(inputJson.userName, inputJson.password);

            var searchResults = ldapConn.Search(inputJson.searchString,
                                       LdapConnection.ScopeSub,
                                       "(objectclass=*)", null, false);

            while (searchResults.HasMore())
            {
                LdapEntry nextEntry = null;
                try
                {
                   nextEntry = searchResults.Next();
                   log += nextEntry.ToString();
                }
                catch (LdapException e)
                {
                   log += e.ToString();
                   return log;
                }
             }

           return log;
        }
    }
}
