using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.SQS.Model;

namespace Amazon.Lambda.TestTool.Services
{
    public interface IAWSService
    {
        IList<string> ListProfiles();

        Task<IList<string>> ListQueuesAsync(string profile, string region);

        Task<Message> ReadMessageAsync(string profile, string region, string queueUrl);

        Task DeleteMessageAsync(string profile, string region, string queueUrl, string receiptHandle);

        Task PurgeQueueAsync(string profile, string region, string queueUrl);
    }
}
