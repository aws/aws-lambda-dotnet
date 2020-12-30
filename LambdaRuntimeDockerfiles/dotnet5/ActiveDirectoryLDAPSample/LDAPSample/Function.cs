using System;
using System.DirectoryServices.Protocols;
using SearchScope = System.DirectoryServices.Protocols.SearchScope;
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
        public string baseDN { get; set; }
        public string searchScope { get; set; }
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
        ///   "userName": "myUser",
        ///   "password": "XXXXX",
        ///   "baseDN": "dc=example,dc=com"
        ///   "searchString" : "(objectClass=*)"
        ///   "searchScope": "OneLevel" or "searchScope" : "Base" or "searchScope" : "SubTree"
        ///  }
        /// </summary>
        /// <param name="inputJson"></param>
        /// <param name="context"></param>
        /// <returns>LDAP response</returns>
        public string FunctionHandler(Input inputJson, ILambdaContext context)
        {
            string log = "***Start of function***\n";

            var credentials =
                new System.Net.NetworkCredential(inputJson.userName, inputJson.password, inputJson.directoryName);
            var serverId = new LdapDirectoryIdentifier(inputJson.directoryName);

            var conn = new LdapConnection(serverId, credentials);
            conn.Bind();

            Console.WriteLine("\r\nPerforming a simple search ...");

            SearchScope searchScope = SearchScope.OneLevel;
            switch (inputJson.searchScope)
            {
                case "Base":
                    searchScope = SearchScope.Base;
                    break;
                case "OneLevel":
                    searchScope = SearchScope.OneLevel;
                    break;
                case "SubTree":
                    searchScope = SearchScope.Subtree;
                    break;
                default:
                    log = "usage: SearchScope must be Base or OneLevel or SubTree";
                    return log;
            }

            Console.WriteLine("search scope is " + searchScope);

            try
            {
                SearchRequest searchRequest = new SearchRequest(inputJson.baseDN,
                    inputJson.searchString,
                    searchScope,
                    null);

                // cast the returned directory response as a SearchResponse object
                SearchResponse searchResponse =
                    (SearchResponse) conn.SendRequest(searchRequest);

                Console.WriteLine("\r\nSearch Response Entries:{0}",
                    searchResponse.Entries.Count);

                // enumerate the entries in the search response
                foreach (SearchResultEntry entry in searchResponse.Entries)
                {
                    Console.WriteLine("{0}:{1}",
                        searchResponse.Entries.IndexOf(entry),
                        entry.DistinguishedName);
                    log += searchResponse.Entries.IndexOf(entry) + entry.DistinguishedName + "\n";
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("\nUnexpected exception occured:\n\t{0}: {1}",
                    e.GetType().Name, e.ToString());
                log = "Unexpected exception occurred" + e.GetType().Name + " " + e.ToString();
            }

            return log;
        }
    }
}

