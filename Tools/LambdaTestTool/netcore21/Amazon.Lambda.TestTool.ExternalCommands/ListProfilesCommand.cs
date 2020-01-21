using System;
using System.Collections.Generic;
using Amazon.Runtime.CredentialManagement;
using Amazon.Runtime.CredentialManagement.Internal;

namespace Amazon.Lambda.TestTool.ExternalCommands
{
    public class ListProfilesCommand
    {
        public void Execute()
        {            
            var profileNames = new List<string>();
            try
            {
                foreach(var profile in new CredentialProfileStoreChain().ListProfiles())
                {
                    // Guard against the same profile existing in both the .NET SDK encrypted store
                    // and the shared credentials file. Lambda test tool does not have a mechanism
                    // to specify the which store to use the profile from. 
                    if(!profileNames.Contains(profile.Name))
                    {
                        profileNames.Add(profile.Name);
                    }
                }
            }
            catch (Exception)
            {
            }
            
            foreach (var name in profileNames)
            {
                Console.WriteLine(name);
            }
        }
    }
}