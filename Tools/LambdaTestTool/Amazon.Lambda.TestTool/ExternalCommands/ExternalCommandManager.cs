using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.TestTool.ExternalCommands.Models;
using Newtonsoft.Json;

namespace Amazon.Lambda.TestTool.ExternalCommands
{
    /// <summary>
    /// This class provides the abstraction for shelling out to the Amazon.Lambda.TestTool.ExternalCommands to make calls that require additional dependencies like
    /// the AWS SDK for .NET.
    /// </summary>
    public class ExternalCommandManager
    {
        public IList<string> ListProfiles()
        {
            var wrapper = new AppWrapper("list-profiles", null);

            var results = wrapper.Execute();

            if (results.ExitCode != 0)
            {
                Console.Error.WriteLine("Error finding registered profiles:");
                Console.Error.WriteLine(results.StandardError);
                return new List<string>();
            }

            var profiles = new List<string>();
            foreach (var token in results.StandardOut.Split('\n').OrderBy(x => x))
            {
                var trimmed = token.Trim();
                if (trimmed.Length > 0)
                {
                    profiles.Add(trimmed);                    
                }
            }

            return profiles;
        }
        
        public IList<string> ListQueues(string profile, string region)
        {
            var wrapper = new AppWrapper("list-queues", new List<string>{"-p", profile, "-r", region});

            var results = wrapper.Execute();

            if (results.ExitCode != 0)
            {
                Console.Error.WriteLine("Error listing SQS queues:");
                Console.Error.WriteLine(results.StandardError);
                return new List<string>();
            }

            var queues = new List<string>();
            foreach (var token in results.StandardOut.Split('\n'))
            {
                var trimmed = token.Trim();
                if (trimmed.Length > 0)
                {
                    queues.Add(trimmed);                    
                }
            }

            return queues;
        }

        public SQSMessage ReadMessage(string profile, string region, string queueUrl)
        {
            var wrapper = new AppWrapper("read-message", new List<string>{"-p", profile, "-r", region, "-q", queueUrl});

            var results = wrapper.Execute();

            if (results.ExitCode != 0)
            {
                throw new Exception(results.StandardError);
            }

            else if (string.IsNullOrEmpty(results.StandardOut))
            {
                return null;
            }

            var message = JsonConvert.DeserializeObject<SQSMessage>(results.StandardOut);
            return message;
        }
        
        public void DeleteMessage(string profile, string region, string queueUrl, string receiptHandle)
        {
            var wrapper = new AppWrapper("delete-message", new List<string>{"-p", profile, "-r", region, "-q", queueUrl, "-rh", receiptHandle});

            var results = wrapper.Execute();

            if (results.ExitCode != 0)
            {
                throw new Exception(results.StandardError);
            }
        }
    }
}